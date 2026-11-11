using PlanelyApp.Application.Abstractions.Cqrs;
using PlanelyApp.Application.Abstractions.Imports;
using PlanelyApp.Application.Abstractions.Results;
using PlanelyApp.Application.Catalog.ServiceCategories.Create;
using PlanelyApp.Application.Catalog.ServiceOfferings;
using PlanelyApp.Application.Catalog.ServiceOfferings.Create;
using PlanelyApp.Application.Organization.Customers.Register;
using PlanelyApp.Domain.Catalog.ServiceCategories;
using PlanelyApp.Domain.Catalog.ServiceOfferings;
using PlanelyApp.Domain.Imports;
using PlanelyApp.Domain.SeedWork;

namespace PlanelyApp.Application.Imports.Processing;

internal sealed class ImportRowImporter(IDispatcher dispatcher, IServiceCategoryRepository categories) : IImportRowImporter
{
    public async Task<ImportRowResult> ImportAsync(ImportWork work, ImportSheetRow row, CancellationToken cancellationToken)
    {
        try
        {
            return work.Type switch
            {
                ImportType.Services => await ImportServiceAsync(work.Map, row.Cells, cancellationToken),
                ImportType.Customers => await ImportCustomerAsync(work.Map, row.Cells, cancellationToken),
                _ => ImportRowResult.Failed(ImportReasons.RowFailed),
            };
        }
        catch (DomainException exception)
        {
            // A rule of the Domain (a negative price, a phone that is not a phone...) is a problem of the row.
            return ImportRowResult.Failed(ImportReasons.Of(exception));
        }
    }

    private async Task<ImportRowResult> ImportServiceAsync(
        ImportColumnMap map, IReadOnlyList<string> cells, CancellationToken cancellationToken)
    {
        var name = map.Get(cells, ImportColumns.Name);

        if (name.Length == 0)
        {
            return Required(ImportColumns.Name);
        }

        var durationText = map.Get(cells, ImportColumns.Duration);

        if (durationText.Length == 0)
        {
            return Required(ImportColumns.Duration);
        }

        if (!ImportValues.TryParseDuration(durationText, out var duration))
        {
            return Invalid(ImportColumns.Duration);
        }

        // No price means "on request". A price that cannot be read is an error, never a silent free service.
        var pricing = new PricingData(PricingType.OnRequest);
        var priceText = map.Get(cells, ImportColumns.Price);

        if (priceText.Length > 0)
        {
            if (!ImportValues.TryParsePrice(priceText, out var price))
            {
                return Invalid(ImportColumns.Price);
            }

            pricing = new PricingData(PricingType.Fixed, price);
        }

        // Every cell was read before this point, so a bad row does not leave a category behind.
        Guid? categoryId = null;
        var categoryName = map.Get(cells, ImportColumns.Category);

        if (categoryName.Length > 0)
        {
            var category = await FindOrCreateCategoryAsync(categoryName, cancellationToken);

            if (category.IsFailure)
            {
                return ImportRowResult.Failed(ImportReasons.Of(category.Error));
            }

            categoryId = category.Value;
        }

        var created = await dispatcher.SendAsync(
            new CreateServiceOfferingCommand(
                name, duration, pricing, NullIfEmpty(map.Get(cells, ImportColumns.Description)), categoryId),
            cancellationToken);

        return ToResult(created);
    }

    private async Task<ImportRowResult> ImportCustomerAsync(
        ImportColumnMap map, IReadOnlyList<string> cells, CancellationToken cancellationToken)
    {
        var acceptsMarketing = false;
        var marketing = map.Get(cells, ImportColumns.Marketing);

        if (marketing.Length > 0)
        {
            if (ImportValues.ParseYesNo(marketing) is not { } accepted)
            {
                return Invalid(ImportColumns.Marketing);
            }

            acceptsMarketing = accepted;
        }

        // A blank cell is "not informed", which is what null means for the use case.
        var registered = await dispatcher.SendAsync(
            new RegisterCustomerCommand(
                NullIfEmpty(map.Get(cells, ImportColumns.Name)),
                NullIfEmpty(map.Get(cells, ImportColumns.Email)),
                NullIfEmpty(ImportValues.NormalizePhone(map.Get(cells, ImportColumns.Phone))),
                acceptsMarketing,
                NullIfEmpty(map.Get(cells, ImportColumns.Language))),
            cancellationToken);

        return ToResult(registered);
    }

    private async Task<Result<Guid>> FindOrCreateCategoryAsync(string name, CancellationToken cancellationToken)
    {
        var existing = await categories.FindByNameAsync(name, cancellationToken);

        if (existing is not null)
        {
            return existing.Id;
        }

        return await dispatcher.SendAsync(new CreateServiceCategoryCommand(name), cancellationToken);
    }

    private static ImportRowResult ToResult(Result<Guid> result) =>
        result.IsSuccess ? ImportRowResult.Ok : ImportRowResult.Failed(ImportReasons.Of(result.Error));

    private static ImportRowResult Required(string column) =>
        ImportRowResult.Failed(ImportReasons.Of(ImportReasons.ValueRequired, column));

    private static ImportRowResult Invalid(string column) =>
        ImportRowResult.Failed(ImportReasons.Of(ImportReasons.ValueInvalid, column));

    private static string? NullIfEmpty(string text) => text.Length == 0 ? null : text;
}
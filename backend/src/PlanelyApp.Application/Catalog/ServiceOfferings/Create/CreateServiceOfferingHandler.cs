using PlanelyApp.Application.Abstractions.Cqrs;
using PlanelyApp.Application.Abstractions.Results;
using PlanelyApp.Application.Abstractions.Tenancy;
using PlanelyApp.Domain.Catalog.ServiceCategories;
using PlanelyApp.Domain.Catalog.ServiceOfferings;
using PlanelyApp.Domain.SeedWork;
using PlanelyApp.Domain.SharedKernel;

namespace PlanelyApp.Application.Catalog.ServiceOfferings.Create;

internal sealed class CreateServiceOfferingHandler(
    IServiceOfferingRepository services,
    IServiceCategoryRepository categories,
    ITenantProvider tenantProvider)
    : ICommandHandler<CreateServiceOfferingCommand, Guid>
{
    public async Task<Result<Guid>> HandleAsync(
        CreateServiceOfferingCommand command,
        CancellationToken cancellationToken)
    {
        var companyId = tenantProvider.RequireCompanyId();
        var name = command.Name.Trim();

        // The Domain keeps only the id of the category. That it exists in this company is checked here, through the
        // repository, which already hides the categories of other companies.
        if (command.CategoryId is { } categoryId
            && await categories.GetByIdAsync(categoryId, cancellationToken) is null)
        {
            return Error.NotFound("ServiceCategory.NotFound");
        }

        if (await services.FindByNameAsync(companyId, name, cancellationToken) is not null)
        {
            return Error.Conflict("ServiceOffering.NameAlreadyExists");
        }

        var duration = TimeSpan.FromMinutes(command.DurationMinutes);
        var pricing = command.Pricing;

        // The validator guarantees a base price for a fixed price service.
        var service = pricing.Type == PricingType.Fixed
            ? ServiceOffering.CreateWithFixedPrice(
                companyId, name, duration, Money.From(pricing.BasePrice!.Value), command.Description, command.CategoryId)
            : ServiceOffering.CreateOnRequest(companyId, name, duration, command.Description, command.CategoryId);

        services.Add(service);

        return service.Id;
    }
}
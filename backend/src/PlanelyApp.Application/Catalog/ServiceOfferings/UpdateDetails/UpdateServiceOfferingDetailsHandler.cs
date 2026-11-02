using PlanelyApp.Application.Abstractions.Cqrs;
using PlanelyApp.Application.Abstractions.Results;
using PlanelyApp.Domain.Catalog.ServiceCategories;
using PlanelyApp.Domain.Catalog.ServiceOfferings;

namespace PlanelyApp.Application.Catalog.ServiceOfferings.UpdateDetails;

internal sealed class UpdateServiceOfferingDetailsHandler(
    IServiceOfferingRepository services,
    IServiceCategoryRepository categories)
    : ICommandHandler<UpdateServiceOfferingDetailsCommand, Unit>
{
    public async Task<Result<Unit>> HandleAsync(
        UpdateServiceOfferingDetailsCommand command,
        CancellationToken cancellationToken)
    {
        var service = await services.GetByIdAsync(command.ServiceOfferingId, cancellationToken);

        if (service is null)
        {
            return Error.NotFound("ServiceOffering.NotFound");
        }

        if (command.CategoryId is { } categoryId
            && await categories.GetByIdAsync(categoryId, cancellationToken) is null)
        {
            return Error.NotFound("ServiceCategory.NotFound");
        }

        var name = command.Name.Trim();

        // Finding the service itself is not a clash: keeping its name must not fail.
        if (await services.FindByNameAsync(service.CompanyId, name, cancellationToken) is { } sameName
            && sameName.Id != service.Id)
        {
            return Error.Conflict("ServiceOffering.NameAlreadyExists");
        }

        service.UpdateDetails(
            name, command.Description, TimeSpan.FromMinutes(command.DurationMinutes), command.CategoryId);

        return Result.Success();
    }
}
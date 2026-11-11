using PlanelyApp.Application.Abstractions.Cqrs;
using PlanelyApp.Application.Abstractions.Results;
using PlanelyApp.Domain.Catalog.Promotions;
using PlanelyApp.Domain.Catalog.ServiceOfferings;

namespace PlanelyApp.Application.Catalog.Promotions.IncludeServices;

internal sealed class IncludePromotionServicesHandler(
    IPromotionRepository promotions,
    IServiceOfferingRepository services)
    : ICommandHandler<IncludePromotionServicesCommand, Unit>
{
    public async Task<Result<Unit>> HandleAsync(
        IncludePromotionServicesCommand command,
        CancellationToken cancellationToken)
    {
        var promotion = await promotions.GetByIdAsync(command.PromotionId, cancellationToken);

        if (promotion is null)
        {
            return Error.NotFound("Promotion.NotFound");
        }

        // A missing id and an id of another company look the same, because the repository hides the other companies.
        var serviceIds = command.ServiceIds.Distinct().ToList();
        var found = (await services.ListByIdsAsync(serviceIds, cancellationToken)).ToDictionary(service => service.Id);

        if (found.Count != serviceIds.Count)
        {
            return Error.NotFound("ServiceOffering.NotFound");
        }

        // The Domain refuses a service on request, and adds all of them or none.
        promotion.IncludeServices(serviceIds.Select(id => found[id]));

        return Result.Success();
    }
}
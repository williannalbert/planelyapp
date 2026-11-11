using PlanelyApp.Application.Abstractions.Cqrs;
using PlanelyApp.Application.Abstractions.Results;
using PlanelyApp.Domain.Catalog.Promotions;

namespace PlanelyApp.Application.Catalog.Promotions.RemoveService;

internal sealed class RemovePromotionServiceHandler(IPromotionRepository promotions)
    : ICommandHandler<RemovePromotionServiceCommand, Unit>
{
    public async Task<Result<Unit>> HandleAsync(
        RemovePromotionServiceCommand command,
        CancellationToken cancellationToken)
    {
        var promotion = await promotions.GetByIdAsync(command.PromotionId, cancellationToken);

        if (promotion is null)
        {
            return Error.NotFound("Promotion.NotFound");
        }

        // No lookup of the service: removing an id the promotion does not have does nothing, and the Domain
        // keeps the minimums (one service, two in a bundle).
        promotion.RemoveService(command.ServiceOfferingId);

        return Result.Success();
    }
}
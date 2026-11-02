using PlanelyApp.Application.Abstractions.Cqrs;
using PlanelyApp.Application.Abstractions.Results;
using PlanelyApp.Domain.Catalog.Promotions;

namespace PlanelyApp.Application.Catalog.Promotions.UpdateDetails;

internal sealed class UpdatePromotionDetailsHandler(IPromotionRepository promotions)
    : ICommandHandler<UpdatePromotionDetailsCommand, Unit>
{
    public async Task<Result<Unit>> HandleAsync(
        UpdatePromotionDetailsCommand command,
        CancellationToken cancellationToken)
    {
        var promotion = await promotions.GetByIdAsync(command.PromotionId, cancellationToken);

        if (promotion is null)
        {
            return Error.NotFound("Promotion.NotFound");
        }

        promotion.UpdateDetails(command.Name, command.Description);

        return Result.Success();
    }
}
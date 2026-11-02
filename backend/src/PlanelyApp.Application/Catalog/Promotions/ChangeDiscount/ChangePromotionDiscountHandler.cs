using PlanelyApp.Application.Abstractions.Cqrs;
using PlanelyApp.Application.Abstractions.Results;
using PlanelyApp.Domain.Catalog.Promotions;

namespace PlanelyApp.Application.Catalog.Promotions.ChangeDiscount;

internal sealed class ChangePromotionDiscountHandler(IPromotionRepository promotions)
    : ICommandHandler<ChangePromotionDiscountCommand, Unit>
{
    public async Task<Result<Unit>> HandleAsync(
        ChangePromotionDiscountCommand command,
        CancellationToken cancellationToken)
    {
        var promotion = await promotions.GetByIdAsync(command.PromotionId, cancellationToken);

        if (promotion is null)
        {
            return Error.NotFound("Promotion.NotFound");
        }

        promotion.ChangeDiscount(command.Discount.ToDomain());

        return Result.Success();
    }
}
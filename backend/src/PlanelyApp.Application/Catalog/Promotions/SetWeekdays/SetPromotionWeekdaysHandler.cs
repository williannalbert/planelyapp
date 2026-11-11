using PlanelyApp.Application.Abstractions.Cqrs;
using PlanelyApp.Application.Abstractions.Results;
using PlanelyApp.Domain.Catalog.Promotions;

namespace PlanelyApp.Application.Catalog.Promotions.SetWeekdays;

internal sealed class SetPromotionWeekdaysHandler(IPromotionRepository promotions)
    : ICommandHandler<SetPromotionWeekdaysCommand, Unit>
{
    public async Task<Result<Unit>> HandleAsync(
        SetPromotionWeekdaysCommand command,
        CancellationToken cancellationToken)
    {
        var promotion = await promotions.GetByIdAsync(command.PromotionId, cancellationToken);

        if (promotion is null)
        {
            return Error.NotFound("Promotion.NotFound");
        }

        promotion.SetWeekdays(command.Weekdays);

        return Result.Success();
    }
}
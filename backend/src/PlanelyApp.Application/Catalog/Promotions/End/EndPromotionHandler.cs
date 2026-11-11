using PlanelyApp.Application.Abstractions.Cqrs;
using PlanelyApp.Application.Abstractions.Results;
using PlanelyApp.Domain.Catalog.Promotions;

namespace PlanelyApp.Application.Catalog.Promotions.End;

internal sealed class EndPromotionHandler(IPromotionRepository promotions)
    : ICommandHandler<EndPromotionCommand, Unit>
{
    public async Task<Result<Unit>> HandleAsync(EndPromotionCommand command, CancellationToken cancellationToken)
    {
        var promotion = await promotions.GetByIdAsync(command.PromotionId, cancellationToken);

        if (promotion is null)
        {
            return Error.NotFound("Promotion.NotFound");
        }

        promotion.End();

        return Result.Success();
    }
}
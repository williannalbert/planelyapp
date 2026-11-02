using PlanelyApp.Application.Abstractions.Cqrs;
using PlanelyApp.Application.Abstractions.Results;
using PlanelyApp.Domain.Catalog.Promotions;

namespace PlanelyApp.Application.Catalog.Promotions.ChangeValidity;

internal sealed class ChangePromotionValidityHandler(IPromotionRepository promotions)
    : ICommandHandler<ChangePromotionValidityCommand, Unit>
{
    public async Task<Result<Unit>> HandleAsync(
        ChangePromotionValidityCommand command,
        CancellationToken cancellationToken)
    {
        var promotion = await promotions.GetByIdAsync(command.PromotionId, cancellationToken);

        if (promotion is null)
        {
            return Error.NotFound("Promotion.NotFound");
        }

        promotion.ChangeValidity(ValidityPeriod.Create(command.ValidFrom, command.ValidUntil));

        return Result.Success();
    }
}
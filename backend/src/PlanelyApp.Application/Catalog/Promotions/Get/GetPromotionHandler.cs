using PlanelyApp.Application.Abstractions.Cqrs;
using PlanelyApp.Application.Abstractions.Results;
using PlanelyApp.Domain.Catalog.Promotions;

namespace PlanelyApp.Application.Catalog.Promotions.Get;

internal sealed class GetPromotionHandler(IPromotionRepository promotions)
    : IQueryHandler<GetPromotionQuery, PromotionDto>
{
    public async Task<Result<PromotionDto>> HandleAsync(GetPromotionQuery query, CancellationToken cancellationToken)
    {
        var promotion = await promotions.GetByIdAsync(query.PromotionId, cancellationToken);

        if (promotion is null)
        {
            return Error.NotFound("Promotion.NotFound");
        }

        return new PromotionDto(
            promotion.Id,
            promotion.Kind,
            promotion.Name,
            promotion.Description,
            new DiscountData(promotion.Discount.Type, promotion.Discount.Value),
            promotion.Validity.Start,
            promotion.Validity.End,
            promotion.Weekdays,
            promotion.AppliesToAllBranches,
            promotion.BranchIds,
            promotion.ServiceIds,
            promotion.IsActive);
    }
}
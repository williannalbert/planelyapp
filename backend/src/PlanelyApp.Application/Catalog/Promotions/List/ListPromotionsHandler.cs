using PlanelyApp.Application.Abstractions.Cqrs;
using PlanelyApp.Application.Abstractions.Paging;
using PlanelyApp.Application.Abstractions.Results;

namespace PlanelyApp.Application.Catalog.Promotions.List;

internal sealed class ListPromotionsHandler(IPromotionReadModel readModel)
    : IQueryHandler<ListPromotionsQuery, PagedResult<PromotionSummaryDto>>
{
    public async Task<Result<PagedResult<PromotionSummaryDto>>> HandleAsync(
        ListPromotionsQuery query,
        CancellationToken cancellationToken)
    {
        var search = string.IsNullOrWhiteSpace(query.Search) ? null : query.Search.Trim();

        return Result<PagedResult<PromotionSummaryDto>>.Success(
            await readModel.ListAsync(
                search, query.Kind, query.IsActive, query.ValidOn, query.Page, query.PageSize, cancellationToken));
    }
}
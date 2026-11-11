using PlanelyApp.Application.Abstractions.Cqrs;
using PlanelyApp.Application.Abstractions.Paging;
using PlanelyApp.Application.Abstractions.Results;

namespace PlanelyApp.Application.Catalog.ServiceOfferings.List;

internal sealed class ListServiceOfferingsHandler(IServiceOfferingReadModel readModel)
    : IQueryHandler<ListServiceOfferingsQuery, PagedResult<ServiceOfferingSummaryDto>>
{
    public async Task<Result<PagedResult<ServiceOfferingSummaryDto>>> HandleAsync(
        ListServiceOfferingsQuery query,
        CancellationToken cancellationToken)
    {
        var search = string.IsNullOrWhiteSpace(query.Search) ? null : query.Search.Trim();

        return Result<PagedResult<ServiceOfferingSummaryDto>>.Success(
            await readModel.ListAsync(
                search, query.CategoryId, query.IsActive, query.Page, query.PageSize, cancellationToken));
    }
}
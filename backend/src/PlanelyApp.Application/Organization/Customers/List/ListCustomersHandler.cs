using PlanelyApp.Application.Abstractions.Cqrs;
using PlanelyApp.Application.Abstractions.Paging;
using PlanelyApp.Application.Abstractions.Results;

namespace PlanelyApp.Application.Organization.Customers.List;

internal sealed class ListCustomersHandler(ICustomerReadModel readModel)
    : IQueryHandler<ListCustomersQuery, PagedResult<CustomerSummaryDto>>
{
    public async Task<Result<PagedResult<CustomerSummaryDto>>> HandleAsync(
        ListCustomersQuery query,
        CancellationToken cancellationToken)
    {
        var search = string.IsNullOrWhiteSpace(query.Search) ? null : query.Search.Trim();

        return Result<PagedResult<CustomerSummaryDto>>.Success(
            await readModel.ListAsync(search, query.Page, query.PageSize, cancellationToken));
    }
}
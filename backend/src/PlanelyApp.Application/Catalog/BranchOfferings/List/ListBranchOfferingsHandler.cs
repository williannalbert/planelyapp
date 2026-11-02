using PlanelyApp.Application.Abstractions.Cqrs;
using PlanelyApp.Application.Abstractions.Paging;
using PlanelyApp.Application.Abstractions.Results;

namespace PlanelyApp.Application.Catalog.BranchOfferings.List;

internal sealed class ListBranchOfferingsHandler(IBranchOfferingReadModel readModel)
    : IQueryHandler<ListBranchOfferingsQuery, PagedResult<BranchOfferingDto>>
{
    public async Task<Result<PagedResult<BranchOfferingDto>>> HandleAsync(
        ListBranchOfferingsQuery query,
        CancellationToken cancellationToken)
    {
        var search = string.IsNullOrWhiteSpace(query.Search) ? null : query.Search.Trim();

        var page = await readModel.ListAsync(
            query.BranchId, search, query.CategoryId, query.IsAvailable, query.Page, query.PageSize, cancellationToken);

        // Null means the company has no such branch, which is not the same as a branch with no services.
        if (page is null)
        {
            return Error.NotFound("Branch.NotFound");
        }

        return page;
    }
}
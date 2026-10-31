using PlanelyApp.Application.Abstractions.Cqrs;
using PlanelyApp.Application.Abstractions.Results;

namespace PlanelyApp.Application.Organization.Branches.List;

internal sealed class ListBranchesHandler(IBranchReadModel readModel)
    : IQueryHandler<ListBranchesQuery, IReadOnlyList<BranchSummaryDto>>
{
    public async Task<Result<IReadOnlyList<BranchSummaryDto>>> HandleAsync(
        ListBranchesQuery query,
        CancellationToken cancellationToken) =>
        Result<IReadOnlyList<BranchSummaryDto>>.Success(await readModel.ListAsync(query.IsActive, cancellationToken));
}
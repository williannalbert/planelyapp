using PlanelyApp.Application.Abstractions.Cqrs;
using PlanelyApp.Application.Abstractions.Identity;
using PlanelyApp.Application.Abstractions.Results;
using PlanelyApp.Application.Abstractions.Tenancy;
using PlanelyApp.Domain.Organization.Users;
using PlanelyApp.Domain.SeedWork;

namespace PlanelyApp.Application.Organization.Branches.List;

internal sealed class ListBranchesHandler(
    IBranchReadModel readModel,
    ICurrentUser currentUser,
    ITenantProvider tenantProvider,
    IUserRepository users) : IQueryHandler<ListBranchesQuery, IReadOnlyList<BranchSummaryDto>>
{
    public async Task<Result<IReadOnlyList<BranchSummaryDto>>> HandleAsync(
        ListBranchesQuery query,
        CancellationToken cancellationToken)
    {
        var branches = await readModel.ListAsync(query.IsActive, cancellationToken);

        // A customer books in any branch; the list of branches of the user (RN11) limits the staff.
        if (currentUser.Kind != ActorKind.Staff)
        {
            return Result<IReadOnlyList<BranchSummaryDto>>.Success(branches);
        }

        // A user has no tenant filter, so the lookup names the company (RN23).
        var user = await users.GetByIdInCompanyAsync(
            tenantProvider.RequireCompanyId(), currentUser.Id, cancellationToken);

        if (user is null)
        {
            return Result<IReadOnlyList<BranchSummaryDto>>.Success([]);
        }

        IReadOnlyList<BranchSummaryDto> visible = branches.Where(branch => user.CanAccess(branch.Id)).ToList();

        return Result<IReadOnlyList<BranchSummaryDto>>.Success(visible);
    }
}
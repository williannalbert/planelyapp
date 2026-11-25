using PlanelyApp.Application.Abstractions.Cqrs;
using PlanelyApp.Application.Abstractions.Results;
using PlanelyApp.Domain.Organization.Users;

namespace PlanelyApp.Application.Organization.Users.ResolveActor;

internal sealed class ResolveStaffActorHandler(IUserRepository users)
    : IQueryHandler<ResolveStaffActorQuery, StaffActor>
{
    public async Task<Result<StaffActor>> HandleAsync(ResolveStaffActorQuery query, CancellationToken cancellationToken)
    {
        // A user has no tenant filter, and the company is not known yet: it is what this lookup finds out.
        var user = await users.GetByIdentityIdAsync(query.IdentityId, cancellationToken);

        if (user is null)
        {
            return Error.Forbidden("User.NotRegistered");
        }

        if (!user.IsActive)
        {
            return Error.Forbidden("User.Inactive");
        }

        return new StaffActor(user.Id, user.CompanyId, user.Role, BranchesOf(user));
    }

    // The same reading as User.CanAccess (RN11): the platform administrator has no company, so it has no branch.
    private static IReadOnlyList<Guid>? BranchesOf(User user)
    {
        if (user.CompanyId is null)
        {
            return [];
        }

        return user.AccessAllBranches ? null : user.AllowedBranchIds.ToList();
    }
}
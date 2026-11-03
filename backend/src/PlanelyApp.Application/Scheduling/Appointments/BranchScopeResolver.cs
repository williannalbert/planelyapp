using PlanelyApp.Application.Abstractions.Identity;
using PlanelyApp.Application.Abstractions.Results;
using PlanelyApp.Application.Abstractions.Tenancy;
using PlanelyApp.Domain.Organization.Users;
using PlanelyApp.Domain.SeedWork;

namespace PlanelyApp.Application.Scheduling.Appointments;

/// <summary>Finds out which branches the member of the staff who is acting may see (RN11).</summary>
internal sealed class BranchScopeResolver(
    IUserRepository users,
    ICurrentUser currentUser,
    ITenantProvider tenantProvider)
{
    public async Task<Result<BranchScope>> ResolveForStaffAsync(CancellationToken cancellationToken)
    {
        // Costs no query, so it comes first.
        if (currentUser.Kind != ActorKind.Staff)
        {
            return Error.Forbidden("Appointment.StaffOnly");
        }

        // A user has no tenant filter, so the lookup names the company (RN23).
        var user = await users.GetByIdInCompanyAsync(tenantProvider.RequireCompanyId(), currentUser.Id, cancellationToken);

        // A user who is gone or inactive sees no branch at all.
        if (user is null || !user.IsActive)
        {
            return Error.Forbidden("Branch.AccessDenied");
        }

        return new BranchScope(user.AccessAllBranches ? null : user.AllowedBranchIds.ToList());
    }
}
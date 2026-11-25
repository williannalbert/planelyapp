using PlanelyApp.Application.Abstractions.Cqrs;
using PlanelyApp.Domain.Organization.Users;

namespace PlanelyApp.Application.Organization.Users.ResolveActor;

/// <summary>Who is the user behind this Keycloak subject. The Api asks it once per request, before any other use case.</summary>
public sealed record ResolveStaffActorQuery(string IdentityId) : IQuery<StaffActor>;

/// <summary>
/// What the Api needs to know about the user who is acting. The company is null only for the platform administrator.
/// <paramref name="BranchIds"/> follows <c>BranchScope</c>: null is every branch of the company, an empty list is none.
/// There is no default on purpose: forgetting the list must not mean "every branch".
/// </summary>
public sealed record StaffActor(Guid UserId, Guid? CompanyId, UserRole Role, IReadOnlyList<Guid>? BranchIds)
{
    /// <summary>RN11.</summary>
    public bool CanAccessBranch(Guid branchId) => BranchIds is null || BranchIds.Contains(branchId);
}
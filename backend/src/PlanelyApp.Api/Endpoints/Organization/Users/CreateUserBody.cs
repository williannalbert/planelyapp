using PlanelyApp.Domain.Organization.Users;

namespace PlanelyApp.Api.Endpoints.Organization.Users;

/// <summary>
/// The body of <c>POST /users</c>. The role is nullable on purpose: <c>PlatformAdmin</c> is the first member of the
/// enum, so a body that leaves the role out would arrive as the one role nobody may be given, after the account was created.
/// </summary>
internal sealed record CreateUserBody(
    string Name,
    string Email,
    UserRole? Role,
    bool AccessAllBranches = false,
    IReadOnlyList<Guid>? BranchIds = null,
    string? PreferredLanguage = null);
using PlanelyApp.Application.Abstractions.Cqrs;
using PlanelyApp.Domain.Organization.Users;

namespace PlanelyApp.Application.Organization.Users.Create;

/// <summary>
/// Creates the login account and the user of the company of the request. With <paramref name="AccessAllBranches"/>
/// false, the user sees only <paramref name="BranchIds"/> (none, if empty).
/// </summary>
public sealed record CreateUserCommand(
    string Name,
    string Email,
    UserRole Role,
    bool AccessAllBranches = false,
    IReadOnlyList<Guid>? BranchIds = null,
    string? PreferredLanguage = null) : ICommand<Guid>;
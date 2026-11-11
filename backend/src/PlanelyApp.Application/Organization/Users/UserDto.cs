using PlanelyApp.Domain.Organization.Users;

namespace PlanelyApp.Application.Organization.Users;

public sealed record UserDto(
    Guid Id,
    string Name,
    string Email,
    UserRole Role,
    bool AccessAllBranches,
    IReadOnlyList<Guid> BranchIds,
    string? PreferredLanguage,
    bool IsActive);
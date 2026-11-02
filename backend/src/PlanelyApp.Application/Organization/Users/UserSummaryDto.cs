using PlanelyApp.Domain.Organization.Users;

namespace PlanelyApp.Application.Organization.Users;

public sealed record UserSummaryDto(Guid Id, string Name, string Email, UserRole Role, bool IsActive, bool AccessAllBranches);
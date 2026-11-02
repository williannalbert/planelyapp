using PlanelyApp.Application.Abstractions.Cqrs;
using PlanelyApp.Application.Abstractions.Results;
using PlanelyApp.Domain.Organization.Users;

namespace PlanelyApp.Application.Organization.Users.ChangeRole;

/// <summary>Changes the role among the company roles. The platform administrator role cannot be given.</summary>
public sealed record ChangeUserRoleCommand(Guid UserId, UserRole Role) : ICommand<Unit>;
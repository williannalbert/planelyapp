using PlanelyApp.Application.Abstractions.Cqrs;
using PlanelyApp.Domain.Organization.Users;

namespace PlanelyApp.Application.Organization.Users.List;

/// <summary>The users of the company of the request. With <see cref="Role"/> or <see cref="IsActive"/>, only those.</summary>
public sealed record ListUsersQuery(UserRole? Role = null, bool? IsActive = null)
    : IQuery<IReadOnlyList<UserSummaryDto>>;
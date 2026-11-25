using PlanelyApp.Domain.Organization.Users;

namespace PlanelyApp.Api.Roles;

/// <summary>The roles that may call an endpoint. It travels as metadata of the endpoint and is read by <see cref="RoleMiddleware"/>.</summary>
internal sealed record RoleRule(IReadOnlySet<UserRole> Roles)
{
    public bool Allows(UserRole? role) => role is { } value && Roles.Contains(value);
}
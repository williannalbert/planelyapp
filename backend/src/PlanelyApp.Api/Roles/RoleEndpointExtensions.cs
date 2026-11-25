using PlanelyApp.Domain.Organization.Users;

namespace PlanelyApp.Api.Roles;

internal static class RoleEndpointExtensions
{
    /// <summary>
    /// Only these roles may call the endpoint (or every endpoint of the group). A declaration on an endpoint replaces the one
    /// of its group. Naming no role is a mistake, not a door that nobody can pass.
    /// </summary>
    public static TBuilder RequireRoles<TBuilder>(this TBuilder builder, params UserRole[] roles)
        where TBuilder : IEndpointConventionBuilder
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(roles);

        if (roles.Length == 0)
        {
            throw new ArgumentException(
                "Name at least one role: an endpoint that accepts no role cannot be called by anyone.", nameof(roles));
        }

        return builder.WithMetadata(new RoleRule(roles.ToHashSet()));
    }

    /// <summary>Any signed-in staff member, whatever the role.</summary>
    public static TBuilder RequireAnyStaff<TBuilder>(this TBuilder builder)
        where TBuilder : IEndpointConventionBuilder =>
        builder.RequireRoles(Enum.GetValues<UserRole>());
}
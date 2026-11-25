namespace PlanelyApp.Api.Roles;

internal static class ApiRolesExtensions
{
    /// <summary>After <c>UseApiTenancy</c>: the role is checked once the user and the company of the route are known.</summary>
    public static IApplicationBuilder UseApiRoles(this IApplicationBuilder app)
    {
        app.UseMiddleware<RoleMiddleware>();

        return app;
    }
}
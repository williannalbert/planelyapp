namespace PlanelyApp.Api.Tenancy;

internal static class ApiTenancyExtensions
{
    /// <summary>After <c>UseApiAuthentication</c>: it needs to know who is acting.</summary>
    public static IApplicationBuilder UseApiTenancy(this IApplicationBuilder app)
    {
        app.UseMiddleware<TenantMiddleware>();

        return app;
    }
}
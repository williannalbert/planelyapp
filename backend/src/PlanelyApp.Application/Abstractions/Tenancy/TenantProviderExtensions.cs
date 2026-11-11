using PlanelyApp.Domain.SeedWork;

namespace PlanelyApp.Application.Abstractions.Tenancy;

internal static class TenantProviderExtensions
{
    /// <summary>
    /// The company of the operation. A use case that works inside a company cannot run without one: the Api sets it
    /// from the route (RN23) before any use case runs, so a missing company is a bug, not an outcome to handle.
    /// </summary>
    public static Guid RequireCompanyId(this ITenantProvider tenantProvider) =>
        tenantProvider.CompanyId
        ?? throw new InvalidOperationException("This use case runs inside a company, but the request has none.");
}
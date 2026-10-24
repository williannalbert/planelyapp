using PlanelyApp.Domain.SeedWork;

namespace PlanelyApp.Infrastructure.Tenancy;

/// <summary>
/// The company of the current scope (one web request, one job run). Phase 5 sets it from the token;
/// until then the tests set it by hand.
/// </summary>
public sealed class TenantContext : ITenantProvider
{
    public Guid? CompanyId { get; private set; }

    public void Use(Guid companyId)
    {
        if (companyId == Guid.Empty)
        {
            throw new ArgumentException("Company id cannot be empty.", nameof(companyId));
        }

        if (CompanyId is { } current && current != companyId)
        {
            throw new InvalidOperationException("The company of this scope is already set and cannot change.");
        }

        CompanyId = companyId;
    }
}
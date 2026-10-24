using PlanelyApp.Infrastructure.Tenancy;
using Shouldly;

namespace PlanelyApp.IntegrationTests.Persistence;

public class TenantContextTests
{
    [Fact]
    public void A_new_context_should_have_no_company()
    {
        new TenantContext().CompanyId.ShouldBeNull();
    }

    [Fact]
    public void Using_the_same_company_twice_should_be_accepted()
    {
        var company = Guid.CreateVersion7();
        var tenant = new TenantContext();

        tenant.Use(company);
        tenant.Use(company);

        tenant.CompanyId.ShouldBe(company);
    }

    [Fact]
    public void Changing_to_another_company_should_be_refused()
    {
        var tenant = new TenantContext();
        tenant.Use(Guid.CreateVersion7());

        Should.Throw<InvalidOperationException>(() => tenant.Use(Guid.CreateVersion7()));
    }

    [Fact]
    public void An_empty_company_id_should_be_refused()
    {
        Should.Throw<ArgumentException>(() => new TenantContext().Use(Guid.Empty));
    }
}
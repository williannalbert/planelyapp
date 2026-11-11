using NSubstitute;
using PlanelyApp.Application.Abstractions.Tenancy;
using PlanelyApp.Domain.SeedWork;
using Shouldly;

namespace PlanelyApp.Application.Tests.Tenancy;

public class TenantProviderExtensionsTests
{
    [Fact]
    public void The_company_of_the_request_should_be_returned()
    {
        var companyId = Guid.NewGuid();
        var tenant = Substitute.For<ITenantProvider>();
        tenant.CompanyId.Returns((Guid?)companyId);

        tenant.RequireCompanyId().ShouldBe(companyId);
    }

    [Fact]
    public void A_request_without_a_company_should_fail_loudly()
    {
        var tenant = Substitute.For<ITenantProvider>();

        Should.Throw<InvalidOperationException>(() => tenant.RequireCompanyId());
    }
}
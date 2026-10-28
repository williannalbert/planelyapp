using PlanelyApp.Application.Abstractions.Results;
using PlanelyApp.Application.Organization.Companies.Activate;
using PlanelyApp.Application.Organization.Companies.Deactivate;
using Shouldly;

namespace PlanelyApp.Application.Tests.Organization.Companies;

public class CompanyStatusTests
{
    [Fact]
    public async Task Deactivating_should_turn_the_company_off()
    {
        var company = CompanyData.NewCompany();
        var handler = new DeactivateCompanyHandler(CompanyData.RepositoryWith(company), CompanyData.TenantOf(company));

        var result = await handler.HandleAsync(new DeactivateCompanyCommand(), TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        company.IsActive.ShouldBeFalse();
    }

    [Fact]
    public async Task Activating_should_turn_the_company_back_on()
    {
        var company = CompanyData.NewCompany();
        company.Deactivate();
        var handler = new ActivateCompanyHandler(CompanyData.RepositoryWith(company), CompanyData.TenantOf(company));

        var result = await handler.HandleAsync(new ActivateCompanyCommand(), TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        company.IsActive.ShouldBeTrue();
    }

    [Fact]
    public async Task Deactivating_a_missing_company_should_be_not_found()
    {
        var handler = new DeactivateCompanyHandler(
            CompanyData.RepositoryWith(null), CompanyData.TenantOf(CompanyData.NewCompany()));

        var result = await handler.HandleAsync(new DeactivateCompanyCommand(), TestContext.Current.CancellationToken);

        result.Error.Type.ShouldBe(ErrorType.NotFound);
    }

    [Fact]
    public async Task Activating_a_missing_company_should_be_not_found()
    {
        var handler = new ActivateCompanyHandler(
            CompanyData.RepositoryWith(null), CompanyData.TenantOf(CompanyData.NewCompany()));

        var result = await handler.HandleAsync(new ActivateCompanyCommand(), TestContext.Current.CancellationToken);

        result.Error.Type.ShouldBe(ErrorType.NotFound);
    }
}
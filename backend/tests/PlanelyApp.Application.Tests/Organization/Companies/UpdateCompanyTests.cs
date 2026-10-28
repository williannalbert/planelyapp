using NSubstitute;
using PlanelyApp.Application.Abstractions.Results;
using PlanelyApp.Application.Organization.Companies;
using PlanelyApp.Application.Organization.Companies.UpdateBookingPolicy;
using PlanelyApp.Application.Organization.Companies.UpdateDetails;
using PlanelyApp.Domain.Organization.Companies;
using PlanelyApp.Domain.SeedWork;
using PlanelyApp.Domain.SharedKernel;
using Shouldly;

namespace PlanelyApp.Application.Tests.Organization.Companies;

public class UpdateCompanyTests
{
    private static readonly UpdateCompanyDetailsCommand NewDetails =
        new("New name", "New legal name", "new@acme.com", "+5511888880000", "es");

    [Fact]
    public async Task Details_should_be_updated_on_the_company_of_the_request()
    {
        var company = CompanyData.NewCompany();
        var handler = new UpdateCompanyDetailsHandler(CompanyData.RepositoryWith(company), CompanyData.TenantOf(company));

        var result = await handler.HandleAsync(NewDetails, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        company.TradeName.ShouldBe("New name");
        company.LegalName.ShouldBe("New legal name");
        company.Email.Value.ShouldBe("new@acme.com");
        company.Phone.Value.ShouldBe("+5511888880000");
        company.DefaultLanguage.ShouldBe(Language.Spanish);
        company.Cnpj.Value.ShouldBe(CompanyData.ValidCnpj);
    }

    [Fact]
    public async Task Details_of_a_missing_company_should_be_not_found()
    {
        var company = CompanyData.NewCompany();
        var handler = new UpdateCompanyDetailsHandler(CompanyData.RepositoryWith(null), CompanyData.TenantOf(company));

        var result = await handler.HandleAsync(NewDetails, TestContext.Current.CancellationToken);

        result.Error.Type.ShouldBe(ErrorType.NotFound);
        result.Error.Code.ShouldBe("Company.NotFound");
    }

    [Fact]
    public async Task A_name_above_the_limit_should_be_refused_by_the_domain()
    {
        var company = CompanyData.NewCompany();
        var handler = new UpdateCompanyDetailsHandler(CompanyData.RepositoryWith(company), CompanyData.TenantOf(company));

        var exception = await Should.ThrowAsync<DomainException>(() => handler.HandleAsync(
            NewDetails with { LegalName = new string('x', 201) }, TestContext.Current.CancellationToken));

        exception.Code.ShouldBe("Company.LegalNameInvalid");
        company.TradeName.ShouldBe("Acme");
    }

    [Fact]
    public async Task A_request_without_a_company_should_fail_loudly()
    {
        var company = CompanyData.NewCompany();
        var handler = new UpdateCompanyDetailsHandler(
            CompanyData.RepositoryWith(company), Substitute.For<ITenantProvider>());

        await Should.ThrowAsync<InvalidOperationException>(
            () => handler.HandleAsync(NewDetails, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task The_details_validator_should_require_every_field()
    {
        var problems = await TestValidation.RunAsync(
            new UpdateCompanyDetailsValidator(), new UpdateCompanyDetailsCommand("", "", "", "", ""));

        problems.ShouldBe(
            [
                "TradeName:Validation.Required",
                "LegalName:Validation.Required",
                "Email:Validation.Required",
                "Phone:Validation.Required",
                "DefaultLanguage:Validation.Required",
            ],
            ignoreOrder: true);
    }

    [Fact]
    public async Task The_policy_should_be_replaced()
    {
        var company = CompanyData.NewCompany();
        var handler = new UpdateBookingPolicyHandler(CompanyData.RepositoryWith(company), CompanyData.TenantOf(company));

        var result = await handler.HandleAsync(
            new UpdateBookingPolicyCommand(new BookingPolicyData(4, 90, 12, 10, 45, true)),
            TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        company.BookingPolicy.ShouldBe(BookingPolicy.Create(4, 90, 12, 10, 45, true));
    }

    [Fact]
    public async Task A_policy_out_of_range_should_be_refused_by_the_domain()
    {
        var company = CompanyData.NewCompany();
        var handler = new UpdateBookingPolicyHandler(CompanyData.RepositoryWith(company), CompanyData.TenantOf(company));

        var exception = await Should.ThrowAsync<DomainException>(() => handler.HandleAsync(
            new UpdateBookingPolicyCommand(new BookingPolicyData(2, 0, 24, 0, 30, false)),
            TestContext.Current.CancellationToken));

        exception.Code.ShouldBe("BookingPolicy.MaximumWindowInvalid");
        company.BookingPolicy.ShouldBe(BookingPolicy.Default);
    }

    [Fact]
    public async Task The_policy_of_a_missing_company_should_be_not_found()
    {
        var company = CompanyData.NewCompany();
        var handler = new UpdateBookingPolicyHandler(CompanyData.RepositoryWith(null), CompanyData.TenantOf(company));

        var result = await handler.HandleAsync(
            new UpdateBookingPolicyCommand(new BookingPolicyData(2, 60, 24, 0, 30, false)),
            TestContext.Current.CancellationToken);

        result.Error.Code.ShouldBe("Company.NotFound");
    }

    [Fact]
    public async Task The_policy_validator_should_require_the_policy()
    {
        var problems = await TestValidation.RunAsync(
            new UpdateBookingPolicyValidator(), new UpdateBookingPolicyCommand(null!));

        problems.ShouldBe(["Policy:Validation.Required"], ignoreOrder: true);
    }
}
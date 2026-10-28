using PlanelyApp.Application.Abstractions.Results;
using PlanelyApp.Application.Organization.Companies;
using PlanelyApp.Application.Organization.Companies.Get;
using PlanelyApp.Domain.Organization.Companies;
using Shouldly;

namespace PlanelyApp.Application.Tests.Organization.Companies;

public class GetCompanyTests
{
    [Fact]
    public async Task The_company_of_the_request_should_come_back_as_a_dto()
    {
        var company = CompanyData.NewCompany();
        var handler = new GetCompanyHandler(CompanyData.RepositoryWith(company), CompanyData.TenantOf(company));

        var result = await handler.HandleAsync(new GetCompanyQuery(), TestContext.Current.CancellationToken);

        var dto = result.Value;
        dto.Id.ShouldBe(company.Id);
        dto.TradeName.ShouldBe("Acme");
        dto.LegalName.ShouldBe("Acme LTDA");
        dto.Cnpj.ShouldBe(CompanyData.ValidCnpj);
        dto.Email.ShouldBe(CompanyData.ValidEmail);
        dto.Phone.ShouldBe(CompanyData.ValidPhone);
        dto.DefaultLanguage.ShouldBe("en");
        dto.BookingPolicy.ShouldBe(BookingPolicyData.From(BookingPolicy.Default));
        dto.IsActive.ShouldBeTrue();
    }

    [Fact]
    public async Task A_missing_company_should_be_not_found()
    {
        var handler = new GetCompanyHandler(
            CompanyData.RepositoryWith(null), CompanyData.TenantOf(CompanyData.NewCompany()));

        var result = await handler.HandleAsync(new GetCompanyQuery(), TestContext.Current.CancellationToken);

        result.Error.Type.ShouldBe(ErrorType.NotFound);
        result.Error.Code.ShouldBe("Company.NotFound");
    }
}
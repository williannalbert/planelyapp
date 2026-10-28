using NSubstitute;
using PlanelyApp.Application.Organization.Companies;
using PlanelyApp.Application.Organization.Companies.Register;
using PlanelyApp.Domain.Organization.Companies;
using PlanelyApp.Domain.SeedWork;
using PlanelyApp.Domain.SharedKernel;

namespace PlanelyApp.Application.Tests.Organization.Companies;

internal static class CompanyData
{
    public const string ValidCnpj = "11222333000181";
    public const string ValidEmail = "contact@acme.com";
    public const string ValidPhone = "+5511999990000";

    public static Company NewCompany() =>
        Company.Create(
            "Acme",
            "Acme LTDA",
            Cnpj.Create(ValidCnpj),
            Email.Create(ValidEmail),
            Phone.Create(ValidPhone),
            Language.English);

    public static RegisterCompanyCommand RegisterCommand(BookingPolicyData? policy = null) =>
        new("Acme", "Acme LTDA", ValidCnpj, ValidEmail, ValidPhone, "en", policy);

    /// <summary>A repository that finds this company (or none) whatever the id.</summary>
    public static ICompanyRepository RepositoryWith(Company? company)
    {
        var companies = Substitute.For<ICompanyRepository>();
        companies.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(company);

        return companies;
    }

    /// <summary>A tenant provider saying the request is for this company.</summary>
    public static ITenantProvider TenantOf(Company company)
    {
        var tenant = Substitute.For<ITenantProvider>();
        tenant.CompanyId.Returns((Guid?)company.Id);

        return tenant;
    }
}
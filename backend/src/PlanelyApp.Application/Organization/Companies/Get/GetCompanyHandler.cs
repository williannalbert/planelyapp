using PlanelyApp.Application.Abstractions.Cqrs;
using PlanelyApp.Application.Abstractions.Results;
using PlanelyApp.Application.Abstractions.Tenancy;
using PlanelyApp.Domain.Organization.Companies;
using PlanelyApp.Domain.SeedWork;

namespace PlanelyApp.Application.Organization.Companies.Get;

internal sealed class GetCompanyHandler(ICompanyRepository companies, ITenantProvider tenantProvider)
    : IQueryHandler<GetCompanyQuery, CompanyDto>
{
    public async Task<Result<CompanyDto>> HandleAsync(GetCompanyQuery query, CancellationToken cancellationToken)
    {
        var company = await companies.GetByIdAsync(tenantProvider.RequireCompanyId(), cancellationToken);

        if (company is null)
        {
            return Error.NotFound("Company.NotFound");
        }

        return new CompanyDto(
            company.Id,
            company.TradeName,
            company.LegalName,
            company.Cnpj.Value,
            company.Email.Value,
            company.Phone.Value,
            company.DefaultLanguage.Code,
            BookingPolicyData.From(company.BookingPolicy),
            company.IsActive);
    }
}
using PlanelyApp.Application.Abstractions.Cqrs;
using PlanelyApp.Application.Abstractions.Results;
using PlanelyApp.Application.Abstractions.Tenancy;
using PlanelyApp.Domain.Organization.Companies;
using PlanelyApp.Domain.SeedWork;
using PlanelyApp.Domain.SharedKernel;

namespace PlanelyApp.Application.Organization.Companies.UpdateDetails;

internal sealed class UpdateCompanyDetailsHandler(ICompanyRepository companies, ITenantProvider tenantProvider)
    : ICommandHandler<UpdateCompanyDetailsCommand, Unit>
{
    public async Task<Result<Unit>> HandleAsync(
        UpdateCompanyDetailsCommand command,
        CancellationToken cancellationToken)
    {
        var company = await companies.GetByIdAsync(tenantProvider.RequireCompanyId(), cancellationToken);

        if (company is null)
        {
            return Error.NotFound("Company.NotFound");
        }

        company.UpdateDetails(
            command.TradeName,
            command.LegalName,
            Email.Create(command.Email),
            Phone.Create(command.Phone),
            Language.Create(command.DefaultLanguage));

        return Result.Success();
    }
}
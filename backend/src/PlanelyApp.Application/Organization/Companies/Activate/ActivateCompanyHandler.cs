using PlanelyApp.Application.Abstractions.Cqrs;
using PlanelyApp.Application.Abstractions.Results;
using PlanelyApp.Application.Abstractions.Tenancy;
using PlanelyApp.Domain.Organization.Companies;
using PlanelyApp.Domain.SeedWork;

namespace PlanelyApp.Application.Organization.Companies.Activate;

internal sealed class ActivateCompanyHandler(ICompanyRepository companies, ITenantProvider tenantProvider)
    : ICommandHandler<ActivateCompanyCommand, Unit>
{
    public async Task<Result<Unit>> HandleAsync(ActivateCompanyCommand command, CancellationToken cancellationToken)
    {
        var company = await companies.GetByIdAsync(tenantProvider.RequireCompanyId(), cancellationToken);

        if (company is null)
        {
            return Error.NotFound("Company.NotFound");
        }

        company.Activate();

        return Result.Success();
    }
}
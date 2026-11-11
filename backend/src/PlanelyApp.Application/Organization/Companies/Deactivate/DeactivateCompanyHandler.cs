using PlanelyApp.Application.Abstractions.Cqrs;
using PlanelyApp.Application.Abstractions.Results;
using PlanelyApp.Application.Abstractions.Tenancy;
using PlanelyApp.Domain.Organization.Companies;
using PlanelyApp.Domain.SeedWork;

namespace PlanelyApp.Application.Organization.Companies.Deactivate;

internal sealed class DeactivateCompanyHandler(ICompanyRepository companies, ITenantProvider tenantProvider)
    : ICommandHandler<DeactivateCompanyCommand, Unit>
{
    public async Task<Result<Unit>> HandleAsync(DeactivateCompanyCommand command, CancellationToken cancellationToken)
    {
        var company = await companies.GetByIdAsync(tenantProvider.RequireCompanyId(), cancellationToken);

        if (company is null)
        {
            return Error.NotFound("Company.NotFound");
        }

        company.Deactivate();

        return Result.Success();
    }
}
using PlanelyApp.Application.Abstractions.Cqrs;
using PlanelyApp.Application.Abstractions.Results;
using PlanelyApp.Application.Abstractions.Tenancy;
using PlanelyApp.Domain.Organization.Companies;
using PlanelyApp.Domain.SeedWork;

namespace PlanelyApp.Application.Organization.Companies.UpdateBookingPolicy;

internal sealed class UpdateBookingPolicyHandler(ICompanyRepository companies, ITenantProvider tenantProvider)
    : ICommandHandler<UpdateBookingPolicyCommand, Unit>
{
    public async Task<Result<Unit>> HandleAsync(
        UpdateBookingPolicyCommand command,
        CancellationToken cancellationToken)
    {
        var company = await companies.GetByIdAsync(tenantProvider.RequireCompanyId(), cancellationToken);

        if (company is null)
        {
            return Error.NotFound("Company.NotFound");
        }

        company.UpdatePolicy(command.Policy.ToDomain());

        return Result.Success();
    }
}
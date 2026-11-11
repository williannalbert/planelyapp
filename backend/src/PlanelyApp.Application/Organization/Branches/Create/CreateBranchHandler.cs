using PlanelyApp.Application.Abstractions.Cqrs;
using PlanelyApp.Application.Abstractions.Results;
using PlanelyApp.Application.Abstractions.Tenancy;
using PlanelyApp.Domain.Organization.Branches;
using PlanelyApp.Domain.SeedWork;
using PlanelyApp.Domain.SharedKernel;

namespace PlanelyApp.Application.Organization.Branches.Create;

internal sealed class CreateBranchHandler(IBranchRepository branches, ITenantProvider tenantProvider)
    : ICommandHandler<CreateBranchCommand, Guid>
{
    public async Task<Result<Guid>> HandleAsync(CreateBranchCommand command, CancellationToken cancellationToken)
    {
        var companyId = tenantProvider.RequireCompanyId();
        var name = command.Name.Trim();

        if (await branches.ExistsByNameAsync(name, null, cancellationToken))
        {
            return Error.Conflict("Branch.NameAlreadyExists");
        }

        var branch = Branch.Create(
            companyId,
            name,
            command.Address.ToDomain(),
            Phone.Create(command.Phone),
            command.TimeZoneId);

        branches.Add(branch);

        return branch.Id;
    }
}
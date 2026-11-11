using PlanelyApp.Application.Abstractions.Cqrs;
using PlanelyApp.Application.Abstractions.Results;
using PlanelyApp.Domain.Organization.Branches;
using PlanelyApp.Domain.SharedKernel;

namespace PlanelyApp.Application.Organization.Branches.UpdateDetails;

internal sealed class UpdateBranchDetailsHandler(IBranchRepository branches)
    : ICommandHandler<UpdateBranchDetailsCommand, Unit>
{
    public async Task<Result<Unit>> HandleAsync(
        UpdateBranchDetailsCommand command,
        CancellationToken cancellationToken)
    {
        var branch = await branches.GetByIdAsync(command.BranchId, cancellationToken);

        if (branch is null)
        {
            return Error.NotFound("Branch.NotFound");
        }

        var name = command.Name.Trim();

        if (await branches.ExistsByNameAsync(name, branch.Id, cancellationToken))
        {
            return Error.Conflict("Branch.NameAlreadyExists");
        }

        branch.UpdateDetails(name, command.Address.ToDomain(), Phone.Create(command.Phone), command.TimeZoneId);

        return Result.Success();
    }
}
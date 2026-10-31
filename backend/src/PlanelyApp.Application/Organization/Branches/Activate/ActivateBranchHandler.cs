using PlanelyApp.Application.Abstractions.Cqrs;
using PlanelyApp.Application.Abstractions.Results;
using PlanelyApp.Domain.Organization.Branches;

namespace PlanelyApp.Application.Organization.Branches.Activate;

internal sealed class ActivateBranchHandler(IBranchRepository branches)
    : ICommandHandler<ActivateBranchCommand, Unit>
{
    public async Task<Result<Unit>> HandleAsync(ActivateBranchCommand command, CancellationToken cancellationToken)
    {
        var branch = await branches.GetByIdAsync(command.BranchId, cancellationToken);

        if (branch is null)
        {
            return Error.NotFound("Branch.NotFound");
        }

        branch.Activate();

        return Result.Success();
    }
}
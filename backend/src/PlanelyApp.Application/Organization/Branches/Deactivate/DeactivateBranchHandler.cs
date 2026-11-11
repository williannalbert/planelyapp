using PlanelyApp.Application.Abstractions.Cqrs;
using PlanelyApp.Application.Abstractions.Results;
using PlanelyApp.Domain.Organization.Branches;

namespace PlanelyApp.Application.Organization.Branches.Deactivate;

internal sealed class DeactivateBranchHandler(IBranchRepository branches)
    : ICommandHandler<DeactivateBranchCommand, Unit>
{
    public async Task<Result<Unit>> HandleAsync(DeactivateBranchCommand command, CancellationToken cancellationToken)
    {
        var branch = await branches.GetByIdAsync(command.BranchId, cancellationToken);

        if (branch is null)
        {
            return Error.NotFound("Branch.NotFound");
        }

        branch.Deactivate();

        return Result.Success();
    }
}
using PlanelyApp.Application.Abstractions.Cqrs;
using PlanelyApp.Application.Abstractions.Results;
using PlanelyApp.Domain.Organization.Branches;

namespace PlanelyApp.Application.Organization.Branches.RemoveBusinessHours;

internal sealed class RemoveBranchBusinessHoursHandler(IBranchRepository branches)
    : ICommandHandler<RemoveBranchBusinessHoursCommand, Unit>
{
    public async Task<Result<Unit>> HandleAsync(
        RemoveBranchBusinessHoursCommand command,
        CancellationToken cancellationToken)
    {
        var branch = await branches.GetByIdAsync(command.BranchId, cancellationToken);

        if (branch is null)
        {
            return Error.NotFound("Branch.NotFound");
        }

        branch.RemoveBusinessHours(command.Day);

        return Result.Success();
    }
}
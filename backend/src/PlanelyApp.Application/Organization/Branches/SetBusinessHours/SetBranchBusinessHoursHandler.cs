using PlanelyApp.Application.Abstractions.Cqrs;
using PlanelyApp.Application.Abstractions.Results;
using PlanelyApp.Domain.Organization.Branches;

namespace PlanelyApp.Application.Organization.Branches.SetBusinessHours;

internal sealed class SetBranchBusinessHoursHandler(IBranchRepository branches)
    : ICommandHandler<SetBranchBusinessHoursCommand, Unit>
{
    public async Task<Result<Unit>> HandleAsync(
        SetBranchBusinessHoursCommand command,
        CancellationToken cancellationToken)
    {
        var branch = await branches.GetByIdAsync(command.BranchId, cancellationToken);

        if (branch is null)
        {
            return Error.NotFound("Branch.NotFound");
        }

        branch.SetBusinessHours(command.Schedule.ToDomain());

        return Result.Success();
    }
}
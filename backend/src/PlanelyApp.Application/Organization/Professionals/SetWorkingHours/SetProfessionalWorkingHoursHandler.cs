using PlanelyApp.Application.Abstractions.Cqrs;
using PlanelyApp.Application.Abstractions.Results;
using PlanelyApp.Domain.Organization.Branches;
using PlanelyApp.Domain.Organization.Professionals;

namespace PlanelyApp.Application.Organization.Professionals.SetWorkingHours;

internal sealed class SetProfessionalWorkingHoursHandler(IProfessionalRepository professionals, IBranchRepository branches)
    : ICommandHandler<SetProfessionalWorkingHoursCommand, Unit>
{
    public async Task<Result<Unit>> HandleAsync(
        SetProfessionalWorkingHoursCommand command,
        CancellationToken cancellationToken)
    {
        var professional = await professionals.GetByIdAsync(command.ProfessionalId, cancellationToken);

        if (professional is null)
        {
            return Error.NotFound("Professional.NotFound");
        }

        // The Domain keeps only the id of the branch. That the branch exists in this company is checked here,
        // through the repository, which already hides the branches of other companies.
        var branch = await branches.GetByIdAsync(command.BranchId, cancellationToken);

        if (branch is null)
        {
            return Error.NotFound("Branch.NotFound");
        }

        // The clash with the hours at another branch (RN10) is the professional's own rule.
        professional.SetWorkingHours(branch.Id, command.Schedule.ToDomain());

        return Result.Success();
    }
}
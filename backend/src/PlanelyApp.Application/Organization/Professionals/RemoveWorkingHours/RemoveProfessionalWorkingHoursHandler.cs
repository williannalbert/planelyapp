using PlanelyApp.Application.Abstractions.Cqrs;
using PlanelyApp.Application.Abstractions.Results;
using PlanelyApp.Domain.Organization.Professionals;

namespace PlanelyApp.Application.Organization.Professionals.RemoveWorkingHours;

internal sealed class RemoveProfessionalWorkingHoursHandler(IProfessionalRepository professionals)
    : ICommandHandler<RemoveProfessionalWorkingHoursCommand, Unit>
{
    public async Task<Result<Unit>> HandleAsync(
        RemoveProfessionalWorkingHoursCommand command,
        CancellationToken cancellationToken)
    {
        var professional = await professionals.GetByIdAsync(command.ProfessionalId, cancellationToken);

        if (professional is null)
        {
            return Error.NotFound("Professional.NotFound");
        }

        // No check on the branch: hours at a branch that no longer matters must still be removable.
        professional.RemoveWorkingHours(command.BranchId, command.Day);

        return Result.Success();
    }
}
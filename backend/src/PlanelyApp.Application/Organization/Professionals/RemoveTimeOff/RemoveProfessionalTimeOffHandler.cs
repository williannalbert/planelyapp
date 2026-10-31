using PlanelyApp.Application.Abstractions.Cqrs;
using PlanelyApp.Application.Abstractions.Results;
using PlanelyApp.Domain.Organization.Professionals;

namespace PlanelyApp.Application.Organization.Professionals.RemoveTimeOff;

internal sealed class RemoveProfessionalTimeOffHandler(IProfessionalRepository professionals)
    : ICommandHandler<RemoveProfessionalTimeOffCommand, Unit>
{
    public async Task<Result<Unit>> HandleAsync(
        RemoveProfessionalTimeOffCommand command,
        CancellationToken cancellationToken)
    {
        var professional = await professionals.GetByIdAsync(command.ProfessionalId, cancellationToken);

        if (professional is null)
        {
            return Error.NotFound("Professional.NotFound");
        }

        // The Domain throws the same code (a 422) if asked to remove what is not there. Checking first
        // gives the client the right answer for a missing thing: a 404.
        if (professional.TimeOffs.All(timeOff => timeOff.Id != command.TimeOffId))
        {
            return Error.NotFound("Professional.TimeOffNotFound");
        }

        professional.RemoveTimeOff(command.TimeOffId);

        return Result.Success();
    }
}
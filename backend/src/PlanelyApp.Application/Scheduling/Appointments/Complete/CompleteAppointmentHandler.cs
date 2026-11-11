using PlanelyApp.Application.Abstractions.Cqrs;
using PlanelyApp.Application.Abstractions.Identity;
using PlanelyApp.Application.Abstractions.Results;

namespace PlanelyApp.Application.Scheduling.Appointments.Complete;

internal sealed class CompleteAppointmentHandler(
    AppointmentAccess access,
    ICurrentUser currentUser,
    TimeProvider timeProvider)
    : ICommandHandler<CompleteAppointmentCommand, Unit>
{
    public async Task<Result<Unit>> HandleAsync(CompleteAppointmentCommand command, CancellationToken cancellationToken)
    {
        var loaded = await access.LoadForStaffAsync(command.AppointmentId, cancellationToken);

        if (loaded.IsFailure)
        {
            return loaded.Error;
        }

        loaded.Value.Complete(currentUser.Id, timeProvider.GetUtcNow().UtcDateTime);

        return Result.Success();
    }
}
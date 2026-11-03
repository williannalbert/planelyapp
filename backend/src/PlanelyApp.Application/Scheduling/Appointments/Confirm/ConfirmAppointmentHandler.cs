using PlanelyApp.Application.Abstractions.Cqrs;
using PlanelyApp.Application.Abstractions.Identity;
using PlanelyApp.Application.Abstractions.Results;

namespace PlanelyApp.Application.Scheduling.Appointments.Confirm;

internal sealed class ConfirmAppointmentHandler(
    AppointmentAccess access,
    ICurrentUser currentUser,
    TimeProvider timeProvider)
    : ICommandHandler<ConfirmAppointmentCommand, Unit>
{
    public async Task<Result<Unit>> HandleAsync(ConfirmAppointmentCommand command, CancellationToken cancellationToken)
    {
        var loaded = await access.LoadForStaffAsync(command.AppointmentId, cancellationToken);

        if (loaded.IsFailure)
        {
            return loaded.Error;
        }

        // The state machine and the pending price are the Domain's.
        loaded.Value.Confirm(currentUser.Id, timeProvider.GetUtcNow().UtcDateTime);

        return Result.Success();
    }
}
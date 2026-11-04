using PlanelyApp.Application.Abstractions.Cqrs;
using PlanelyApp.Application.Abstractions.Results;
using PlanelyApp.Domain.Scheduling.Appointments;

namespace PlanelyApp.Application.Scheduling.Appointments.Remind;

/// <summary>
/// Run by the job, not by a person, so there is no actor and no staff check. The company is the one of the scope the
/// job opened, so the tenant filter applies as in any request.
/// </summary>
internal sealed class SendAppointmentReminderHandler(
    IAppointmentRepository appointments,
    TimeProvider timeProvider)
    : ICommandHandler<SendAppointmentReminderCommand, bool>
{
    public async Task<Result<bool>> HandleAsync(SendAppointmentReminderCommand command, CancellationToken cancellationToken)
    {
        var appointment = await appointments.GetByIdAsync(command.AppointmentId, cancellationToken);

        if (appointment is null)
        {
            return Error.NotFound("Appointment.NotFound");
        }

        var nowUtc = timeProvider.GetUtcNow().UtcDateTime;

        // A stale list is not an error: the state changed since the job read it.
        if (!appointment.CanBeReminded(nowUtc))
        {
            return false;
        }

        appointment.MarkReminderSent(nowUtc);

        return true;
    }
}
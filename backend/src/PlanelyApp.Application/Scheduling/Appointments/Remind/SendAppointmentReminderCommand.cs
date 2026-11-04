using PlanelyApp.Application.Abstractions.Cqrs;

namespace PlanelyApp.Application.Scheduling.Appointments.Remind;

/// <summary>
/// Reminds the customer of one appointment. The result is true when the reminder was recorded and false when there was
/// nothing to do any more: the list the job read is a little old (cancelled, moved, already reminded, started).
/// </summary>
public sealed record SendAppointmentReminderCommand(Guid AppointmentId) : ICommand<bool>;
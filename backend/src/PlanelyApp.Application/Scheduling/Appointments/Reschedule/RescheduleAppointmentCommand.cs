using PlanelyApp.Application.Abstractions.Cqrs;
using PlanelyApp.Application.Abstractions.Results;

namespace PlanelyApp.Application.Scheduling.Appointments.Reschedule;

/// <summary>
/// Moves an appointment to another start (UTC), with the same professional, branch and services. The customer can do it
/// until the deadline of the policy; the staff, at any time (RN06). The price is kept.
/// </summary>
public sealed record RescheduleAppointmentCommand(Guid AppointmentId, DateTime NewStartUtc) : ICommand<Unit>;
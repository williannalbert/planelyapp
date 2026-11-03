using PlanelyApp.Application.Abstractions.Cqrs;
using PlanelyApp.Application.Abstractions.Results;

namespace PlanelyApp.Application.Scheduling.Appointments.Cancel;

/// <summary>
/// Cancels an appointment. The customer can do it until the deadline of the policy; the staff, at any time (RN06).
/// </summary>
public sealed record CancelAppointmentCommand(Guid AppointmentId, string? Reason = null) : ICommand<Unit>;
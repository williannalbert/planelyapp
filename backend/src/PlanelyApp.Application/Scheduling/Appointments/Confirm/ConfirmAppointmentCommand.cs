using PlanelyApp.Application.Abstractions.Cqrs;
using PlanelyApp.Application.Abstractions.Results;

namespace PlanelyApp.Application.Scheduling.Appointments.Confirm;

/// <summary>Confirms a pending appointment. Every service on request must already have its price (RN19).</summary>
public sealed record ConfirmAppointmentCommand(Guid AppointmentId) : ICommand<Unit>;
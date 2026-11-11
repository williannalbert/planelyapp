using PlanelyApp.Application.Abstractions.Cqrs;
using PlanelyApp.Application.Abstractions.Results;

namespace PlanelyApp.Application.Scheduling.Appointments.Complete;

/// <summary>The service ends. Only for an appointment in progress.</summary>
public sealed record CompleteAppointmentCommand(Guid AppointmentId) : ICommand<Unit>;
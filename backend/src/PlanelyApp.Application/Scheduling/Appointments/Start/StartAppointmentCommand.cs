using PlanelyApp.Application.Abstractions.Cqrs;
using PlanelyApp.Application.Abstractions.Results;

namespace PlanelyApp.Application.Scheduling.Appointments.Start;

/// <summary>The service begins. Only for a confirmed appointment.</summary>
public sealed record StartAppointmentCommand(Guid AppointmentId) : ICommand<Unit>;
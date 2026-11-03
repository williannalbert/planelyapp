using PlanelyApp.Application.Abstractions.Cqrs;
using PlanelyApp.Application.Abstractions.Results;

namespace PlanelyApp.Application.Scheduling.Appointments.MarkNoShow;

/// <summary>The customer did not show up. Only for a confirmed appointment, and only from its start time on.</summary>
public sealed record MarkAppointmentAsNoShowCommand(Guid AppointmentId) : ICommand<Unit>;
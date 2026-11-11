using PlanelyApp.Application.Abstractions.Cqrs;

namespace PlanelyApp.Application.Scheduling.Appointments.Create;

/// <summary>
/// Books a professional, at a branch, for a customer, starting at <see cref="StartUtc"/> (UTC), with these services.
/// The same service may come more than once. Returns the id of the appointment.
/// </summary>
public sealed record CreateAppointmentCommand(
    Guid BranchId,
    Guid CustomerId,
    Guid ProfessionalId,
    DateTime StartUtc,
    IReadOnlyList<Guid> ServiceIds,
    string? Notes = null) : ICommand<Guid>;
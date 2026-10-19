using PlanelyApp.Domain.SeedWork;

namespace PlanelyApp.Domain.Scheduling.Appointments;

/// <summary>
/// An appointment was created. Status tells whether it was born Confirmed or is waiting (Pending).
/// </summary>
public sealed record AppointmentCreated(
    Guid AppointmentId,
    Guid CompanyId,
    Guid BranchId,
    Guid CustomerId,
    Guid ProfessionalId,
    DateTime StartUtc,
    AppointmentStatus Status) : DomainEvent;
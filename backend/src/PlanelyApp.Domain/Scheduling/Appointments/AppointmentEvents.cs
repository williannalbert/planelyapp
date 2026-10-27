using PlanelyApp.Domain.SeedWork;
using PlanelyApp.Domain.SharedKernel;

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

/// <summary>A pending appointment was confirmed (every price known).</summary>
public sealed record AppointmentConfirmed(
    Guid AppointmentId,
    Guid CompanyId,
    Guid BranchId,
    Guid CustomerId,
    Guid ProfessionalId,
    DateTime StartUtc,
    Money TotalPrice) : DomainEvent;

/// <summary>An appointment was cancelled, by the customer or by the company.</summary>
public sealed record AppointmentCancelled(
    Guid AppointmentId,
    Guid CompanyId,
    Guid BranchId,
    Guid CustomerId,
    Guid ProfessionalId,
    DateTime StartUtc,
    string? Reason,
    bool CancelledByCustomer) : DomainEvent;

/// <summary>The appointment moved to another time. Its status did not change.</summary>
public sealed record AppointmentRescheduled(
    Guid AppointmentId,
    Guid CompanyId,
    Guid BranchId,
    Guid CustomerId,
    Guid ProfessionalId,
    DateTime PreviousStartUtc,
    DateTime NewStartUtc,
    Guid RescheduledBy,
    bool RescheduledByCustomer) : DomainEvent;

/// <summary>
/// A service was added to or removed from the appointment. Carries the resulting period and total,
/// so the consumer does not need to rebuild them.
/// </summary>
public sealed record AppointmentServicesChanged(
    Guid AppointmentId,
    Guid CompanyId,
    Guid BranchId,
    Guid CustomerId,
    Guid ProfessionalId,
    DateTime StartUtc,
    DateTime EndUtc,
    Money? TotalPrice,
    Guid ChangedBy,
    bool ChangedByCustomer) : DomainEvent;
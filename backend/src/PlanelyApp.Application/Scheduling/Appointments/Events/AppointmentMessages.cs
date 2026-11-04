using PlanelyApp.Domain.Scheduling.Appointments;

namespace PlanelyApp.Application.Scheduling.Appointments.Events;

// The data of each message (version 1). The company is in the envelope, so it is not repeated here.
// Thin on purpose: ids, instants, status, total and who acted. No name, phone, e-mail or free text.

internal sealed record AppointmentCreatedMessage(
    Guid AppointmentId,
    Guid BranchId,
    Guid CustomerId,
    Guid ProfessionalId,
    DateTime StartUtc,
    AppointmentStatus Status);

internal sealed record AppointmentConfirmedMessage(
    Guid AppointmentId,
    Guid BranchId,
    Guid CustomerId,
    Guid ProfessionalId,
    DateTime StartUtc,
    decimal TotalPrice);

internal sealed record AppointmentCancelledMessage(
    Guid AppointmentId,
    Guid BranchId,
    Guid CustomerId,
    Guid ProfessionalId,
    DateTime StartUtc,
    bool CancelledByCustomer);

internal sealed record AppointmentRescheduledMessage(
    Guid AppointmentId,
    Guid BranchId,
    Guid CustomerId,
    Guid ProfessionalId,
    DateTime PreviousStartUtc,
    DateTime NewStartUtc,
    Guid RescheduledBy,
    bool RescheduledByCustomer);

internal sealed record AppointmentServicesChangedMessage(
    Guid AppointmentId,
    Guid BranchId,
    Guid CustomerId,
    Guid ProfessionalId,
    DateTime StartUtc,
    DateTime EndUtc,
    decimal? TotalPrice,
    Guid ChangedBy,
    bool ChangedByCustomer);
using PlanelyApp.Domain.Scheduling.Appointments;

namespace PlanelyApp.Application.Scheduling.Appointments;

/// <summary>
/// What an agenda needs. <see cref="EndUtc"/> is the end of the period, so it includes the interval of the policy.
/// The total is empty while any service on request is still without a price.
/// </summary>
public sealed record AppointmentSummaryDto(
    Guid Id,
    Guid BranchId,
    string? BranchName,
    Guid CustomerId,
    string? CustomerName,
    Guid ProfessionalId,
    string? ProfessionalName,
    DateTime StartUtc,
    DateTime EndUtc,
    AppointmentStatus Status,
    decimal? TotalPrice,
    bool HasPendingPrice,
    IReadOnlyList<string> Services);
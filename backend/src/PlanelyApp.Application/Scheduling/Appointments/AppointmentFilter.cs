using PlanelyApp.Domain.Scheduling.Appointments;

namespace PlanelyApp.Application.Scheduling.Appointments;

/// <summary>
/// What the read model filters by. Everything is optional, and what is given is combined with "and".
/// </summary>
/// <param name="FromUtc">Appointments that start at this instant or later.</param>
/// <param name="ToUtc">Appointments that start before this instant.</param>
/// <param name="AllowedBranchIds">Only these branches (null is every branch; an empty list is none): RN11.</param>
/// <param name="UpcomingFromUtc">
/// Appointments that start at this instant or later and still take up the agenda (pending, confirmed or in progress).
/// </param>
/// <param name="NewestFirst">By start, from the latest to the earliest. Otherwise from the earliest to the latest.</param>
public sealed record AppointmentFilter(
    Guid? BranchId = null,
    Guid? ProfessionalId = null,
    Guid? CustomerId = null,
    DateTime? FromUtc = null,
    DateTime? ToUtc = null,
    AppointmentStatus? Status = null,
    IReadOnlyCollection<Guid>? AllowedBranchIds = null,
    DateTime? UpcomingFromUtc = null,
    bool NewestFirst = false);
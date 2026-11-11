using PlanelyApp.Application.Abstractions.Cqrs;
using PlanelyApp.Application.Abstractions.Paging;
using PlanelyApp.Domain.Scheduling.Appointments;

namespace PlanelyApp.Application.Scheduling.Appointments.ListByProfessional;

/// <summary>
/// The agenda of a professional, in every branch the staff may see: the appointments that start from
/// <see cref="FromUtc"/> up to (not including) <see cref="ToUtc"/>, both in UTC, up to 31 days, by start.
/// A professional can work in several branches, so there is no single local day to ask for.
/// </summary>
public sealed record ListProfessionalAppointmentsQuery(
    Guid ProfessionalId,
    DateTime FromUtc,
    DateTime ToUtc,
    AppointmentStatus? Status = null,
    int Page = 1,
    int PageSize = PageLimits.DefaultPageSize) : IQuery<PagedResult<AppointmentSummaryDto>>;
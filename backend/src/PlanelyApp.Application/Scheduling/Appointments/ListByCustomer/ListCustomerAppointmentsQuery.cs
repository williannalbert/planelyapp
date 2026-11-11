using PlanelyApp.Application.Abstractions.Cqrs;
using PlanelyApp.Application.Abstractions.Paging;
using PlanelyApp.Domain.Scheduling.Appointments;

namespace PlanelyApp.Application.Scheduling.Appointments.ListByCustomer;

/// <summary>
/// The appointments of a customer. A customer only asks for their own; the staff, for any customer, but only what is
/// in the branches they may see (RN11). With <see cref="OnlyUpcoming"/>, those that start from now on and still take
/// up the agenda, the nearest first; without it, all of them, the latest first.
/// </summary>
public sealed record ListCustomerAppointmentsQuery(
    Guid CustomerId,
    bool OnlyUpcoming = false,
    AppointmentStatus? Status = null,
    int Page = 1,
    int PageSize = PageLimits.DefaultPageSize) : IQuery<PagedResult<AppointmentSummaryDto>>;
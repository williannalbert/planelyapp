using PlanelyApp.Application.Abstractions.Cqrs;
using PlanelyApp.Application.Abstractions.Paging;
using PlanelyApp.Domain.Scheduling.Appointments;

namespace PlanelyApp.Application.Scheduling.Appointments.ListByBranch;

/// <summary>
/// The agenda of a branch: its appointments that start from <see cref="FromDate"/> to <see cref="ToDate"/> (both
/// included, local days of the branch, up to 31 days), by start. Only for the staff with access to the branch (RN11).
/// </summary>
public sealed record ListBranchAppointmentsQuery(
    Guid BranchId,
    DateOnly FromDate,
    DateOnly ToDate,
    Guid? ProfessionalId = null,
    AppointmentStatus? Status = null,
    int Page = 1,
    int PageSize = PageLimits.DefaultPageSize) : IQuery<PagedResult<AppointmentSummaryDto>>;
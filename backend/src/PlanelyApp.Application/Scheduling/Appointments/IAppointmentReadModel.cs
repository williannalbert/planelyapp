using PlanelyApp.Application.Abstractions.Paging;

namespace PlanelyApp.Application.Scheduling.Appointments;

/// <summary>Reads appointments for agendas, straight from the data store. The company of the request applies.</summary>
public interface IAppointmentReadModel
{
    /// <summary>One page of the appointments that match the filter, ordered by start (the id breaks ties).</summary>
    Task<PagedResult<AppointmentSummaryDto>> ListAsync(
        AppointmentFilter filter,
        int page,
        int pageSize,
        CancellationToken cancellationToken);
}
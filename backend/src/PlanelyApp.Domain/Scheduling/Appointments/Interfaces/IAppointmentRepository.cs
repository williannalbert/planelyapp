using PlanelyApp.Domain.SeedWork;

namespace PlanelyApp.Domain.Scheduling.Appointments;

public interface IAppointmentRepository : IRepository<Appointment>
{
    /// <summary>
    /// The appointments of these professionals, in any branch of the company of the request, that still occupy the
    /// agenda (pending, confirmed or in progress) and whose period overlaps [<paramref name="fromUtc"/>,
    /// <paramref name="toUtc"/>): one that only touches an end of the window is left out. The caller makes the window
    /// wide enough for the travel time between branches.
    /// </summary>
    Task<IReadOnlyList<Appointment>> ListOccupyingScheduleAsync(
        IReadOnlyCollection<Guid> professionalIds,
        DateTime fromUtc,
        DateTime toUtc,
        CancellationToken cancellationToken = default);
}
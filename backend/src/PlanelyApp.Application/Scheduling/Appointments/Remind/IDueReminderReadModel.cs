namespace PlanelyApp.Application.Scheduling.Appointments.Remind;

/// <summary>An appointment to remind, and the company the job must act as to remind it.</summary>
public sealed record DueReminder(Guid AppointmentId, Guid CompanyId);

/// <summary>
/// The one read that looks at every company at once, for the job. It only reads: each reminder is then sent inside
/// the scope of its own company.
/// </summary>
public interface IDueReminderReadModel
{
    /// <summary>
    /// Confirmed appointments with no reminder that start after <paramref name="nowUtc"/> and up to
    /// <paramref name="leadTime"/> from it, the nearest first, at most <paramref name="limit"/>.
    /// </summary>
    Task<IReadOnlyList<DueReminder>> ListAsync(
        DateTime nowUtc,
        TimeSpan leadTime,
        int limit,
        CancellationToken cancellationToken);
}
using Microsoft.EntityFrameworkCore;
using PlanelyApp.Application.Scheduling.Appointments.Remind;
using PlanelyApp.Domain.Scheduling.Appointments;

namespace PlanelyApp.Infrastructure.Persistence.Queries;

/// <summary>
/// Looks across every company on purpose (IgnoreQueryFilters), so it only reads and only returns ids. What is done with
/// each id happens in a scope that has its company.
/// </summary>
internal sealed class DueReminderReadModel(PlanelyDbContext context) : IDueReminderReadModel
{
    public async Task<IReadOnlyList<DueReminder>> ListAsync(
        DateTime nowUtc,
        TimeSpan leadTime,
        int limit,
        CancellationToken cancellationToken)
    {
        var untilUtc = nowUtc + leadTime;

        return await context.Set<Appointment>()
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(appointment => appointment.Status == AppointmentStatus.Confirmed
                && appointment.ReminderSentAtUtc == null
                && appointment.Period.Start > nowUtc
                && appointment.Period.Start <= untilUtc)
            .OrderBy(appointment => appointment.Period.Start)
            .ThenBy(appointment => appointment.Id)
            .Select(appointment => new DueReminder(appointment.Id, appointment.CompanyId))
            .Take(limit)
            .ToListAsync(cancellationToken);
    }
}
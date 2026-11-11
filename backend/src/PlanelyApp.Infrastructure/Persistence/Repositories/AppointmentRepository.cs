using Microsoft.EntityFrameworkCore;
using PlanelyApp.Domain.Scheduling.Appointments;

namespace PlanelyApp.Infrastructure.Persistence.Repositories;

internal sealed class AppointmentRepository(PlanelyDbContext context)
    : Repository<Appointment>(context), IAppointmentRepository
{
    public async Task<IReadOnlyList<Appointment>> ListOccupyingScheduleAsync(
        IReadOnlyCollection<Guid> professionalIds,
        DateTime fromUtc,
        DateTime toUtc,
        CancellationToken cancellationToken = default) =>
        await Context.Set<Appointment>()
            .Where(appointment => professionalIds.Contains(appointment.ProfessionalId)
                                  && (appointment.Status == AppointmentStatus.Pending
                                      || appointment.Status == AppointmentStatus.Confirmed
                                      || appointment.Status == AppointmentStatus.InProgress)
                                  && appointment.Period.Start < toUtc
                                  && appointment.Period.End > fromUtc)
            .ToListAsync(cancellationToken);
}
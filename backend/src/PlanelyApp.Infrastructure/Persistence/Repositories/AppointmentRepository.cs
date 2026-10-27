using PlanelyApp.Domain.Scheduling.Appointments;

namespace PlanelyApp.Infrastructure.Persistence.Repositories;

// The availability queries arrive with the use cases that need them, in phase 4.
internal sealed class AppointmentRepository(PlanelyDbContext context)
    : Repository<Appointment>(context), IAppointmentRepository;
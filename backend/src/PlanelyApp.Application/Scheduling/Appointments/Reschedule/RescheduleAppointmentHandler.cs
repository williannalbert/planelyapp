using PlanelyApp.Application.Abstractions.Cqrs;
using PlanelyApp.Application.Abstractions.Identity;
using PlanelyApp.Application.Abstractions.Results;
using PlanelyApp.Application.Abstractions.Tenancy;
using PlanelyApp.Domain.Organization.Companies;
using PlanelyApp.Domain.SeedWork;

namespace PlanelyApp.Application.Scheduling.Appointments.Reschedule;

internal sealed class RescheduleAppointmentHandler(
    AppointmentAccess access,
    AppointmentAgenda agenda,
    ICompanyRepository companies,
    ICurrentUser currentUser,
    ITenantProvider tenantProvider,
    TimeProvider timeProvider)
    : ICommandHandler<RescheduleAppointmentCommand, Unit>
{
    public async Task<Result<Unit>> HandleAsync(
        RescheduleAppointmentCommand command,
        CancellationToken cancellationToken)
    {
        var loaded = await access.LoadForActorAsync(command.AppointmentId, cancellationToken);

        if (loaded.IsFailure)
        {
            return loaded.Error;
        }

        var company = await companies.GetByIdAsync(tenantProvider.RequireCompanyId(), cancellationToken);

        if (company is null)
        {
            return Error.NotFound("Company.NotFound");
        }

        if (!company.IsActive)
        {
            return Error.Conflict("Company.Inactive");
        }

        var appointment = loaded.Value;
        var previousStart = appointment.Period.Start;
        var policy = company.BookingPolicy;
        var nowUtc = timeProvider.GetUtcNow().UtcDateTime;

        // Who moves it decides which rule applies (RN06). The Domain's rules (422) come before the clash (409).
        if (currentUser.Kind == ActorKind.Customer)
        {
            appointment.RescheduleByCustomer(currentUser.Id, command.NewStartUtc, policy, nowUtc);
        }
        else
        {
            appointment.RescheduleByStaff(currentUser.Id, command.NewStartUtc, policy, nowUtc);
        }

        // Moving to the time it already has is not a change: nothing to check and nothing to save.
        if (appointment.Period.Start == previousStart)
        {
            return Result.Success();
        }

        return await agenda.RegisterChangeAsync(appointment, policy, needsRoom: true, cancellationToken);
    }
}
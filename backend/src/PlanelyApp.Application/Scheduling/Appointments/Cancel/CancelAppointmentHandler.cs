using PlanelyApp.Application.Abstractions.Cqrs;
using PlanelyApp.Application.Abstractions.Identity;
using PlanelyApp.Application.Abstractions.Results;
using PlanelyApp.Application.Abstractions.Tenancy;
using PlanelyApp.Domain.Organization.Companies;
using PlanelyApp.Domain.SeedWork;

namespace PlanelyApp.Application.Scheduling.Appointments.Cancel;

internal sealed class CancelAppointmentHandler(
    AppointmentAccess access,
    ICompanyRepository companies,
    ICurrentUser currentUser,
    ITenantProvider tenantProvider,
    TimeProvider timeProvider)
    : ICommandHandler<CancelAppointmentCommand, Unit>
{
    public async Task<Result<Unit>> HandleAsync(CancelAppointmentCommand command, CancellationToken cancellationToken)
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

        var appointment = loaded.Value;
        var nowUtc = timeProvider.GetUtcNow().UtcDateTime;

        // Who cancels decides which rule applies (RN06): the Domain has one method for each, not a flag.
        if (currentUser.Kind == ActorKind.Customer)
        {
            appointment.CancelByCustomer(currentUser.Id, company.BookingPolicy, nowUtc, command.Reason);
        }
        else
        {
            appointment.CancelByStaff(currentUser.Id, nowUtc, command.Reason);
        }

        return Result.Success();
    }
}
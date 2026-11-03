using PlanelyApp.Application.Abstractions.Cqrs;
using PlanelyApp.Application.Abstractions.Results;
using PlanelyApp.Application.Abstractions.Tenancy;
using PlanelyApp.Domain.Organization.Companies;
using PlanelyApp.Domain.SeedWork;
using PlanelyApp.Domain.SharedKernel;

namespace PlanelyApp.Application.Scheduling.Appointments.SetItemPrice;

internal sealed class SetAppointmentItemPriceHandler(
    AppointmentAccess access,
    AppointmentAgenda agenda,
    ICompanyRepository companies,
    ITenantProvider tenantProvider)
    : ICommandHandler<SetAppointmentItemPriceCommand, Unit>
{
    public async Task<Result<Unit>> HandleAsync(
        SetAppointmentItemPriceCommand command,
        CancellationToken cancellationToken)
    {
        var loaded = await access.LoadForStaffAsync(command.AppointmentId, cancellationToken);

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
        var previousEnd = appointment.Period.End;

        TimeSpan? duration = command.DurationMinutes is { } minutes ? TimeSpan.FromMinutes(minutes) : null;

        // The state, the item and the value are the Domain's (422).
        appointment.SetItemPrice(command.ItemId, Money.From(command.Price), duration);

        // Only a longer period can stop it from fitting; a shorter one only frees time.
        var needsRoom = appointment.Period.End > previousEnd;

        return await agenda.RegisterChangeAsync(appointment, company.BookingPolicy, needsRoom, cancellationToken);
    }
}
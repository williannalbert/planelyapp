using PlanelyApp.Application.Abstractions.Events;
using PlanelyApp.Application.Abstractions.Messaging;
using PlanelyApp.Application.Scheduling.Appointments.Events;
using PlanelyApp.Domain.Scheduling.Appointments;

namespace PlanelyApp.Application.Scheduling.Appointments.Events;

internal sealed class AppointmentConfirmedOutboxHandler(IOutbox outbox) : IDomainEventHandler<AppointmentConfirmed>
{
    public Task HandleAsync(AppointmentConfirmed domainEvent, CancellationToken cancellationToken)
    {
        outbox.Add(
            AppointmentMessageTypes.Confirmed,
            domainEvent.CompanyId,
            new AppointmentConfirmedMessage(
                domainEvent.AppointmentId,
                domainEvent.BranchId,
                domainEvent.CustomerId,
                domainEvent.ProfessionalId,
                domainEvent.StartUtc,
                domainEvent.TotalPrice.Amount),
            domainEvent.OccurredAtUtc);

        return Task.CompletedTask;
    }
}
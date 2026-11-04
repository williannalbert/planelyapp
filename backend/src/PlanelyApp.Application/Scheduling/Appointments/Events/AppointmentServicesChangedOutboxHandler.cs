using PlanelyApp.Application.Abstractions.Events;
using PlanelyApp.Application.Abstractions.Messaging;
using PlanelyApp.Application.Scheduling.Appointments.Events;
using PlanelyApp.Domain.Scheduling.Appointments;

namespace PlanelyApp.Application.Scheduling.Appointments.Events;

internal sealed class AppointmentServicesChangedOutboxHandler(IOutbox outbox)
    : IDomainEventHandler<AppointmentServicesChanged>
{
    public Task HandleAsync(AppointmentServicesChanged domainEvent, CancellationToken cancellationToken)
    {
        outbox.Add(
            AppointmentMessageTypes.ServicesChanged,
            domainEvent.CompanyId,
            new AppointmentServicesChangedMessage(
                domainEvent.AppointmentId,
                domainEvent.BranchId,
                domainEvent.CustomerId,
                domainEvent.ProfessionalId,
                domainEvent.StartUtc,
                domainEvent.EndUtc,
                domainEvent.TotalPrice?.Amount,
                domainEvent.ChangedBy,
                domainEvent.ChangedByCustomer),
            domainEvent.OccurredAtUtc);

        return Task.CompletedTask;
    }
}
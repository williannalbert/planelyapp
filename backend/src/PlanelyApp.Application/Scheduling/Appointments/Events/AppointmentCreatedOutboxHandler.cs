using PlanelyApp.Application.Abstractions.Events;
using PlanelyApp.Application.Abstractions.Messaging;
using PlanelyApp.Domain.Scheduling.Appointments;

namespace PlanelyApp.Application.Scheduling.Appointments.Events;

/// <summary>Turns the domain event into the message for other systems. It runs inside the transaction of the use case.</summary>
internal sealed class AppointmentCreatedOutboxHandler(IOutbox outbox) : IDomainEventHandler<AppointmentCreated>
{
    public Task HandleAsync(AppointmentCreated domainEvent, CancellationToken cancellationToken)
    {
        outbox.Add(
            AppointmentMessageTypes.Created,
            domainEvent.CompanyId,
            new AppointmentCreatedMessage(
                domainEvent.AppointmentId,
                domainEvent.BranchId,
                domainEvent.CustomerId,
                domainEvent.ProfessionalId,
                domainEvent.StartUtc,
                domainEvent.Status),
            domainEvent.OccurredAtUtc);

        return Task.CompletedTask;
    }
}
using PlanelyApp.Application.Abstractions.Events;
using PlanelyApp.Application.Abstractions.Messaging;
using PlanelyApp.Application.Scheduling.Appointments.Events;
using PlanelyApp.Domain.Scheduling.Appointments;

namespace PlanelyApp.Application.Scheduling.Appointments.Events;

internal sealed class AppointmentCancelledOutboxHandler(IOutbox outbox) : IDomainEventHandler<AppointmentCancelled>
{
    public Task HandleAsync(AppointmentCancelled domainEvent, CancellationToken cancellationToken)
    {
        // The reason is free text and may hold personal data: it does not leave the monolith.
        outbox.Add(
            AppointmentMessageTypes.Cancelled,
            domainEvent.CompanyId,
            new AppointmentCancelledMessage(
                domainEvent.AppointmentId,
                domainEvent.BranchId,
                domainEvent.CustomerId,
                domainEvent.ProfessionalId,
                domainEvent.StartUtc,
                domainEvent.CancelledByCustomer),
            domainEvent.OccurredAtUtc);

        return Task.CompletedTask;
    }
}
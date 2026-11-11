using PlanelyApp.Application.Abstractions.Events;
using PlanelyApp.Application.Abstractions.Messaging;
using PlanelyApp.Application.Scheduling.Appointments.Events;
using PlanelyApp.Domain.Scheduling.Appointments;

namespace PlanelyApp.Application.Scheduling.Appointments.Events;

internal sealed class AppointmentRescheduledOutboxHandler(IOutbox outbox) : IDomainEventHandler<AppointmentRescheduled>
{
    public Task HandleAsync(AppointmentRescheduled domainEvent, CancellationToken cancellationToken)
    {
        outbox.Add(
            AppointmentMessageTypes.Rescheduled,
            domainEvent.CompanyId,
            new AppointmentRescheduledMessage(
                domainEvent.AppointmentId,
                domainEvent.BranchId,
                domainEvent.CustomerId,
                domainEvent.ProfessionalId,
                domainEvent.PreviousStartUtc,
                domainEvent.NewStartUtc,
                domainEvent.RescheduledBy,
                domainEvent.RescheduledByCustomer),
            domainEvent.OccurredAtUtc);

        return Task.CompletedTask;
    }
}
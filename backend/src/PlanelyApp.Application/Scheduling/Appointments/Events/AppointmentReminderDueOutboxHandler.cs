using PlanelyApp.Application.Abstractions.Events;
using PlanelyApp.Application.Abstractions.Messaging;
using PlanelyApp.Domain.Scheduling.Appointments;

namespace PlanelyApp.Application.Scheduling.Appointments.Events;

internal sealed class AppointmentReminderDueOutboxHandler(IOutbox outbox) : IDomainEventHandler<AppointmentReminderDue>
{
    public Task HandleAsync(AppointmentReminderDue domainEvent, CancellationToken cancellationToken)
    {
        outbox.Add(
            AppointmentMessageTypes.ReminderDue,
            domainEvent.CompanyId,
            new AppointmentReminderDueMessage(
                domainEvent.AppointmentId,
                domainEvent.BranchId,
                domainEvent.CustomerId,
                domainEvent.ProfessionalId,
                domainEvent.StartUtc),
            domainEvent.OccurredAtUtc);

        return Task.CompletedTask;
    }
}
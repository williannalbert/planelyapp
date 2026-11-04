using PlanelyApp.Application.Abstractions.Events;
using PlanelyApp.Application.Abstractions.Messaging;
using PlanelyApp.Domain.Organization.Customers;

namespace PlanelyApp.Application.Organization.Customers.Events;

/// <summary>Contact, profile or marketing consent changed. It does not say which: the consumer reads the customer.</summary>
internal sealed class CustomerUpdatedOutboxHandler(IOutbox outbox) : IDomainEventHandler<CustomerUpdated>
{
    public Task HandleAsync(CustomerUpdated domainEvent, CancellationToken cancellationToken)
    {
        outbox.Add(
            CustomerMessageTypes.Updated,
            domainEvent.CompanyId,
            new CustomerUpdatedMessage(domainEvent.CustomerId),
            domainEvent.OccurredAtUtc);

        return Task.CompletedTask;
    }
}
using PlanelyApp.Application.Abstractions.Events;
using PlanelyApp.Application.Abstractions.Messaging;
using PlanelyApp.Domain.Organization.Customers;

namespace PlanelyApp.Application.Organization.Customers.Events;

internal sealed class CustomerRegisteredOutboxHandler(IOutbox outbox) : IDomainEventHandler<CustomerRegistered>
{
    public Task HandleAsync(CustomerRegistered domainEvent, CancellationToken cancellationToken)
    {
        outbox.Add(
            CustomerMessageTypes.Registered,
            domainEvent.CompanyId,
            new CustomerRegisteredMessage(domainEvent.CustomerId),
            domainEvent.OccurredAtUtc);

        return Task.CompletedTask;
    }
}
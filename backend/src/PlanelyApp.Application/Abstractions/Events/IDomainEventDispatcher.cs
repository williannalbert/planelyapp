using PlanelyApp.Domain.SeedWork;

namespace PlanelyApp.Application.Abstractions.Events;

/// <summary>Sends an event to all its handlers, one after the other. An event with no handler is fine.</summary>
public interface IDomainEventDispatcher
{
    Task DispatchAsync(IDomainEvent domainEvent, CancellationToken cancellationToken);
}
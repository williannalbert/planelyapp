using PlanelyApp.Domain.SeedWork;

namespace PlanelyApp.Application.Abstractions.Events;

/// <summary>
/// Reacts to something that happened in the Domain, inside the unit of work of the command that caused it.
/// Typical use: leave a message in the outbox. It may change other aggregates; the events they raise are dispatched too.
/// </summary>
public interface IDomainEventHandler<TEvent>
    where TEvent : IDomainEvent
{
    Task HandleAsync(TEvent domainEvent, CancellationToken cancellationToken);
}
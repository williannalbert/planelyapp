using PlanelyApp.Domain.SeedWork;

namespace PlanelyApp.Application.Abstractions.Events;

/// <summary>
/// Where the events raised by the aggregates of the current unit of work are collected. The Application cannot
/// see EF Core's change tracker, so the Infrastructure implements this.
/// </summary>
public interface IDomainEventSource
{
    /// <summary>Returns the events raised so far, oldest first, and forgets them: each event is handed over once.</summary>
    IReadOnlyList<IDomainEvent> TakeDomainEvents();
}
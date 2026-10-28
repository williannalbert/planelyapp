using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using PlanelyApp.Domain.SeedWork;

namespace PlanelyApp.Application.Abstractions.Events;

/// <summary>Same idea as the request dispatcher: one small invoker per event type, built once and reused.</summary>
internal sealed class DomainEventDispatcher(IServiceProvider services) : IDomainEventDispatcher
{
    private static readonly ConcurrentDictionary<Type, DomainEventInvoker> Invokers = new();

    public Task DispatchAsync(IDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        var invoker = Invokers.GetOrAdd(
            domainEvent.GetType(),
            eventType => (DomainEventInvoker)Activator.CreateInstance(
                typeof(DomainEventInvoker<>).MakeGenericType(eventType))!);

        return invoker.InvokeAsync(services, domainEvent, cancellationToken);
    }

    private abstract class DomainEventInvoker
    {
        public abstract Task InvokeAsync(
            IServiceProvider services, IDomainEvent domainEvent, CancellationToken cancellationToken);
    }

    private sealed class DomainEventInvoker<TEvent> : DomainEventInvoker
        where TEvent : IDomainEvent
    {
        public override async Task InvokeAsync(
            IServiceProvider services, IDomainEvent domainEvent, CancellationToken cancellationToken)
        {
            // One after the other: they share the same DbContext, which is not thread safe.
            foreach (var handler in services.GetServices<IDomainEventHandler<TEvent>>())
            {
                await handler.HandleAsync((TEvent)domainEvent, cancellationToken);
            }
        }
    }
}
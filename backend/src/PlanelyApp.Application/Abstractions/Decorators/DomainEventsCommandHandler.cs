using PlanelyApp.Application.Abstractions.Cqrs;
using PlanelyApp.Application.Abstractions.Events;
using PlanelyApp.Application.Abstractions.Results;

namespace PlanelyApp.Application.Abstractions.Decorators;

/// <summary>
/// After a successful handler and before the save, hands the events the aggregates raised to their handlers.
/// Inside the transaction decorator, so what the event handlers write (the outbox, for one) is saved with the change.
/// </summary>
internal sealed class DomainEventsCommandHandler<TCommand, TResponse>(
    ICommandHandler<TCommand, TResponse> inner,
    IDomainEventSource source,
    IDomainEventDispatcher dispatcher) : ICommandHandler<TCommand, TResponse>
    where TCommand : ICommand<TResponse>
{
    // An event handler may change another aggregate, which raises another event. A few rounds are normal;
    // an endless chain is a bug and must stop instead of hanging the request.
    private const int MaxRounds = 10;

    public async Task<Result<TResponse>> HandleAsync(TCommand command, CancellationToken cancellationToken)
    {
        var result = await inner.HandleAsync(command, cancellationToken);

        if (result.IsFailure)
        {
            return result;
        }

        for (var round = 0; ; round++)
        {
            var events = source.TakeDomainEvents();

            if (events.Count == 0)
            {
                return result;
            }

            if (round == MaxRounds)
            {
                throw new InvalidOperationException(
                    $"The domain events of {typeof(TCommand).Name} did not settle after {MaxRounds} rounds: some handler keeps raising new events.");
            }

            foreach (var domainEvent in events)
            {
                await dispatcher.DispatchAsync(domainEvent, cancellationToken);
            }
        }
    }
}
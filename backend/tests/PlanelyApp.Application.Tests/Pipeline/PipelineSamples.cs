using FluentValidation;
using Microsoft.Extensions.Logging;
using PlanelyApp.Application.Abstractions.Cqrs;
using PlanelyApp.Application.Abstractions.Events;
using PlanelyApp.Application.Abstractions.Results;
using PlanelyApp.Domain.SeedWork;

namespace PlanelyApp.Application.Tests.Pipeline;

/// <summary>What happened, in order, across the fakes: the pipeline is about order.</summary>
internal sealed class OperationLog
{
    public List<string> Entries { get; } = [];

    public void Add(string entry) => Entries.Add(entry);
}

internal sealed class FakeUnitOfWork(OperationLog log) : IUnitOfWork
{
    public int Saves { get; private set; }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        Saves++;
        log.Add("save");

        return Task.FromResult(1);
    }
}

/// <summary>Plays the part of the aggregates: what is raised here comes out on the next TakeDomainEvents.</summary>
internal sealed class FakeDomainEventSource : IDomainEventSource
{
    private readonly List<IDomainEvent> _pending = [];

    public void Raise(IDomainEvent domainEvent) => _pending.Add(domainEvent);

    public IReadOnlyList<IDomainEvent> TakeDomainEvents()
    {
        var events = _pending.ToList();
        _pending.Clear();

        return events;
    }
}

internal sealed record LogEntry(LogLevel Level, string Message, Exception? Exception);

internal sealed class ListLogger<T> : ILogger<T>
{
    public List<LogEntry> Entries { get; } = [];

    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter) =>
        Entries.Add(new LogEntry(logLevel, formatter(state, exception), exception));
}

// The item picks the story: "missing" fails, "boom" throws, "refused" is refused by the domain,
// "quiet" raises an event nobody handles, "loop" raises events forever, "faulty" raises one whose handler fails.
internal sealed record PlaceOrderCommand(string Item, int Quantity, string? Note = null) : ICommand<Guid>;

internal sealed class PlaceOrderValidator : AbstractValidator<PlaceOrderCommand>
{
    public PlaceOrderValidator()
    {
        RuleFor(command => command.Item).NotEmpty().MaximumLength(10);
        RuleFor(command => command.Quantity).GreaterThan(0);
    }
}

/// <summary>A second validator for the same command: all of them run.</summary>
internal sealed class PlaceOrderRulesValidator : AbstractValidator<PlaceOrderCommand>
{
    public PlaceOrderRulesValidator()
    {
        RuleFor(command => command.Item).Must(item => item != "forbidden").WithErrorCode("Order.ItemForbidden");
        RuleFor(command => command.Note).Must(note => note is null || !note.Contains('!'));
    }
}

internal sealed class PlaceOrderHandler(OperationLog log, FakeDomainEventSource source)
    : ICommandHandler<PlaceOrderCommand, Guid>
{
    public Task<Result<Guid>> HandleAsync(PlaceOrderCommand command, CancellationToken cancellationToken)
    {
        log.Add("handler");

        switch (command.Item)
        {
            case "missing":
                return Task.FromResult(Result<Guid>.Failure(Error.NotFound("Order.NotFound")));
            case "boom":
                throw new InvalidOperationException("boom");
            case "refused":
                throw new DomainException("Order.Refused");
            case "quiet":
                source.Raise(new OrderQuiet());
                break;
            case "loop":
                source.Raise(new LoopTick());
                break;
            case "faulty":
                source.Raise(new OrderFaulty());
                break;
            default:
                source.Raise(new OrderPlaced());
                break;
        }

        return Task.FromResult(Result<Guid>.Success(Guid.NewGuid()));
    }
}

internal sealed record OrderPlaced : DomainEvent;

internal sealed record OrderNotified : DomainEvent;

internal sealed record OrderQuiet : DomainEvent;

internal sealed record LoopTick : DomainEvent;

internal sealed record OrderFaulty : DomainEvent;

internal sealed class OrderPlacedHandler(OperationLog log, FakeDomainEventSource source)
    : IDomainEventHandler<OrderPlaced>
{
    public Task HandleAsync(OrderPlaced domainEvent, CancellationToken cancellationToken)
    {
        log.Add("event:OrderPlaced");
        source.Raise(new OrderNotified());

        return Task.CompletedTask;
    }
}

internal sealed class OrderNotifiedHandler(OperationLog log) : IDomainEventHandler<OrderNotified>
{
    public Task HandleAsync(OrderNotified domainEvent, CancellationToken cancellationToken)
    {
        log.Add("event:OrderNotified");

        return Task.CompletedTask;
    }
}

internal sealed class LoopTickHandler(FakeDomainEventSource source) : IDomainEventHandler<LoopTick>
{
    public Task HandleAsync(LoopTick domainEvent, CancellationToken cancellationToken)
    {
        source.Raise(new LoopTick());

        return Task.CompletedTask;
    }
}

internal sealed class OrderFaultyHandler : IDomainEventHandler<OrderFaulty>
{
    public Task HandleAsync(OrderFaulty domainEvent, CancellationToken cancellationToken) =>
        throw new NotSupportedException("faulty");
}

internal sealed record ListOrdersQuery(string Filter) : IQuery<int>;

internal sealed class ListOrdersValidator : AbstractValidator<ListOrdersQuery>
{
    public ListOrdersValidator() => RuleFor(query => query.Filter).NotEmpty();
}

internal sealed class ListOrdersHandler(OperationLog log) : IQueryHandler<ListOrdersQuery, int>
{
    public Task<Result<int>> HandleAsync(ListOrdersQuery query, CancellationToken cancellationToken)
    {
        log.Add("query-handler");

        return Task.FromResult(Result<int>.Success(3));
    }
}
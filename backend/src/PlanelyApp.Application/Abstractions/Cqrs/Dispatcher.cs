using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using PlanelyApp.Application.Abstractions.Results;

namespace PlanelyApp.Application.Abstractions.Cqrs;

/// <summary>
/// Finds the handler of a request by its type and calls it. The type is only known at run time, but the handler
/// interface is generic, so each request type gets a small invoker that knows both types at compile time.
/// The invoker is built once per request type (that is the slow part) and reused.
/// </summary>
internal sealed class Dispatcher(IServiceProvider services) : IDispatcher
{
    private static readonly ConcurrentDictionary<Type, object> CommandInvokers = new();
    private static readonly ConcurrentDictionary<Type, object> QueryInvokers = new();

    public Task<Result<TResponse>> SendAsync<TResponse>(
        ICommand<TResponse> command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var invoker = (CommandInvoker<TResponse>)CommandInvokers.GetOrAdd(
            command.GetType(),
            commandType => Activator.CreateInstance(
                typeof(CommandInvoker<,>).MakeGenericType(commandType, typeof(TResponse)))!);

        return invoker.InvokeAsync(services, command, cancellationToken);
    }

    public Task<Result<TResponse>> QueryAsync<TResponse>(
        IQuery<TResponse> query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var invoker = (QueryInvoker<TResponse>)QueryInvokers.GetOrAdd(
            query.GetType(),
            queryType => Activator.CreateInstance(
                typeof(QueryInvoker<,>).MakeGenericType(queryType, typeof(TResponse)))!);

        return invoker.InvokeAsync(services, query, cancellationToken);
    }

    private abstract class CommandInvoker<TResponse>
    {
        public abstract Task<Result<TResponse>> InvokeAsync(
            IServiceProvider services, ICommand<TResponse> command, CancellationToken cancellationToken);
    }

    private sealed class CommandInvoker<TCommand, TResponse> : CommandInvoker<TResponse>
        where TCommand : ICommand<TResponse>
    {
        public override Task<Result<TResponse>> InvokeAsync(
            IServiceProvider services, ICommand<TResponse> command, CancellationToken cancellationToken) =>
            services.GetRequiredService<ICommandHandler<TCommand, TResponse>>()
                .HandleAsync((TCommand)command, cancellationToken);
    }

    private abstract class QueryInvoker<TResponse>
    {
        public abstract Task<Result<TResponse>> InvokeAsync(
            IServiceProvider services, IQuery<TResponse> query, CancellationToken cancellationToken);
    }

    private sealed class QueryInvoker<TQuery, TResponse> : QueryInvoker<TResponse>
        where TQuery : IQuery<TResponse>
    {
        public override Task<Result<TResponse>> InvokeAsync(
            IServiceProvider services, IQuery<TResponse> query, CancellationToken cancellationToken) =>
            services.GetRequiredService<IQueryHandler<TQuery, TResponse>>()
                .HandleAsync((TQuery)query, cancellationToken);
    }
}
using PlanelyApp.Application.Abstractions.Results;

namespace PlanelyApp.Application.Abstractions.Cqrs;

/// <summary>The single door to the use cases: the Api sends a command or a query and gets the result back.</summary>
public interface IDispatcher
{
    Task<Result<TResponse>> SendAsync<TResponse>(ICommand<TResponse> command, CancellationToken cancellationToken = default);

    Task<Result<TResponse>> QueryAsync<TResponse>(IQuery<TResponse> query, CancellationToken cancellationToken = default);
}
using Microsoft.Extensions.Logging;
using PlanelyApp.Application.Abstractions.Cqrs;
using PlanelyApp.Application.Abstractions.Results;

namespace PlanelyApp.Application.Abstractions.Decorators;

internal sealed class LoggingQueryHandler<TQuery, TResponse>(
    IQueryHandler<TQuery, TResponse> inner,
    ILogger<UseCase> logger,
    TimeProvider timeProvider) : IQueryHandler<TQuery, TResponse>
    where TQuery : IQuery<TResponse>
{
    public Task<Result<TResponse>> HandleAsync(TQuery query, CancellationToken cancellationToken) =>
        RequestLogging.RunAsync(
            logger, timeProvider, typeof(TQuery).Name, () => inner.HandleAsync(query, cancellationToken));
}
using Microsoft.Extensions.Logging;
using PlanelyApp.Application.Abstractions.Cqrs;
using PlanelyApp.Application.Abstractions.Results;

namespace PlanelyApp.Application.Abstractions.Decorators;

internal sealed class LoggingCommandHandler<TCommand, TResponse>(
    ICommandHandler<TCommand, TResponse> inner,
    ILogger<UseCase> logger,
    TimeProvider timeProvider) : ICommandHandler<TCommand, TResponse>
    where TCommand : ICommand<TResponse>
{
    public Task<Result<TResponse>> HandleAsync(TCommand command, CancellationToken cancellationToken) =>
        RequestLogging.RunAsync(
            logger, timeProvider, typeof(TCommand).Name, () => inner.HandleAsync(command, cancellationToken));
}
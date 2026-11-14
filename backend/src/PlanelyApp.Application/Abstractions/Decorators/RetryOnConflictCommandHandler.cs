using Microsoft.Extensions.Logging;
using PlanelyApp.Application.Abstractions.Cqrs;
using PlanelyApp.Application.Abstractions.Results;
using PlanelyApp.Domain.SeedWork;

namespace PlanelyApp.Application.Abstractions.Decorators;

/// <summary>
/// Runs a command again when it lost a race, up to <see cref="MaxRetries"/> more times. It sits outside the transaction,
/// so each run has its own save, and it first discards what the failed run left tracked. Only for commands that
/// implement <see cref="IRetryOnConflict"/>; any other command, a failed result or another exception passes through.
/// </summary>
internal sealed class RetryOnConflictCommandHandler<TCommand, TResponse>(
    ICommandHandler<TCommand, TResponse> inner,
    IUnitOfWork unitOfWork,
    ILogger<UseCase> logger,
    TimeProvider timeProvider) : ICommandHandler<TCommand, TResponse>
    where TCommand : ICommand<TResponse>
{
    private const int MaxRetries = 2;

    public async Task<Result<TResponse>> HandleAsync(TCommand command, CancellationToken cancellationToken)
    {
        if (command is not IRetryOnConflict)
        {
            return await inner.HandleAsync(command, cancellationToken);
        }

        for (var attempt = 0; ; attempt++)
        {
            try
            {
                return await inner.HandleAsync(command, cancellationToken);
            }
            catch (ConcurrencyConflictException) when (attempt < MaxRetries)
            {
                logger.LogInformation(
                    "{Request} lost a race and runs again (retry {Retry} of {MaxRetries})",
                    typeof(TCommand).Name, attempt + 1, MaxRetries);

                unitOfWork.DiscardChanges();

                // A little random wait, so two requests that collided do not collide again at the same instant.
                var delay = TimeSpan.FromMilliseconds(Random.Shared.Next(20, 60) * (attempt + 1));
                await Task.Delay(delay, timeProvider, cancellationToken);
            }
        }
    }
}
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace PlanelyApp.Infrastructure.Messaging.Outbox;

/// <summary>
/// Runs the processor for as long as the application lives. It knows nothing about RabbitMQ or about any message:
/// it only asks for a pass, and waits (longer after failures) before the next one.
/// </summary>
internal sealed class OutboxPublisherService(
    IServiceScopeFactory scopeFactory,
    OutboxOptions options,
    TimeProvider timeProvider,
    ILogger<OutboxPublisherService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var failures = 0;

        while (!stoppingToken.IsCancellationRequested)
        {
            OutboxRunResult result;

            try
            {
                // A scope per pass: a DbContext must not live as long as a singleton service.
                await using var scope = scopeFactory.CreateAsyncScope();
                result = await scope.ServiceProvider.GetRequiredService<OutboxProcessor>().ProcessPendingAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                // The database may be down. The service must survive: an unhandled exception here stops the host.
                logger.LogError(exception, "The outbox could not be processed.");
                result = new OutboxRunResult(0, Failed: true);
            }

            failures = result.Failed ? failures + 1 : 0;

            // A full batch means there is probably more waiting: go again without a pause.
            if (!result.Failed && result.Published >= options.BatchSize)
            {
                continue;
            }

            try
            {
                await Task.Delay(DelayAfter(failures), timeProvider, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private TimeSpan DelayAfter(int failures)
    {
        if (failures == 0)
        {
            return options.PollingInterval;
        }

        var factor = 1L << Math.Min(failures, 10);
        var delay = TimeSpan.FromTicks(options.PollingInterval.Ticks * factor);

        return delay < options.MaxBackoff ? delay : options.MaxBackoff;
    }
}
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace PlanelyApp.Infrastructure.Imports;

/// <summary>
/// Runs the processor for as long as the application lives. It knows nothing about spreadsheets: it asks for a pass and
/// waits before the next one. Meant to run in one instance only (see the notes of the phase).
/// </summary>
internal sealed class ImportWorkerService(
    ImportProcessor processor,
    ImportOptions options,
    TimeProvider timeProvider,
    ILogger<ImportWorkerService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            ImportRunResult result;

            try
            {
                result = await processor.RunAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                // The database may be down. The service must survive: an unhandled exception here stops the host.
                logger.LogError(exception, "The imports could not be processed.");
                result = new ImportRunResult(0, 1);
            }

            // A full batch means there is probably more waiting: go again without a pause.
            if (result.Aborted == 0 && result.Finished >= options.BatchSize)
            {
                continue;
            }

            try
            {
                await Task.Delay(options.PollingInterval, timeProvider, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }
}
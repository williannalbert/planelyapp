using Microsoft.Extensions.Logging;
using Quartz;

namespace PlanelyApp.Infrastructure.Reminders;

/// <summary>
/// The only thing Quartz knows about the reminders: "now". What is reminded, and how, is the processor's. The job never
/// overlaps itself in one process, and a failure is logged here instead of thrown: the next firing tries again.
/// Across processes, the concurrency token of the appointment is what stops a double reminder.
/// </summary>
[DisallowConcurrentExecution]
internal sealed class SendRemindersJob(ReminderProcessor processor, ILogger<SendRemindersJob> logger) : IJob
{
    public async ValueTask Execute(IJobExecutionContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            await processor.RunAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // The host is stopping: whatever was not saved is found again by the next run.
        }
        catch (Exception exception)
        {
            // The database may be down. This must not become an error of the scheduler.
            logger.LogError(exception, "The reminders could not be processed.");
        }
    }
}
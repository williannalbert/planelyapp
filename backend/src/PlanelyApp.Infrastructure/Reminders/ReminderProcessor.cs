using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PlanelyApp.Application.Abstractions.Cqrs;
using PlanelyApp.Application.Scheduling.Appointments.Remind;
using PlanelyApp.Infrastructure.Tenancy;

namespace PlanelyApp.Infrastructure.Reminders;

/// <param name="Sent">Reminders recorded in this run.</param>
/// <param name="Skipped">Appointments that no longer needed one (changed since the list, or another run got there first).</param>
/// <param name="Failed">Appointments that will be tried again in the next run.</param>
public readonly record struct ReminderRunResult(int Sent, int Skipped, int Failed);

/// <summary>
/// One pass of the reminders: list what is due across every company, then handle each appointment in its own scope with
/// its company set, through the dispatcher (validation, transaction, domain events and outbox as in a request).
/// When it runs is the job of the scheduler (4.12d); keeping it apart lets a test run exactly one pass.
/// </summary>
public sealed class ReminderProcessor(
    IServiceScopeFactory scopeFactory,
    TimeProvider timeProvider,
    ReminderOptions options,
    ILogger<ReminderProcessor> logger)
{
    public async Task<ReminderRunResult> RunAsync(CancellationToken cancellationToken = default)
    {
        var due = await ListDueAsync(cancellationToken);
        int sent = 0, skipped = 0, failed = 0;

        foreach (var reminder in due)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                // A new scope for each appointment: its own context and its own company. A failure here cannot
                // leave a half-tracked entity behind for the next one.
                await using var scope = scopeFactory.CreateAsyncScope();
                scope.ServiceProvider.GetRequiredService<TenantContext>().Use(reminder.CompanyId);

                var result = await scope.ServiceProvider.GetRequiredService<IDispatcher>()
                    .SendAsync(new SendAppointmentReminderCommand(reminder.AppointmentId), cancellationToken);

                if (result.IsFailure)
                {
                    failed++;
                    logger.LogWarning(
                        "Reminder of appointment {AppointmentId} was refused with {Code}.",
                        reminder.AppointmentId,
                        result.Error.Code);
                }
                else if (result.Value)
                {
                    sent++;
                }
                else
                {
                    skipped++;
                }
            }
            catch (DbUpdateConcurrencyException)
            {
                // Another run (or a confirm/cancel at the same moment) changed the appointment first.
                skipped++;
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                // Nothing was saved (mark and outbox row go in one transaction), so the next run tries again.
                failed++;
                logger.LogError(exception, "Reminder of appointment {AppointmentId} failed.", reminder.AppointmentId);
            }
        }

        if (due.Count > 0)
        {
            logger.LogInformation("Reminders: {Sent} sent, {Skipped} skipped, {Failed} failed.", sent, skipped, failed);
        }

        return new ReminderRunResult(sent, skipped, failed);
    }

    private async Task<IReadOnlyList<DueReminder>> ListDueAsync(CancellationToken cancellationToken)
    {
        // No company in this scope on purpose: the read model looks at all of them and only returns ids.
        await using var scope = scopeFactory.CreateAsyncScope();

        return await scope.ServiceProvider.GetRequiredService<IDueReminderReadModel>().ListAsync(
            timeProvider.GetUtcNow().UtcDateTime,
            options.LeadTime,
            options.BatchSize,
            cancellationToken);
    }
}
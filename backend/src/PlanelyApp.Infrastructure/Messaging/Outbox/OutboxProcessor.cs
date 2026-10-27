using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PlanelyApp.Infrastructure.Persistence;

namespace PlanelyApp.Infrastructure.Messaging.Outbox;

public readonly record struct OutboxRunResult(int Published, bool Failed);

/// <summary>
/// One pass over the pending messages: publish in the order they happened, mark each as processed.
/// The loop, the waits and the scope are the job of <see cref="OutboxPublisherService"/>; keeping them apart
/// lets a test run exactly one pass.
/// </summary>
public sealed class OutboxProcessor(
    PlanelyDbContext context,
    IMessagePublisher publisher,
    TimeProvider timeProvider,
    OutboxOptions options,
    ILogger<OutboxProcessor> logger)
{
    public async Task<OutboxRunResult> ProcessPendingAsync(CancellationToken cancellationToken = default)
    {
        var pending = await context.Set<OutboxMessage>()
            .Where(message => message.ProcessedAtUtc == null)
            .OrderBy(message => message.OccurredAtUtc)
            .ThenBy(message => message.Id)
            .Take(options.BatchSize)
            .ToListAsync(cancellationToken);

        var published = 0;

        foreach (var message in pending)
        {
            try
            {
                await publisher.PublishAsync(ToOutbound(message), cancellationToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                // Stops here: publishing the next ones would deliver them before this one.
                message.RegisterFailure(exception.Message);
                await context.SaveChangesAsync(cancellationToken);

                // Never the payload: it carries names, phones and e-mails (LGPD).
                logger.LogError(
                    exception,
                    "Publishing outbox message {MessageId} ({Type}) failed; attempt {Attempts}.",
                    message.Id,
                    message.Type,
                    message.Attempts);

                return new OutboxRunResult(published, Failed: true);
            }

            message.MarkProcessed(timeProvider.GetUtcNow().UtcDateTime);

            // One save per message: if the process dies, at most this one is published twice.
            await context.SaveChangesAsync(cancellationToken);
            published++;
        }

        return new OutboxRunResult(published, Failed: false);
    }

    private static OutboundMessage ToOutbound(OutboxMessage message)
    {
        if (!MessageTopology.TryGetExchange(message.Type, out var exchange))
        {
            throw new InvalidOperationException($"No exchange is known for the message type '{message.Type}'.");
        }

        return new OutboundMessage(message.Id, exchange, message.Type, OutboxEnvelope.Serialize(message));
    }
}
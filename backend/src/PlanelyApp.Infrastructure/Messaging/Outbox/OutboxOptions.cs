namespace PlanelyApp.Infrastructure.Messaging.Outbox;

public sealed class OutboxOptions
{
    /// <summary>How many messages one run reads at most.</summary>
    public int BatchSize { get; set; } = 50;

    /// <summary>How long the service waits when there is nothing (more) to publish.</summary>
    public TimeSpan PollingInterval { get; set; } = TimeSpan.FromSeconds(1);

    /// <summary>The longest wait after consecutive failures. The wait doubles from the polling interval up to this.</summary>
    public TimeSpan MaxBackoff { get; set; } = TimeSpan.FromSeconds(30);

    internal void Validate()
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(BatchSize, 1);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(PollingInterval, TimeSpan.Zero);
        ArgumentOutOfRangeException.ThrowIfLessThan(MaxBackoff, PollingInterval);
    }
}
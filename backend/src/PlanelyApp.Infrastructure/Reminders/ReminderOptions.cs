namespace PlanelyApp.Infrastructure.Reminders;

public sealed class ReminderOptions
{
    /// <summary>How long before the start the customer is reminded. The same for every company.</summary>
    public TimeSpan LeadTime { get; set; } = TimeSpan.FromHours(24);

    /// <summary>How many appointments one run handles at most. The rest waits for the next run.</summary>
    public int BatchSize { get; set; } = 200;

    internal void Validate()
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(LeadTime, TimeSpan.Zero);
        ArgumentOutOfRangeException.ThrowIfLessThan(BatchSize, 1);
    }
}
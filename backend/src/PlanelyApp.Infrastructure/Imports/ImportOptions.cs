namespace PlanelyApp.Infrastructure.Imports;

public sealed class ImportOptions
{
    /// <summary>How many imports one pass takes. The rest waits for the next pass.</summary>
    public int BatchSize { get; set; } = 5;

    /// <summary>How long the worker waits when there is nothing more to do. Short: an empty pass is one cheap query.</summary>
    public TimeSpan PollingInterval { get; set; } = TimeSpan.FromSeconds(5);

    internal void Validate()
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(BatchSize, 1);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(PollingInterval, TimeSpan.Zero);
    }
}
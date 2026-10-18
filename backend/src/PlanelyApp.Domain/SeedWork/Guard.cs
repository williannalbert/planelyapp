namespace PlanelyApp.Domain.SeedWork;

internal static class Guard
{
    public static string RequiredText(string? value, int maxLength, string errorCode)
    {
        var trimmed = value?.Trim();

        if (string.IsNullOrEmpty(trimmed) || trimmed.Length > maxLength)
        {
            throw new DomainException(errorCode);
        }

        return trimmed;
    }

    public static string? OptionalText(string? value, int maxLength, string errorCode) =>
        string.IsNullOrWhiteSpace(value) ? null : RequiredText(value, maxLength, errorCode);

    private static readonly TimeSpan MaximumDuration = TimeSpan.FromHours(24);

    /// <summary>A duration from one minute to 24 hours, in whole minutes (the unit stored in the database).</summary>
    public static TimeSpan DurationInWholeMinutes(TimeSpan value, string errorCode)
    {
        if (value <= TimeSpan.Zero
            || value > MaximumDuration
            || value.Ticks % TimeSpan.TicksPerMinute != 0)
        {
            throw new DomainException(errorCode);
        }

        return value;
    }
}
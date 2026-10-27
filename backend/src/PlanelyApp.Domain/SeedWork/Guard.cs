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
    /// <summary>
    /// Required text that is cut to the maximum length instead of rejected. For messages written by the
    /// system itself (an exception message, a row error), which must never make a job fail.
    /// </summary>
    public static string RequiredTextTruncated(string? value, int maxLength, string errorCode)
    {
        var trimmed = value?.Trim();

        if (string.IsNullOrEmpty(trimmed))
        {
            throw new DomainException(errorCode);
        }

        return trimmed.Length > maxLength ? trimmed[..maxLength] : trimmed;
    }

    /// <summary>An instant that must be expressed in UTC.</summary>
    public static DateTime Utc(DateTime value, string errorCode)
    {
        if (value.Kind != DateTimeKind.Utc)
        {
            throw new DomainException(errorCode);
        }

        return value;
    }
}
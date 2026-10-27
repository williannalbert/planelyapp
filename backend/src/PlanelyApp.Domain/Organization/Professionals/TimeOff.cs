using PlanelyApp.Domain.SeedWork;
using PlanelyApp.Domain.SharedKernel;

namespace PlanelyApp.Domain.Organization.Professionals;

/// <summary>A period in which the professional is unavailable (vacation, medical appointment...).</summary>
public sealed class TimeOff : Entity
{
    private const int ReasonMaxLength = 200;

    private TimeOff(DateTimeRange range, string? reason)
    {
        Range = range;
        Reason = reason;
    }

    #pragma warning disable CS8618
    private TimeOff()
    {
    }
    #pragma warning restore CS8618

    /// <summary>UTC instants.</summary>
    public DateTimeRange Range { get; }

    public string? Reason { get; }

    // Only the Professional aggregate creates its time offs.
    internal static TimeOff Create(DateTimeRange range, string? reason) =>
        new(range, Guard.OptionalText(reason, ReasonMaxLength, "TimeOff.ReasonInvalid"));
}
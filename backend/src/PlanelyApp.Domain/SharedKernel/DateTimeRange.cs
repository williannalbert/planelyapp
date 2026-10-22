using PlanelyApp.Domain.SeedWork;

namespace PlanelyApp.Domain.SharedKernel;

/// <summary>A range of instants in UTC. Start is inclusive, End is exclusive.</summary>
public sealed class DateTimeRange : ValueObject
{
    private DateTimeRange(DateTime start, DateTime end)
    {
        Start = start;
        End = end;
    }

    public DateTime Start { get; }

    public DateTime End { get; }

    public TimeSpan Duration => End - Start;

    public static DateTimeRange Create(DateTime start, DateTime end)
    {
        if (start.Kind != DateTimeKind.Utc || end.Kind != DateTimeKind.Utc)
        {
            throw new DomainException("DateTimeRange.MustBeUtc");
        }

        if (end <= start)
        {
            throw new DomainException("DateTimeRange.EndMustBeAfterStart");
        }

        return new DateTimeRange(start, end);
    }

    public static DateTimeRange FromDuration(DateTime start, TimeSpan duration) =>
        Create(start, start + duration);

    public bool Contains(DateTime instant) => instant >= Start && instant < End;

    public bool Contains(DateTimeRange other) => other.Start >= Start && other.End <= End;

    // Ranges that only touch (one ends exactly when the other starts) do not overlap.
    public bool Overlaps(DateTimeRange other) => Start < other.End && other.Start < End;
    /// <summary>The part both ranges share, or null when they are apart or only touch.</summary>
    public DateTimeRange? Intersect(DateTimeRange other)
    {
        var start = Start > other.Start ? Start : other.Start;
        var end = End < other.End ? End : other.End;

        return start < end ? new DateTimeRange(start, end) : null;
    }

    /// <summary>What is left of this range after taking the other out: none, one or two ranges, in order.</summary>
    public IReadOnlyList<DateTimeRange> Subtract(DateTimeRange other)
    {
        var shared = Intersect(other);

        if (shared is null)
        {
            return [this];
        }

        var remaining = new List<DateTimeRange>(2);

        if (Start < shared.Start)
        {
            remaining.Add(new DateTimeRange(Start, shared.Start));
        }

        if (shared.End < End)
        {
            remaining.Add(new DateTimeRange(shared.End, End));
        }

        return remaining;
    }
    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Start;
        yield return End;
    }
}
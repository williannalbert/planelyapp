using PlanelyApp.Domain.SeedWork;

namespace PlanelyApp.Domain.SharedKernel;

/// <summary>A range of local time within one day. Start is inclusive, End is exclusive.</summary>
public sealed class TimeOfDayRange : ValueObject
{
    private TimeOfDayRange(TimeOnly start, TimeOnly end)
    {
        Start = start;
        End = end;
    }

    public TimeOnly Start { get; }

    public TimeOnly End { get; }

    public TimeSpan Duration => End - Start;

    public static TimeOfDayRange Create(TimeOnly start, TimeOnly end)
    {
        if (end <= start)
        {
            throw new DomainException("TimeOfDayRange.EndMustBeAfterStart");
        }

        return new TimeOfDayRange(start, end);
    }

    public bool Contains(TimeOnly time) => time >= Start && time < End;

    public bool Contains(TimeOfDayRange other) => other.Start >= Start && other.End <= End;

    public bool Overlaps(TimeOfDayRange other) => Start < other.End && other.Start < End;
    /// <summary>The part both ranges share, or null when they are apart or only touch.</summary>
    public TimeOfDayRange? Intersect(TimeOfDayRange other)
    {
        var start = Start > other.Start ? Start : other.Start;
        var end = End < other.End ? End : other.End;

        return start < end ? new TimeOfDayRange(start, end) : null;
    }
    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Start;
        yield return End;
    }
}
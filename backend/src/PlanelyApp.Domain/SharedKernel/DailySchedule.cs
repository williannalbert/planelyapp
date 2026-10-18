using PlanelyApp.Domain.SeedWork;

namespace PlanelyApp.Domain.SharedKernel;

/// <summary>
/// The schedule of one weekday in local time: the main range plus the breaks inside it (RN10).
/// Used for a branch's business hours and, later, for a professional's working hours.
/// </summary>
public sealed class DailySchedule : ValueObject
{
    private readonly List<Break> _breaks;

    private DailySchedule(DayOfWeek day, TimeOfDayRange range, List<Break> breaks)
    {
        Day = day;
        Range = range;
        _breaks = breaks;
    }

    public DayOfWeek Day { get; }

    public TimeOfDayRange Range { get; }

    /// <summary>Ordered by start time.</summary>
    public IReadOnlyList<Break> Breaks => _breaks.AsReadOnly();

    public static DailySchedule Create(DayOfWeek day, TimeOfDayRange range, IEnumerable<Break>? breaks = null)
    {
        if (!Enum.IsDefined(day))
        {
            throw new DomainException("DailySchedule.DayInvalid");
        }

        var ordered = (breaks ?? []).OrderBy(b => b.Range.Start).ToList();

        if (ordered.Any(b => !range.Contains(b.Range)))
        {
            throw new DomainException("DailySchedule.BreakOutsideRange");
        }

        // Sorted by start, an overlap between any two breaks implies an overlap between neighbours.
        for (var i = 1; i < ordered.Count; i++)
        {
            if (ordered[i - 1].Range.Overlaps(ordered[i].Range))
            {
                throw new DomainException("DailySchedule.BreaksOverlap");
            }
        }

        return new DailySchedule(day, range, ordered);
    }

    /// <summary>True when the time is inside the range and not inside a break.</summary>
    public bool IsOpenAt(TimeOnly time) =>
        Range.Contains(time) && !_breaks.Any(b => b.Range.Contains(time));

    /// <summary>The range minus the breaks: the periods in which appointments can happen.</summary>
    public IReadOnlyList<TimeOfDayRange> GetWorkingPeriods()
    {
        var periods = new List<TimeOfDayRange>();
        var cursor = Range.Start;

        foreach (var pause in _breaks)
        {
            if (pause.Range.Start > cursor)
            {
                periods.Add(TimeOfDayRange.Create(cursor, pause.Range.Start));
            }

            cursor = pause.Range.End;
        }

        if (cursor < Range.End)
        {
            periods.Add(TimeOfDayRange.Create(cursor, Range.End));
        }

        return periods;
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Day;
        yield return Range;

        foreach (var pause in _breaks)
        {
            yield return pause;
        }
    }
}
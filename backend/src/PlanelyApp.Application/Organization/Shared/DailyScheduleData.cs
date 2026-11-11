using PlanelyApp.Domain.SharedKernel;

namespace PlanelyApp.Application.Organization.Shared;

/// <summary>The hours of one weekday, in local time, with the breaks inside it.</summary>
public sealed record DailyScheduleData(DayOfWeek Day, TimeOnly Start, TimeOnly End, IReadOnlyList<BreakData> Breaks)
{
    internal static DailyScheduleData From(DailySchedule schedule) => new(
        schedule.Day,
        schedule.Range.Start,
        schedule.Range.End,
        schedule.Breaks
            .Select(pause => new BreakData(pause.Range.Start, pause.Range.End, pause.Description))
            .ToList());

    /// <summary>Throws <c>DomainException</c> for a bad range, a break outside it or overlapping breaks (RN10).</summary>
    internal DailySchedule ToDomain() => DailySchedule.Create(
        Day,
        TimeOfDayRange.Create(Start, End),
        Breaks.Select(pause => Break.Create(TimeOfDayRange.Create(pause.Start, pause.End), pause.Description)));
}
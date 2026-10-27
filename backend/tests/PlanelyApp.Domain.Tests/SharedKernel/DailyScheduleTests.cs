using PlanelyApp.Domain.SeedWork;
using PlanelyApp.Domain.SharedKernel;
using Shouldly;

namespace PlanelyApp.Domain.Tests.SharedKernel;

public class DailyScheduleTests
{
    private static TimeOfDayRange Range(int startHour, int endHour) =>
        TimeOfDayRange.Create(new TimeOnly(startHour, 0), new TimeOnly(endHour, 0));

    private static Break Pause(int startHour, int endHour) => Break.Create(Range(startHour, endHour));

    private static DailySchedule Workday(params Break[] breaks) =>
        DailySchedule.Create(DayOfWeek.Monday, Range(9, 18), breaks);

    private static string Describe(IEnumerable<TimeOfDayRange> ranges) =>
        string.Join(", ", ranges.Select(r => $"{r.Start:HH\\:mm}-{r.End:HH\\:mm}"));

    [Fact]
    public void Should_expose_day_range_and_no_breaks_by_default()
    {
        var schedule = DailySchedule.Create(DayOfWeek.Tuesday, Range(9, 18));

        schedule.Day.ShouldBe(DayOfWeek.Tuesday);
        schedule.Range.ShouldBe(Range(9, 18));
        schedule.Breaks.ShouldBeEmpty();
    }

    [Fact]
    public void Breaks_should_be_ordered_by_start_time()
    {
        var schedule = Workday(Pause(15, 16), Pause(12, 13));

        schedule.Breaks[0].Range.ShouldBe(Range(12, 13));
        schedule.Breaks[1].Range.ShouldBe(Range(15, 16));
    }

    [Fact]
    public void Invalid_day_should_be_rejected()
    {
        Should.Throw<DomainException>(() => DailySchedule.Create((DayOfWeek)9, Range(9, 18)))
            .Code.ShouldBe("DailySchedule.DayInvalid");
    }

    [Theory]
    [InlineData(8, 10)]   // starts before the range
    [InlineData(17, 19)]  // ends after the range
    [InlineData(6, 7)]    // entirely outside
    public void Break_outside_the_range_should_be_rejected(int start, int end)
    {
        Should.Throw<DomainException>(() => Workday(Pause(start, end)))
            .Code.ShouldBe("DailySchedule.BreakOutsideRange");
    }

    [Fact]
    public void Break_touching_the_range_boundaries_should_be_accepted()
    {
        Should.NotThrow(() => Workday(Pause(9, 10), Pause(17, 18)));
    }

    [Fact]
    public void Overlapping_breaks_should_be_rejected()
    {
        Should.Throw<DomainException>(() => Workday(Pause(12, 14), Pause(13, 15)))
            .Code.ShouldBe("DailySchedule.BreaksOverlap");
        Should.Throw<DomainException>(() => Workday(Pause(10, 16), Pause(12, 13)))
            .Code.ShouldBe("DailySchedule.BreaksOverlap");
    }

    [Fact]
    public void Breaks_that_only_touch_should_be_accepted()
    {
        Should.NotThrow(() => Workday(Pause(12, 13), Pause(13, 14)));
    }

    [Fact]
    public void Changing_the_source_collection_afterwards_should_not_affect_the_schedule()
    {
        var breaks = new List<Break> { Pause(12, 13) };
        var schedule = DailySchedule.Create(DayOfWeek.Monday, Range(9, 18), breaks);

        breaks.Add(Pause(15, 16));

        schedule.Breaks.Count.ShouldBe(1);
    }

    [Fact]
    public void IsOpenAt_should_be_false_outside_the_range_and_inside_breaks()
    {
        var schedule = Workday(Pause(12, 13));

        schedule.IsOpenAt(new TimeOnly(8, 59)).ShouldBeFalse();
        schedule.IsOpenAt(new TimeOnly(9, 0)).ShouldBeTrue();
        schedule.IsOpenAt(new TimeOnly(11, 59)).ShouldBeTrue();
        schedule.IsOpenAt(new TimeOnly(12, 0)).ShouldBeFalse();
        schedule.IsOpenAt(new TimeOnly(12, 59)).ShouldBeFalse();
        schedule.IsOpenAt(new TimeOnly(13, 0)).ShouldBeTrue();
        schedule.IsOpenAt(new TimeOnly(17, 59)).ShouldBeTrue();
        schedule.IsOpenAt(new TimeOnly(18, 0)).ShouldBeFalse();
    }

    [Fact]
    public void Working_periods_without_breaks_should_be_the_whole_range()
    {
        Describe(Workday().GetWorkingPeriods()).ShouldBe("09:00-18:00");
    }

    [Fact]
    public void Working_periods_should_exclude_the_breaks()
    {
        Describe(Workday(Pause(12, 13)).GetWorkingPeriods()).ShouldBe("09:00-12:00, 13:00-18:00");
        Describe(Workday(Pause(10, 11), Pause(15, 16)).GetWorkingPeriods())
            .ShouldBe("09:00-10:00, 11:00-15:00, 16:00-18:00");
    }

    [Fact]
    public void Working_periods_should_handle_breaks_at_the_edges()
    {
        Describe(Workday(Pause(9, 10)).GetWorkingPeriods()).ShouldBe("10:00-18:00");
        Describe(Workday(Pause(17, 18)).GetWorkingPeriods()).ShouldBe("09:00-17:00");
        Describe(Workday(Pause(12, 13), Pause(13, 14)).GetWorkingPeriods())
            .ShouldBe("09:00-12:00, 14:00-18:00");
    }

    [Fact]
    public void Schedules_with_same_content_should_be_equal_regardless_of_break_input_order()
    {
        Workday(Pause(12, 13), Pause(15, 16)).ShouldBe(Workday(Pause(15, 16), Pause(12, 13)));
        Workday(Pause(12, 13)).ShouldNotBe(Workday());
    }
}
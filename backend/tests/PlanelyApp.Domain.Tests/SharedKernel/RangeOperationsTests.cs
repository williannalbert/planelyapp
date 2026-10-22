using PlanelyApp.Domain.SharedKernel;
using Shouldly;

namespace PlanelyApp.Domain.Tests.SharedKernel;

public class RangeOperationsTests
{
    private static DateTime Utc(int hour) => new(2026, 11, 9, hour, 0, 0, DateTimeKind.Utc);

    private static DateTimeRange Instants(int startHour, int endHour) =>
        DateTimeRange.Create(Utc(startHour), Utc(endHour));

    private static TimeOfDayRange Times(int startHour, int endHour) =>
        TimeOfDayRange.Create(new TimeOnly(startHour, 0), new TimeOnly(endHour, 0));

    // ---- DateTimeRange.Intersect ----

    [Fact]
    public void Intersect_should_return_the_shared_part_of_overlapping_ranges()
    {
        Instants(9, 12).Intersect(Instants(11, 14)).ShouldBe(Instants(11, 12));
    }

    [Fact]
    public void Intersect_should_return_the_inner_range_when_one_contains_the_other()
    {
        Instants(9, 18).Intersect(Instants(12, 13)).ShouldBe(Instants(12, 13));
    }

    [Fact]
    public void Intersect_should_be_null_when_the_ranges_only_touch_or_are_apart()
    {
        Instants(9, 12).Intersect(Instants(12, 14)).ShouldBeNull();
        Instants(9, 10).Intersect(Instants(11, 12)).ShouldBeNull();
    }

    // ---- DateTimeRange.Subtract ----

    [Fact]
    public void Subtract_should_keep_the_range_when_the_other_does_not_overlap()
    {
        Instants(9, 12).Subtract(Instants(13, 14)).ShouldBe([Instants(9, 12)]);
        Instants(9, 12).Subtract(Instants(12, 14)).ShouldBe([Instants(9, 12)]);
    }

    [Fact]
    public void Subtract_should_split_the_range_when_the_other_is_in_the_middle()
    {
        Instants(9, 18).Subtract(Instants(12, 13)).ShouldBe([Instants(9, 12), Instants(13, 18)]);
    }

    [Fact]
    public void Subtract_should_trim_the_start_or_the_end()
    {
        Instants(9, 12).Subtract(Instants(8, 10)).ShouldBe([Instants(10, 12)]);
        Instants(9, 12).Subtract(Instants(11, 13)).ShouldBe([Instants(9, 11)]);
    }

    [Fact]
    public void Subtract_should_leave_nothing_when_the_other_covers_the_range()
    {
        Instants(10, 11).Subtract(Instants(9, 12)).ShouldBeEmpty();
        Instants(10, 11).Subtract(Instants(10, 11)).ShouldBeEmpty();
    }

    // ---- TimeOfDayRange.Intersect ----

    [Fact]
    public void Times_Intersect_should_return_the_shared_part()
    {
        Times(9, 12).Intersect(Times(11, 14)).ShouldBe(Times(11, 12));
        Times(9, 18).Intersect(Times(12, 13)).ShouldBe(Times(12, 13));
    }

    [Fact]
    public void Times_Intersect_should_be_null_when_the_ranges_only_touch_or_are_apart()
    {
        Times(9, 12).Intersect(Times(12, 14)).ShouldBeNull();
        Times(9, 10).Intersect(Times(11, 12)).ShouldBeNull();
    }
}
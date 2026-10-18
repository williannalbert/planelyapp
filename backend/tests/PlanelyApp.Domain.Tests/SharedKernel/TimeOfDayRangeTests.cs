using PlanelyApp.Domain.SeedWork;
using PlanelyApp.Domain.SharedKernel;
using Shouldly;

namespace PlanelyApp.Domain.Tests.SharedKernel;

public class TimeOfDayRangeTests
{
    private static TimeOfDayRange Range(int startHour, int endHour) =>
        TimeOfDayRange.Create(new TimeOnly(startHour, 0), new TimeOnly(endHour, 0));

    [Fact]
    public void Should_expose_start_end_and_duration()
    {
        var range = TimeOfDayRange.Create(new TimeOnly(9, 0), new TimeOnly(12, 30));

        range.Start.ShouldBe(new TimeOnly(9, 0));
        range.End.ShouldBe(new TimeOnly(12, 30));
        range.Duration.ShouldBe(TimeSpan.FromMinutes(210));
    }

    [Fact]
    public void End_equal_or_before_start_should_be_rejected()
    {
        Should.Throw<DomainException>(() => Range(9, 9))
            .Code.ShouldBe("TimeOfDayRange.EndMustBeAfterStart");
        Should.Throw<DomainException>(() => Range(18, 9))
            .Code.ShouldBe("TimeOfDayRange.EndMustBeAfterStart");
    }

    [Theory]
    [InlineData(9, 12, 11, 14, true)]
    [InlineData(9, 18, 12, 13, true)]
    [InlineData(9, 12, 12, 14, false)]
    [InlineData(9, 10, 12, 14, false)]
    public void Overlaps_should_detect_intersection(
        int firstStart, int firstEnd, int secondStart, int secondEnd, bool expected)
    {
        var first = Range(firstStart, firstEnd);
        var second = Range(secondStart, secondEnd);

        first.Overlaps(second).ShouldBe(expected);
        second.Overlaps(first).ShouldBe(expected);
    }

    [Fact]
    public void Contains_time_should_include_start_and_exclude_end()
    {
        var range = Range(9, 12);

        range.Contains(new TimeOnly(9, 0)).ShouldBeTrue();
        range.Contains(new TimeOnly(11, 59)).ShouldBeTrue();
        range.Contains(new TimeOnly(12, 0)).ShouldBeFalse();
    }

    [Fact]
    public void Contains_range_should_require_it_to_be_fully_inside()
    {
        var workday = Range(9, 18);

        workday.Contains(Range(12, 13)).ShouldBeTrue();
        workday.Contains(Range(8, 10)).ShouldBeFalse();
    }

    [Fact]
    public void Ranges_with_same_start_and_end_should_be_equal()
    {
        Range(9, 12).ShouldBe(Range(9, 12));
        Range(9, 12).ShouldNotBe(Range(9, 13));
    }
}
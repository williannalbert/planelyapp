using PlanelyApp.Domain.SeedWork;
using PlanelyApp.Domain.SharedKernel;
using Shouldly;

namespace PlanelyApp.Domain.Tests.SharedKernel;

public class DateTimeRangeTests
{
    private static DateTime Utc(int hour, int minute = 0) =>
        new(2026, 10, 20, hour, minute, 0, DateTimeKind.Utc);

    private static DateTimeRange Range(int startHour, int endHour) =>
        DateTimeRange.Create(Utc(startHour), Utc(endHour));

    [Fact]
    public void Should_expose_start_end_and_duration()
    {
        var range = DateTimeRange.Create(Utc(9), Utc(10, 30));

        range.Start.ShouldBe(Utc(9));
        range.End.ShouldBe(Utc(10, 30));
        range.Duration.ShouldBe(TimeSpan.FromMinutes(90));
    }

    [Fact]
    public void Non_utc_values_should_be_rejected()
    {
        var local = new DateTime(2026, 10, 20, 9, 0, 0, DateTimeKind.Local);
        var unspecified = new DateTime(2026, 10, 20, 9, 0, 0, DateTimeKind.Unspecified);

        Should.Throw<DomainException>(() => DateTimeRange.Create(local, Utc(10)))
            .Code.ShouldBe("DateTimeRange.TimeMustBeUtc");
        Should.Throw<DomainException>(() => DateTimeRange.Create(Utc(9), unspecified))
            .Code.ShouldBe("DateTimeRange.TimeMustBeUtc");
    }

    [Fact]
    public void End_equal_or_before_start_should_be_rejected()
    {
        Should.Throw<DomainException>(() => DateTimeRange.Create(Utc(9), Utc(9)))
            .Code.ShouldBe("DateTimeRange.EndMustBeAfterStart");
        Should.Throw<DomainException>(() => DateTimeRange.Create(Utc(10), Utc(9)))
            .Code.ShouldBe("DateTimeRange.EndMustBeAfterStart");
    }

    [Fact]
    public void FromDuration_should_add_the_duration_to_the_start()
    {
        var range = DateTimeRange.FromDuration(Utc(9), TimeSpan.FromMinutes(45));

        range.End.ShouldBe(Utc(9, 45));
    }

    [Fact]
    public void FromDuration_with_zero_duration_should_be_rejected()
    {
        Should.Throw<DomainException>(() => DateTimeRange.FromDuration(Utc(9), TimeSpan.Zero))
            .Code.ShouldBe("DateTimeRange.EndMustBeAfterStart");
    }

    [Theory]
    [InlineData(9, 12, 11, 14, true)]   // partial overlap
    [InlineData(9, 12, 10, 11, true)]   // contained
    [InlineData(9, 12, 12, 14, false)]  // touching: back-to-back appointments
    [InlineData(9, 12, 7, 9, false)]    // touching on the other side
    [InlineData(9, 10, 12, 14, false)]  // disjoint
    public void Overlaps_should_detect_intersection(
        int firstStart, int firstEnd, int secondStart, int secondEnd, bool expected)
    {
        var first = Range(firstStart, firstEnd);
        var second = Range(secondStart, secondEnd);

        first.Overlaps(second).ShouldBe(expected);
        second.Overlaps(first).ShouldBe(expected);
    }

    [Fact]
    public void Contains_instant_should_include_start_and_exclude_end()
    {
        var range = Range(9, 10);

        range.Contains(Utc(9)).ShouldBeTrue();
        range.Contains(Utc(9, 59)).ShouldBeTrue();
        range.Contains(Utc(10)).ShouldBeFalse();
        range.Contains(Utc(8, 59)).ShouldBeFalse();
    }

    [Fact]
    public void Contains_range_should_require_it_to_be_fully_inside()
    {
        var range = Range(9, 12);

        range.Contains(Range(9, 12)).ShouldBeTrue();
        range.Contains(Range(10, 11)).ShouldBeTrue();
        range.Contains(Range(8, 10)).ShouldBeFalse();
        range.Contains(Range(11, 13)).ShouldBeFalse();
    }

    [Fact]
    public void Ranges_with_same_start_and_end_should_be_equal()
    {
        Range(9, 10).ShouldBe(Range(9, 10));
        Range(9, 10).ShouldNotBe(Range(9, 11));
    }
}
using PlanelyApp.Domain.Catalog.Promotions;
using PlanelyApp.Domain.SeedWork;
using Shouldly;

namespace PlanelyApp.Domain.Tests.Catalog.Promotions;

public class ValidityPeriodTests
{
    private static readonly DateOnly Start = new(2026, 11, 1);
    private static readonly DateOnly End = new(2026, 11, 30);

    [Fact]
    public void Create_should_expose_start_and_end()
    {
        var period = ValidityPeriod.Create(Start, End);

        period.Start.ShouldBe(Start);
        period.End.ShouldBe(End);
    }

    [Fact]
    public void Contains_should_include_both_ends()
    {
        var period = ValidityPeriod.Create(Start, End);

        period.Contains(Start).ShouldBeTrue();
        period.Contains(End).ShouldBeTrue();
        period.Contains(new DateOnly(2026, 11, 15)).ShouldBeTrue();
    }

    [Fact]
    public void Contains_should_exclude_days_outside_the_period()
    {
        var period = ValidityPeriod.Create(Start, End);

        period.Contains(Start.AddDays(-1)).ShouldBeFalse();
        period.Contains(End.AddDays(1)).ShouldBeFalse();
    }

    [Fact]
    public void A_single_day_period_should_be_valid()
    {
        var period = ValidityPeriod.Create(Start, Start);

        period.Contains(Start).ShouldBeTrue();
        period.Contains(Start.AddDays(1)).ShouldBeFalse();
    }

    [Fact]
    public void Create_with_the_end_before_the_start_should_be_rejected()
    {
        Should.Throw<DomainException>(() => ValidityPeriod.Create(End, Start))
            .Code.ShouldBe("ValidityPeriod.EndBeforeStart");
    }

    [Fact]
    public void Periods_with_the_same_days_should_be_equal()
    {
        ValidityPeriod.Create(Start, End).ShouldBe(ValidityPeriod.Create(Start, End));
        ValidityPeriod.Create(Start, End).ShouldNotBe(ValidityPeriod.Create(Start, End.AddDays(1)));
    }
}
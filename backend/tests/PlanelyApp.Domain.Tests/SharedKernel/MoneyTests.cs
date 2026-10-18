using PlanelyApp.Domain.SeedWork;
using PlanelyApp.Domain.SharedKernel;
using Shouldly;

namespace PlanelyApp.Domain.Tests.SharedKernel;

public class MoneyTests
{
    [Fact]
    public void Should_round_to_two_decimals_away_from_zero()
    {
        Money.From(10.005m).Amount.ShouldBe(10.01m);
        Money.From(10.004m).Amount.ShouldBe(10.00m);
    }

    [Fact]
    public void Negative_amount_should_be_rejected()
    {
        Should.Throw<DomainException>(() => Money.From(-0.01m))
            .Code.ShouldBe("Money.NegativeAmount");
    }

    [Fact]
    public void Same_amount_should_be_equal()
    {
        Money.From(10m).ShouldBe(Money.From(10.00m));
    }

    [Fact]
    public void Zero_should_have_amount_zero()
    {
        Money.Zero.Amount.ShouldBe(0m);
    }

    [Fact]
    public void Add_should_sum_amounts()
    {
        Money.From(10.50m).Add(Money.From(4.75m)).ShouldBe(Money.From(15.25m));
    }

    [Fact]
    public void Subtract_should_reduce_amount()
    {
        Money.From(10m).Subtract(Money.From(2.50m)).ShouldBe(Money.From(7.50m));
    }

    [Fact]
    public void Subtract_below_zero_should_be_rejected()
    {
        Should.Throw<DomainException>(() => Money.From(5m).Subtract(Money.From(5.01m)))
            .Code.ShouldBe("Money.NegativeAmount");
    }

    [Fact]
    public void Percentage_should_return_the_share_of_the_amount()
    {
        Money.From(200m).Percentage(15m).ShouldBe(Money.From(30m));
        Money.From(99.99m).Percentage(10m).ShouldBe(Money.From(10.00m));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(101)]
    public void Percentage_outside_zero_and_hundred_should_be_rejected(int percent)
    {
        Should.Throw<DomainException>(() => Money.From(100m).Percentage(percent))
            .Code.ShouldBe("Money.InvalidPercentage");
    }

    [Fact]
    public void Comparison_operators_should_follow_the_amount()
    {
        (Money.From(5m) < Money.From(10m)).ShouldBeTrue();
        (Money.From(10m) > Money.From(5m)).ShouldBeTrue();
        (Money.From(10m) >= Money.From(10m)).ShouldBeTrue();
        (Money.From(10m) <= Money.From(9.99m)).ShouldBeFalse();
    }
}
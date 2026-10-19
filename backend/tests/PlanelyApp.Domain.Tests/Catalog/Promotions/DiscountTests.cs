using PlanelyApp.Domain.Catalog.Promotions;
using PlanelyApp.Domain.SeedWork;
using PlanelyApp.Domain.SharedKernel;
using Shouldly;

namespace PlanelyApp.Domain.Tests.Catalog.Promotions;

public class DiscountTests
{
    [Fact]
    public void Percentage_should_expose_type_and_value()
    {
        var discount = Discount.Percentage(10m);

        discount.Type.ShouldBe(DiscountType.Percentage);
        discount.Value.ShouldBe(10m);
    }

    [Fact]
    public void Percentage_should_take_that_share_off_the_price()
    {
        Discount.Percentage(10m).CalculateOn(Money.From(80m)).ShouldBe(Money.From(8m));
    }

    [Fact]
    public void Percentage_should_round_to_cents()
    {
        Discount.Percentage(33.33m).CalculateOn(Money.From(10m)).ShouldBe(Money.From(3.33m));
    }

    [Fact]
    public void Percentage_of_one_hundred_should_take_the_whole_price()
    {
        Discount.Percentage(100m).CalculateOn(Money.From(80m)).ShouldBe(Money.From(80m));
    }

    [Fact]
    public void Percentage_outside_the_allowed_range_should_be_rejected()
    {
        foreach (var percent in new[] { 0m, -5m, 100.01m })
        {
            Should.Throw<DomainException>(() => Discount.Percentage(percent))
                .Code.ShouldBe("Discount.InvalidPercentage");
        }
    }

    [Fact]
    public void FixedAmount_should_expose_type_and_value()
    {
        var discount = Discount.FixedAmount(Money.From(20m));

        discount.Type.ShouldBe(DiscountType.FixedAmount);
        discount.Value.ShouldBe(20m);
    }

    [Fact]
    public void FixedAmount_should_take_that_amount_off_the_price()
    {
        Discount.FixedAmount(Money.From(20m)).CalculateOn(Money.From(80m)).ShouldBe(Money.From(20m));
    }

    [Fact]
    public void FixedAmount_larger_than_the_price_should_never_make_the_final_price_negative()
    {
        // Final price = 80 - 80 = 0, not 80 - 100.
        Discount.FixedAmount(Money.From(100m)).CalculateOn(Money.From(80m)).ShouldBe(Money.From(80m));
    }

    [Fact]
    public void FixedAmount_of_zero_should_be_rejected()
    {
        Should.Throw<DomainException>(() => Discount.FixedAmount(Money.Zero))
            .Code.ShouldBe("Discount.InvalidFixedAmount");
    }

    [Fact]
    public void Discounts_with_the_same_type_and_value_should_be_equal()
    {
        Discount.Percentage(10m).ShouldBe(Discount.Percentage(10m));
        Discount.Percentage(10m).ShouldNotBe(Discount.Percentage(15m));
        Discount.Percentage(10m).ShouldNotBe(Discount.FixedAmount(Money.From(10m)));
    }
}
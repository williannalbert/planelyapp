using PlanelyApp.Domain.Scheduling.Pricing;
using PlanelyApp.Domain.SeedWork;
using PlanelyApp.Domain.SharedKernel;
using Shouldly;

namespace PlanelyApp.Domain.Tests.Scheduling.Pricing;

public class PriceQuoteTests
{
    [Fact]
    public void OnRequest_should_have_no_prices()
    {
        var quote = PriceQuote.OnRequest();

        quote.IsOnRequest.ShouldBeTrue();
        quote.ListPrice.ShouldBeNull();
        quote.FinalPrice.ShouldBeNull();
        quote.Discount.ShouldBe(Money.Zero);
        quote.PromotionId.ShouldBeNull();
    }

    [Fact]
    public void Fixed_without_discount_should_have_the_final_price_equal_to_the_list_price()
    {
        var quote = PriceQuote.Fixed(Money.From(100m));

        quote.IsOnRequest.ShouldBeFalse();
        quote.ListPrice.ShouldBe(Money.From(100m));
        quote.Discount.ShouldBe(Money.Zero);
        quote.FinalPrice.ShouldBe(Money.From(100m));
        quote.PromotionId.ShouldBeNull();
    }

    [Fact]
    public void Fixed_with_discount_should_subtract_it_from_the_list_price()
    {
        var promotionId = Guid.CreateVersion7();

        var quote = PriceQuote.Fixed(Money.From(100m), Money.From(25m), promotionId);

        quote.ListPrice.ShouldBe(Money.From(100m));
        quote.Discount.ShouldBe(Money.From(25m));
        quote.FinalPrice.ShouldBe(Money.From(75m));
        quote.PromotionId.ShouldBe(promotionId);
    }

    [Fact]
    public void Fixed_with_a_discount_larger_than_the_price_should_be_rejected()
    {
        Should.Throw<DomainException>(() =>
                PriceQuote.Fixed(Money.From(100m), Money.From(120m), Guid.CreateVersion7()))
            .Code.ShouldBe("Money.NegativeAmount");
    }

    [Fact]
    public void Fixed_with_an_empty_promotion_id_should_be_rejected()
    {
        Should.Throw<DomainException>(() => PriceQuote.Fixed(Money.From(100m), Money.From(10m), Guid.Empty))
            .Code.ShouldBe("PriceQuote.PromotionRequired");
    }

    [Fact]
    public void Quotes_with_the_same_content_should_be_equal()
    {
        var promotionId = Guid.CreateVersion7();

        PriceQuote.Fixed(Money.From(100m), Money.From(10m), promotionId)
            .ShouldBe(PriceQuote.Fixed(Money.From(100m), Money.From(10m), promotionId));
        PriceQuote.Fixed(Money.From(100m)).ShouldNotBe(PriceQuote.OnRequest());
    }
}
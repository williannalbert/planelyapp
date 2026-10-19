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
    [Fact]
    public void Quotes_that_did_not_come_from_a_bundle_should_say_so()
    {
        PriceQuote.OnRequest().IsFromBundle.ShouldBeFalse();
        PriceQuote.Fixed(Money.From(100m)).IsFromBundle.ShouldBeFalse();
        PriceQuote.Fixed(Money.From(100m), Money.From(10m), Guid.CreateVersion7()).IsFromBundle.ShouldBeFalse();
    }

    [Fact]
    public void FromBundle_should_subtract_the_share_from_the_list_price()
    {
        var promotionId = Guid.CreateVersion7();

        var quote = PriceQuote.FromBundle(Money.From(100m), Money.From(10m), promotionId);

        quote.IsFromBundle.ShouldBeTrue();
        quote.ListPrice.ShouldBe(Money.From(100m));
        quote.Discount.ShouldBe(Money.From(10m));
        quote.FinalPrice.ShouldBe(Money.From(90m));
        quote.PromotionId.ShouldBe(promotionId);
    }

    [Fact]
    public void FromBundle_should_accept_a_share_of_zero()
    {
        var quote = PriceQuote.FromBundle(Money.From(5m), Money.Zero, Guid.CreateVersion7());

        quote.IsFromBundle.ShouldBeTrue();
        quote.FinalPrice.ShouldBe(Money.From(5m));
    }

    [Fact]
    public void FromBundle_with_an_empty_promotion_id_should_be_rejected()
    {
        Should.Throw<DomainException>(() => PriceQuote.FromBundle(Money.From(100m), Money.From(10m), Guid.Empty))
            .Code.ShouldBe("PriceQuote.PromotionRequired");
    }

    [Fact]
    public void FromBundle_with_a_share_larger_than_the_price_should_be_rejected()
    {
        Should.Throw<DomainException>(() =>
                PriceQuote.FromBundle(Money.From(10m), Money.From(20m), Guid.CreateVersion7()))
            .Code.ShouldBe("Money.NegativeAmount");
    }

    [Fact]
    public void A_bundle_quote_should_differ_from_a_plain_quote_with_the_same_numbers()
    {
        var promotionId = Guid.CreateVersion7();

        PriceQuote.FromBundle(Money.From(100m), Money.From(10m), promotionId)
            .ShouldNotBe(PriceQuote.Fixed(Money.From(100m), Money.From(10m), promotionId));
    }
}
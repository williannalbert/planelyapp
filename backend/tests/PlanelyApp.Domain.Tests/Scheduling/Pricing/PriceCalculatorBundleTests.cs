using PlanelyApp.Domain.Catalog.Promotions;
using PlanelyApp.Domain.Catalog.ServiceOfferings;
using PlanelyApp.Domain.Organization.Branches;
using PlanelyApp.Domain.Scheduling.Pricing;
using PlanelyApp.Domain.SeedWork;
using PlanelyApp.Domain.SharedKernel;
using Shouldly;

namespace PlanelyApp.Domain.Tests.Scheduling.Pricing;

public class PriceCalculatorBundleTests
{
    private static readonly Guid CompanyId = Guid.CreateVersion7();
    private static readonly ValidityPeriod November =
        ValidityPeriod.Create(new DateOnly(2026, 11, 1), new DateOnly(2026, 11, 30));

    // Friday 2026-11-06, 12:00 in São Paulo.
    private static readonly DateTime FridayNoon = new(2026, 11, 6, 15, 0, 0, DateTimeKind.Utc);

    private static Branch CreateBranch() =>
        Branch.Create(
            CompanyId,
            "Centro",
            Address.Create("Rua das Flores", "123", null, "Centro", "São Paulo", "SP", "01310-100"),
            Phone.Create("+551133334444"),
            "America/Sao_Paulo");

    private static ServiceOffering FixedService(string name, decimal price, Guid? companyId = null) =>
        ServiceOffering.CreateWithFixedPrice(
            companyId ?? CompanyId, name, TimeSpan.FromMinutes(45), Money.From(price));

    private static Promotion Bundle(Discount discount, params ServiceOffering[] services) =>
        Promotion.CreateBundleForAllBranches(CompanyId, "Combo", discount, November, services);

    private static Promotion Simple(ServiceOffering service, Discount discount) =>
        Promotion.CreateForAllBranches(CompanyId, "Promo", discount, November, [service]);

    // ---- A bundle that applies ----

    [Fact]
    public void A_bundle_should_split_its_discount_among_the_services_in_proportion_to_their_prices()
    {
        var haircut = FixedService("Haircut", 100m);
        var beard = FixedService("Beard", 50m);
        var bundle = Bundle(Discount.Percentage(10m), haircut, beard); // 10% of 150 = 15

        var quotes = PriceCalculator.CalculateAll([haircut, beard], CreateBranch(), FridayNoon, [bundle]);

        quotes[0].ListPrice.ShouldBe(Money.From(100m));
        quotes[0].Discount.ShouldBe(Money.From(10m));
        quotes[0].FinalPrice.ShouldBe(Money.From(90m));
        quotes[0].PromotionId.ShouldBe(bundle.Id);
        quotes[0].IsFromBundle.ShouldBeTrue();

        quotes[1].ListPrice.ShouldBe(Money.From(50m));
        quotes[1].Discount.ShouldBe(Money.From(5m));
        quotes[1].FinalPrice.ShouldBe(Money.From(45m));
        quotes[1].PromotionId.ShouldBe(bundle.Id);
        quotes[1].IsFromBundle.ShouldBeTrue();
    }

    [Fact]
    public void The_shares_should_always_add_up_to_the_discount_of_the_bundle()
    {
        var a = FixedService("A", 10m);
        var b = FixedService("B", 10m);
        var c = FixedService("C", 10m);
        var bundle = Bundle(Discount.FixedAmount(Money.From(10m)), a, b, c); // 10.00 / 3 does not divide

        var quotes = PriceCalculator.CalculateAll([a, b, c], CreateBranch(), FridayNoon, [bundle]);

        quotes.Select(quote => quote.Discount).ShouldBe([Money.From(3.34m), Money.From(3.33m), Money.From(3.33m)]);
        quotes.Aggregate(Money.Zero, (sum, quote) => sum.Add(quote.Discount)).ShouldBe(Money.From(10m));
    }

    [Fact]
    public void A_fixed_amount_bundle_larger_than_the_sum_should_zero_the_prices()
    {
        var a = FixedService("A", 10m);
        var b = FixedService("B", 20m);
        var bundle = Bundle(Discount.FixedAmount(Money.From(500m)), a, b);

        var quotes = PriceCalculator.CalculateAll([a, b], CreateBranch(), FridayNoon, [bundle]);

        quotes.ShouldAllBe(quote => quote.FinalPrice == Money.Zero);
    }

    [Fact]
    public void The_bundle_should_use_the_branch_prices_as_list_prices()
    {
        var haircut = FixedService("Haircut", 100m);
        var beard = FixedService("Beard", 50m);
        var branch = CreateBranch();
        branch.SetCustomPrice(haircut, Money.From(80m));
        var bundle = Bundle(Discount.Percentage(10m), haircut, beard); // 10% of 130 = 13

        var quotes = PriceCalculator.CalculateAll([haircut, beard], branch, FridayNoon, [bundle]);

        quotes[0].ListPrice.ShouldBe(Money.From(80m));
        quotes[0].Discount.ShouldBe(Money.From(8m));
        quotes[1].Discount.ShouldBe(Money.From(5m));
    }

    [Fact]
    public void The_quotes_should_come_back_in_the_order_of_the_services()
    {
        var haircut = FixedService("Haircut", 100m);
        var beard = FixedService("Beard", 50m);
        var wash = FixedService("Wash", 30m);
        var bundle = Bundle(Discount.Percentage(10m), haircut, wash);

        var quotes = PriceCalculator.CalculateAll([wash, beard, haircut], CreateBranch(), FridayNoon, [bundle]);

        quotes[0].ListPrice.ShouldBe(Money.From(30m));
        quotes[0].IsFromBundle.ShouldBeTrue();
        quotes[1].ListPrice.ShouldBe(Money.From(50m));
        quotes[1].IsFromBundle.ShouldBeFalse();
        quotes[2].ListPrice.ShouldBe(Money.From(100m));
        quotes[2].IsFromBundle.ShouldBeTrue();
    }

    // ---- A bundle that does not apply ----

    [Fact]
    public void Without_bundles_each_service_should_get_the_same_quote_as_Calculate()
    {
        var haircut = FixedService("Haircut", 100m);
        var beard = FixedService("Beard", 50m);
        var branch = CreateBranch();
        var simple = Simple(haircut, Discount.Percentage(10m));

        var quotes = PriceCalculator.CalculateAll([haircut, beard], branch, FridayNoon, [simple]);

        quotes.ShouldBe([
            PriceCalculator.Calculate(haircut, branch, FridayNoon, [simple]),
            PriceCalculator.Calculate(beard, branch, FridayNoon, [simple])]);
    }

    [Fact]
    public void A_bundle_with_a_missing_service_should_not_apply()
    {
        var haircut = FixedService("Haircut", 100m);
        var beard = FixedService("Beard", 50m);
        var bundle = Bundle(Discount.Percentage(10m), haircut, beard);

        var quotes = PriceCalculator.CalculateAll([haircut], CreateBranch(), FridayNoon, [bundle]);

        quotes[0].PromotionId.ShouldBeNull();
        quotes[0].IsFromBundle.ShouldBeFalse();
    }

    [Fact]
    public void A_service_on_request_should_not_count_as_a_member_of_the_bundle()
    {
        var haircut = FixedService("Haircut", 100m);
        var tattoo = FixedService("Tattoo", 300m);
        var bundle = Bundle(Discount.Percentage(10m), haircut, tattoo);
        tattoo.SetOnRequest();

        var quotes = PriceCalculator.CalculateAll([haircut, tattoo], CreateBranch(), FridayNoon, [bundle]);

        quotes[0].PromotionId.ShouldBeNull();
        quotes[1].IsOnRequest.ShouldBeTrue();
    }

    [Fact]
    public void A_bundle_outside_its_period_should_not_apply()
    {
        var haircut = FixedService("Haircut", 100m);
        var beard = FixedService("Beard", 50m);
        var bundle = Bundle(Discount.Percentage(10m), haircut, beard);
        var december = new DateTime(2026, 12, 3, 15, 0, 0, DateTimeKind.Utc);

        var quotes = PriceCalculator.CalculateAll([haircut, beard], CreateBranch(), december, [bundle]);

        quotes.ShouldAllBe(quote => quote.PromotionId == null);
    }

    [Fact]
    public void The_weekdays_of_the_bundle_should_be_respected()
    {
        var haircut = FixedService("Haircut", 100m);
        var beard = FixedService("Beard", 50m);
        var bundle = Bundle(Discount.Percentage(10m), haircut, beard);
        bundle.SetWeekdays([DayOfWeek.Monday]);

        var quotes = PriceCalculator.CalculateAll([haircut, beard], CreateBranch(), FridayNoon, [bundle]);

        quotes.ShouldAllBe(quote => quote.PromotionId == null);
    }

    [Fact]
    public void An_ended_bundle_should_not_apply()
    {
        var haircut = FixedService("Haircut", 100m);
        var beard = FixedService("Beard", 50m);
        var bundle = Bundle(Discount.Percentage(10m), haircut, beard);
        bundle.End();

        var quotes = PriceCalculator.CalculateAll([haircut, beard], CreateBranch(), FridayNoon, [bundle]);

        quotes.ShouldAllBe(quote => quote.PromotionId == null);
    }

    // ---- Bundle against per-service promotions (RN15) ----

    [Fact]
    public void A_stronger_per_service_promotion_should_beat_a_weaker_bundle()
    {
        var haircut = FixedService("Haircut", 100m);
        var beard = FixedService("Beard", 50m);
        var simple = Simple(haircut, Discount.Percentage(30m));       // 30.00
        var bundle = Bundle(Discount.Percentage(10m), haircut, beard); // 15.00

        var quotes = PriceCalculator.CalculateAll([haircut, beard], CreateBranch(), FridayNoon, [bundle, simple]);

        quotes[0].PromotionId.ShouldBe(simple.Id);
        quotes[0].IsFromBundle.ShouldBeFalse();
        quotes[1].PromotionId.ShouldBeNull();
    }

    [Fact]
    public void A_stronger_bundle_should_replace_the_per_service_promotion_of_its_members()
    {
        var haircut = FixedService("Haircut", 100m);
        var beard = FixedService("Beard", 50m);
        var simple = Simple(haircut, Discount.Percentage(5m));         // 5.00
        var bundle = Bundle(Discount.Percentage(20m), haircut, beard); // 30.00

        var quotes = PriceCalculator.CalculateAll([haircut, beard], CreateBranch(), FridayNoon, [simple, bundle]);

        quotes[0].PromotionId.ShouldBe(bundle.Id);
        quotes[0].Discount.ShouldBe(Money.From(20m));
        quotes[1].PromotionId.ShouldBe(bundle.Id);
        quotes[1].Discount.ShouldBe(Money.From(10m));
    }

    [Fact]
    public void Two_per_service_promotions_that_together_beat_the_bundle_should_win()
    {
        var a = FixedService("A", 100m);
        var b = FixedService("B", 100m);
        var promotionA = Simple(a, Discount.Percentage(15m)); // 15.00
        var promotionB = Simple(b, Discount.Percentage(15m)); // 15.00, together 30.00
        var bundle = Bundle(Discount.FixedAmount(Money.From(20m)), a, b); // 20.00 beats each one, not both

        var quotes = PriceCalculator.CalculateAll([a, b], CreateBranch(), FridayNoon, [bundle, promotionA, promotionB]);

        quotes[0].PromotionId.ShouldBe(promotionA.Id);
        quotes[1].PromotionId.ShouldBe(promotionB.Id);
    }

    [Fact]
    public void A_bundle_that_adds_nothing_to_what_the_services_already_get_should_not_be_used()
    {
        var haircut = FixedService("Haircut", 100m);
        var beard = FixedService("Beard", 50m);
        var simple = Simple(haircut, Discount.Percentage(10m));                // 10.00
        var bundle = Bundle(Discount.FixedAmount(Money.From(10m)), haircut, beard); // 10.00, gain 0

        var quotes = PriceCalculator.CalculateAll([haircut, beard], CreateBranch(), FridayNoon, [bundle, simple]);

        quotes[0].PromotionId.ShouldBe(simple.Id);
        quotes[1].PromotionId.ShouldBeNull();
    }

    // ---- Several bundles ----

    [Fact]
    public void Bundles_with_different_services_should_all_apply()
    {
        var a = FixedService("A", 100m);
        var b = FixedService("B", 100m);
        var c = FixedService("C", 100m);
        var d = FixedService("D", 100m);
        var first = Bundle(Discount.Percentage(10m), a, b);
        var second = Bundle(Discount.Percentage(20m), c, d);

        var quotes = PriceCalculator.CalculateAll([a, b, c, d], CreateBranch(), FridayNoon, [first, second]);

        quotes[0].PromotionId.ShouldBe(first.Id);
        quotes[1].PromotionId.ShouldBe(first.Id);
        quotes[2].PromotionId.ShouldBe(second.Id);
        quotes[3].PromotionId.ShouldBe(second.Id);
        quotes[0].Discount.ShouldBe(Money.From(10m));
        quotes[2].Discount.ShouldBe(Money.From(20m));
    }

    [Fact]
    public void Bundles_that_share_a_service_should_not_stack_and_the_larger_discount_should_win()
    {
        var a = FixedService("A", 100m);
        var b = FixedService("B", 100m);
        var c = FixedService("C", 100m);
        var small = Bundle(Discount.Percentage(10m), a, b);  // 20.00
        var large = Bundle(Discount.Percentage(25m), b, c);  // 50.00

        var quotes = PriceCalculator.CalculateAll([a, b, c], CreateBranch(), FridayNoon, [small, large]);

        quotes[0].PromotionId.ShouldBeNull();
        quotes[1].PromotionId.ShouldBe(large.Id);
        quotes[2].PromotionId.ShouldBe(large.Id);
    }

    [Fact]
    public void A_tie_between_bundles_should_be_decided_the_same_way_regardless_of_the_list_order()
    {
        var a = FixedService("A", 100m);
        var b = FixedService("B", 100m);
        var c = FixedService("C", 100m);
        var first = Bundle(Discount.Percentage(10m), a, b);
        var second = Bundle(Discount.Percentage(10m), b, c);
        var branch = CreateBranch();

        var forward = PriceCalculator.CalculateAll([a, b, c], branch, FridayNoon, [first, second]);
        var backward = PriceCalculator.CalculateAll([a, b, c], branch, FridayNoon, [second, first]);

        forward.ShouldBe(backward);

        var used = forward.Where(quote => quote.PromotionId is not null)
            .Select(quote => quote.PromotionId).Distinct().ToList();
        used.Count.ShouldBe(1);
        used[0].ShouldBe(new[] { first.Id, second.Id }.Min());
    }

    // ---- Repeated service ----

    [Fact]
    public void When_a_service_is_repeated_only_its_first_occurrence_should_join_the_bundle()
    {
        var haircut = FixedService("Haircut", 100m);
        var beard = FixedService("Beard", 50m);
        var bundle = Bundle(Discount.Percentage(10m), haircut, beard); // 10% of 150 = 15

        var quotes = PriceCalculator.CalculateAll([haircut, haircut, beard], CreateBranch(), FridayNoon, [bundle]);

        quotes[0].PromotionId.ShouldBe(bundle.Id);
        quotes[0].Discount.ShouldBe(Money.From(10m));
        quotes[1].PromotionId.ShouldBeNull();
        quotes[1].FinalPrice.ShouldBe(Money.From(100m));
        quotes[2].PromotionId.ShouldBe(bundle.Id);
        quotes[2].Discount.ShouldBe(Money.From(5m));
    }

    // ---- Invalid input ----

    [Fact]
    public void An_empty_list_of_services_should_give_an_empty_list_of_quotes()
    {
        PriceCalculator.CalculateAll([], CreateBranch(), FridayNoon, []).ShouldBeEmpty();
    }

    [Fact]
    public void A_service_from_another_company_should_be_rejected()
    {
        var foreign = FixedService("Haircut", 100m, companyId: Guid.CreateVersion7());

        Should.Throw<DomainException>(() => PriceCalculator.CalculateAll([foreign], CreateBranch(), FridayNoon, []))
            .Code.ShouldBe("PriceCalculator.CompanyMismatch");
    }

    [Fact]
    public void An_appointment_instant_that_is_not_UTC_should_be_rejected()
    {
        var haircut = FixedService("Haircut", 100m);
        var unspecified = new DateTime(2026, 11, 6, 15, 0, 0, DateTimeKind.Unspecified);

        Should.Throw<DomainException>(() => PriceCalculator.CalculateAll([haircut], CreateBranch(), unspecified, []))
            .Code.ShouldBe("Branch.TimeMustBeUtc");
    }
}
using PlanelyApp.Domain.Catalog.Promotions;
using PlanelyApp.Domain.Catalog.ServiceOfferings;
using PlanelyApp.Domain.Organization.Branches;
using PlanelyApp.Domain.Scheduling.Pricing;
using PlanelyApp.Domain.SeedWork;
using PlanelyApp.Domain.SharedKernel;
using Shouldly;

namespace PlanelyApp.Domain.Tests.Scheduling.Pricing;

public class PriceCalculatorTests
{
    private static readonly Guid CompanyId = Guid.CreateVersion7();
    private static readonly ValidityPeriod November =
        ValidityPeriod.Create(new DateOnly(2026, 11, 1), new DateOnly(2026, 11, 30));

    // Friday 2026-11-06, 12:00 in São Paulo.
    private static readonly DateTime FridayNoon = new(2026, 11, 6, 15, 0, 0, DateTimeKind.Utc);

    private static Branch CreateBranch(Guid? companyId = null) =>
        Branch.Create(
            companyId ?? CompanyId,
            "Centro",
            Address.Create("Rua das Flores", "123", null, "Centro", "São Paulo", "SP", "01310-100"),
            Phone.Create("+551133334444"),
            "America/Sao_Paulo");

    private static ServiceOffering FixedService(decimal basePrice = 100m, Guid? companyId = null) =>
        ServiceOffering.CreateWithFixedPrice(
            companyId ?? CompanyId, "Haircut", TimeSpan.FromMinutes(45), Money.From(basePrice));

    private static Promotion PromotionFor(ServiceOffering service, Discount discount, ValidityPeriod? validity = null) =>
        Promotion.CreateForAllBranches(CompanyId, "Promo", discount, validity ?? November, [service]);

    // ---- Price of the service (RN13) ----

    [Fact]
    public void Without_branch_price_or_promotion_the_price_should_be_the_base_price()
    {
        var service = FixedService(100m);

        var quote = PriceCalculator.Calculate(service, CreateBranch(), FridayNoon, []);

        quote.IsOnRequest.ShouldBeFalse();
        quote.ListPrice.ShouldBe(Money.From(100m));
        quote.Discount.ShouldBe(Money.Zero);
        quote.FinalPrice.ShouldBe(Money.From(100m));
        quote.PromotionId.ShouldBeNull();
    }

    [Fact]
    public void The_branch_price_should_replace_the_base_price()
    {
        var service = FixedService(100m);
        var branch = CreateBranch();
        branch.SetCustomPrice(service, Money.From(80m));

        var quote = PriceCalculator.Calculate(service, branch, FridayNoon, []);

        quote.ListPrice.ShouldBe(Money.From(80m));
        quote.FinalPrice.ShouldBe(Money.From(80m));
    }

    [Fact]
    public void The_price_of_another_service_in_the_branch_should_not_interfere()
    {
        var service = FixedService(100m);
        var other = FixedService(50m);
        var branch = CreateBranch();
        branch.SetCustomPrice(other, Money.From(30m));

        var quote = PriceCalculator.Calculate(service, branch, FridayNoon, []);

        quote.ListPrice.ShouldBe(Money.From(100m));
    }

    // ---- On request ----

    [Fact]
    public void A_service_on_request_should_give_a_quote_on_request()
    {
        var service = ServiceOffering.CreateOnRequest(CompanyId, "Custom tattoo", TimeSpan.FromHours(3));

        var quote = PriceCalculator.Calculate(service, CreateBranch(), FridayNoon, []);

        quote.IsOnRequest.ShouldBeTrue();
        quote.ListPrice.ShouldBeNull();
        quote.FinalPrice.ShouldBeNull();
    }

    [Fact]
    public void A_service_that_became_on_request_should_ignore_old_branch_prices_and_promotions()
    {
        var service = FixedService(100m);
        var branch = CreateBranch();
        branch.SetCustomPrice(service, Money.From(80m));
        var promotion = PromotionFor(service, Discount.Percentage(10m));

        service.SetOnRequest();

        var quote = PriceCalculator.Calculate(service, branch, FridayNoon, [promotion]);

        quote.IsOnRequest.ShouldBeTrue();
        quote.PromotionId.ShouldBeNull();
    }

    // ---- Promotions (RN15) ----

    [Fact]
    public void A_percentage_promotion_should_reduce_the_price()
    {
        var service = FixedService(100m);
        var promotion = PromotionFor(service, Discount.Percentage(10m));

        var quote = PriceCalculator.Calculate(service, CreateBranch(), FridayNoon, [promotion]);

        quote.ListPrice.ShouldBe(Money.From(100m));
        quote.Discount.ShouldBe(Money.From(10m));
        quote.FinalPrice.ShouldBe(Money.From(90m));
        quote.PromotionId.ShouldBe(promotion.Id);
    }

    [Fact]
    public void A_fixed_amount_promotion_should_reduce_the_price()
    {
        var service = FixedService(100m);
        var promotion = PromotionFor(service, Discount.FixedAmount(Money.From(15m)));

        var quote = PriceCalculator.Calculate(service, CreateBranch(), FridayNoon, [promotion]);

        quote.FinalPrice.ShouldBe(Money.From(85m));
    }

    [Fact]
    public void The_promotion_should_apply_on_top_of_the_branch_price()
    {
        var service = FixedService(100m);
        var branch = CreateBranch();
        branch.SetCustomPrice(service, Money.From(80m));
        var promotion = PromotionFor(service, Discount.Percentage(10m));

        var quote = PriceCalculator.Calculate(service, branch, FridayNoon, [promotion]);

        quote.ListPrice.ShouldBe(Money.From(80m));
        quote.Discount.ShouldBe(Money.From(8m));
        quote.FinalPrice.ShouldBe(Money.From(72m));
    }

    [Fact]
    public void A_fixed_amount_larger_than_the_price_should_leave_the_final_price_at_zero()
    {
        var service = FixedService(100m);
        var promotion = PromotionFor(service, Discount.FixedAmount(Money.From(150m)));

        var quote = PriceCalculator.Calculate(service, CreateBranch(), FridayNoon, [promotion]);

        quote.Discount.ShouldBe(Money.From(100m));
        quote.FinalPrice.ShouldBe(Money.Zero);
    }

    [Fact]
    public void Promotions_should_not_stack_and_the_largest_discount_should_win()
    {
        var service = FixedService(100m);
        var tenPercent = PromotionFor(service, Discount.Percentage(10m));
        var twentyFivePesos = PromotionFor(service, Discount.FixedAmount(Money.From(25m)));
        var twentyPercent = PromotionFor(service, Discount.Percentage(20m));

        var quote = PriceCalculator.Calculate(
            service, CreateBranch(), FridayNoon, [tenPercent, twentyFivePesos, twentyPercent]);

        quote.PromotionId.ShouldBe(twentyFivePesos.Id);
        quote.Discount.ShouldBe(Money.From(25m));
        quote.FinalPrice.ShouldBe(Money.From(75m));
    }

    [Fact]
    public void The_largest_discount_should_be_compared_in_money_not_in_percentage()
    {
        var service = FixedService(50m);
        var tenPercent = PromotionFor(service, Discount.Percentage(10m));       // 5.00
        var sevenFixed = PromotionFor(service, Discount.FixedAmount(Money.From(7m))); // 7.00

        var quote = PriceCalculator.Calculate(service, CreateBranch(), FridayNoon, [tenPercent, sevenFixed]);

        quote.PromotionId.ShouldBe(sevenFixed.Id);
    }

    [Fact]
    public void A_tie_should_be_decided_the_same_way_regardless_of_the_list_order()
    {
        var service = FixedService(100m);
        var first = PromotionFor(service, Discount.Percentage(10m));
        var second = PromotionFor(service, Discount.FixedAmount(Money.From(10m)));
        var branch = CreateBranch();

        var forward = PriceCalculator.Calculate(service, branch, FridayNoon, [first, second]);
        var backward = PriceCalculator.Calculate(service, branch, FridayNoon, [second, first]);

        forward.PromotionId.ShouldBe(backward.PromotionId);
        forward.PromotionId.ShouldBe(new[] { first.Id, second.Id }.Min());
    }

    [Fact]
    public void A_promotion_whose_discount_rounds_to_zero_should_not_be_applied()
    {
        var service = FixedService(0.04m);
        var promotion = PromotionFor(service, Discount.Percentage(10m)); // 0.004 rounds to 0.00

        var quote = PriceCalculator.Calculate(service, CreateBranch(), FridayNoon, [promotion]);

        quote.PromotionId.ShouldBeNull();
        quote.FinalPrice.ShouldBe(Money.From(0.04m));
    }

    // ---- Promotions that do not apply ----

    [Fact]
    public void A_promotion_for_another_service_should_not_apply()
    {
        var service = FixedService(100m);
        var other = FixedService(100m);
        var promotion = PromotionFor(other, Discount.Percentage(10m));

        var quote = PriceCalculator.Calculate(service, CreateBranch(), FridayNoon, [promotion]);

        quote.PromotionId.ShouldBeNull();
    }

    [Fact]
    public void A_promotion_for_other_branches_should_not_apply()
    {
        var service = FixedService(100m);
        var promotion = Promotion.CreateForBranches(
            CompanyId, "Promo", Discount.Percentage(10m), November, [service], [Guid.CreateVersion7()]);

        var quote = PriceCalculator.Calculate(service, CreateBranch(), FridayNoon, [promotion]);

        quote.PromotionId.ShouldBeNull();
    }

    [Fact]
    public void A_promotion_for_this_branch_should_apply()
    {
        var service = FixedService(100m);
        var branch = CreateBranch();
        var promotion = Promotion.CreateForBranches(
            CompanyId, "Promo", Discount.Percentage(10m), November, [service], [branch.Id]);

        var quote = PriceCalculator.Calculate(service, branch, FridayNoon, [promotion]);

        quote.PromotionId.ShouldBe(promotion.Id);
    }

    [Fact]
    public void A_promotion_outside_its_period_should_not_apply()
    {
        var service = FixedService(100m);
        var promotion = PromotionFor(service, Discount.Percentage(10m));
        var december = new DateTime(2026, 12, 3, 15, 0, 0, DateTimeKind.Utc);

        var quote = PriceCalculator.Calculate(service, CreateBranch(), december, [promotion]);

        quote.PromotionId.ShouldBeNull();
    }

    [Fact]
    public void An_ended_promotion_should_not_apply()
    {
        var service = FixedService(100m);
        var promotion = PromotionFor(service, Discount.Percentage(10m));
        promotion.End();

        var quote = PriceCalculator.Calculate(service, CreateBranch(), FridayNoon, [promotion]);

        quote.PromotionId.ShouldBeNull();
    }

    [Fact]
    public void The_weekdays_of_the_promotion_should_be_respected()
    {
        var service = FixedService(100m);
        var promotion = PromotionFor(service, Discount.Percentage(10m));
        promotion.SetWeekdays([DayOfWeek.Monday]);

        var quote = PriceCalculator.Calculate(service, CreateBranch(), FridayNoon, [promotion]);

        quote.PromotionId.ShouldBeNull();
    }

    // ---- Local date at the branch ----

    [Fact]
    public void The_weekday_should_be_the_one_at_the_branch_not_in_UTC()
    {
        var service = FixedService(100m);
        var fridayPromotion = PromotionFor(service, Discount.Percentage(10m));
        fridayPromotion.SetWeekdays([DayOfWeek.Friday]);
        var saturdayPromotion = PromotionFor(service, Discount.Percentage(20m));
        saturdayPromotion.SetWeekdays([DayOfWeek.Saturday]);

        // Saturday 01:00 UTC is still Friday 22:00 in São Paulo.
        var lateFriday = new DateTime(2026, 11, 7, 1, 0, 0, DateTimeKind.Utc);

        var quote = PriceCalculator.Calculate(
            service, CreateBranch(), lateFriday, [fridayPromotion, saturdayPromotion]);

        quote.PromotionId.ShouldBe(fridayPromotion.Id);
    }

    [Fact]
    public void The_validity_should_be_evaluated_on_the_local_date_of_the_branch()
    {
        var service = FixedService(100m);
        var lastDay = ValidityPeriod.Create(new DateOnly(2026, 11, 6), new DateOnly(2026, 11, 6));
        var promotion = PromotionFor(service, Discount.Percentage(10m), lastDay);
        var branch = CreateBranch();

        // 01:00 UTC on the 7th is still the 6th in São Paulo; 03:00 UTC is already the 7th.
        var stillSixth = new DateTime(2026, 11, 7, 1, 0, 0, DateTimeKind.Utc);
        var alreadySeventh = new DateTime(2026, 11, 7, 3, 0, 0, DateTimeKind.Utc);

        PriceCalculator.Calculate(service, branch, stillSixth, [promotion]).PromotionId.ShouldBe(promotion.Id);
        PriceCalculator.Calculate(service, branch, alreadySeventh, [promotion]).PromotionId.ShouldBeNull();
    }

    // ---- Invalid input ----

    [Fact]
    public void A_service_and_a_branch_of_different_companies_should_be_rejected()
    {
        var service = FixedService(100m, companyId: Guid.CreateVersion7());

        Should.Throw<DomainException>(() => PriceCalculator.Calculate(service, CreateBranch(), FridayNoon, []))
            .Code.ShouldBe("PriceCalculator.CompanyMismatch");
    }

    [Fact]
    public void An_appointment_instant_that_is_not_UTC_should_be_rejected()
    {
        var service = FixedService(100m);
        var unspecified = new DateTime(2026, 11, 6, 15, 0, 0, DateTimeKind.Unspecified);

        Should.Throw<DomainException>(() => PriceCalculator.Calculate(service, CreateBranch(), unspecified, []))
            .Code.ShouldBe("Branch.InstantMustBeUtc");
    }
}
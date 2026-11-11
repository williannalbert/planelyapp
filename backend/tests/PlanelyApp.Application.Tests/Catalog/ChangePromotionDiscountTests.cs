using PlanelyApp.Application.Abstractions.Results;
using PlanelyApp.Application.Catalog.Promotions;
using PlanelyApp.Application.Catalog.Promotions.ChangeDiscount;
using PlanelyApp.Domain.Catalog.Promotions;
using PlanelyApp.Domain.SeedWork;
using PlanelyApp.Domain.SharedKernel;
using Shouldly;

namespace PlanelyApp.Application.Tests.Catalog;

public class ChangePromotionDiscountTests
{
    [Fact]
    public async Task Changing_should_replace_the_discount_and_keep_the_rest()
    {
        var promotion = PromotionData.NewPromotion();

        var result = await Handler(promotion).HandleAsync(
            new ChangePromotionDiscountCommand(promotion.Id, new DiscountData(DiscountType.FixedAmount, 15m)),
            TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        promotion.Discount.ShouldBe(Discount.FixedAmount(Money.From(15m)));
        promotion.Name.ShouldBe("Black Friday");
        promotion.Validity.ShouldBe(PromotionData.November);
    }

    [Fact]
    public async Task A_missing_promotion_should_be_not_found()
    {
        var result = await Handler(null).HandleAsync(
            new ChangePromotionDiscountCommand(Guid.NewGuid(), new DiscountData(DiscountType.Percentage, 5m)),
            TestContext.Current.CancellationToken);

        result.Error.Type.ShouldBe(ErrorType.NotFound);
        result.Error.Code.ShouldBe("Promotion.NotFound");
    }

    [Theory]
    [InlineData(DiscountType.Percentage, 0, "Discount.PercentageInvalid")]
    [InlineData(DiscountType.Percentage, 101, "Discount.PercentageInvalid")]
    [InlineData(DiscountType.FixedAmount, 0, "Discount.FixedAmountInvalid")]
    public async Task A_discount_the_domain_refuses_should_be_refused_and_leave_the_promotion_alone(
        DiscountType type, int value, string code)
    {
        var promotion = PromotionData.NewPromotion();

        var exception = await Should.ThrowAsync<DomainException>(() => Handler(promotion).HandleAsync(
            new ChangePromotionDiscountCommand(promotion.Id, new DiscountData(type, value)),
            TestContext.Current.CancellationToken));

        exception.Code.ShouldBe(code);
        promotion.Discount.ShouldBe(Discount.Percentage(10m));
    }

    [Fact]
    public async Task A_valid_command_should_pass_the_validator()
    {
        var problems = await TestValidation.RunAsync(
            new ChangePromotionDiscountValidator(),
            new ChangePromotionDiscountCommand(Guid.NewGuid(), new DiscountData(DiscountType.Percentage, 12.5m)));

        problems.ShouldBeEmpty();
    }

    [Fact]
    public async Task A_missing_id_and_a_missing_discount_should_be_reported_as_required()
    {
        var problems = await TestValidation.RunAsync(
            new ChangePromotionDiscountValidator(), new ChangePromotionDiscountCommand(Guid.Empty, null!));

        problems.ShouldBe(["PromotionId:Validation.Required", "Discount:Validation.Required"], ignoreOrder: true);
    }

    [Fact]
    public async Task A_discount_type_that_does_not_exist_should_be_reported_as_invalid()
    {
        var problems = await TestValidation.RunAsync(
            new ChangePromotionDiscountValidator(),
            new ChangePromotionDiscountCommand(Guid.NewGuid(), new DiscountData((DiscountType)99, 10m)));

        problems.ShouldBe(["Discount.Type:Validation.Invalid"], ignoreOrder: true);
    }

    [Fact]
    public async Task A_discount_with_three_decimals_should_be_reported_as_invalid()
    {
        var problems = await TestValidation.RunAsync(
            new ChangePromotionDiscountValidator(),
            new ChangePromotionDiscountCommand(Guid.NewGuid(), new DiscountData(DiscountType.Percentage, 10.123m)));

        problems.ShouldBe(["Discount.Value:Validation.Invalid"], ignoreOrder: true);
    }

    private static ChangePromotionDiscountHandler Handler(Promotion? promotion) =>
        new(PromotionData.PromotionsWith(promotion));
}
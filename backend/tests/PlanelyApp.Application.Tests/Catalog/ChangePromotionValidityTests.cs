using PlanelyApp.Application.Abstractions.Results;
using PlanelyApp.Application.Catalog.Promotions.ChangeValidity;
using PlanelyApp.Domain.Catalog.Promotions;
using PlanelyApp.Domain.SeedWork;
using Shouldly;

namespace PlanelyApp.Application.Tests.Catalog;

public class ChangePromotionValidityTests
{
    [Fact]
    public async Task Changing_should_replace_the_period_and_keep_the_rest()
    {
        var promotion = PromotionData.NewPromotion();

        var result = await Handler(promotion).HandleAsync(
            new ChangePromotionValidityCommand(promotion.Id, new DateOnly(2027, 12, 1), new DateOnly(2027, 12, 31)),
            TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        promotion.Validity.ShouldBe(ValidityPeriod.Create(new DateOnly(2027, 12, 1), new DateOnly(2027, 12, 31)));
        promotion.Discount.ShouldBe(Discount.Percentage(10m));
        promotion.IsActive.ShouldBeTrue();
    }

    [Fact]
    public async Task A_missing_promotion_should_be_not_found()
    {
        var result = await Handler(null).HandleAsync(
            new ChangePromotionValidityCommand(Guid.NewGuid(), PromotionData.Start, PromotionData.End),
            TestContext.Current.CancellationToken);

        result.Error.Type.ShouldBe(ErrorType.NotFound);
        result.Error.Code.ShouldBe("Promotion.NotFound");
    }

    [Fact]
    public async Task A_period_that_ends_before_it_starts_should_be_refused_and_leave_the_promotion_alone()
    {
        var promotion = PromotionData.NewPromotion();

        var exception = await Should.ThrowAsync<DomainException>(() => Handler(promotion).HandleAsync(
            new ChangePromotionValidityCommand(promotion.Id, new DateOnly(2027, 12, 1), new DateOnly(2027, 11, 30)),
            TestContext.Current.CancellationToken));

        exception.Code.ShouldBe("ValidityPeriod.EndBeforeStart");
        promotion.Validity.ShouldBe(PromotionData.November);
    }

    [Fact]
    public async Task A_missing_id_should_be_reported_as_required()
    {
        var problems = await TestValidation.RunAsync(
            new ChangePromotionValidityValidator(),
            new ChangePromotionValidityCommand(Guid.Empty, PromotionData.Start, PromotionData.End));

        problems.ShouldBe(["PromotionId:Validation.Required"], ignoreOrder: true);
    }

    private static ChangePromotionValidityHandler Handler(Promotion? promotion) =>
        new(PromotionData.PromotionsWith(promotion));
}
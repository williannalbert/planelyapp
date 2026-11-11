using PlanelyApp.Application.Abstractions.Results;
using PlanelyApp.Application.Catalog.Promotions.End;
using Shouldly;

namespace PlanelyApp.Application.Tests.Catalog;

public class EndPromotionTests
{
    [Fact]
    public async Task Ending_should_turn_the_promotion_off()
    {
        var promotion = PromotionData.NewPromotion();

        var result = await new EndPromotionHandler(PromotionData.PromotionsWith(promotion)).HandleAsync(
            new EndPromotionCommand(promotion.Id), TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        promotion.IsActive.ShouldBeFalse();
    }

    [Fact]
    public async Task Ending_a_promotion_that_already_ended_should_still_succeed()
    {
        var promotion = PromotionData.NewPromotion();
        promotion.End();

        var result = await new EndPromotionHandler(PromotionData.PromotionsWith(promotion)).HandleAsync(
            new EndPromotionCommand(promotion.Id), TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        promotion.IsActive.ShouldBeFalse();
    }

    [Fact]
    public async Task Ending_a_missing_promotion_should_be_not_found()
    {
        var result = await new EndPromotionHandler(PromotionData.PromotionsWith(null)).HandleAsync(
            new EndPromotionCommand(Guid.NewGuid()), TestContext.Current.CancellationToken);

        result.Error.Type.ShouldBe(ErrorType.NotFound);
        result.Error.Code.ShouldBe("Promotion.NotFound");
    }
}
using PlanelyApp.Application.Abstractions.Results;
using PlanelyApp.Application.Catalog.Promotions.UpdateDetails;
using PlanelyApp.Domain.Catalog.Promotions;
using PlanelyApp.Domain.SeedWork;
using Shouldly;

namespace PlanelyApp.Application.Tests.Catalog;

public class UpdatePromotionDetailsTests
{
    [Fact]
    public async Task Updating_should_replace_the_name_and_the_description_and_keep_the_rest()
    {
        var promotion = PromotionData.NewPromotion();

        var result = await Handler(promotion).HandleAsync(
            new UpdatePromotionDetailsCommand(promotion.Id, "  Cyber Monday ", " Online only "),
            TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        promotion.Name.ShouldBe("Cyber Monday");
        promotion.Description.ShouldBe("Online only");
        promotion.Discount.ShouldBe(Discount.Percentage(10m));
        promotion.Validity.ShouldBe(PromotionData.November);
        promotion.IsActive.ShouldBeTrue();
    }

    [Fact]
    public async Task A_description_left_out_should_clear_it()
    {
        var promotion = PromotionData.NewPromotion();
        promotion.UpdateDetails("Black Friday", "Old text");

        var result = await Handler(promotion).HandleAsync(
            new UpdatePromotionDetailsCommand(promotion.Id, "Black Friday"), TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        promotion.Description.ShouldBeNull();
    }

    [Fact]
    public async Task A_missing_promotion_should_be_not_found()
    {
        var result = await Handler(null).HandleAsync(
            new UpdatePromotionDetailsCommand(Guid.NewGuid(), "Black Friday"), TestContext.Current.CancellationToken);

        result.Error.Type.ShouldBe(ErrorType.NotFound);
        result.Error.Code.ShouldBe("Promotion.NotFound");
    }

    [Fact]
    public async Task A_name_above_the_limit_should_be_refused_and_leave_the_promotion_alone()
    {
        var promotion = PromotionData.NewPromotion();

        var exception = await Should.ThrowAsync<DomainException>(() => Handler(promotion).HandleAsync(
            new UpdatePromotionDetailsCommand(promotion.Id, new string('x', 101), "New text"),
            TestContext.Current.CancellationToken));

        exception.Code.ShouldBe("Promotion.NameInvalid");
        promotion.Name.ShouldBe("Black Friday");
        promotion.Description.ShouldBeNull();
    }

    [Fact]
    public async Task A_description_above_the_limit_should_be_refused()
    {
        var promotion = PromotionData.NewPromotion();

        var exception = await Should.ThrowAsync<DomainException>(() => Handler(promotion).HandleAsync(
            new UpdatePromotionDetailsCommand(promotion.Id, "Cyber Monday", new string('x', 1001)),
            TestContext.Current.CancellationToken));

        exception.Code.ShouldBe("Promotion.DescriptionInvalid");
        promotion.Name.ShouldBe("Black Friday");
    }

    [Fact]
    public async Task A_missing_id_and_a_blank_name_should_be_reported_as_required()
    {
        var problems = await TestValidation.RunAsync(
            new UpdatePromotionDetailsValidator(), new UpdatePromotionDetailsCommand(Guid.Empty, "   "));

        problems.ShouldBe(["PromotionId:Validation.Required", "Name:Validation.Required"], ignoreOrder: true);
    }

    private static UpdatePromotionDetailsHandler Handler(Promotion? promotion) =>
        new(PromotionData.PromotionsWith(promotion));
}
using PlanelyApp.Application.Abstractions.Results;
using PlanelyApp.Application.Catalog.Promotions.SetWeekdays;
using PlanelyApp.Domain.Catalog.Promotions;
using PlanelyApp.Domain.SeedWork;
using Shouldly;

namespace PlanelyApp.Application.Tests.Catalog;

public class SetPromotionWeekdaysTests
{
    [Fact]
    public async Task Setting_should_replace_the_weekdays_ordered_and_without_repeats()
    {
        var promotion = PromotionData.NewPromotion();
        promotion.SetWeekdays([DayOfWeek.Sunday]);

        var result = await Handler(promotion).HandleAsync(
            new SetPromotionWeekdaysCommand(promotion.Id, [DayOfWeek.Saturday, DayOfWeek.Monday, DayOfWeek.Saturday]),
            TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        promotion.Weekdays.ShouldBe([DayOfWeek.Monday, DayOfWeek.Saturday], ignoreOrder: false);
    }

    [Fact]
    public async Task An_empty_list_should_make_the_promotion_work_every_day()
    {
        var promotion = PromotionData.NewPromotion();
        promotion.SetWeekdays([DayOfWeek.Sunday]);

        var result = await Handler(promotion).HandleAsync(
            new SetPromotionWeekdaysCommand(promotion.Id, []), TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        promotion.Weekdays.ShouldBeEmpty();
    }

    [Fact]
    public async Task A_missing_promotion_should_be_not_found()
    {
        var result = await Handler(null).HandleAsync(
            new SetPromotionWeekdaysCommand(Guid.NewGuid(), [DayOfWeek.Monday]),
            TestContext.Current.CancellationToken);

        result.Error.Type.ShouldBe(ErrorType.NotFound);
        result.Error.Code.ShouldBe("Promotion.NotFound");
    }

    [Fact]
    public async Task A_weekday_that_does_not_exist_should_be_refused_and_leave_the_promotion_alone()
    {
        var promotion = PromotionData.NewPromotion();
        promotion.SetWeekdays([DayOfWeek.Sunday]);

        var exception = await Should.ThrowAsync<DomainException>(() => Handler(promotion).HandleAsync(
            new SetPromotionWeekdaysCommand(promotion.Id, [DayOfWeek.Monday, (DayOfWeek)9]),
            TestContext.Current.CancellationToken));

        exception.Code.ShouldBe("Promotion.WeekdayInvalid");
        promotion.Weekdays.ShouldBe([DayOfWeek.Sunday], ignoreOrder: false);
    }

    [Fact]
    public async Task A_missing_list_should_be_reported_as_required()
    {
        var problems = await TestValidation.RunAsync(
            new SetPromotionWeekdaysValidator(), new SetPromotionWeekdaysCommand(Guid.NewGuid(), null!));

        problems.ShouldBe(["Weekdays:Validation.Required"], ignoreOrder: true);
    }

    [Fact]
    public async Task A_missing_id_should_be_reported_as_required()
    {
        var problems = await TestValidation.RunAsync(
            new SetPromotionWeekdaysValidator(), new SetPromotionWeekdaysCommand(Guid.Empty, []));

        problems.ShouldBe(["PromotionId:Validation.Required"], ignoreOrder: true);
    }

    private static SetPromotionWeekdaysHandler Handler(Promotion? promotion) =>
        new(PromotionData.PromotionsWith(promotion));
}
using PlanelyApp.Application.Abstractions.Results;
using PlanelyApp.Application.Catalog.Promotions;
using PlanelyApp.Application.Catalog.Promotions.Get;
using PlanelyApp.Domain.Catalog.Promotions;
using PlanelyApp.Domain.SharedKernel;
using Shouldly;

namespace PlanelyApp.Application.Tests.Catalog;

public class GetPromotionTests
{
    [Fact]
    public async Task A_promotion_should_come_back_with_everything_it_has()
    {
        var company = Guid.NewGuid();
        var haircut = CatalogData.NewFixedService(company, "Haircut");
        var beard = CatalogData.NewFixedService(company, "Beard");
        var branchId = Guid.NewGuid();
        var promotion = Promotion.CreateBundleForBranches(
            company, "Combo", Discount.FixedAmount(Money.From(15m)), PromotionData.November, [haircut, beard], [branchId], "Cut and beard");
        promotion.SetWeekdays([DayOfWeek.Saturday]);

        var result = await new GetPromotionHandler(PromotionData.PromotionsWith(promotion)).HandleAsync(
            new GetPromotionQuery(promotion.Id), TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        var dto = result.Value;
        dto.Id.ShouldBe(promotion.Id);
        dto.Kind.ShouldBe(PromotionKind.Bundle);
        dto.Name.ShouldBe("Combo");
        dto.Description.ShouldBe("Cut and beard");
        dto.Discount.ShouldBe(new DiscountData(DiscountType.FixedAmount, 15m));
        dto.ValidFrom.ShouldBe(PromotionData.Start);
        dto.ValidUntil.ShouldBe(PromotionData.End);
        dto.Weekdays.ShouldBe([DayOfWeek.Saturday], ignoreOrder: false);
        dto.AppliesToAllBranches.ShouldBeFalse();
        dto.BranchIds.ShouldBe([branchId], ignoreOrder: false);
        dto.ServiceIds.ShouldBe([haircut.Id, beard.Id], ignoreOrder: false);
        dto.IsActive.ShouldBeTrue();
    }

    [Fact]
    public async Task A_missing_promotion_should_be_not_found()
    {
        var result = await new GetPromotionHandler(PromotionData.PromotionsWith(null)).HandleAsync(
            new GetPromotionQuery(Guid.NewGuid()), TestContext.Current.CancellationToken);

        result.Error.Type.ShouldBe(ErrorType.NotFound);
        result.Error.Code.ShouldBe("Promotion.NotFound");
    }
}
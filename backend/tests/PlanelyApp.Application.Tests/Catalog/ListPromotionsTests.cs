using NSubstitute;
using PlanelyApp.Application.Abstractions.Paging;
using PlanelyApp.Application.Catalog.Promotions;
using PlanelyApp.Application.Catalog.Promotions.List;
using PlanelyApp.Domain.Catalog.Promotions;
using Shouldly;

namespace PlanelyApp.Application.Tests.Catalog;

public class ListPromotionsTests
{
    [Fact]
    public async Task What_the_read_model_lists_should_come_back_as_it_is()
    {
        var page = new PagedResult<PromotionSummaryDto>(
            [new(Guid.NewGuid(), PromotionKind.Bundle, "Combo", DiscountType.FixedAmount, 15m,
                PromotionData.Start, PromotionData.End, true, 2, true)],
            2, 20, 21);
        var readModel = Substitute.For<IPromotionReadModel>();
        readModel.ListAsync(null, PromotionKind.Bundle, true, PromotionData.Start, 2, 20, Arg.Any<CancellationToken>())
            .Returns(page);

        var result = await new ListPromotionsHandler(readModel).HandleAsync(
            new ListPromotionsQuery(Kind: PromotionKind.Bundle, IsActive: true, ValidOn: PromotionData.Start, Page: 2),
            TestContext.Current.CancellationToken);

        result.Value.ShouldBe(page);
    }

    [Fact]
    public async Task The_search_should_reach_the_read_model_trimmed()
    {
        var readModel = Substitute.For<IPromotionReadModel>();

        await new ListPromotionsHandler(readModel).HandleAsync(
            new ListPromotionsQuery("  black  ", null, null, null, 3, 50), TestContext.Current.CancellationToken);

        await readModel.Received(1).ListAsync("black", null, null, null, 3, 50, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task A_blank_search_should_reach_the_read_model_as_none()
    {
        var readModel = Substitute.For<IPromotionReadModel>();

        await new ListPromotionsHandler(readModel).HandleAsync(
            new ListPromotionsQuery("   "), TestContext.Current.CancellationToken);

        await readModel.Received(1).ListAsync(
            null, null, null, null, 1, PageLimits.DefaultPageSize, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task A_default_query_should_pass_the_validator()
    {
        var problems = await TestValidation.RunAsync(new ListPromotionsValidator(), new ListPromotionsQuery());

        problems.ShouldBeEmpty();
    }

    [Fact]
    public async Task A_page_and_a_size_out_of_range_should_be_reported()
    {
        var problems = await TestValidation.RunAsync(
            new ListPromotionsValidator(), new ListPromotionsQuery(Page: 0, PageSize: 101));

        problems.ShouldBe(["Page:Validation.OutOfRange", "PageSize:Validation.OutOfRange"], ignoreOrder: true);
    }

    [Fact]
    public async Task A_kind_that_does_not_exist_should_be_reported_as_invalid()
    {
        var problems = await TestValidation.RunAsync(
            new ListPromotionsValidator(), new ListPromotionsQuery(Kind: (PromotionKind)99));

        problems.ShouldBe(["Kind:Validation.Invalid"], ignoreOrder: true);
    }
}
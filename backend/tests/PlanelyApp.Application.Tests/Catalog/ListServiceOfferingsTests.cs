using NSubstitute;
using PlanelyApp.Application.Abstractions.Paging;
using PlanelyApp.Application.Catalog.ServiceOfferings;
using PlanelyApp.Application.Catalog.ServiceOfferings.List;
using PlanelyApp.Domain.Catalog.ServiceOfferings;
using Shouldly;

namespace PlanelyApp.Application.Tests.Catalog;

public class ListServiceOfferingsTests
{
    [Fact]
    public async Task What_the_read_model_lists_should_come_back_as_it_is()
    {
        var categoryId = Guid.NewGuid();
        var page = new PagedResult<ServiceOfferingSummaryDto>(
            [new(Guid.NewGuid(), "Haircut", categoryId, "Hair", 30, PricingType.Fixed, 50m, true)], 2, 20, 21);
        var readModel = Substitute.For<IServiceOfferingReadModel>();
        readModel.ListAsync(null, categoryId, true, 2, 20, Arg.Any<CancellationToken>()).Returns(page);

        var result = await new ListServiceOfferingsHandler(readModel).HandleAsync(
            new ListServiceOfferingsQuery(CategoryId: categoryId, IsActive: true, Page: 2),
            TestContext.Current.CancellationToken);

        result.Value.ShouldBe(page);
    }

    [Fact]
    public async Task The_search_should_reach_the_read_model_trimmed()
    {
        var readModel = Substitute.For<IServiceOfferingReadModel>();

        await new ListServiceOfferingsHandler(readModel).HandleAsync(
            new ListServiceOfferingsQuery("  cut  ", null, null, 3, 50), TestContext.Current.CancellationToken);

        await readModel.Received(1).ListAsync("cut", null, null, 3, 50, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task A_blank_search_should_reach_the_read_model_as_none()
    {
        var readModel = Substitute.For<IServiceOfferingReadModel>();

        await new ListServiceOfferingsHandler(readModel).HandleAsync(
            new ListServiceOfferingsQuery("   "), TestContext.Current.CancellationToken);

        await readModel.Received(1).ListAsync(
            null, null, null, 1, PageLimits.DefaultPageSize, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task A_default_query_should_pass_the_validator()
    {
        var problems = await TestValidation.RunAsync(new ListServiceOfferingsValidator(), new ListServiceOfferingsQuery());

        problems.ShouldBeEmpty();
    }

    [Fact]
    public async Task A_page_and_a_size_out_of_range_should_be_reported()
    {
        var problems = await TestValidation.RunAsync(
            new ListServiceOfferingsValidator(), new ListServiceOfferingsQuery(Page: 0, PageSize: 101));

        problems.ShouldBe(["Page:Validation.OutOfRange", "PageSize:Validation.OutOfRange"], ignoreOrder: true);
    }

    [Fact]
    public async Task An_empty_category_id_should_be_reported_as_invalid()
    {
        var problems = await TestValidation.RunAsync(
            new ListServiceOfferingsValidator(), new ListServiceOfferingsQuery(CategoryId: Guid.Empty));

        problems.ShouldBe(["CategoryId:Validation.Invalid"], ignoreOrder: true);
    }
}
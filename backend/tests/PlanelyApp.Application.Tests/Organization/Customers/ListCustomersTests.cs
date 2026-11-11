using NSubstitute;
using PlanelyApp.Application.Abstractions.Paging;
using PlanelyApp.Application.Organization.Customers;
using PlanelyApp.Application.Organization.Customers.List;
using Shouldly;

namespace PlanelyApp.Application.Tests.Organization.Customers;

public class ListCustomersTests
{
    [Fact]
    public async Task What_the_read_model_lists_should_come_back_as_it_is()
    {
        var page = new PagedResult<CustomerSummaryDto>(
            [new(Guid.NewGuid(), "Ana Souza", "ana@example.com", "+5511988887777", true, null)], 2, 20, 21);
        var readModel = Substitute.For<ICustomerReadModel>();
        readModel.ListAsync(null, 2, 20, Arg.Any<CancellationToken>()).Returns(page);

        var result = await new ListCustomersHandler(readModel)
            .HandleAsync(new ListCustomersQuery(Page: 2), TestContext.Current.CancellationToken);

        result.Value.ShouldBe(page);
    }

    [Fact]
    public async Task The_search_should_reach_the_read_model_trimmed()
    {
        var readModel = Substitute.For<ICustomerReadModel>();

        await new ListCustomersHandler(readModel).HandleAsync(
            new ListCustomersQuery("  ana  ", 3, 50), TestContext.Current.CancellationToken);

        await readModel.Received(1).ListAsync("ana", 3, 50, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task A_blank_search_should_reach_the_read_model_as_none()
    {
        var readModel = Substitute.For<ICustomerReadModel>();

        await new ListCustomersHandler(readModel).HandleAsync(
            new ListCustomersQuery("   "), TestContext.Current.CancellationToken);

        await readModel.Received(1).ListAsync(null, 1, PageLimits.DefaultPageSize, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task A_default_query_should_pass_the_validator()
    {
        var problems = await TestValidation.RunAsync(new ListCustomersValidator(), new ListCustomersQuery());

        problems.ShouldBeEmpty();
    }

    [Fact]
    public async Task A_page_and_a_size_out_of_range_should_be_reported()
    {
        var problems = await TestValidation.RunAsync(
            new ListCustomersValidator(), new ListCustomersQuery(Page: 0, PageSize: 101));

        problems.ShouldBe(["Page:Validation.OutOfRange", "PageSize:Validation.OutOfRange"], ignoreOrder: true);
    }

    [Fact]
    public async Task The_limits_themselves_should_be_accepted()
    {
        var first = await TestValidation.RunAsync(
            new ListCustomersValidator(), new ListCustomersQuery(Page: 1, PageSize: 1));
        var last = await TestValidation.RunAsync(
            new ListCustomersValidator(),
            new ListCustomersQuery(Page: PageLimits.MaxPage, PageSize: PageLimits.MaxPageSize));

        first.ShouldBeEmpty();
        last.ShouldBeEmpty();
    }
}
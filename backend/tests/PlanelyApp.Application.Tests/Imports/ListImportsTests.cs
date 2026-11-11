using NSubstitute;
using PlanelyApp.Application.Abstractions.Paging;
using PlanelyApp.Application.Imports;
using PlanelyApp.Application.Imports.List;
using PlanelyApp.Domain.Imports;
using Shouldly;

namespace PlanelyApp.Application.Tests.Imports;

public class ListImportsTests
{
    private static readonly DateTime Now = new(2027, 11, 15, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task What_the_read_model_lists_should_come_back_as_it_is()
    {
        var page = new PagedResult<DataImportSummaryDto>(
            [new(Guid.NewGuid(), ImportType.Services, "services.xlsx", ImportStatus.Completed, 10, 10, 0, Now, Now.AddMinutes(1))],
            2, 20, 21);
        var readModel = Substitute.For<IDataImportReadModel>();
        readModel.ListAsync(2, 20, Arg.Any<CancellationToken>()).Returns(page);

        var result = await new ListImportsHandler(readModel)
            .HandleAsync(new ListImportsQuery(Page: 2), TestContext.Current.CancellationToken);

        result.Value.ShouldBe(page);
    }

    [Fact]
    public async Task A_default_query_should_ask_for_the_first_page_of_the_default_size()
    {
        var readModel = Substitute.For<IDataImportReadModel>();

        await new ListImportsHandler(readModel)
            .HandleAsync(new ListImportsQuery(), TestContext.Current.CancellationToken);

        await readModel.Received(1).ListAsync(1, PageLimits.DefaultPageSize, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task A_default_query_should_pass_the_validator()
    {
        var problems = await TestValidation.RunAsync(new ListImportsValidator(), new ListImportsQuery());

        problems.ShouldBeEmpty();
    }

    [Fact]
    public async Task A_page_and_a_size_out_of_range_should_be_reported()
    {
        var problems = await TestValidation.RunAsync(
            new ListImportsValidator(), new ListImportsQuery(Page: 0, PageSize: 101));

        problems.ShouldBe(["Page:Validation.OutOfRange", "PageSize:Validation.OutOfRange"], ignoreOrder: true);
    }

    [Fact]
    public async Task The_limits_themselves_should_be_accepted()
    {
        var first = await TestValidation.RunAsync(new ListImportsValidator(), new ListImportsQuery(Page: 1, PageSize: 1));
        var last = await TestValidation.RunAsync(
            new ListImportsValidator(), new ListImportsQuery(Page: PageLimits.MaxPage, PageSize: PageLimits.MaxPageSize));

        first.ShouldBeEmpty();
        last.ShouldBeEmpty();
    }
}
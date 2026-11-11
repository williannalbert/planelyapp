using NSubstitute;
using PlanelyApp.Application.Abstractions.Paging;
using PlanelyApp.Application.Abstractions.Results;
using PlanelyApp.Application.Catalog.BranchOfferings;
using PlanelyApp.Application.Catalog.BranchOfferings.List;
using PlanelyApp.Domain.Catalog.ServiceOfferings;
using Shouldly;

namespace PlanelyApp.Application.Tests.Catalog;

public class ListBranchOfferingsTests
{
    [Fact]
    public async Task What_the_read_model_lists_should_come_back_as_it_is()
    {
        var branchId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();
        var page = new PagedResult<BranchOfferingDto>(
            [new(Guid.NewGuid(), "Haircut", categoryId, "Hair", 30, PricingType.Fixed, 50m, 40m, 40m, true)], 2, 20, 21);
        var readModel = Substitute.For<IBranchOfferingReadModel>();
        readModel.ListAsync(branchId, null, categoryId, true, 2, 20, Arg.Any<CancellationToken>()).Returns(page);

        var result = await new ListBranchOfferingsHandler(readModel).HandleAsync(
            new ListBranchOfferingsQuery(branchId, CategoryId: categoryId, IsAvailable: true, Page: 2),
            TestContext.Current.CancellationToken);

        result.Value.ShouldBe(page);
    }

    [Fact]
    public async Task The_search_should_reach_the_read_model_trimmed()
    {
        var branchId = Guid.NewGuid();
        var readModel = Substitute.For<IBranchOfferingReadModel>();

        await new ListBranchOfferingsHandler(readModel).HandleAsync(
            new ListBranchOfferingsQuery(branchId, "  cut  ", null, null, 3, 50),
            TestContext.Current.CancellationToken);

        await readModel.Received(1).ListAsync(branchId, "cut", null, null, 3, 50, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task A_branch_the_company_does_not_have_should_be_not_found()
    {
        var readModel = Substitute.For<IBranchOfferingReadModel>();
        readModel.ListAsync(
                Arg.Any<Guid>(), Arg.Any<string?>(), Arg.Any<Guid?>(), Arg.Any<bool?>(), Arg.Any<int>(), Arg.Any<int>(),
                Arg.Any<CancellationToken>())
            .Returns((PagedResult<BranchOfferingDto>?)null);

        var result = await new ListBranchOfferingsHandler(readModel).HandleAsync(
            new ListBranchOfferingsQuery(Guid.NewGuid()), TestContext.Current.CancellationToken);

        result.Error.Type.ShouldBe(ErrorType.NotFound);
        result.Error.Code.ShouldBe("Branch.NotFound");
    }

    [Fact]
    public async Task A_default_query_should_pass_the_validator()
    {
        var problems = await TestValidation.RunAsync(
            new ListBranchOfferingsValidator(), new ListBranchOfferingsQuery(Guid.NewGuid()));

        problems.ShouldBeEmpty();
    }

    [Fact]
    public async Task Values_out_of_range_should_be_reported()
    {
        var problems = await TestValidation.RunAsync(
            new ListBranchOfferingsValidator(),
            new ListBranchOfferingsQuery(Guid.Empty, CategoryId: Guid.Empty, Page: 0));

        problems.ShouldBe(
            ["BranchId:Validation.Required", "CategoryId:Validation.Invalid", "Page:Validation.OutOfRange"],
            ignoreOrder: true);
    }
}
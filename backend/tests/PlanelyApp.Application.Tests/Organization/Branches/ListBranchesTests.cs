using NSubstitute;
using PlanelyApp.Application.Organization.Branches;
using PlanelyApp.Application.Organization.Branches.List;
using Shouldly;

namespace PlanelyApp.Application.Tests.Organization.Branches;

public class ListBranchesTests
{
    [Fact]
    public async Task What_the_read_model_lists_should_come_back_as_it_is()
    {
        IReadOnlyList<BranchSummaryDto> summaries =
            [new(Guid.NewGuid(), "Centro", "São Paulo", "SP", "+551133334444", true)];
        var readModel = Substitute.For<IBranchReadModel>();
        readModel.ListAsync(null, Arg.Any<CancellationToken>()).Returns(summaries);

        var result = await new ListBranchesHandler(readModel)
            .HandleAsync(new ListBranchesQuery(), TestContext.Current.CancellationToken);

        result.Value.ShouldBe(summaries, ignoreOrder: false);
    }

    [Fact]
    public async Task The_active_filter_should_reach_the_read_model()
    {
        var readModel = Substitute.For<IBranchReadModel>();

        await new ListBranchesHandler(readModel)
            .HandleAsync(new ListBranchesQuery(IsActive: true), TestContext.Current.CancellationToken);

        await readModel.Received().ListAsync(true, Arg.Any<CancellationToken>());
    }
}
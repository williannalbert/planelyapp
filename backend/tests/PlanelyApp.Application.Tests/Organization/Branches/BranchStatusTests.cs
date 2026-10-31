using PlanelyApp.Application.Abstractions.Results;
using PlanelyApp.Application.Organization.Branches.Activate;
using PlanelyApp.Application.Organization.Branches.Deactivate;
using Shouldly;

namespace PlanelyApp.Application.Tests.Organization.Branches;

public class BranchStatusTests
{
    [Fact]
    public async Task Deactivating_should_turn_the_branch_off()
    {
        var branch = BranchData.NewBranch();
        var handler = new DeactivateBranchHandler(BranchData.RepositoryWith(branch));

        var result = await handler.HandleAsync(
            new DeactivateBranchCommand(branch.Id), TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        branch.IsActive.ShouldBeFalse();
    }

    [Fact]
    public async Task Activating_should_turn_the_branch_back_on()
    {
        var branch = BranchData.NewBranch();
        branch.Deactivate();
        var handler = new ActivateBranchHandler(BranchData.RepositoryWith(branch));

        var result = await handler.HandleAsync(
            new ActivateBranchCommand(branch.Id), TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        branch.IsActive.ShouldBeTrue();
    }

    [Fact]
    public async Task Deactivating_a_missing_branch_should_be_not_found()
    {
        var handler = new DeactivateBranchHandler(BranchData.RepositoryWith(null));

        var result = await handler.HandleAsync(
            new DeactivateBranchCommand(Guid.NewGuid()), TestContext.Current.CancellationToken);

        result.Error.Type.ShouldBe(ErrorType.NotFound);
        result.Error.Code.ShouldBe("Branch.NotFound");
    }

    [Fact]
    public async Task Activating_a_missing_branch_should_be_not_found()
    {
        var handler = new ActivateBranchHandler(BranchData.RepositoryWith(null));

        var result = await handler.HandleAsync(
            new ActivateBranchCommand(Guid.NewGuid()), TestContext.Current.CancellationToken);

        result.Error.Type.ShouldBe(ErrorType.NotFound);
    }
}
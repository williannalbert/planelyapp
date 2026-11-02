using NSubstitute;
using PlanelyApp.Application.Abstractions.Results;
using PlanelyApp.Application.Organization.Users.SetBranchAccess;
using PlanelyApp.Domain.Organization.Branches;
using Shouldly;
using BranchData = PlanelyApp.Application.Tests.Organization.Branches.BranchData;

namespace PlanelyApp.Application.Tests.Organization.Users;

public class SetUserBranchAccessTests
{
    [Fact]
    public async Task Only_the_given_branches_should_be_reachable()
    {
        var user = UserData.NewUser();
        var branchA = Guid.NewGuid();
        var branchB = Guid.NewGuid();
        var handler = new SetUserBranchAccessHandler(
            UserData.RepositoryWith(user),
            BranchData.RepositoryWith(BranchData.NewBranch(user.CompanyId)),
            BranchData.TenantOf(user.CompanyId!.Value));

        var result = await handler.HandleAsync(
            new SetUserBranchAccessCommand(user.Id, false, [branchA, branchB, branchA]),
            TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        user.AccessAllBranches.ShouldBeFalse();
        user.AllowedBranchIds.ShouldBe([branchA, branchB], ignoreOrder: false);
        user.CanAccess(branchA).ShouldBeTrue();
        user.CanAccess(Guid.NewGuid()).ShouldBeFalse();
    }

    [Fact]
    public async Task Every_branch_should_clear_the_list_and_look_up_no_branch()
    {
        var user = UserData.NewUser();
        user.SetBranchAccess(false, [Guid.NewGuid()]);
        var branches = Substitute.For<IBranchRepository>();
        var handler = new SetUserBranchAccessHandler(
            UserData.RepositoryWith(user), branches, BranchData.TenantOf(user.CompanyId!.Value));

        var result = await handler.HandleAsync(
            new SetUserBranchAccessCommand(user.Id, true), TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        user.AccessAllBranches.ShouldBeTrue();
        user.AllowedBranchIds.ShouldBeEmpty();
        await branches.DidNotReceive().GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task A_branch_that_is_not_in_the_company_should_be_not_found_and_change_nothing()
    {
        var user = UserData.NewUser();
        var kept = Guid.NewGuid();
        user.SetBranchAccess(false, [kept]);
        var handler = new SetUserBranchAccessHandler(
            UserData.RepositoryWith(user), BranchData.RepositoryWith(null), BranchData.TenantOf(user.CompanyId!.Value));

        var result = await handler.HandleAsync(
            new SetUserBranchAccessCommand(user.Id, false, [Guid.NewGuid()]), TestContext.Current.CancellationToken);

        result.Error.Type.ShouldBe(ErrorType.NotFound);
        result.Error.Code.ShouldBe("Branch.NotFound");
        user.AllowedBranchIds.ShouldBe([kept], ignoreOrder: false);
    }

    [Fact]
    public async Task An_empty_list_should_mean_no_branch_at_all()
    {
        var user = UserData.NewUser();
        user.SetBranchAccess(true);
        var handler = new SetUserBranchAccessHandler(
            UserData.RepositoryWith(user), BranchData.RepositoryWith(null), BranchData.TenantOf(user.CompanyId!.Value));

        var result = await handler.HandleAsync(
            new SetUserBranchAccessCommand(user.Id, false, []), TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        user.AccessAllBranches.ShouldBeFalse();
        user.CanAccess(Guid.NewGuid()).ShouldBeFalse();
    }

    [Fact]
    public async Task A_missing_user_should_be_not_found()
    {
        var handler = new SetUserBranchAccessHandler(
            UserData.RepositoryWith(null), BranchData.RepositoryWith(null), BranchData.TenantOf(Guid.NewGuid()));

        var result = await handler.HandleAsync(
            new SetUserBranchAccessCommand(Guid.NewGuid(), true), TestContext.Current.CancellationToken);

        result.Error.Code.ShouldBe("User.NotFound");
    }
}
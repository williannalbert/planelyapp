using NSubstitute;
using PlanelyApp.Application.Abstractions.Results;
using PlanelyApp.Application.Organization.Users.Activate;
using PlanelyApp.Application.Organization.Users.Deactivate;
using PlanelyApp.Domain.Organization.Users;
using Shouldly;
using BranchData = PlanelyApp.Application.Tests.Organization.Branches.BranchData;

namespace PlanelyApp.Application.Tests.Organization.Users;

public class UserStatusTests
{
    private readonly Abstractions.Identity.IIdentityProvider _identity = UserData.IdentityProvider();

    private DeactivateUserHandler Deactivate(IUserRepository users, Guid companyId) =>
        new(users, _identity, BranchData.TenantOf(companyId));

    private ActivateUserHandler Activate(IUserRepository users, Guid companyId) =>
        new(users, _identity, BranchData.TenantOf(companyId));

    [Fact]
    public async Task Deactivating_should_block_the_user_and_disable_the_account()
    {
        var user = UserData.NewUser();
        var users = UserData.RepositoryWith(user);

        var result = await Deactivate(users, user.CompanyId!.Value).HandleAsync(
            new DeactivateUserCommand(user.Id), TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        user.IsActive.ShouldBeFalse();
        await _identity.Received(1).SetEnabledAsync(user.IdentityId, false, Arg.Any<CancellationToken>());
        await users.DidNotReceive().CountActiveAdministratorsAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task The_last_administrator_should_not_be_deactivated()
    {
        var user = UserData.NewUser(role: UserRole.CompanyAdmin);
        var users = UserData.RepositoryWith(user);
        users.CountActiveAdministratorsAsync(user.CompanyId!.Value, Arg.Any<CancellationToken>()).Returns(1);

        var result = await Deactivate(users, user.CompanyId!.Value).HandleAsync(
            new DeactivateUserCommand(user.Id), TestContext.Current.CancellationToken);

        result.Error.Type.ShouldBe(ErrorType.Conflict);
        result.Error.Code.ShouldBe("User.LastAdministrator");
        user.IsActive.ShouldBeTrue();
        await _identity.DidNotReceive().SetEnabledAsync(
            Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task An_administrator_should_be_deactivated_when_there_is_another_one()
    {
        var user = UserData.NewUser(role: UserRole.CompanyAdmin);
        var users = UserData.RepositoryWith(user);
        users.CountActiveAdministratorsAsync(user.CompanyId!.Value, Arg.Any<CancellationToken>()).Returns(2);

        var result = await Deactivate(users, user.CompanyId!.Value).HandleAsync(
            new DeactivateUserCommand(user.Id), TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        user.IsActive.ShouldBeFalse();
    }

    [Fact]
    public async Task Activating_should_allow_the_user_again_and_enable_the_account()
    {
        var user = UserData.NewUser();
        user.Deactivate();
        var users = UserData.RepositoryWith(user);

        var result = await Activate(users, user.CompanyId!.Value).HandleAsync(
            new ActivateUserCommand(user.Id), TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        user.IsActive.ShouldBeTrue();
        await _identity.Received(1).SetEnabledAsync(user.IdentityId, true, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Activating_a_user_that_is_already_active_should_still_enable_the_account()
    {
        var user = UserData.NewUser();
        var users = UserData.RepositoryWith(user);

        var result = await Activate(users, user.CompanyId!.Value).HandleAsync(
            new ActivateUserCommand(user.Id), TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        await _identity.Received(1).SetEnabledAsync(user.IdentityId, true, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Deactivating_a_missing_user_should_be_not_found()
    {
        var result = await Deactivate(UserData.RepositoryWith(null), Guid.NewGuid()).HandleAsync(
            new DeactivateUserCommand(Guid.NewGuid()), TestContext.Current.CancellationToken);

        result.Error.Type.ShouldBe(ErrorType.NotFound);
        result.Error.Code.ShouldBe("User.NotFound");
    }

    [Fact]
    public async Task Activating_a_missing_user_should_be_not_found()
    {
        var result = await Activate(UserData.RepositoryWith(null), Guid.NewGuid()).HandleAsync(
            new ActivateUserCommand(Guid.NewGuid()), TestContext.Current.CancellationToken);

        result.Error.Code.ShouldBe("User.NotFound");
    }
}
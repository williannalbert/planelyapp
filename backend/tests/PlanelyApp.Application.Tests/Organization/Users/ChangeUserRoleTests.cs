using NSubstitute;
using PlanelyApp.Application.Abstractions.Results;
using PlanelyApp.Application.Organization.Users.ChangeRole;
using PlanelyApp.Domain.Organization.Users;
using PlanelyApp.Domain.SeedWork;
using Shouldly;
using BranchData = PlanelyApp.Application.Tests.Organization.Branches.BranchData;

namespace PlanelyApp.Application.Tests.Organization.Users;

public class ChangeUserRoleTests
{
    private static ChangeUserRoleHandler Handler(IUserRepository users, User? user) =>
        new(users, BranchData.TenantOf(user?.CompanyId ?? Guid.NewGuid()));

    [Fact]
    public async Task A_role_should_be_changed_without_counting_administrators()
    {
        var user = UserData.NewUser();
        var users = UserData.RepositoryWith(user);

        var result = await Handler(users, user).HandleAsync(
            new ChangeUserRoleCommand(user.Id, UserRole.Professional), TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        user.Role.ShouldBe(UserRole.Professional);
        await users.DidNotReceive().CountActiveAdministratorsAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task The_last_administrator_should_not_be_demoted()
    {
        var user = UserData.NewUser(role: UserRole.CompanyAdmin);
        var users = UserData.RepositoryWith(user);
        users.CountActiveAdministratorsAsync(user.CompanyId!.Value, Arg.Any<CancellationToken>()).Returns(1);

        var result = await Handler(users, user).HandleAsync(
            new ChangeUserRoleCommand(user.Id, UserRole.Receptionist), TestContext.Current.CancellationToken);

        result.Error.Type.ShouldBe(ErrorType.Conflict);
        result.Error.Code.ShouldBe("User.LastAdministrator");
    }

    [Fact]
    public async Task An_administrator_should_be_demoted_when_there_is_another_one()
    {
        var user = UserData.NewUser(role: UserRole.CompanyAdmin);
        var users = UserData.RepositoryWith(user);
        users.CountActiveAdministratorsAsync(user.CompanyId!.Value, Arg.Any<CancellationToken>()).Returns(2);

        var result = await Handler(users, user).HandleAsync(
            new ChangeUserRoleCommand(user.Id, UserRole.Receptionist), TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        user.Role.ShouldBe(UserRole.Receptionist);
    }

    [Fact]
    public async Task Promoting_to_administrator_should_not_count_anyone()
    {
        var user = UserData.NewUser();
        var users = UserData.RepositoryWith(user);

        var result = await Handler(users, user).HandleAsync(
            new ChangeUserRoleCommand(user.Id, UserRole.CompanyAdmin), TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        user.Role.ShouldBe(UserRole.CompanyAdmin);
        await users.DidNotReceive().CountActiveAdministratorsAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task The_platform_administrator_role_should_be_refused_by_the_domain()
    {
        var user = UserData.NewUser();
        var users = UserData.RepositoryWith(user);

        var exception = await Should.ThrowAsync<DomainException>(() => Handler(users, user).HandleAsync(
            new ChangeUserRoleCommand(user.Id, UserRole.PlatformAdmin), TestContext.Current.CancellationToken));

        exception.Code.ShouldBe("User.RoleInvalid");
        user.Role.ShouldBe(UserRole.Receptionist);
    }

    [Fact]
    public async Task A_missing_user_should_be_not_found()
    {
        var result = await Handler(UserData.RepositoryWith(null), null).HandleAsync(
            new ChangeUserRoleCommand(Guid.NewGuid(), UserRole.Professional), TestContext.Current.CancellationToken);

        result.Error.Type.ShouldBe(ErrorType.NotFound);
        result.Error.Code.ShouldBe("User.NotFound");
    }
}
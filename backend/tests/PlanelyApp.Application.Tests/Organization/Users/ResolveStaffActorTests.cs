using NSubstitute;
using PlanelyApp.Application.Abstractions.Results;
using PlanelyApp.Application.Organization.Users.ResolveActor;
using PlanelyApp.Domain.Organization.Users;
using PlanelyApp.Domain.SharedKernel;
using Shouldly;

namespace PlanelyApp.Application.Tests.Organization.Users;

public class ResolveStaffActorTests
{
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    private static IUserRepository RepositoryWith(User? user)
    {
        var users = Substitute.For<IUserRepository>();
        users.GetByIdentityIdAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(user);

        return users;
    }

    [Theory]
    [InlineData(UserRole.CompanyAdmin)]
    [InlineData(UserRole.Receptionist)]
    [InlineData(UserRole.Professional)]
    public async Task A_user_of_a_company_should_come_back_with_its_company_and_role(UserRole role)
    {
        var user = UserData.NewUser(role: role);
        var handler = new ResolveStaffActorHandler(RepositoryWith(user));

        var result = await handler.HandleAsync(new ResolveStaffActorQuery(user.IdentityId), _ct);

        result.Value.UserId.ShouldBe(user.Id);
        result.Value.CompanyId.ShouldBe(user.CompanyId);
        result.Value.Role.ShouldBe(role);
    }

    [Fact]
    public async Task The_platform_administrator_should_come_back_without_a_company()
    {
        var admin = User.CreatePlatformAdmin("kc-root", "Root", Email.Create(UserData.ValidEmail));
        var handler = new ResolveStaffActorHandler(RepositoryWith(admin));

        var result = await handler.HandleAsync(new ResolveStaffActorQuery("kc-root"), _ct);

        result.Value.UserId.ShouldBe(admin.Id);
        result.Value.CompanyId.ShouldBeNull();
        result.Value.Role.ShouldBe(UserRole.PlatformAdmin);
    }

    [Fact]
    public async Task A_user_with_access_to_every_branch_should_come_back_with_no_list()
    {
        var user = UserData.NewUser();
        user.SetBranchAccess(true);
        var handler = new ResolveStaffActorHandler(RepositoryWith(user));

        var result = await handler.HandleAsync(new ResolveStaffActorQuery(user.IdentityId), _ct);

        result.Value.BranchIds.ShouldBeNull();
    }

    [Fact]
    public async Task A_user_with_some_branches_should_come_back_with_exactly_those()
    {
        var a = Guid.CreateVersion7();
        var b = Guid.CreateVersion7();
        var user = UserData.NewUser();
        user.SetBranchAccess(false, [a, b]);
        var handler = new ResolveStaffActorHandler(RepositoryWith(user));

        var result = await handler.HandleAsync(new ResolveStaffActorQuery(user.IdentityId), _ct);

        result.Value.BranchIds.ShouldNotBeNull().ShouldBe([a, b], ignoreOrder: true);
    }

    [Fact]
    public async Task A_user_with_no_branch_at_all_should_come_back_with_an_empty_list_and_not_with_every_branch()
    {
        var user = UserData.NewUser();
        var handler = new ResolveStaffActorHandler(RepositoryWith(user));

        var result = await handler.HandleAsync(new ResolveStaffActorQuery(user.IdentityId), _ct);

        result.Value.BranchIds.ShouldNotBeNull().ShouldBeEmpty();
    }

    [Fact]
    public async Task The_platform_administrator_should_have_no_branch_because_it_has_no_company()
    {
        var admin = User.CreatePlatformAdmin("kc-root", "Root", Email.Create(UserData.ValidEmail));
        var handler = new ResolveStaffActorHandler(RepositoryWith(admin));

        var result = await handler.HandleAsync(new ResolveStaffActorQuery("kc-root"), _ct);

        result.Value.BranchIds.ShouldNotBeNull().ShouldBeEmpty();
    }

    [Fact]
    public async Task The_repository_should_be_asked_for_the_identity_of_the_token_and_nothing_else()
    {
        var users = RepositoryWith(UserData.NewUser());
        var handler = new ResolveStaffActorHandler(users);

        await handler.HandleAsync(new ResolveStaffActorQuery("kc-subject-1"), _ct);

        await users.Received(1).GetByIdentityIdAsync("kc-subject-1", Arg.Any<CancellationToken>());
        await users.DidNotReceiveWithAnyArgs().GetByIdInCompanyAsync(Guid.Empty, Guid.Empty, _ct);
    }

    [Fact]
    public async Task An_identity_with_no_user_should_be_forbidden()
    {
        var handler = new ResolveStaffActorHandler(RepositoryWith(null));

        var result = await handler.HandleAsync(new ResolveStaffActorQuery("kc-unknown"), _ct);

        result.Error.Type.ShouldBe(ErrorType.Forbidden);
        result.Error.Code.ShouldBe("User.NotRegistered");
    }

    [Fact]
    public async Task An_inactive_user_should_be_forbidden()
    {
        var user = UserData.NewUser();
        user.Deactivate();
        var handler = new ResolveStaffActorHandler(RepositoryWith(user));

        var result = await handler.HandleAsync(new ResolveStaffActorQuery(user.IdentityId), _ct);

        result.Error.Type.ShouldBe(ErrorType.Forbidden);
        result.Error.Code.ShouldBe("User.Inactive");
    }
}

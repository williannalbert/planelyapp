using PlanelyApp.Application.Abstractions.Results;
using PlanelyApp.Application.Organization.Users.Get;
using PlanelyApp.Domain.Organization.Users;
using Shouldly;
using BranchData = PlanelyApp.Application.Tests.Organization.Branches.BranchData;

namespace PlanelyApp.Application.Tests.Organization.Users;

public class GetUserTests
{
    [Fact]
    public async Task The_user_should_come_back_with_its_branches_in_order()
    {
        var user = UserData.NewUser(role: UserRole.Professional);
        var a = Guid.CreateVersion7();
        var b = Guid.CreateVersion7();
        user.SetBranchAccess(false, [b, a]);
        var handler = new GetUserHandler(UserData.RepositoryWith(user), BranchData.TenantOf(user.CompanyId!.Value));

        var result = await handler.HandleAsync(new GetUserQuery(user.Id), TestContext.Current.CancellationToken);

        var dto = result.Value;
        dto.Id.ShouldBe(user.Id);
        dto.Name.ShouldBe("Bia Lopes");
        dto.Email.ShouldBe(UserData.ValidEmail);
        dto.Role.ShouldBe(UserRole.Professional);
        dto.AccessAllBranches.ShouldBeFalse();
        dto.BranchIds.ShouldBe(new[] { a, b }.Order().ToList(), ignoreOrder: false);
        dto.PreferredLanguage.ShouldBeNull();
        dto.IsActive.ShouldBeTrue();
    }

    [Fact]
    public async Task An_inactive_user_of_every_branch_should_come_back_without_a_list()
    {
        var user = UserData.NewUser();
        user.SetBranchAccess(true);
        user.Deactivate();
        var handler = new GetUserHandler(UserData.RepositoryWith(user), BranchData.TenantOf(user.CompanyId!.Value));

        var result = await handler.HandleAsync(new GetUserQuery(user.Id), TestContext.Current.CancellationToken);

        result.Value.AccessAllBranches.ShouldBeTrue();
        result.Value.BranchIds.ShouldBeEmpty();
        result.Value.IsActive.ShouldBeFalse();
    }

    [Fact]
    public async Task A_missing_user_should_be_not_found()
    {
        var handler = new GetUserHandler(UserData.RepositoryWith(null), BranchData.TenantOf(Guid.NewGuid()));

        var result = await handler.HandleAsync(new GetUserQuery(Guid.NewGuid()), TestContext.Current.CancellationToken);

        result.Error.Type.ShouldBe(ErrorType.NotFound);
        result.Error.Code.ShouldBe("User.NotFound");
    }
}
using NSubstitute;
using PlanelyApp.Application.Abstractions.Identity;
using PlanelyApp.Application.Abstractions.Results;
using PlanelyApp.Application.Organization.Branches;
using PlanelyApp.Application.Organization.Branches.List;
using PlanelyApp.Application.Tests.Organization.Users;
using PlanelyApp.Domain.Organization.Users;
using Shouldly;

namespace PlanelyApp.Application.Tests.Organization.Branches;

/// <summary>What the staff sees of the branches is limited by the list of the user (RN11); a customer, who books in any branch, sees them all.</summary>
public class ListBranchesTests
{
    private static readonly Guid CompanyId = Guid.NewGuid();

    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;
    private readonly IBranchReadModel _readModel = Substitute.For<IBranchReadModel>();
    private readonly ICurrentUser _currentUser = Substitute.For<ICurrentUser>();
    private readonly BranchSummaryDto _centro = Summary("Centro");
    private readonly BranchSummaryDto _sul = Summary("Zona Sul");

    public ListBranchesTests()
    {
        _currentUser.Id.Returns(Guid.NewGuid());
        _currentUser.Kind.Returns(ActorKind.Staff);
        IReadOnlyList<BranchSummaryDto> all = [_centro, _sul];
        _readModel.ListAsync(Arg.Any<bool?>(), Arg.Any<CancellationToken>()).Returns(all);
    }

    private static BranchSummaryDto Summary(string name) =>
        new(Guid.NewGuid(), name, "São Paulo", "SP", "+551133334444", true);

    private static User UserOf(Action<User>? configure = null)
    {
        var user = UserData.NewUser(CompanyId);
        configure?.Invoke(user);

        return user;
    }

    private ListBranchesHandler Handler(User? user) =>
        new(_readModel, _currentUser, BranchData.TenantOf(CompanyId), UserData.RepositoryWith(user));

    private Task<Result<IReadOnlyList<BranchSummaryDto>>> ListAsync(User? user, bool? isActive = null) =>
        Handler(user).HandleAsync(new ListBranchesQuery(isActive), _ct);

    [Fact]
    public async Task A_user_with_access_to_every_branch_should_see_what_the_read_model_lists_in_its_order()
    {
        var result = await ListAsync(UserOf(user => user.SetBranchAccess(true)));

        result.Value.ShouldBe([_centro, _sul], ignoreOrder: false);
    }

    [Fact]
    public async Task A_user_with_a_list_should_see_only_the_branches_of_the_list()
    {
        var result = await ListAsync(UserOf(user => user.SetBranchAccess(false, [_sul.Id])));

        result.Value.ShouldBe([_sul]);
    }

    [Fact]
    public async Task A_user_with_no_branch_should_see_none()
    {
        var result = await ListAsync(UserOf());

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBeEmpty();
    }

    [Fact]
    public async Task A_deactivated_user_should_see_none()
    {
        var result = await ListAsync(UserOf(user =>
        {
            user.SetBranchAccess(true);
            user.Deactivate();
        }));

        result.Value.ShouldBeEmpty();
    }

    [Fact]
    public async Task A_user_who_is_not_found_in_the_company_should_see_none()
    {
        var result = await ListAsync(user: null);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBeEmpty();
    }

    [Fact]
    public async Task The_user_should_be_looked_up_in_the_company_of_the_request()
    {
        var users = Substitute.For<IUserRepository>();
        var handler = new ListBranchesHandler(_readModel, _currentUser, BranchData.TenantOf(CompanyId), users);
        var userId = _currentUser.Id;

        await handler.HandleAsync(new ListBranchesQuery(), _ct);

        await users.Received().GetByIdInCompanyAsync(CompanyId, userId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task A_customer_should_see_every_branch_without_a_user_being_looked_up()
    {
        _currentUser.Kind.Returns(ActorKind.Customer);
        var users = Substitute.For<IUserRepository>();
        var handler = new ListBranchesHandler(_readModel, _currentUser, BranchData.TenantOf(CompanyId), users);

        var result = await handler.HandleAsync(new ListBranchesQuery(), _ct);

        result.Value.ShouldBe([_centro, _sul], ignoreOrder: false);
        await users.DidNotReceiveWithAnyArgs().GetByIdInCompanyAsync(Guid.Empty, Guid.Empty, _ct);
    }

    [Fact]
    public async Task The_active_filter_should_reach_the_read_model()
    {
        await ListAsync(UserOf(user => user.SetBranchAccess(true)), isActive: true);

        await _readModel.Received().ListAsync(true, Arg.Any<CancellationToken>());
    }
}

using PlanelyApp.Application.Abstractions.Results;
using Shouldly;
using UserData = PlanelyApp.Application.Tests.Organization.Users.UserData;

namespace PlanelyApp.Application.Tests.Scheduling;

public class BranchScopeResolverTests
{
    private readonly AgendaQueryScenario _scenario = new();

    [Fact]
    public async Task Staff_with_access_to_every_branch_should_have_no_limit()
    {
        var result = await _scenario.Scopes().ResolveForStaffAsync(TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value.BranchIds.ShouldBeNull();
        result.Value.Allows(Guid.NewGuid()).ShouldBeTrue();
    }

    [Fact]
    public async Task Staff_with_access_to_some_branches_should_be_limited_to_them()
    {
        var allowed = Guid.NewGuid();
        _scenario.Staff.SetBranchAccess(false, [allowed]);

        var result = await _scenario.Scopes().ResolveForStaffAsync(TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value.BranchIds.ShouldNotBeNull();
        result.Value.BranchIds.ShouldBe([allowed], ignoreOrder: true);
        result.Value.Allows(allowed).ShouldBeTrue();
        result.Value.Allows(Guid.NewGuid()).ShouldBeFalse();
    }

    [Fact]
    public async Task An_inactive_user_should_be_forbidden()
    {
        _scenario.Staff.Deactivate();

        var result = await _scenario.Scopes().ResolveForStaffAsync(TestContext.Current.CancellationToken);

        result.Error.Type.ShouldBe(ErrorType.Forbidden);
        result.Error.Code.ShouldBe("Branch.AccessDenied");
    }

    [Fact]
    public async Task A_user_who_is_not_of_the_company_should_be_forbidden()
    {
        _scenario.Users = UserData.RepositoryWith(null);

        var result = await _scenario.Scopes().ResolveForStaffAsync(TestContext.Current.CancellationToken);

        result.Error.Code.ShouldBe("Branch.AccessDenied");
    }

    [Fact]
    public async Task A_customer_should_be_forbidden()
    {
        _scenario.ActAsCustomer();

        var result = await _scenario.Scopes().ResolveForStaffAsync(TestContext.Current.CancellationToken);

        result.Error.Type.ShouldBe(ErrorType.Forbidden);
        result.Error.Code.ShouldBe("Appointment.StaffOnly");
    }
}
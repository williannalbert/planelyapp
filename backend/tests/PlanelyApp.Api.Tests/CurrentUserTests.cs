using PlanelyApp.Api.Authentication;
using PlanelyApp.Application.Organization.Users.ResolveActor;
using PlanelyApp.Domain.Organization.Users;
using Shouldly;
using ActorKind = PlanelyApp.Application.Abstractions.Identity.ActorKind;

namespace PlanelyApp.Api.Tests;

public class CurrentUserTests
{
    [Fact]
    public void Before_the_middleware_fills_it_nobody_should_be_acting()
    {
        var user = new CurrentUser();

        Should.Throw<InvalidOperationException>(() => { _ = user.Id; });
        Should.Throw<InvalidOperationException>(() => { _ = user.Kind; });
        Should.Throw<InvalidOperationException>(() => { _ = user.Role; });
        Should.Throw<InvalidOperationException>(() => { _ = user.CompanyId; });
    }

    [Fact]
    public void After_it_is_filled_it_should_say_who_is_acting_as_staff()
    {
        var id = Guid.CreateVersion7();
        var company = Guid.CreateVersion7();
        var user = new CurrentUser();

        user.Use(new StaffActor(id, company, UserRole.Professional, null));

        user.Id.ShouldBe(id);
        user.Kind.ShouldBe(ActorKind.Staff);
        user.Role.ShouldBe(UserRole.Professional);
        user.CompanyId.ShouldBe(company);
    }

    [Fact]
    public void The_platform_administrator_should_have_a_role_and_no_company()
    {
        var user = new CurrentUser();

        user.Use(new StaffActor(Guid.CreateVersion7(), null, UserRole.PlatformAdmin, []));

        user.Role.ShouldBe(UserRole.PlatformAdmin);
        user.CompanyId.ShouldBeNull();
    }

    [Fact]
    public void It_should_know_whether_the_middleware_has_filled_it()
    {
        var user = new CurrentUser();

        user.IsResolved.ShouldBeFalse();

        user.Use(new StaffActor(Guid.CreateVersion7(), Guid.CreateVersion7(), UserRole.Receptionist, null));

        user.IsResolved.ShouldBeTrue();
    }

    [Fact]
    public void A_user_may_act_only_in_the_company_of_its_own()
    {
        var company = Guid.CreateVersion7();
        var user = new CurrentUser();
        user.Use(new StaffActor(Guid.CreateVersion7(), company, UserRole.CompanyAdmin, null));

        user.MayActInCompany(company).ShouldBeTrue();
        user.MayActInCompany(Guid.CreateVersion7()).ShouldBeFalse();
    }

    [Fact]
    public void The_platform_administrator_may_act_in_any_company()
    {
        var user = new CurrentUser();
        user.Use(new StaffActor(Guid.CreateVersion7(), null, UserRole.PlatformAdmin, []));

        user.MayActInCompany(Guid.CreateVersion7()).ShouldBeTrue();
    }

    [Fact]
    public void The_branches_should_follow_the_list_of_the_user_and_the_platform_administrator_has_none()
    {
        var branch = Guid.CreateVersion7();
        var everything = new CurrentUser();
        everything.Use(new StaffActor(Guid.CreateVersion7(), Guid.CreateVersion7(), UserRole.CompanyAdmin, null));
        var some = new CurrentUser();
        some.Use(new StaffActor(Guid.CreateVersion7(), Guid.CreateVersion7(), UserRole.Receptionist, [branch]));
        var admin = new CurrentUser();
        admin.Use(new StaffActor(Guid.CreateVersion7(), null, UserRole.PlatformAdmin, []));

        everything.CanAccessBranch(Guid.CreateVersion7()).ShouldBeTrue();
        some.CanAccessBranch(branch).ShouldBeTrue();
        some.CanAccessBranch(Guid.CreateVersion7()).ShouldBeFalse();
        admin.CanAccessBranch(branch).ShouldBeFalse();
    }

    [Fact]
    public void Asking_about_a_company_or_a_branch_before_it_is_filled_should_fail_instead_of_allowing()
    {
        var user = new CurrentUser();

        Should.Throw<InvalidOperationException>(() => { _ = user.MayActInCompany(Guid.CreateVersion7()); });
        Should.Throw<InvalidOperationException>(() => { _ = user.CanAccessBranch(Guid.CreateVersion7()); });
    }
}

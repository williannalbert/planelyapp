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

        user.Use(new StaffActor(id, company, UserRole.Professional));

        user.Id.ShouldBe(id);
        user.Kind.ShouldBe(ActorKind.Staff);
        user.Role.ShouldBe(UserRole.Professional);
        user.CompanyId.ShouldBe(company);
    }

    [Fact]
    public void The_platform_administrator_should_have_a_role_and_no_company()
    {
        var user = new CurrentUser();

        user.Use(new StaffActor(Guid.CreateVersion7(), null, UserRole.PlatformAdmin));

        user.Role.ShouldBe(UserRole.PlatformAdmin);
        user.CompanyId.ShouldBeNull();
    }
}

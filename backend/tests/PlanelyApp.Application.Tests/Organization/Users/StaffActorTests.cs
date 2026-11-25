using PlanelyApp.Application.Organization.Users.ResolveActor;
using PlanelyApp.Domain.Organization.Users;
using Shouldly;

namespace PlanelyApp.Application.Tests.Organization.Users;

public class StaffActorTests
{
    private static StaffActor ActorWith(IReadOnlyList<Guid>? branches) =>
        new(Guid.CreateVersion7(), Guid.CreateVersion7(), UserRole.Receptionist, branches);

    [Fact]
    public void No_list_should_mean_every_branch()
    {
        ActorWith(null).CanAccessBranch(Guid.CreateVersion7()).ShouldBeTrue();
    }

    [Fact]
    public void A_branch_in_the_list_should_be_allowed()
    {
        var branch = Guid.CreateVersion7();

        ActorWith([Guid.CreateVersion7(), branch]).CanAccessBranch(branch).ShouldBeTrue();
    }

    [Fact]
    public void A_branch_that_is_not_in_the_list_should_be_refused()
    {
        ActorWith([Guid.CreateVersion7()]).CanAccessBranch(Guid.CreateVersion7()).ShouldBeFalse();
    }

    [Fact]
    public void An_empty_list_should_mean_no_branch_and_not_every_branch()
    {
        ActorWith([]).CanAccessBranch(Guid.CreateVersion7()).ShouldBeFalse();
    }
}

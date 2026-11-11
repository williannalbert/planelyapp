using NSubstitute;
using PlanelyApp.Application.Abstractions.Results;
using PlanelyApp.Application.Organization.Professionals.LinkUser;
using PlanelyApp.Application.Organization.Professionals.UnlinkUser;
using PlanelyApp.Application.Tests.Organization.Users;
using PlanelyApp.Domain.Organization.Professionals;
using PlanelyApp.Domain.Organization.Users;
using Shouldly;
using BranchData = PlanelyApp.Application.Tests.Organization.Branches.BranchData;

namespace PlanelyApp.Application.Tests.Organization.Professionals;

public class ProfessionalUserLinkTests
{
    private readonly Guid _companyId = Guid.NewGuid();

    private LinkProfessionalUserHandler Link(IProfessionalRepository professionals, IUserRepository users) =>
        new(professionals, users, BranchData.TenantOf(_companyId));

    [Fact]
    public async Task A_user_of_the_company_should_be_linked_to_the_professional()
    {
        var professional = ProfessionalData.NewProfessional(_companyId);
        var user = UserData.NewUser(_companyId);
        var users = UserData.RepositoryWith(user);

        var result = await Link(ProfessionalData.RepositoryWith(professional), users).HandleAsync(
            new LinkProfessionalUserCommand(professional.Id, user.Id), TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        professional.UserId.ShouldBe(user.Id);
        await users.Received(1).GetByIdInCompanyAsync(_companyId, user.Id, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Linking_the_same_pair_again_should_be_a_success()
    {
        var professional = ProfessionalData.NewProfessional(_companyId);
        var user = UserData.NewUser(_companyId);
        professional.LinkUser(user.Id);
        var professionals = ProfessionalData.RepositoryWith(professional);
        professionals.FindByUserIdAsync(user.Id, Arg.Any<CancellationToken>()).Returns(professional);

        var result = await Link(professionals, UserData.RepositoryWith(user)).HandleAsync(
            new LinkProfessionalUserCommand(professional.Id, user.Id), TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        professional.UserId.ShouldBe(user.Id);
    }

    [Fact]
    public async Task A_user_already_linked_to_another_professional_should_be_a_conflict()
    {
        var professional = ProfessionalData.NewProfessional(_companyId);
        var user = UserData.NewUser(_companyId);
        var professionals = ProfessionalData.RepositoryWith(professional);
        professionals.FindByUserIdAsync(user.Id, Arg.Any<CancellationToken>())
            .Returns(ProfessionalData.NewProfessional(_companyId, "Bruno Alves"));

        var result = await Link(professionals, UserData.RepositoryWith(user)).HandleAsync(
            new LinkProfessionalUserCommand(professional.Id, user.Id), TestContext.Current.CancellationToken);

        result.Error.Type.ShouldBe(ErrorType.Conflict);
        result.Error.Code.ShouldBe("Professional.UserAlreadyLinked");
        professional.UserId.ShouldBeNull();
    }

    [Fact]
    public async Task Linking_another_user_should_replace_the_link()
    {
        var professional = ProfessionalData.NewProfessional(_companyId);
        var previous = UserData.NewUser(_companyId);
        var next = UserData.NewUser(_companyId);
        professional.LinkUser(previous.Id);

        var result = await Link(ProfessionalData.RepositoryWith(professional), UserData.RepositoryWith(next)).HandleAsync(
            new LinkProfessionalUserCommand(professional.Id, next.Id), TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        professional.UserId.ShouldBe(next.Id);
    }

    [Fact]
    public async Task A_missing_professional_should_be_not_found_without_looking_for_the_user()
    {
        var users = UserData.RepositoryWith(UserData.NewUser(_companyId));

        var result = await Link(ProfessionalData.RepositoryWith(null), users).HandleAsync(
            new LinkProfessionalUserCommand(Guid.NewGuid(), Guid.NewGuid()), TestContext.Current.CancellationToken);

        result.Error.Type.ShouldBe(ErrorType.NotFound);
        result.Error.Code.ShouldBe("Professional.NotFound");
        await users.DidNotReceive().GetByIdInCompanyAsync(
            Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task A_user_that_is_not_in_the_company_should_be_not_found_and_link_nothing()
    {
        var professional = ProfessionalData.NewProfessional(_companyId);

        var result = await Link(ProfessionalData.RepositoryWith(professional), UserData.RepositoryWith(null)).HandleAsync(
            new LinkProfessionalUserCommand(professional.Id, Guid.NewGuid()), TestContext.Current.CancellationToken);

        result.Error.Type.ShouldBe(ErrorType.NotFound);
        result.Error.Code.ShouldBe("User.NotFound");
        professional.UserId.ShouldBeNull();
    }

    [Fact]
    public async Task Unlinking_should_remove_the_link()
    {
        var professional = ProfessionalData.NewProfessional(_companyId);
        professional.LinkUser(Guid.NewGuid());

        var result = await new UnlinkProfessionalUserHandler(ProfessionalData.RepositoryWith(professional)).HandleAsync(
            new UnlinkProfessionalUserCommand(professional.Id), TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        professional.UserId.ShouldBeNull();
    }

    [Fact]
    public async Task Unlinking_a_professional_without_a_link_should_be_a_success()
    {
        var professional = ProfessionalData.NewProfessional(_companyId);

        var result = await new UnlinkProfessionalUserHandler(ProfessionalData.RepositoryWith(professional)).HandleAsync(
            new UnlinkProfessionalUserCommand(professional.Id), TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        professional.UserId.ShouldBeNull();
    }

    [Fact]
    public async Task Unlinking_a_missing_professional_should_be_not_found()
    {
        var result = await new UnlinkProfessionalUserHandler(ProfessionalData.RepositoryWith(null)).HandleAsync(
            new UnlinkProfessionalUserCommand(Guid.NewGuid()), TestContext.Current.CancellationToken);

        result.Error.Code.ShouldBe("Professional.NotFound");
    }
}
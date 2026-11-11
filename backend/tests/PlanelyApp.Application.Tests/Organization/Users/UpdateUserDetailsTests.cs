using NSubstitute;
using PlanelyApp.Application.Abstractions.Identity;
using PlanelyApp.Application.Abstractions.Results;
using PlanelyApp.Application.Organization.Users.UpdateDetails;
using PlanelyApp.Domain.Organization.Users;
using PlanelyApp.Domain.SeedWork;
using PlanelyApp.Domain.SharedKernel;
using Shouldly;
using BranchData = PlanelyApp.Application.Tests.Organization.Branches.BranchData;

namespace PlanelyApp.Application.Tests.Organization.Users;

public class UpdateUserDetailsTests
{
    private readonly IIdentityProvider _identity = UserData.IdentityProvider();
    private readonly User _user;
    private readonly IUserRepository _users;

    public UpdateUserDetailsTests()
    {
        _user = UserData.NewUser();
        _users = UserData.RepositoryWith(_user);
    }

    [Fact]
    public async Task The_details_should_be_replaced_and_sent_to_the_provider()
    {
        var result = await Handler().HandleAsync(
            new UpdateUserDetailsCommand(_user.Id, "  Beatriz Lopes  ", "Beatriz@Example.com", "en"),
            TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        _user.Name.ShouldBe("Beatriz Lopes");
        _user.Email.Value.ShouldBe("beatriz@example.com");
        _user.PreferredLanguage!.Code.ShouldBe("en");
        await _identity.Received(1).UpdateUserAsync(
            _user.IdentityId,
            Arg.Is<IdentityProfile>(profile =>
                profile.Name == "Beatriz Lopes" && profile.Email.Value == "beatriz@example.com"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task An_email_of_another_user_should_be_a_conflict_and_change_nothing()
    {
        _users.FindByEmailAsync(Arg.Any<Guid>(), Arg.Any<Email>(), Arg.Any<CancellationToken>())
            .Returns(UserData.NewUser());

        var result = await Handler().HandleAsync(
            new UpdateUserDetailsCommand(_user.Id, "Bia Lopes", "other@example.com", null),
            TestContext.Current.CancellationToken);

        result.Error.Type.ShouldBe(ErrorType.Conflict);
        result.Error.Code.ShouldBe("User.EmailAlreadyRegistered");
        _user.Email.Value.ShouldBe(UserData.ValidEmail);
        await _identity.DidNotReceive().UpdateUserAsync(
            Arg.Any<string>(), Arg.Any<IdentityProfile>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Keeping_the_own_email_should_not_be_a_conflict()
    {
        _users.FindByEmailAsync(Arg.Any<Guid>(), Arg.Any<Email>(), Arg.Any<CancellationToken>()).Returns(_user);

        var result = await Handler().HandleAsync(
            new UpdateUserDetailsCommand(_user.Id, "Beatriz Lopes", UserData.ValidEmail, null),
            TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        _user.Name.ShouldBe("Beatriz Lopes");
    }

    [Fact]
    public async Task Sending_what_the_user_already_has_should_not_call_the_provider()
    {
        var result = await Handler().HandleAsync(
            new UpdateUserDetailsCommand(_user.Id, "Bia Lopes", UserData.ValidEmail, null),
            TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        await _identity.DidNotReceive().UpdateUserAsync(
            Arg.Any<string>(), Arg.Any<IdentityProfile>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task An_email_the_provider_refuses_should_come_back_as_it_is()
    {
        _identity.UpdateUserAsync(Arg.Any<string>(), Arg.Any<IdentityProfile>(), Arg.Any<CancellationToken>())
            .Returns(Result<Unit>.Failure(Error.Conflict("Identity.EmailAlreadyInUse")));

        var result = await Handler().HandleAsync(
            new UpdateUserDetailsCommand(_user.Id, "Bia Lopes", "taken@example.com", null),
            TestContext.Current.CancellationToken);

        result.Error.Type.ShouldBe(ErrorType.Conflict);
        result.Error.Code.ShouldBe("Identity.EmailAlreadyInUse");
    }

    [Fact]
    public async Task A_name_above_the_limit_should_be_refused_before_the_provider_is_called()
    {
        var exception = await Should.ThrowAsync<DomainException>(() => Handler().HandleAsync(
            new UpdateUserDetailsCommand(_user.Id, new string('x', 101), UserData.ValidEmail, null),
            TestContext.Current.CancellationToken));

        exception.Code.ShouldBe("User.NameInvalid");
        await _identity.DidNotReceive().UpdateUserAsync(
            Arg.Any<string>(), Arg.Any<IdentityProfile>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task A_missing_user_should_be_not_found()
    {
        var handler = new UpdateUserDetailsHandler(
            UserData.RepositoryWith(null), _identity, BranchData.TenantOf(Guid.NewGuid()));

        var result = await handler.HandleAsync(
            new UpdateUserDetailsCommand(Guid.NewGuid(), "Bia", UserData.ValidEmail, null),
            TestContext.Current.CancellationToken);

        result.Error.Type.ShouldBe(ErrorType.NotFound);
        result.Error.Code.ShouldBe("User.NotFound");
    }

    [Fact]
    public async Task A_valid_command_should_pass_the_validator()
    {
        var problems = await TestValidation.RunAsync(
            new UpdateUserDetailsValidator(),
            new UpdateUserDetailsCommand(Guid.NewGuid(), "Bia Lopes", UserData.ValidEmail, "pt-BR"));

        problems.ShouldBeEmpty();
    }

    [Fact]
    public async Task Missing_and_invalid_values_should_be_reported()
    {
        var problems = await TestValidation.RunAsync(
            new UpdateUserDetailsValidator(), new UpdateUserDetailsCommand(Guid.NewGuid(), "", "x", "fr"));

        problems.ShouldBe(
            ["Name:Validation.Required", "Email:Validation.Invalid", "PreferredLanguage:Validation.Invalid"],
            ignoreOrder: true);
    }

    private UpdateUserDetailsHandler Handler() =>
        new(_users, _identity, BranchData.TenantOf(_user.CompanyId!.Value));
}
using NSubstitute;
using PlanelyApp.Application.Abstractions.Identity;
using PlanelyApp.Application.Abstractions.Results;
using PlanelyApp.Application.Organization.Users.Create;
using PlanelyApp.Application.Tests.Organization.Companies;
using PlanelyApp.Domain.Organization.Branches;
using PlanelyApp.Domain.Organization.Users;
using PlanelyApp.Domain.SeedWork;
using PlanelyApp.Domain.SharedKernel;
using Shouldly;
using BranchData = PlanelyApp.Application.Tests.Organization.Branches.BranchData;

namespace PlanelyApp.Application.Tests.Organization.Users;

public class CreateUserTests
{
    private readonly IUserRepository _users = Substitute.For<IUserRepository>();
    private readonly IIdentityProvider _identity = UserData.IdentityProvider("kc-new");
    private readonly Guid _companyId = Guid.NewGuid();

    [Fact]
    public async Task A_user_should_get_an_account_and_be_added_to_the_company()
    {
        var added = CaptureAdded();
        var branchA = Guid.NewGuid();
        var branchB = Guid.NewGuid();

        var result = await Handler().HandleAsync(
            new CreateUserCommand("  Bia Lopes  ", " Bia@Example.com ", UserRole.Receptionist, false, [branchA, branchB, branchA], "es"),
            TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        var user = added()!;
        result.Value.ShouldBe(user.Id);
        user.CompanyId.ShouldBe(_companyId);
        user.IdentityId.ShouldBe("kc-new");
        user.Name.ShouldBe("Bia Lopes");
        user.Email.Value.ShouldBe("bia@example.com");
        user.Role.ShouldBe(UserRole.Receptionist);
        user.IsActive.ShouldBeTrue();
        user.AccessAllBranches.ShouldBeFalse();
        user.AllowedBranchIds.ShouldBe([branchA, branchB], ignoreOrder: false);
        user.PreferredLanguage!.Code.ShouldBe("es");
        await _identity.Received(1).CreateUserAsync(
            Arg.Is<IdentityProfile>(profile =>
                profile.Name == "Bia Lopes" && profile.Email.Value == "bia@example.com" && profile.Language!.Code == "es"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task A_user_of_every_branch_should_not_need_any_branch_to_exist()
    {
        var added = CaptureAdded();
        var branches = Substitute.For<IBranchRepository>();

        var result = await Handler(branches).HandleAsync(
            new CreateUserCommand("Bia Lopes", UserData.ValidEmail, UserRole.CompanyAdmin, true),
            TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        added()!.AccessAllBranches.ShouldBeTrue();
        added()!.AllowedBranchIds.ShouldBeEmpty();
        await branches.DidNotReceive().GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task An_email_already_in_the_company_should_be_a_conflict_without_creating_an_account()
    {
        _users.FindByEmailAsync(_companyId, Arg.Any<Email>(), Arg.Any<CancellationToken>())
            .Returns(UserData.NewUser(_companyId));

        var result = await Handler().HandleAsync(
            new CreateUserCommand("Bia Lopes", UserData.ValidEmail, UserRole.Receptionist, true),
            TestContext.Current.CancellationToken);

        result.Error.Type.ShouldBe(ErrorType.Conflict);
        result.Error.Code.ShouldBe("User.EmailAlreadyRegistered");
        await _identity.DidNotReceive().CreateUserAsync(Arg.Any<IdentityProfile>(), Arg.Any<CancellationToken>());
        _users.DidNotReceive().Add(Arg.Any<User>());
    }

    [Fact]
    public async Task A_branch_that_is_not_in_the_company_should_be_not_found_without_creating_an_account()
    {
        var result = await Handler(BranchData.RepositoryWith(null)).HandleAsync(
            new CreateUserCommand("Bia Lopes", UserData.ValidEmail, UserRole.Receptionist, false, [Guid.NewGuid()]),
            TestContext.Current.CancellationToken);

        result.Error.Type.ShouldBe(ErrorType.NotFound);
        result.Error.Code.ShouldBe("Branch.NotFound");
        await _identity.DidNotReceive().CreateUserAsync(Arg.Any<IdentityProfile>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task An_email_the_provider_refuses_should_come_back_as_it_is_and_add_nothing()
    {
        _identity.CreateUserAsync(Arg.Any<IdentityProfile>(), Arg.Any<CancellationToken>())
            .Returns(Result<string>.Failure(Error.Conflict("Identity.EmailAlreadyInUse")));

        var result = await Handler().HandleAsync(
            new CreateUserCommand("Bia Lopes", UserData.ValidEmail, UserRole.Receptionist, true),
            TestContext.Current.CancellationToken);

        result.Error.Type.ShouldBe(ErrorType.Conflict);
        result.Error.Code.ShouldBe("Identity.EmailAlreadyInUse");
        _users.DidNotReceive().Add(Arg.Any<User>());
        await _identity.DidNotReceive().DeleteUserAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task A_role_the_domain_refuses_should_delete_the_account_that_was_created()
    {
        var exception = await Should.ThrowAsync<DomainException>(() => Handler().HandleAsync(
            new CreateUserCommand("Bia Lopes", UserData.ValidEmail, UserRole.PlatformAdmin, true),
            TestContext.Current.CancellationToken));

        exception.Code.ShouldBe("User.RoleInvalid");
        await _identity.Received(1).DeleteUserAsync("kc-new", Arg.Any<CancellationToken>());
        _users.DidNotReceive().Add(Arg.Any<User>());
    }

    [Fact]
    public async Task A_name_above_the_limit_should_delete_the_account_that_was_created()
    {
        var exception = await Should.ThrowAsync<DomainException>(() => Handler().HandleAsync(
            new CreateUserCommand(new string('x', 101), UserData.ValidEmail, UserRole.Receptionist, true),
            TestContext.Current.CancellationToken));

        exception.Code.ShouldBe("User.NameInvalid");
        await _identity.Received(1).DeleteUserAsync("kc-new", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task A_missing_company_should_be_not_found_without_creating_an_account()
    {
        var result = await Handler(companyExists: false).HandleAsync(
            new CreateUserCommand("Bia Lopes", UserData.ValidEmail, UserRole.Receptionist, true),
            TestContext.Current.CancellationToken);

        result.Error.Code.ShouldBe("Company.NotFound");
        await _identity.DidNotReceive().CreateUserAsync(Arg.Any<IdentityProfile>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task A_request_without_a_company_should_fail_loudly()
    {
        var handler = new CreateUserHandler(
            _users,
            BranchData.RepositoryWith(null),
            CompanyData.RepositoryWith(CompanyData.NewCompany()),
            _identity,
            Substitute.For<ITenantProvider>());

        await Should.ThrowAsync<InvalidOperationException>(() => handler.HandleAsync(
            new CreateUserCommand("Bia Lopes", UserData.ValidEmail, UserRole.Receptionist, true),
            TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task A_valid_command_should_pass_the_validator()
    {
        var problems = await TestValidation.RunAsync(
            new CreateUserValidator(),
            new CreateUserCommand("Bia Lopes", UserData.ValidEmail, UserRole.Receptionist, true, null, "pt-BR"));

        problems.ShouldBeEmpty();
    }

    [Fact]
    public async Task A_name_and_an_email_left_empty_should_be_reported_as_required()
    {
        var problems = await TestValidation.RunAsync(
            new CreateUserValidator(), new CreateUserCommand("", "", UserRole.Receptionist));

        problems.ShouldBe(["Name:Validation.Required", "Email:Validation.Required"], ignoreOrder: true);
    }

    [Fact]
    public async Task Values_the_domain_refuses_should_be_reported_as_invalid()
    {
        var problems = await TestValidation.RunAsync(
            new CreateUserValidator(),
            new CreateUserCommand("Bia Lopes", "nope", UserRole.Receptionist, true, null, "fr"));

        problems.ShouldBe(["Email:Validation.Invalid", "PreferredLanguage:Validation.Invalid"], ignoreOrder: true);
    }

    private CreateUserHandler Handler(IBranchRepository? branches = null, bool companyExists = true) => new(
        _users,
        branches ?? BranchData.RepositoryWith(BranchData.NewBranch(_companyId)),
        CompanyData.RepositoryWith(companyExists ? CompanyData.NewCompany() : null),
        _identity,
        BranchData.TenantOf(_companyId));

    private Func<User?> CaptureAdded()
    {
        User? added = null;
        _users.When(repository => repository.Add(Arg.Any<User>()))
            .Do(call => added = call.Arg<User>());

        return () => added;
    }
}
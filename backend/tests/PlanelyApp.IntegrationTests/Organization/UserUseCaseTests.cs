using Microsoft.Extensions.DependencyInjection;
using PlanelyApp.Application.Organization.Users;
using PlanelyApp.Application.Organization.Users.Activate;
using PlanelyApp.Application.Organization.Users.ChangeRole;
using PlanelyApp.Application.Organization.Users.Create;
using PlanelyApp.Application.Organization.Users.Deactivate;
using PlanelyApp.Application.Organization.Users.Get;
using PlanelyApp.Application.Organization.Users.List;
using PlanelyApp.Application.Organization.Users.SetBranchAccess;
using PlanelyApp.Application.Organization.Users.UpdateDetails;
using PlanelyApp.Domain.Organization.Users;
using PlanelyApp.Domain.SeedWork;
using Shouldly;

namespace PlanelyApp.IntegrationTests.Organization;

/// <summary>The whole way, as a request has it, with the identity provider replaced by one in memory.</summary>
public class UserUseCaseTests(MySqlFixture mysql)
{
    [Fact]
    public async Task A_created_user_should_have_an_account_and_be_saved_with_its_branch_access()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var company = await OrganizationData.SeedCompanyAsync(mysql, cancellationToken);
        var branch = await StaffData.SeedBranchAsync(mysql, company.Id, cancellationToken);
        var email = StaffData.NewEmail();

        var created = await mysql.SendAsync(
            company.Id,
            new CreateUserCommand("Bia Lopes", email, UserRole.Receptionist, false, [branch.Id], "en"),
            cancellationToken);

        created.IsSuccess.ShouldBeTrue();
        var dto = (await mysql.QueryAsync(company.Id, new GetUserQuery(created.Value), cancellationToken)).Value;
        dto.Name.ShouldBe("Bia Lopes");
        dto.Email.ShouldBe(email);
        dto.Role.ShouldBe(UserRole.Receptionist);
        dto.AccessAllBranches.ShouldBeFalse();
        dto.BranchIds.ShouldBe([branch.Id], ignoreOrder: false);
        dto.PreferredLanguage.ShouldBe("en");
        dto.IsActive.ShouldBeTrue();

        var (identityId, account) = mysql.Identity.Accounts.Single(pair => pair.Value.Profile.Email.Value == email);
        account.Enabled.ShouldBeTrue();
        account.Profile.Name.ShouldBe("Bia Lopes");
        await using var scope = mysql.CreateScope(company.Id);
        var stored = await scope.ServiceProvider.GetRequiredService<IUserRepository>()
            .GetByIdentityIdAsync(identityId, cancellationToken);
        stored.ShouldNotBeNull();
        stored.Id.ShouldBe(created.Value);
    }

    [Fact]
    public async Task The_email_should_be_unique_in_the_company_and_in_the_provider()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var company = await OrganizationData.SeedCompanyAsync(mysql, cancellationToken);
        var other = await OrganizationData.SeedCompanyAsync(mysql, cancellationToken);
        var email = StaffData.NewEmail();
        await CreateAsync(company.Id, "Bia Lopes", email, UserRole.Receptionist, cancellationToken);

        var sameCompany = await mysql.SendAsync(
            company.Id, new CreateUserCommand("Other", email.ToUpperInvariant(), UserRole.Receptionist, true), cancellationToken);
        var otherCompany = await mysql.SendAsync(
            other.Id, new CreateUserCommand("Other", email, UserRole.Receptionist, true), cancellationToken);

        sameCompany.Error.Code.ShouldBe("User.EmailAlreadyRegistered");
        otherCompany.Error.Code.ShouldBe("Identity.EmailAlreadyInUse");
        mysql.Identity.Accounts.Values.Count(account => account.Profile.Email.Value == email).ShouldBe(1);
    }

    [Fact]
    public async Task A_creation_the_domain_refuses_should_leave_no_account_behind()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var company = await OrganizationData.SeedCompanyAsync(mysql, cancellationToken);
        var email = StaffData.NewEmail();

        var exception = await Should.ThrowAsync<DomainException>(() => mysql.SendAsync(
            company.Id, new CreateUserCommand("Bia Lopes", email, UserRole.PlatformAdmin, true), cancellationToken));

        exception.Code.ShouldBe("User.RoleInvalid");
        mysql.Identity.Accounts.Values.ShouldNotContain(account => account.Profile.Email.Value == email);
        (await mysql.QueryAsync(company.Id, new ListUsersQuery(), cancellationToken)).Value.ShouldBeEmpty();
    }

    [Fact]
    public async Task New_details_should_be_saved_and_sent_to_the_provider_and_a_refused_email_should_save_nothing()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var company = await OrganizationData.SeedCompanyAsync(mysql, cancellationToken);
        var other = await OrganizationData.SeedCompanyAsync(mysql, cancellationToken);
        var email = StaffData.NewEmail();
        var newEmail = StaffData.NewEmail();
        var takenEmail = StaffData.NewEmail();
        var id = await CreateAsync(company.Id, "Bia Lopes", email, UserRole.Receptionist, cancellationToken);
        await CreateAsync(other.Id, "Carla Dias", takenEmail, UserRole.Receptionist, cancellationToken);

        var updated = await mysql.SendAsync(
            company.Id, new UpdateUserDetailsCommand(id, "Beatriz Lopes", newEmail, "es"), cancellationToken);
        var refused = await mysql.SendAsync(
            company.Id, new UpdateUserDetailsCommand(id, "Beatriz Lopes", takenEmail, "es"), cancellationToken);

        updated.IsSuccess.ShouldBeTrue();
        refused.Error.Code.ShouldBe("Identity.EmailAlreadyInUse");
        var dto = (await mysql.QueryAsync(company.Id, new GetUserQuery(id), cancellationToken)).Value;
        dto.Name.ShouldBe("Beatriz Lopes");
        dto.Email.ShouldBe(newEmail);
        dto.PreferredLanguage.ShouldBe("es");
        mysql.Identity.Accounts.Values.ShouldContain(account => account.Profile.Email.Value == newEmail);
        mysql.Identity.Accounts.Values.ShouldNotContain(account => account.Profile.Email.Value == email);
    }

    [Fact]
    public async Task A_user_of_another_company_should_not_be_found()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var company = await OrganizationData.SeedCompanyAsync(mysql, cancellationToken);
        var other = await OrganizationData.SeedCompanyAsync(mysql, cancellationToken);
        var id = await CreateAsync(company.Id, "Bia Lopes", StaffData.NewEmail(), UserRole.Receptionist, cancellationToken);

        var results = new[]
        {
            (await mysql.QueryAsync(other.Id, new GetUserQuery(id), cancellationToken)).Error.Code,
            (await mysql.SendAsync(other.Id, new UpdateUserDetailsCommand(id, "Bia", StaffData.NewEmail(), null), cancellationToken)).Error.Code,
            (await mysql.SendAsync(other.Id, new ChangeUserRoleCommand(id, UserRole.Professional), cancellationToken)).Error.Code,
            (await mysql.SendAsync(other.Id, new SetUserBranchAccessCommand(id, true), cancellationToken)).Error.Code,
            (await mysql.SendAsync(other.Id, new ActivateUserCommand(id), cancellationToken)).Error.Code,
            (await mysql.SendAsync(other.Id, new DeactivateUserCommand(id), cancellationToken)).Error.Code,
        };

        results.ShouldAllBe(code => code == "User.NotFound");
        (await mysql.QueryAsync(company.Id, new GetUserQuery(id), cancellationToken)).Value.IsActive.ShouldBeTrue();
    }

    [Fact]
    public async Task Branch_access_should_only_accept_branches_of_the_company()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var company = await OrganizationData.SeedCompanyAsync(mysql, cancellationToken);
        var other = await OrganizationData.SeedCompanyAsync(mysql, cancellationToken);
        var branch = await StaffData.SeedBranchAsync(mysql, company.Id, cancellationToken);
        var foreign = await StaffData.SeedBranchAsync(mysql, other.Id, cancellationToken);
        var id = await CreateAsync(company.Id, "Bia Lopes", StaffData.NewEmail(), UserRole.Receptionist, cancellationToken);

        var refused = await mysql.SendAsync(
            company.Id, new SetUserBranchAccessCommand(id, false, [foreign.Id]), cancellationToken);
        var accepted = await mysql.SendAsync(
            company.Id, new SetUserBranchAccessCommand(id, false, [branch.Id]), cancellationToken);

        refused.Error.Code.ShouldBe("Branch.NotFound");
        accepted.IsSuccess.ShouldBeTrue();
        var dto = (await mysql.QueryAsync(company.Id, new GetUserQuery(id), cancellationToken)).Value;
        dto.AccessAllBranches.ShouldBeFalse();
        dto.BranchIds.ShouldBe([branch.Id], ignoreOrder: false);
    }

    [Fact]
    public async Task Deactivating_and_activating_should_be_saved_and_reach_the_provider()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var company = await OrganizationData.SeedCompanyAsync(mysql, cancellationToken);
        var email = StaffData.NewEmail();
        var id = await CreateAsync(company.Id, "Bia Lopes", email, UserRole.Receptionist, cancellationToken);

        (await mysql.SendAsync(company.Id, new DeactivateUserCommand(id), cancellationToken)).IsSuccess.ShouldBeTrue();
        var inactive = (await mysql.QueryAsync(company.Id, new GetUserQuery(id), cancellationToken)).Value;
        var disabled = mysql.Identity.Accounts.Values.Single(account => account.Profile.Email.Value == email).Enabled;

        (await mysql.SendAsync(company.Id, new ActivateUserCommand(id), cancellationToken)).IsSuccess.ShouldBeTrue();
        var active = (await mysql.QueryAsync(company.Id, new GetUserQuery(id), cancellationToken)).Value;
        var enabled = mysql.Identity.Accounts.Values.Single(account => account.Profile.Email.Value == email).Enabled;

        inactive.IsActive.ShouldBeFalse();
        disabled.ShouldBeFalse();
        active.IsActive.ShouldBeTrue();
        enabled.ShouldBeTrue();
    }

    [Fact]
    public async Task The_company_should_always_keep_one_active_administrator()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var company = await OrganizationData.SeedCompanyAsync(mysql, cancellationToken);
        var first = await CreateAsync(company.Id, "Ana Admin", StaffData.NewEmail(), UserRole.CompanyAdmin, cancellationToken);

        var deactivateAlone = await mysql.SendAsync(company.Id, new DeactivateUserCommand(first), cancellationToken);
        var demoteAlone = await mysql.SendAsync(
            company.Id, new ChangeUserRoleCommand(first, UserRole.Receptionist), cancellationToken);

        var second = await CreateAsync(company.Id, "Beto Admin", StaffData.NewEmail(), UserRole.CompanyAdmin, cancellationToken);
        var deactivateFirst = await mysql.SendAsync(company.Id, new DeactivateUserCommand(first), cancellationToken);
        var demoteSecond = await mysql.SendAsync(
            company.Id, new ChangeUserRoleCommand(second, UserRole.Receptionist), cancellationToken);

        deactivateAlone.Error.Code.ShouldBe("User.LastAdministrator");
        demoteAlone.Error.Code.ShouldBe("User.LastAdministrator");
        deactivateFirst.IsSuccess.ShouldBeTrue();
        demoteSecond.Error.Code.ShouldBe("User.LastAdministrator");
        (await mysql.QueryAsync(company.Id, new GetUserQuery(second), cancellationToken)).Value.Role
            .ShouldBe(UserRole.CompanyAdmin);
    }

    [Fact]
    public async Task The_list_should_show_only_the_users_of_the_company_and_filter_by_role_and_status()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var company = await OrganizationData.SeedCompanyAsync(mysql, cancellationToken);
        var other = await OrganizationData.SeedCompanyAsync(mysql, cancellationToken);
        await CreateAsync(company.Id, "Carla Admin", StaffData.NewEmail(), UserRole.CompanyAdmin, cancellationToken);
        await CreateAsync(company.Id, "Ana Recepção", StaffData.NewEmail(), UserRole.Receptionist, cancellationToken);
        var inactive = await CreateAsync(company.Id, "Bruno Recepção", StaffData.NewEmail(), UserRole.Receptionist, cancellationToken);
        await CreateAsync(other.Id, "Daniel Outro", StaffData.NewEmail(), UserRole.CompanyAdmin, cancellationToken);
        await mysql.SendAsync(company.Id, new DeactivateUserCommand(inactive), cancellationToken);

        var all = await ListAsync(company.Id, new ListUsersQuery(), cancellationToken);
        var receptionists = await ListAsync(company.Id, new ListUsersQuery(UserRole.Receptionist), cancellationToken);
        var activeReceptionists = await ListAsync(company.Id, new ListUsersQuery(UserRole.Receptionist, true), cancellationToken);

        all.Select(user => user.Name).ShouldBe(["Ana Recepção", "Bruno Recepção", "Carla Admin"], ignoreOrder: false);
        receptionists.Select(user => user.Name).ShouldBe(["Ana Recepção", "Bruno Recepção"], ignoreOrder: false);
        activeReceptionists.ShouldHaveSingleItem().Name.ShouldBe("Ana Recepção");
        all.Single(user => user.Name == "Carla Admin").AccessAllBranches.ShouldBeTrue();
    }

    private async Task<Guid> CreateAsync(
        Guid companyId,
        string name,
        string email,
        UserRole role,
        CancellationToken cancellationToken)
    {
        var result = await mysql.SendAsync(
            companyId, new CreateUserCommand(name, email, role, true), cancellationToken);

        return result.Value;
    }

    private async Task<IReadOnlyList<UserSummaryDto>> ListAsync(
        Guid companyId,
        ListUsersQuery query,
        CancellationToken cancellationToken) =>
        (await mysql.QueryAsync(companyId, query, cancellationToken)).Value;
}
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PlanelyApp.Domain.Organization.Users;
using PlanelyApp.Domain.SeedWork;
using PlanelyApp.Domain.SharedKernel;
using Shouldly;

namespace PlanelyApp.IntegrationTests.Organization;

public class UserRepositoryTests(MySqlFixture mysql)
{
    [Fact]
    public async Task A_saved_user_should_come_back_with_its_branch_access()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var company = await OrganizationData.SeedCompanyAsync(mysql, cancellationToken);
        var (branchA, branchB) = (Guid.CreateVersion7(), Guid.CreateVersion7());

        var user = StaffData.NewUser(company.Id, role: UserRole.CompanyAdmin);
        user.SetBranchAccess(false, [branchA, branchB]);
        await SaveAsync(user, cancellationToken);

        var loaded = await LoadAsync(user.Id, cancellationToken);

        loaded.ShouldNotBeNull();
        loaded.CompanyId.ShouldBe(company.Id);
        loaded.IdentityId.ShouldBe(user.IdentityId);
        loaded.Name.ShouldBe("Maria Atendente");
        loaded.Email.ShouldBe(user.Email);
        loaded.Role.ShouldBe(UserRole.CompanyAdmin);
        loaded.PreferredLanguage.ShouldBe(Language.English);
        loaded.IsActive.ShouldBeTrue();
        loaded.AccessAllBranches.ShouldBeFalse();
        loaded.AllowedBranchIds.Order().ShouldBe(new[] { branchA, branchB }.Order());
        loaded.CanAccess(branchA).ShouldBeTrue();
        loaded.CanAccess(Guid.CreateVersion7()).ShouldBeFalse();
    }

    [Fact]
    public async Task Changing_the_branch_access_should_replace_the_stored_list()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var company = await OrganizationData.SeedCompanyAsync(mysql, cancellationToken);
        var user = StaffData.NewUser(company.Id);
        user.SetBranchAccess(false, [Guid.CreateVersion7(), Guid.CreateVersion7()]);
        await SaveAsync(user, cancellationToken);

        var onlyBranch = Guid.CreateVersion7();
        await UpdateAsync(user.Id, cancellationToken, loaded => loaded.SetBranchAccess(false, [onlyBranch]));

        var narrowed = await LoadAsync(user.Id, cancellationToken);
        narrowed!.AllowedBranchIds.ShouldHaveSingleItem().ShouldBe(onlyBranch);

        await UpdateAsync(user.Id, cancellationToken, loaded => loaded.SetBranchAccess(true));

        var everything = await LoadAsync(user.Id, cancellationToken);
        everything!.AccessAllBranches.ShouldBeTrue();
        everything.AllowedBranchIds.ShouldBeEmpty();
    }

    [Fact]
    public async Task The_platform_administrator_should_be_saved_without_a_company()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var admin = User.CreatePlatformAdmin($"sub-{Guid.NewGuid():N}", "Root", Email.Create(StaffData.NewEmail()));
        await SaveAsync(admin, cancellationToken);

        var loaded = await LoadAsync(admin.Id, cancellationToken);

        loaded.ShouldNotBeNull();
        loaded.CompanyId.ShouldBeNull();
        loaded.Role.ShouldBe(UserRole.PlatformAdmin);
        loaded.PreferredLanguage.ShouldBeNull();
    }

    [Fact]
    public async Task A_user_should_be_found_by_the_identity_id_of_the_token()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var company = await OrganizationData.SeedCompanyAsync(mysql, cancellationToken);
        var user = StaffData.NewUser(company.Id);
        await SaveAsync(user, cancellationToken);

        await using var scope = mysql.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IUserRepository>();

        (await repository.GetByIdentityIdAsync(user.IdentityId, cancellationToken))!.Id.ShouldBe(user.Id);
        (await repository.GetByIdentityIdAsync("sub-unknown", cancellationToken)).ShouldBeNull();
    }

    [Fact]
    public async Task Two_users_should_not_share_an_identity_id()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var company = await OrganizationData.SeedCompanyAsync(mysql, cancellationToken);
        var identityId = $"sub-{Guid.NewGuid():N}";

        await SaveAsync(User.CreateForCompany(company.Id, identityId, "Um", Email.Create(StaffData.NewEmail()), UserRole.Receptionist), cancellationToken);

        await Should.ThrowAsync<DbUpdateException>(() => SaveAsync(
            User.CreateForCompany(company.Id, identityId, "Dois", Email.Create(StaffData.NewEmail()), UserRole.Receptionist),
            cancellationToken));
    }

    [Fact]
    public async Task An_email_should_be_used_once_per_company_but_may_repeat_across_companies()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var company = await OrganizationData.SeedCompanyAsync(mysql, cancellationToken);
        var other = await OrganizationData.SeedCompanyAsync(mysql, cancellationToken);
        var email = StaffData.NewEmail();

        await SaveAsync(StaffData.NewUser(company.Id, email), cancellationToken);
        await SaveAsync(StaffData.NewUser(other.Id, email), cancellationToken);

        await Should.ThrowAsync<DbUpdateException>(() => SaveAsync(StaffData.NewUser(company.Id, email), cancellationToken));
    }

    private Task SaveAsync(User user, CancellationToken cancellationToken) =>
        StaffData.SaveAsync(mysql, null, cancellationToken,
            services => services.GetRequiredService<IUserRepository>().Add(user));

    private async Task UpdateAsync(Guid id, CancellationToken cancellationToken, Action<User> change)
    {
        await using var scope = mysql.CreateScope();
        var loaded = await scope.ServiceProvider.GetRequiredService<IUserRepository>().GetByIdAsync(id, cancellationToken);

        change(loaded!);
        await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync(cancellationToken);
    }

    private async Task<User?> LoadAsync(Guid id, CancellationToken cancellationToken)
    {
        await using var scope = mysql.CreateScope();

        return await scope.ServiceProvider.GetRequiredService<IUserRepository>().GetByIdAsync(id, cancellationToken);
    }
}
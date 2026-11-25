using Microsoft.Extensions.DependencyInjection;
using PlanelyApp.Domain.Organization.Companies;
using PlanelyApp.Domain.Organization.Users;
using PlanelyApp.Domain.SeedWork;
using PlanelyApp.Infrastructure.Development;
using PlanelyApp.IntegrationTests.Organization;
using Shouldly;

namespace PlanelyApp.IntegrationTests.Development;

/// <summary>
/// The company of the development environment and its administrator: the second user of the realm, who is what the Bruno collection
/// signs in as to work on branches, professionals and the rest. The company and its administrator are saved together or not at all.
/// </summary>
public class DevelopmentCompanySeedTests(MySqlFixture mysql)
{
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    private static DevelopmentCompanyOptions NewOptions(string? cnpj = null) => new()
    {
        TradeName = "Barbearia Dev",
        LegalName = "Barbearia Dev Ltda",
        Cnpj = cnpj ?? OrganizationData.NewCnpj().Value,
        Email = "contato@barbeariadev.local",
        Phone = "+5511999990000",
        DefaultLanguage = "pt-BR",
        AdminIdentityId = $"sub-{Guid.NewGuid():N}",
        AdminName = "Company Admin",
        AdminEmail = StaffData.NewEmail(),
    };

    private async Task<DevelopmentCompanySeedResult> EnsureAsync(DevelopmentCompanyOptions options)
    {
        await using var scope = mysql.CreateScope();
        var seed = new DevelopmentCompanySeed(
            scope.ServiceProvider.GetRequiredService<ICompanyRepository>(),
            scope.ServiceProvider.GetRequiredService<IUserRepository>(),
            scope.ServiceProvider.GetRequiredService<IUnitOfWork>());

        return await seed.EnsureAsync(options, _ct);
    }

    private async Task<User?> FindAdminAsync(DevelopmentCompanyOptions options)
    {
        await using var scope = mysql.CreateScope();

        return await scope.ServiceProvider.GetRequiredService<IUserRepository>()
            .GetByIdentityIdAsync(options.AdminIdentityId, _ct);
    }

    [Fact]
    public async Task It_should_create_the_company_and_its_administrator_with_access_to_every_branch()
    {
        var options = NewOptions();

        var result = await EnsureAsync(options);
        var admin = await FindAdminAsync(options);

        result.ShouldBe(DevelopmentCompanySeedResult.Created);
        admin.ShouldNotBeNull();
        admin.Role.ShouldBe(UserRole.CompanyAdmin);
        admin.CompanyId.ShouldNotBeNull();
        admin.AccessAllBranches.ShouldBeTrue();
        admin.CanAccess(Guid.CreateVersion7()).ShouldBeTrue();
        admin.Email.Value.ShouldBe(options.AdminEmail);

        await using var scope = mysql.CreateScope();
        var companies = scope.ServiceProvider.GetRequiredService<ICompanyRepository>();
        (await companies.ExistsByCnpjAsync(PlanelyApp.Domain.SharedKernel.Cnpj.Create(options.Cnpj), _ct)).ShouldBeTrue();
    }

    [Fact]
    public async Task A_second_run_should_create_nothing()
    {
        var options = NewOptions();
        await EnsureAsync(options);
        var first = await FindAdminAsync(options);

        var again = await EnsureAsync(options);
        var second = await FindAdminAsync(options);

        again.ShouldBe(DevelopmentCompanySeedResult.AlreadyThere);
        second!.Id.ShouldBe(first!.Id);
        second.CompanyId.ShouldBe(first.CompanyId);
    }

    [Fact]
    public async Task A_cnpj_that_belongs_to_another_company_should_create_no_administrator()
    {
        var taken = await OrganizationData.SeedCompanyAsync(mysql, _ct);
        var options = NewOptions(taken.Cnpj.Value);

        var result = await EnsureAsync(options);

        result.ShouldBe(DevelopmentCompanySeedResult.CnpjTaken);
        (await FindAdminAsync(options)).ShouldBeNull();
    }

    [Theory]
    [InlineData("", "sub-1")]
    [InlineData("11444777000161", " ")]
    public void The_options_of_the_company_should_be_complete_too(string cnpj, string adminIdentityId)
    {
        var options = new DevelopmentSeedOptions
        {
            IdentityId = "sub-0",
            Name = "Admin",
            Email = "admin@planelyapp.local",
            Company = NewOptions(cnpj),
        };
        options.Company.AdminIdentityId = adminIdentityId;
        options.Company.Cnpj = cnpj;

        Should.Throw<ArgumentException>(() => new ServiceCollection().AddDevelopmentSeed(options));
    }
}

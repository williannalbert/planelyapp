using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PlanelyApp.Domain.Organization.Companies;
using PlanelyApp.Domain.SeedWork;
using PlanelyApp.Domain.SharedKernel;
using Shouldly;

namespace PlanelyApp.IntegrationTests.Organization;

public class CompanyRepositoryTests(MySqlFixture mysql)
{
    [Fact]
    public async Task A_saved_company_should_come_back_equal()
    {
        var saved = await OrganizationData.SeedCompanyAsync(mysql, TestContext.Current.CancellationToken);

        await using var scope = mysql.CreateScope();
        var loaded = await scope.ServiceProvider.GetRequiredService<ICompanyRepository>()
            .GetByIdAsync(saved.Id, TestContext.Current.CancellationToken);

        loaded.ShouldNotBeNull();
        loaded.TradeName.ShouldBe(saved.TradeName);
        loaded.LegalName.ShouldBe(saved.LegalName);
        loaded.Cnpj.ShouldBe(saved.Cnpj);
        loaded.Email.ShouldBe(saved.Email);
        loaded.Phone.ShouldBe(saved.Phone);
        loaded.DefaultLanguage.ShouldBe(Language.Spanish);
        loaded.BookingPolicy.ShouldBe(saved.BookingPolicy);
        loaded.IsActive.ShouldBeTrue();
    }

    [Fact]
    public async Task Changes_to_a_company_should_be_saved()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var saved = await OrganizationData.SeedCompanyAsync(mysql, cancellationToken);

        await using (var scope = mysql.CreateScope())
        {
            var company = await scope.ServiceProvider.GetRequiredService<ICompanyRepository>()
                .GetByIdAsync(saved.Id, cancellationToken);

            company!.UpdateDetails("Novo Nome", "Nova Razão LTDA", Email.Create("novo@exemplo.com"), Phone.Create("+14155550100"), Language.English);
            company.UpdatePolicy(BookingPolicy.Default);
            company.Deactivate();

            await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync(cancellationToken);
        }

        await using var check = mysql.CreateScope();
        var reloaded = await check.ServiceProvider.GetRequiredService<ICompanyRepository>()
            .GetByIdAsync(saved.Id, cancellationToken);

        reloaded.ShouldNotBeNull();
        reloaded.TradeName.ShouldBe("Novo Nome");
        reloaded.LegalName.ShouldBe("Nova Razão LTDA");
        reloaded.Email.ShouldBe(Email.Create("novo@exemplo.com"));
        reloaded.Phone.ShouldBe(Phone.Create("+14155550100"));
        reloaded.DefaultLanguage.ShouldBe(Language.English);
        reloaded.BookingPolicy.ShouldBe(BookingPolicy.Default);
        reloaded.IsActive.ShouldBeFalse();
    }

    [Fact]
    public async Task A_registered_cnpj_should_be_found_and_an_unknown_one_should_not()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var saved = await OrganizationData.SeedCompanyAsync(mysql, cancellationToken);

        await using var scope = mysql.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<ICompanyRepository>();

        (await repository.ExistsByCnpjAsync(saved.Cnpj, cancellationToken)).ShouldBeTrue();
        (await repository.ExistsByCnpjAsync(OrganizationData.NewCnpj(), cancellationToken)).ShouldBeFalse();
    }

    [Fact]
    public async Task Two_companies_with_the_same_cnpj_should_not_be_saved()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var saved = await OrganizationData.SeedCompanyAsync(mysql, cancellationToken);

        await using var scope = mysql.CreateScope();
        scope.ServiceProvider.GetRequiredService<ICompanyRepository>()
            .Add(OrganizationData.NewCompany("Outra", saved.Cnpj));

        await Should.ThrowAsync<DbUpdateException>(
            () => scope.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync(cancellationToken));
    }

    [Fact]
    public async Task The_longest_values_the_domain_allows_should_fit_in_the_columns()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var longEmail = new string('a', 242) + "@example.com"; // 254 characters

        var company = Company.Create(
            new string('A', 150),
            new string('B', 200),
            OrganizationData.NewCnpj(),
            Email.Create(longEmail),
            Phone.Create("+123456789012345"),
            Language.PortugueseBrazil);

        await using (var scope = mysql.CreateScope())
        {
            scope.ServiceProvider.GetRequiredService<ICompanyRepository>().Add(company);
            await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync(cancellationToken);
        }

        await using var check = mysql.CreateScope();
        var loaded = await check.ServiceProvider.GetRequiredService<ICompanyRepository>()
            .GetByIdAsync(company.Id, cancellationToken);

        loaded.ShouldNotBeNull();
        loaded.TradeName.Length.ShouldBe(150);
        loaded.LegalName.Length.ShouldBe(200);
        loaded.Email.Value.Length.ShouldBe(254);
    }
}
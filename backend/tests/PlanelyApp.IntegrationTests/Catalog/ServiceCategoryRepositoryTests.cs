using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PlanelyApp.Domain.Catalog.ServiceCategories;
using PlanelyApp.Domain.SeedWork;
using PlanelyApp.IntegrationTests.Organization;
using Shouldly;

namespace PlanelyApp.IntegrationTests.Catalog;

public class ServiceCategoryRepositoryTests(MySqlFixture mysql)
{
    [Fact]
    public async Task A_saved_category_should_come_back_and_keep_a_new_name()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var company = await OrganizationData.SeedCompanyAsync(mysql, cancellationToken);
        var category = ServiceCategory.Create(company.Id, "Hair");
        await SaveAsync(company.Id, category, cancellationToken);

        var loaded = await LoadAsync(company.Id, category.Id, cancellationToken);

        loaded.ShouldNotBeNull();
        loaded.CompanyId.ShouldBe(company.Id);
        loaded.Name.ShouldBe("Hair");

        await using (var scope = mysql.CreateScope(company.Id))
        {
            var tracked = await scope.ServiceProvider.GetRequiredService<IServiceCategoryRepository>()
                .GetByIdAsync(category.Id, cancellationToken);

            tracked!.Rename("Hair and beard");
            await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync(cancellationToken);
        }

        (await LoadAsync(company.Id, category.Id, cancellationToken))!.Name.ShouldBe("Hair and beard");
    }

    [Fact]
    public async Task A_category_should_only_be_found_by_its_own_company()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var company = await OrganizationData.SeedCompanyAsync(mysql, cancellationToken);
        var other = await OrganizationData.SeedCompanyAsync(mysql, cancellationToken);
        var category = CatalogData.NewCategory(company.Id);
        await SaveAsync(company.Id, category, cancellationToken);

        (await LoadAsync(company.Id, category.Id, cancellationToken)).ShouldNotBeNull();
        (await LoadAsync(other.Id, category.Id, cancellationToken)).ShouldBeNull();
        (await LoadAsync(null, category.Id, cancellationToken)).ShouldBeNull();
    }

    [Fact]
    public async Task The_longest_name_the_domain_allows_should_fit_in_the_column()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var company = await OrganizationData.SeedCompanyAsync(mysql, cancellationToken);
        var category = ServiceCategory.Create(company.Id, new string('N', 100));
        await SaveAsync(company.Id, category, cancellationToken);

        (await LoadAsync(company.Id, category.Id, cancellationToken))!.Name.Length.ShouldBe(100);
    }

    [Fact]
    public async Task The_name_should_be_found_inside_the_company_ignoring_case_and_leaving_one_category_out()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var company = await OrganizationData.SeedCompanyAsync(mysql, cancellationToken);
        var other = await OrganizationData.SeedCompanyAsync(mysql, cancellationToken);
        var category = ServiceCategory.Create(company.Id, "Hair");
        await SaveAsync(company.Id, category, cancellationToken);

        (await ExistsAsync(company.Id, "Hair", null, cancellationToken)).ShouldBeTrue();
        (await ExistsAsync(company.Id, "hair", null, cancellationToken)).ShouldBeTrue();
        (await ExistsAsync(company.Id, "Hair", category.Id, cancellationToken)).ShouldBeFalse();
        (await ExistsAsync(company.Id, "Beard", null, cancellationToken)).ShouldBeFalse();
        (await ExistsAsync(other.Id, "Hair", null, cancellationToken)).ShouldBeFalse();
    }

    [Fact]
    public async Task Two_categories_with_the_same_name_in_a_company_should_not_be_saved()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var company = await OrganizationData.SeedCompanyAsync(mysql, cancellationToken);
        await SaveAsync(company.Id, ServiceCategory.Create(company.Id, "Hair"), cancellationToken);

        await Should.ThrowAsync<DbUpdateException>(() =>
            SaveAsync(company.Id, ServiceCategory.Create(company.Id, "Hair"), cancellationToken));
    }

    private async Task<bool> ExistsAsync(Guid companyId, string name, Guid? exceptId, CancellationToken cancellationToken)
    {
        await using var scope = mysql.CreateScope(companyId);

        return await scope.ServiceProvider.GetRequiredService<IServiceCategoryRepository>()
            .ExistsByNameAsync(name, exceptId, cancellationToken);
    }

    private Task SaveAsync(Guid companyId, ServiceCategory category, CancellationToken cancellationToken) =>
        mysql.SaveAsync(companyId, cancellationToken,
            services => services.GetRequiredService<IServiceCategoryRepository>().Add(category));

    private async Task<ServiceCategory?> LoadAsync(Guid? companyId, Guid id, CancellationToken cancellationToken)
    {
        await using var scope = mysql.CreateScope(companyId);

        return await scope.ServiceProvider.GetRequiredService<IServiceCategoryRepository>().GetByIdAsync(id, cancellationToken);
    }
}
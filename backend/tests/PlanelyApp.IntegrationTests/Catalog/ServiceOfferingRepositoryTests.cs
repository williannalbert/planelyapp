using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PlanelyApp.Domain.Catalog.ServiceCategories;
using PlanelyApp.Domain.Catalog.ServiceOfferings;
using PlanelyApp.Domain.SeedWork;
using PlanelyApp.Domain.SharedKernel;
using PlanelyApp.IntegrationTests.Organization;
using Shouldly;

namespace PlanelyApp.IntegrationTests.Catalog;

public class ServiceOfferingRepositoryTests(MySqlFixture mysql)
{
    [Fact]
    public async Task A_fixed_price_service_should_come_back_with_everything()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var company = await OrganizationData.SeedCompanyAsync(mysql, cancellationToken);
        var category = CatalogData.NewCategory(company.Id);
        await mysql.SaveAsync(company.Id, cancellationToken,
            services => services.GetRequiredService<IServiceCategoryRepository>().Add(category));

        var service = ServiceOffering.CreateWithFixedPrice(
            company.Id,
            "Corte masculino",
            TimeSpan.FromMinutes(45),
            Money.From(79.90m),
            "Corte com máquina e tesoura",
            category.Id);
        await SaveAsync(company.Id, service, cancellationToken);

        var loaded = await LoadAsync(company.Id, service.Id, cancellationToken);

        loaded.ShouldNotBeNull();
        loaded.CompanyId.ShouldBe(company.Id);
        loaded.CategoryId.ShouldBe(category.Id);
        loaded.Name.ShouldBe("Corte masculino");
        loaded.Description.ShouldBe("Corte com máquina e tesoura");
        loaded.StandardDuration.ShouldBe(TimeSpan.FromMinutes(45));
        loaded.PricingType.ShouldBe(PricingType.Fixed);
        loaded.BasePrice.ShouldBe(Money.From(79.90m));
        loaded.IsActive.ShouldBeTrue();
    }

    [Fact]
    public async Task An_on_request_service_should_have_no_price_and_changing_the_pricing_should_be_saved()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var company = await OrganizationData.SeedCompanyAsync(mysql, cancellationToken);
        var service = CatalogData.NewOnRequestService(company.Id);
        await SaveAsync(company.Id, service, cancellationToken);

        var onRequest = await LoadAsync(company.Id, service.Id, cancellationToken);
        onRequest.ShouldNotBeNull();
        onRequest.PricingType.ShouldBe(PricingType.OnRequest);
        onRequest.BasePrice.ShouldBeNull();

        await UpdateAsync(company.Id, service.Id, cancellationToken, loaded => loaded.SetFixedPrice(Money.From(120m)));

        var fixedPrice = await LoadAsync(company.Id, service.Id, cancellationToken);
        fixedPrice!.PricingType.ShouldBe(PricingType.Fixed);
        fixedPrice.BasePrice.ShouldBe(Money.From(120m));

        await UpdateAsync(company.Id, service.Id, cancellationToken, loaded =>
        {
            loaded.SetOnRequest();
            loaded.Deactivate();
        });

        var back = await LoadAsync(company.Id, service.Id, cancellationToken);
        back!.PricingType.ShouldBe(PricingType.OnRequest);
        back.BasePrice.ShouldBeNull();
        back.IsActive.ShouldBeFalse();
    }

    [Fact]
    public async Task Updated_details_should_be_saved_and_the_category_removed()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var company = await OrganizationData.SeedCompanyAsync(mysql, cancellationToken);
        var category = CatalogData.NewCategory(company.Id);
        await mysql.SaveAsync(company.Id, cancellationToken,
            services => services.GetRequiredService<IServiceCategoryRepository>().Add(category));

        var service = CatalogData.NewFixedService(company.Id, categoryId: category.Id);
        await SaveAsync(company.Id, service, cancellationToken);

        await UpdateAsync(company.Id, service.Id, cancellationToken,
            loaded => loaded.UpdateDetails("Novo nome", null, TimeSpan.FromMinutes(90), null));

        var loadedAgain = await LoadAsync(company.Id, service.Id, cancellationToken);

        loadedAgain.ShouldNotBeNull();
        loadedAgain.Name.ShouldBe("Novo nome");
        loadedAgain.Description.ShouldBeNull();
        loadedAgain.StandardDuration.ShouldBe(TimeSpan.FromMinutes(90));
        loadedAgain.CategoryId.ShouldBeNull();
    }

    [Fact]
    public async Task A_service_should_be_found_by_name_inside_its_own_company_only()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var company = await OrganizationData.SeedCompanyAsync(mysql, cancellationToken);
        var other = await OrganizationData.SeedCompanyAsync(mysql, cancellationToken);
        var service = CatalogData.NewFixedService(company.Id, name: "Corte masculino");
        await SaveAsync(company.Id, service, cancellationToken);

        await using var scope = mysql.CreateScope(company.Id);
        var repository = scope.ServiceProvider.GetRequiredService<IServiceOfferingRepository>();

        (await repository.FindByNameAsync(company.Id, "Corte masculino", cancellationToken))!.Id.ShouldBe(service.Id);
        (await repository.FindByNameAsync(company.Id, "  Corte masculino  ", cancellationToken))!.Id.ShouldBe(service.Id);
        (await repository.FindByNameAsync(company.Id, "Outro serviço", cancellationToken)).ShouldBeNull();
        (await repository.FindByNameAsync(other.Id, "Corte masculino", cancellationToken)).ShouldBeNull();
    }

    [Fact]
    public async Task A_name_should_be_used_once_per_company_but_may_repeat_across_companies()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var company = await OrganizationData.SeedCompanyAsync(mysql, cancellationToken);
        var other = await OrganizationData.SeedCompanyAsync(mysql, cancellationToken);
        var name = CatalogData.NewName("Corte");

        await SaveAsync(company.Id, CatalogData.NewFixedService(company.Id, name: name), cancellationToken);
        await SaveAsync(other.Id, CatalogData.NewFixedService(other.Id, name: name), cancellationToken);

        await Should.ThrowAsync<DbUpdateException>(
            () => SaveAsync(company.Id, CatalogData.NewFixedService(company.Id, name: name), cancellationToken));

        // MySQL compares text ignoring case by default, so only the capitalization differing is still the same name.
        await Should.ThrowAsync<DbUpdateException>(
            () => SaveAsync(company.Id, CatalogData.NewFixedService(company.Id, name: name.ToUpperInvariant()), cancellationToken));
    }

    [Fact]
    public async Task A_service_in_a_category_that_does_not_exist_should_not_be_saved()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var company = await OrganizationData.SeedCompanyAsync(mysql, cancellationToken);
        var service = CatalogData.NewFixedService(company.Id, categoryId: Guid.CreateVersion7());

        await Should.ThrowAsync<DbUpdateException>(() => SaveAsync(company.Id, service, cancellationToken));
    }

    [Fact]
    public async Task A_service_should_only_be_found_by_its_own_company()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var company = await OrganizationData.SeedCompanyAsync(mysql, cancellationToken);
        var other = await OrganizationData.SeedCompanyAsync(mysql, cancellationToken);
        var service = CatalogData.NewFixedService(company.Id);
        await SaveAsync(company.Id, service, cancellationToken);

        (await LoadAsync(company.Id, service.Id, cancellationToken)).ShouldNotBeNull();
        (await LoadAsync(other.Id, service.Id, cancellationToken)).ShouldBeNull();
        (await LoadAsync(null, service.Id, cancellationToken)).ShouldBeNull();
    }

    [Fact]
    public async Task The_largest_values_that_fit_in_the_columns_should_be_saved_as_they_are()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var company = await OrganizationData.SeedCompanyAsync(mysql, cancellationToken);
        var service = ServiceOffering.CreateWithFixedPrice(
            company.Id,
            new string('N', 150),
            TimeSpan.FromHours(24),
            Money.From(9_999_999_999.99m),
            new string('D', 2000));
        await SaveAsync(company.Id, service, cancellationToken);

        var loaded = await LoadAsync(company.Id, service.Id, cancellationToken);

        loaded.ShouldNotBeNull();
        loaded.Name.Length.ShouldBe(150);
        loaded.Description!.Length.ShouldBe(2000);
        loaded.StandardDuration.ShouldBe(TimeSpan.FromHours(24));
        loaded.BasePrice.ShouldBe(Money.From(9_999_999_999.99m));
    }

    private Task SaveAsync(Guid companyId, ServiceOffering service, CancellationToken cancellationToken) =>
        mysql.SaveAsync(companyId, cancellationToken,
            services => services.GetRequiredService<IServiceOfferingRepository>().Add(service));

    private async Task UpdateAsync(Guid companyId, Guid id, CancellationToken cancellationToken, Action<ServiceOffering> change)
    {
        await using var scope = mysql.CreateScope(companyId);
        var loaded = await scope.ServiceProvider.GetRequiredService<IServiceOfferingRepository>().GetByIdAsync(id, cancellationToken);

        change(loaded!);
        await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync(cancellationToken);
    }

    private async Task<ServiceOffering?> LoadAsync(Guid? companyId, Guid id, CancellationToken cancellationToken)
    {
        await using var scope = mysql.CreateScope(companyId);

        return await scope.ServiceProvider.GetRequiredService<IServiceOfferingRepository>().GetByIdAsync(id, cancellationToken);
    }
}
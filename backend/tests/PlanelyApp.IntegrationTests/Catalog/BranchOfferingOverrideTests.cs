using Microsoft.Extensions.DependencyInjection;
using PlanelyApp.Domain.Organization.Branches;
using PlanelyApp.Domain.SeedWork;
using PlanelyApp.Domain.SharedKernel;
using PlanelyApp.IntegrationTests.Organization;
using Shouldly;

namespace PlanelyApp.IntegrationTests.Catalog;

public class BranchOfferingOverrideTests(MySqlFixture mysql)
{
    [Fact]
    public async Task Overrides_should_come_back_with_the_branch()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var company = await OrganizationData.SeedCompanyAsync(mysql, cancellationToken);
        var haircut = CatalogData.NewFixedService(company.Id);
        var beard = CatalogData.NewFixedService(company.Id);

        var branch = OrganizationData.NewBranch(company.Id, CatalogData.NewName("Filial"));
        branch.SetCustomPrice(haircut, Money.From(99.90m));
        branch.MakeOfferingUnavailable(beard.Id);
        await SaveAsync(company.Id, branch, cancellationToken);

        var loaded = await LoadAsync(company.Id, branch.Id, cancellationToken);

        loaded.ShouldNotBeNull();
        loaded.OfferingOverrides.Count.ShouldBe(2);
        loaded.GetCustomPrice(haircut.Id).ShouldBe(Money.From(99.90m));
        loaded.Offers(haircut.Id).ShouldBeTrue();
        loaded.Offers(beard.Id).ShouldBeFalse();
        loaded.GetCustomPrice(beard.Id).ShouldBeNull();
        loaded.Offers(Guid.CreateVersion7()).ShouldBeTrue(); // no override means the default
    }

    [Fact]
    public async Task Changing_an_override_should_replace_it_and_going_back_to_the_default_should_remove_it()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var company = await OrganizationData.SeedCompanyAsync(mysql, cancellationToken);
        var service = CatalogData.NewFixedService(company.Id);

        var branch = OrganizationData.NewBranch(company.Id, CatalogData.NewName("Filial"));
        branch.SetCustomPrice(service, Money.From(100m));
        await SaveAsync(company.Id, branch, cancellationToken);

        await UpdateAsync(company.Id, branch.Id, cancellationToken, loaded => loaded.SetCustomPrice(service, Money.From(80m)));

        var cheaper = await LoadAsync(company.Id, branch.Id, cancellationToken);
        cheaper!.OfferingOverrides.ShouldHaveSingleItem();
        cheaper.GetCustomPrice(service.Id).ShouldBe(Money.From(80m));

        await UpdateAsync(company.Id, branch.Id, cancellationToken, loaded => loaded.MakeOfferingUnavailable(service.Id));

        var unavailable = await LoadAsync(company.Id, branch.Id, cancellationToken);
        unavailable!.OfferingOverrides.ShouldHaveSingleItem();
        unavailable.GetCustomPrice(service.Id).ShouldBe(Money.From(80m));
        unavailable.Offers(service.Id).ShouldBeFalse();

        await UpdateAsync(company.Id, branch.Id, cancellationToken, loaded =>
        {
            loaded.ClearCustomPrice(service.Id);
            loaded.MakeOfferingAvailable(service.Id);
        });

        var back = await LoadAsync(company.Id, branch.Id, cancellationToken);
        back!.OfferingOverrides.ShouldBeEmpty();
    }

    private Task SaveAsync(Guid companyId, Branch branch, CancellationToken cancellationToken) =>
        StaffData.SaveAsync(mysql, companyId, cancellationToken,
            services => services.GetRequiredService<IBranchRepository>().Add(branch));

    private async Task UpdateAsync(Guid companyId, Guid id, CancellationToken cancellationToken, Action<Branch> change)
    {
        await using var scope = mysql.CreateScope(companyId);
        var loaded = await scope.ServiceProvider.GetRequiredService<IBranchRepository>().GetByIdAsync(id, cancellationToken);

        change(loaded!);
        await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync(cancellationToken);
    }

    private async Task<Branch?> LoadAsync(Guid companyId, Guid id, CancellationToken cancellationToken)
    {
        await using var scope = mysql.CreateScope(companyId);

        return await scope.ServiceProvider.GetRequiredService<IBranchRepository>().GetByIdAsync(id, cancellationToken);
    }
}
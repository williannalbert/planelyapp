using Microsoft.Extensions.DependencyInjection;
using PlanelyApp.Domain.Catalog.ServiceOfferings;
using PlanelyApp.Domain.Organization.Branches;
using PlanelyApp.IntegrationTests.Organization;
using Shouldly;

namespace PlanelyApp.IntegrationTests.Catalog;

public class CatalogLookupTests(MySqlFixture mysql)
{
    [Fact]
    public async Task Services_by_id_should_come_only_from_the_company_of_the_request()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var company = await OrganizationData.SeedCompanyAsync(mysql, cancellationToken);
        var other = await OrganizationData.SeedCompanyAsync(mysql, cancellationToken);
        var mine = CatalogData.NewFixedService(company.Id);
        var alsoMine = CatalogData.NewFixedService(company.Id);
        var theirs = CatalogData.NewFixedService(other.Id);
        await mysql.SaveAsync(company.Id, cancellationToken,
            services => services.GetRequiredService<IServiceOfferingRepository>().Add(mine));
        await mysql.SaveAsync(company.Id, cancellationToken,
            services => services.GetRequiredService<IServiceOfferingRepository>().Add(alsoMine));
        await mysql.SaveAsync(other.Id, cancellationToken,
            services => services.GetRequiredService<IServiceOfferingRepository>().Add(theirs));

        await using var scope = mysql.CreateScope(company.Id);
        var found = await scope.ServiceProvider.GetRequiredService<IServiceOfferingRepository>()
            .ListByIdsAsync([mine.Id, theirs.Id, Guid.CreateVersion7()], cancellationToken);

        found.Select(service => service.Id).ShouldBe([mine.Id], ignoreOrder: false);
    }

    [Fact]
    public async Task Branches_by_id_should_be_counted_only_in_the_company_of_the_request()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var company = await OrganizationData.SeedCompanyAsync(mysql, cancellationToken);
        var other = await OrganizationData.SeedCompanyAsync(mysql, cancellationToken);
        var centro = OrganizationData.NewBranch(company.Id, "Centro");
        var norte = OrganizationData.NewBranch(company.Id, "Norte");
        var theirs = OrganizationData.NewBranch(other.Id, "Centro");
        await mysql.SaveAsync(company.Id, cancellationToken, services =>
        {
            var branches = services.GetRequiredService<IBranchRepository>();
            branches.Add(centro);
            branches.Add(norte);
        });
        await mysql.SaveAsync(other.Id, cancellationToken,
            services => services.GetRequiredService<IBranchRepository>().Add(theirs));

        await using var scope = mysql.CreateScope(company.Id);
        var branchRepository = scope.ServiceProvider.GetRequiredService<IBranchRepository>();

        (await branchRepository.CountByIdsAsync([centro.Id, norte.Id, theirs.Id], cancellationToken)).ShouldBe(2);
        (await branchRepository.CountByIdsAsync([Guid.CreateVersion7()], cancellationToken)).ShouldBe(0);
    }
}
using Microsoft.Extensions.DependencyInjection;
using PlanelyApp.Domain.Catalog.Promotions;
using PlanelyApp.IntegrationTests.Organization;
using Shouldly;

namespace PlanelyApp.IntegrationTests.Catalog;

public class PromotionInForceRepositoryTests(MySqlFixture mysql)
{
    [Fact]
    public async Task Only_the_active_promotions_whose_period_contains_the_day_should_come_back()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var company = await OrganizationData.SeedCompanyAsync(mysql, cancellationToken);
        var november = NewPromotion(company.Id, new DateOnly(2027, 11, 20), new DateOnly(2027, 11, 30));
        var december = NewPromotion(company.Id, new DateOnly(2027, 12, 1), new DateOnly(2027, 12, 10));
        var ended = NewPromotion(company.Id, new DateOnly(2027, 11, 20), new DateOnly(2027, 11, 30));
        ended.End();
        await SaveAsync(company.Id, november, cancellationToken);
        await SaveAsync(company.Id, december, cancellationToken);
        await SaveAsync(company.Id, ended, cancellationToken);

        var onFirstDay = await ListAsync(company.Id, new DateOnly(2027, 11, 20), cancellationToken);
        var onLastDay = await ListAsync(company.Id, new DateOnly(2027, 11, 30), cancellationToken);
        var inDecember = await ListAsync(company.Id, new DateOnly(2027, 12, 1), cancellationToken);
        var before = await ListAsync(company.Id, new DateOnly(2027, 11, 19), cancellationToken);

        onFirstDay.ShouldBe([november.Id], ignoreOrder: true);
        onLastDay.ShouldBe([november.Id], ignoreOrder: true);
        inDecember.ShouldBe([december.Id], ignoreOrder: true);
        before.ShouldBeEmpty();
    }

    [Fact]
    public async Task The_promotions_of_another_company_should_not_come_back()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var company = await OrganizationData.SeedCompanyAsync(mysql, cancellationToken);
        var other = await OrganizationData.SeedCompanyAsync(mysql, cancellationToken);
        var mine = NewPromotion(company.Id, new DateOnly(2027, 11, 20), new DateOnly(2027, 11, 30));
        var theirs = NewPromotion(other.Id, new DateOnly(2027, 11, 20), new DateOnly(2027, 11, 30));
        await SaveAsync(company.Id, mine, cancellationToken);
        await SaveAsync(other.Id, theirs, cancellationToken);

        var found = await ListAsync(company.Id, new DateOnly(2027, 11, 25), cancellationToken);

        found.ShouldBe([mine.Id], ignoreOrder: true);
    }

    private static Promotion NewPromotion(Guid companyId, DateOnly start, DateOnly end) =>
        Promotion.CreateForAllBranches(
            companyId,
            CatalogData.NewName("Promotion"),
            Discount.Percentage(10m),
            ValidityPeriod.Create(start, end),
            [CatalogData.NewFixedService(companyId)]);

    private Task SaveAsync(Guid companyId, Promotion promotion, CancellationToken cancellationToken) =>
        mysql.SaveAsync(companyId, cancellationToken,
            services => services.GetRequiredService<IPromotionRepository>().Add(promotion));

    private async Task<IReadOnlyList<Guid>> ListAsync(Guid companyId, DateOnly day, CancellationToken cancellationToken)
    {
        await using var scope = mysql.CreateScope(companyId);
        var found = await scope.ServiceProvider.GetRequiredService<IPromotionRepository>()
            .ListInForceAsync(day, cancellationToken);

        return found.Select(promotion => promotion.Id).ToList();
    }
}
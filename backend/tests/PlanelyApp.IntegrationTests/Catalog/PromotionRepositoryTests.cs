using Microsoft.Extensions.DependencyInjection;
using PlanelyApp.Domain.Catalog.Promotions;
using PlanelyApp.Domain.SeedWork;
using PlanelyApp.Domain.SharedKernel;
using PlanelyApp.IntegrationTests.Organization;
using Shouldly;

namespace PlanelyApp.IntegrationTests.Catalog;

public class PromotionRepositoryTests(MySqlFixture mysql)
{
    // 2027-11-20 is a Saturday, so the 22nd is a Monday and the 23rd a Tuesday.
    private static readonly ValidityPeriod November = ValidityPeriod.Create(new DateOnly(2027, 11, 20), new DateOnly(2027, 11, 30));

    [Fact]
    public async Task A_per_service_promotion_for_all_branches_should_come_back_with_everything_and_behave_the_same()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var company = await OrganizationData.SeedCompanyAsync(mysql, cancellationToken);
        var haircut = CatalogData.NewFixedService(company.Id);
        var beard = CatalogData.NewFixedService(company.Id);

        var promotion = Promotion.CreateForAllBranches(
            company.Id, "Black Friday", Discount.Percentage(12.5m), November, [haircut, beard], "Weekend offer");
        promotion.SetWeekdays([DayOfWeek.Saturday, DayOfWeek.Monday]);
        await SaveAsync(company.Id, promotion, cancellationToken);

        var loaded = await LoadAsync(company.Id, promotion.Id, cancellationToken);

        loaded.ShouldNotBeNull();
        loaded.CompanyId.ShouldBe(company.Id);
        loaded.Kind.ShouldBe(PromotionKind.PerService);
        loaded.Name.ShouldBe("Black Friday");
        loaded.Description.ShouldBe("Weekend offer");
        loaded.Discount.ShouldBe(Discount.Percentage(12.5m));
        loaded.Validity.ShouldBe(November);
        loaded.AppliesToAllBranches.ShouldBeTrue();
        loaded.BranchIds.ShouldBeEmpty();
        loaded.ServiceIds.ShouldBe([haircut.Id, beard.Id]);
        loaded.Weekdays.ShouldBe([DayOfWeek.Monday, DayOfWeek.Saturday]);
        loaded.IsActive.ShouldBeTrue();

        // The reloaded promotion must answer like the original.
        var anyBranch = Guid.CreateVersion7();
        loaded.AppliesTo(anyBranch, haircut.Id, new DateOnly(2027, 11, 22)).ShouldBeTrue();   // Monday
        loaded.AppliesTo(anyBranch, haircut.Id, new DateOnly(2027, 11, 20)).ShouldBeTrue();   // Saturday
        loaded.AppliesTo(anyBranch, haircut.Id, new DateOnly(2027, 11, 23)).ShouldBeFalse();  // Tuesday
        loaded.AppliesTo(anyBranch, haircut.Id, new DateOnly(2027, 12, 4)).ShouldBeFalse();   // Saturday, after the period
        loaded.AppliesTo(anyBranch, Guid.CreateVersion7(), new DateOnly(2027, 11, 22)).ShouldBeFalse();
        loaded.CalculateDiscount(Money.From(80m)).ShouldBe(Money.From(10m));
    }

    [Fact]
    public async Task A_bundle_for_some_branches_should_come_back_with_its_branches_and_services()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var company = await OrganizationData.SeedCompanyAsync(mysql, cancellationToken);
        var haircut = CatalogData.NewFixedService(company.Id);
        var beard = CatalogData.NewFixedService(company.Id);
        var (branchA, branchB) = (Guid.CreateVersion7(), Guid.CreateVersion7());

        var promotion = Promotion.CreateBundleForBranches(
            company.Id, "Combo", Discount.FixedAmount(Money.From(15m)), November, [haircut, beard], [branchA, branchB]);
        await SaveAsync(company.Id, promotion, cancellationToken);

        var loaded = await LoadAsync(company.Id, promotion.Id, cancellationToken);

        loaded.ShouldNotBeNull();
        loaded.Kind.ShouldBe(PromotionKind.Bundle);
        loaded.Description.ShouldBeNull();
        loaded.Discount.ShouldBe(Discount.FixedAmount(Money.From(15m)));
        loaded.AppliesToAllBranches.ShouldBeFalse();
        loaded.BranchIds.ShouldBe([branchA, branchB]);
        loaded.ServiceIds.ShouldBe([haircut.Id, beard.Id]);
        loaded.Weekdays.ShouldBeEmpty();

        var date = new DateOnly(2027, 11, 24);
        loaded.AppliesToBundle(branchA, [haircut.Id, beard.Id], date).ShouldBeTrue();
        loaded.AppliesToBundle(branchA, [haircut.Id], date).ShouldBeFalse();
        loaded.AppliesToBundle(Guid.CreateVersion7(), [haircut.Id, beard.Id], date).ShouldBeFalse();
    }

    [Fact]
    public async Task Changes_to_a_promotion_should_be_saved()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var company = await OrganizationData.SeedCompanyAsync(mysql, cancellationToken);
        var haircut = CatalogData.NewFixedService(company.Id);
        var beard = CatalogData.NewFixedService(company.Id);
        var massage = CatalogData.NewFixedService(company.Id);
        var branch = Guid.CreateVersion7();
        var december = ValidityPeriod.Create(new DateOnly(2027, 12, 1), new DateOnly(2027, 12, 31));

        var promotion = Promotion.CreateForAllBranches(
            company.Id, "Original", Discount.Percentage(10m), November, [haircut, beard], "First");
        await SaveAsync(company.Id, promotion, cancellationToken);

        await UpdateAsync(company.Id, promotion.Id, cancellationToken, loaded =>
        {
            loaded.UpdateDetails("Renamed", null);
            loaded.ChangeDiscount(Discount.FixedAmount(Money.From(10m)));   // replaces the owned value
            loaded.ChangeValidity(december);                                // replaces the owned value
            loaded.SetWeekdays([DayOfWeek.Friday]);
            loaded.ApplyToBranches([branch]);
            loaded.IncludeServices([massage]);
            loaded.RemoveService(haircut.Id);
        });

        var changed = await LoadAsync(company.Id, promotion.Id, cancellationToken);

        changed.ShouldNotBeNull();
        changed.Name.ShouldBe("Renamed");
        changed.Description.ShouldBeNull();
        changed.Discount.ShouldBe(Discount.FixedAmount(Money.From(10m)));
        changed.Validity.ShouldBe(december);
        changed.Weekdays.ShouldBe([DayOfWeek.Friday]);
        changed.AppliesToAllBranches.ShouldBeFalse();
        changed.BranchIds.ShouldBe([branch]);
        changed.ServiceIds.ShouldBe([beard.Id, massage.Id]);

        await UpdateAsync(company.Id, promotion.Id, cancellationToken, loaded =>
        {
            loaded.ApplyToAllBranches();
            loaded.SetWeekdays([]);
            loaded.End();
        });

        var ended = await LoadAsync(company.Id, promotion.Id, cancellationToken);

        ended.ShouldNotBeNull();
        ended.AppliesToAllBranches.ShouldBeTrue();
        ended.BranchIds.ShouldBeEmpty();
        ended.Weekdays.ShouldBeEmpty();
        ended.IsActive.ShouldBeFalse();
    }

    [Fact]
    public async Task Every_weekday_should_survive_the_mask()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var company = await OrganizationData.SeedCompanyAsync(mysql, cancellationToken);
        var promotion = Promotion.CreateForAllBranches(
            company.Id, "Every day", Discount.Percentage(5m), November, [CatalogData.NewFixedService(company.Id)]);
        promotion.SetWeekdays(Enum.GetValues<DayOfWeek>());
        await SaveAsync(company.Id, promotion, cancellationToken);

        var loaded = await LoadAsync(company.Id, promotion.Id, cancellationToken);

        loaded.ShouldNotBeNull();
        loaded.Weekdays.Count.ShouldBe(7);
        loaded.Weekdays.ShouldBe(Enum.GetValues<DayOfWeek>().Order());
    }

    [Fact]
    public async Task A_promotion_should_only_be_found_by_its_own_company()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var company = await OrganizationData.SeedCompanyAsync(mysql, cancellationToken);
        var other = await OrganizationData.SeedCompanyAsync(mysql, cancellationToken);
        var promotion = Promotion.CreateForAllBranches(
            company.Id, "Mine", Discount.Percentage(5m), November, [CatalogData.NewFixedService(company.Id)]);
        await SaveAsync(company.Id, promotion, cancellationToken);

        (await LoadAsync(company.Id, promotion.Id, cancellationToken)).ShouldNotBeNull();
        (await LoadAsync(other.Id, promotion.Id, cancellationToken)).ShouldBeNull();
        (await LoadAsync(null, promotion.Id, cancellationToken)).ShouldBeNull();
    }

    [Fact]
    public async Task The_longest_values_the_domain_allows_should_fit_in_the_columns()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var company = await OrganizationData.SeedCompanyAsync(mysql, cancellationToken);
        var promotion = Promotion.CreateForAllBranches(
            company.Id,
            new string('N', 100),
            Discount.Percentage(100m),
            November,
            [CatalogData.NewFixedService(company.Id)],
            new string('D', 1000));
        await SaveAsync(company.Id, promotion, cancellationToken);

        var loaded = await LoadAsync(company.Id, promotion.Id, cancellationToken);

        loaded.ShouldNotBeNull();
        loaded.Name.Length.ShouldBe(100);
        loaded.Description!.Length.ShouldBe(1000);
        loaded.Discount.ShouldBe(Discount.Percentage(100m));
    }

    private Task SaveAsync(Guid companyId, Promotion promotion, CancellationToken cancellationToken) =>
        mysql.SaveAsync(companyId, cancellationToken,
            services => services.GetRequiredService<IPromotionRepository>().Add(promotion));

    private async Task UpdateAsync(Guid companyId, Guid id, CancellationToken cancellationToken, Action<Promotion> change)
    {
        await using var scope = mysql.CreateScope(companyId);
        var loaded = await scope.ServiceProvider.GetRequiredService<IPromotionRepository>().GetByIdAsync(id, cancellationToken);

        change(loaded!);
        await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync(cancellationToken);
    }

    private async Task<Promotion?> LoadAsync(Guid? companyId, Guid id, CancellationToken cancellationToken)
    {
        await using var scope = mysql.CreateScope(companyId);

        return await scope.ServiceProvider.GetRequiredService<IPromotionRepository>().GetByIdAsync(id, cancellationToken);
    }
}
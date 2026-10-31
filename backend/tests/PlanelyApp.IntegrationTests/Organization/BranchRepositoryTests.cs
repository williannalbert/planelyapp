using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PlanelyApp.Domain.Organization.Branches;
using PlanelyApp.Domain.SeedWork;
using PlanelyApp.Domain.SharedKernel;
using Shouldly;

namespace PlanelyApp.IntegrationTests.Organization;

public class BranchRepositoryTests(MySqlFixture mysql)
{
    [Fact]
    public async Task A_saved_branch_should_come_back_with_address_and_business_hours()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var company = await OrganizationData.SeedCompanyAsync(mysql, cancellationToken);

        var branch = OrganizationData.NewBranch(company.Id);
        branch.SetBusinessHours(OrganizationData.WorkDay(DayOfWeek.Monday));
        branch.SetBusinessHours(OrganizationData.WorkDay(DayOfWeek.Tuesday, withBreaks: false));
        await SaveAsync(company.Id, branch, cancellationToken);

        var loaded = await LoadAsync(company.Id, branch.Id, cancellationToken);

        loaded.ShouldNotBeNull();
        loaded.CompanyId.ShouldBe(company.Id);
        loaded.Name.ShouldBe("Centro");
        loaded.Address.ShouldBe(branch.Address);
        loaded.Phone.ShouldBe(branch.Phone);
        loaded.TimeZoneId.ShouldBe("America/Sao_Paulo");
        loaded.IsActive.ShouldBeTrue();

        // Looked up by day: the order of the list after a reload is the order of insertion.
        loaded.BusinessHours.Count.ShouldBe(2);
        loaded.GetBusinessHours(DayOfWeek.Monday).ShouldBe(branch.GetBusinessHours(DayOfWeek.Monday));
        loaded.GetBusinessHours(DayOfWeek.Tuesday).ShouldBe(branch.GetBusinessHours(DayOfWeek.Tuesday));
        loaded.GetBusinessHours(DayOfWeek.Wednesday).ShouldBeNull();

        var breaks = loaded.GetBusinessHours(DayOfWeek.Monday)!.Breaks;
        breaks.Count.ShouldBe(2);
        breaks[0].Range.Start.ShouldBe(new TimeOnly(12, 0));
        breaks[0].Description.ShouldBe("Lunch");
        breaks[1].Description.ShouldBeNull();
    }

    [Fact]
    public async Task Replacing_the_hours_of_a_weekday_should_replace_the_row()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var company = await OrganizationData.SeedCompanyAsync(mysql, cancellationToken);

        var branch = OrganizationData.NewBranch(company.Id);
        branch.SetBusinessHours(OrganizationData.WorkDay(DayOfWeek.Monday));
        branch.SetBusinessHours(OrganizationData.WorkDay(DayOfWeek.Tuesday, withBreaks: false));
        await SaveAsync(company.Id, branch, cancellationToken);

        var shorter = DailySchedule.Create(DayOfWeek.Monday, TimeOfDayRange.Create(new TimeOnly(8, 0), new TimeOnly(12, 0)));

        await using (var scope = mysql.CreateScope(company.Id))
        {
            var loadedBranch = await scope.ServiceProvider.GetRequiredService<IBranchRepository>()
                .GetByIdAsync(branch.Id, cancellationToken);

            loadedBranch!.SetBusinessHours(shorter);
            await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync(cancellationToken);
        }

        var reloaded = await LoadAsync(company.Id, branch.Id, cancellationToken);

        reloaded.ShouldNotBeNull();
        reloaded.BusinessHours.Count.ShouldBe(2);
        reloaded.GetBusinessHours(DayOfWeek.Monday).ShouldBe(shorter);
        reloaded.GetBusinessHours(DayOfWeek.Monday)!.Breaks.ShouldBeEmpty();
    }

    [Fact]
    public async Task Closing_a_weekday_should_remove_its_row()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var company = await OrganizationData.SeedCompanyAsync(mysql, cancellationToken);

        var branch = OrganizationData.NewBranch(company.Id);
        branch.SetBusinessHours(OrganizationData.WorkDay(DayOfWeek.Monday));
        branch.SetBusinessHours(OrganizationData.WorkDay(DayOfWeek.Tuesday));
        await SaveAsync(company.Id, branch, cancellationToken);

        await using (var scope = mysql.CreateScope(company.Id))
        {
            var loadedBranch = await scope.ServiceProvider.GetRequiredService<IBranchRepository>()
                .GetByIdAsync(branch.Id, cancellationToken);

            loadedBranch!.RemoveBusinessHours(DayOfWeek.Tuesday);
            await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync(cancellationToken);
        }

        var reloaded = await LoadAsync(company.Id, branch.Id, cancellationToken);

        reloaded.ShouldNotBeNull();
        reloaded.BusinessHours.ShouldHaveSingleItem().Day.ShouldBe(DayOfWeek.Monday);
    }

    [Fact]
    public async Task A_branch_should_only_be_found_by_its_own_company()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var company = await OrganizationData.SeedCompanyAsync(mysql, cancellationToken);
        var other = await OrganizationData.SeedCompanyAsync(mysql, cancellationToken);

        var branch = OrganizationData.NewBranch(company.Id);
        await SaveAsync(company.Id, branch, cancellationToken);

        (await LoadAsync(company.Id, branch.Id, cancellationToken)).ShouldNotBeNull();
        (await LoadAsync(other.Id, branch.Id, cancellationToken)).ShouldBeNull();
        (await LoadAsync(null, branch.Id, cancellationToken)).ShouldBeNull();
    }

    [Fact]
    public async Task Two_branches_of_a_company_should_not_share_a_name()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var company = await OrganizationData.SeedCompanyAsync(mysql, cancellationToken);
        await SaveAsync(company.Id, OrganizationData.NewBranch(company.Id, "Centro"), cancellationToken);

        // Same name, different case: the collation does not tell them apart.
        await Should.ThrowAsync<DbUpdateException>(
            () => SaveAsync(company.Id, OrganizationData.NewBranch(company.Id, "CENTRO"), cancellationToken));
    }

    [Fact]
    public async Task Two_companies_may_have_a_branch_with_the_same_name()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var first = await OrganizationData.SeedCompanyAsync(mysql, cancellationToken);
        var second = await OrganizationData.SeedCompanyAsync(mysql, cancellationToken);

        await SaveAsync(first.Id, OrganizationData.NewBranch(first.Id, "Centro"), cancellationToken);
        await SaveAsync(second.Id, OrganizationData.NewBranch(second.Id, "Centro"), cancellationToken);
    }

    [Fact]
    public async Task A_branch_of_a_company_that_does_not_exist_should_not_be_saved()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var missingCompany = Guid.CreateVersion7();

        await Should.ThrowAsync<DbUpdateException>(
            () => SaveAsync(missingCompany, OrganizationData.NewBranch(missingCompany), cancellationToken));
    }

    [Fact]
    public async Task The_longest_values_the_domain_allows_should_fit_in_the_columns()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var company = await OrganizationData.SeedCompanyAsync(mysql, cancellationToken);

        var address = Address.Create(
            new string('R', 150),
            new string('9', 20),
            new string('C', 100),
            new string('D', 100),
            new string('M', 100),
            "RJ",
            "20040-020");

        var branch = Branch.Create(
            company.Id,
            new string('F', 100),
            address,
            Phone.Create("+123456789012345"),
            "America/Sao_Paulo");

        await SaveAsync(company.Id, branch, cancellationToken);

        var loaded = await LoadAsync(company.Id, branch.Id, cancellationToken);

        loaded.ShouldNotBeNull();
        loaded.Name.Length.ShouldBe(100);
        loaded.Address.ShouldBe(address);
    }

    private async Task SaveAsync(Guid companyId, Branch branch, CancellationToken cancellationToken)
    {
        await using var scope = mysql.CreateScope(companyId);

        scope.ServiceProvider.GetRequiredService<IBranchRepository>().Add(branch);
        await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync(cancellationToken);
    }

    private async Task<Branch?> LoadAsync(Guid? companyId, Guid branchId, CancellationToken cancellationToken)
    {
        await using var scope = mysql.CreateScope(companyId);

        return await scope.ServiceProvider.GetRequiredService<IBranchRepository>()
            .GetByIdAsync(branchId, cancellationToken);
    }

    [Fact]
    public async Task The_name_check_should_see_only_the_company_of_the_request()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var company = await OrganizationData.SeedCompanyAsync(mysql, cancellationToken);
        var other = await OrganizationData.SeedCompanyAsync(mysql, cancellationToken);
        var branch = OrganizationData.NewBranch(company.Id, "Centro");
        await SaveAsync(company.Id, branch, cancellationToken);

        await using var own = mysql.CreateScope(company.Id);
        var ownBranches = own.ServiceProvider.GetRequiredService<IBranchRepository>();

        (await ownBranches.ExistsByNameAsync("Centro", null, cancellationToken)).ShouldBeTrue();
        (await ownBranches.ExistsByNameAsync("CENTRO", null, cancellationToken)).ShouldBeTrue();
        (await ownBranches.ExistsByNameAsync("Centro", branch.Id, cancellationToken)).ShouldBeFalse();
        (await ownBranches.ExistsByNameAsync("Norte", null, cancellationToken)).ShouldBeFalse();

        await using var foreign = mysql.CreateScope(other.Id);
        (await foreign.ServiceProvider.GetRequiredService<IBranchRepository>()
            .ExistsByNameAsync("Centro", null, cancellationToken)).ShouldBeFalse();
    }
}
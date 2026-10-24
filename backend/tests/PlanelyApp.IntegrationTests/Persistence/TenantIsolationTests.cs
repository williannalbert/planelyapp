using Microsoft.EntityFrameworkCore;
using PlanelyApp.Infrastructure.Tenancy;
using PlanelyApp.IntegrationTests.Persistence.Support;
using Shouldly;

namespace PlanelyApp.IntegrationTests.Persistence;

public class TenantIsolationTests(MySqlFixture mysql) : IAsyncLifetime
{
    private WidgetHost _host = null!;

    public async ValueTask InitializeAsync() =>
        _host = await WidgetHost.CreateAsync(mysql, TestContext.Current.CancellationToken);

    public ValueTask DisposeAsync() => _host.DisposeAsync();

    [Fact]
    public async Task A_query_should_only_return_rows_of_the_current_company()
    {
        var (companyA, companyB) = (Guid.CreateVersion7(), Guid.CreateVersion7());
        var idA = await _host.SeedWidgetAsync(companyA, TestContext.Current.CancellationToken, "A");
        await _host.SeedWidgetAsync(companyB, TestContext.Current.CancellationToken, "B");

        await using var scope = _host.CreateScope(companyA);
        var visible = await scope.Db().Set<Widget>().ToListAsync(TestContext.Current.CancellationToken);

        visible.ShouldHaveSingleItem().Id.ShouldBe(idA);
    }

    [Fact]
    public async Task Without_a_company_in_the_scope_no_row_should_be_visible()
    {
        await _host.SeedWidgetAsync(Guid.CreateVersion7(), TestContext.Current.CancellationToken);

        await using var scope = _host.CreateScope(); // no tenant: the filter must fail closed
        var visible = await scope.Db().Set<Widget>().ToListAsync(TestContext.Current.CancellationToken);

        visible.ShouldBeEmpty();
    }

    [Fact]
    public async Task A_row_of_another_company_should_not_be_found_by_id()
    {
        var (companyA, companyB) = (Guid.CreateVersion7(), Guid.CreateVersion7());
        var idOfB = await _host.SeedWidgetAsync(companyB, TestContext.Current.CancellationToken);

        await using var scope = _host.CreateScope(companyA);
        var found = await scope.Db().Set<Widget>().FirstOrDefaultAsync(w => w.Id == idOfB, TestContext.Current.CancellationToken);

        found.ShouldBeNull();
    }

    [Fact]
    public async Task Ignoring_the_filter_on_purpose_should_see_every_company()
    {
        var (companyA, companyB) = (Guid.CreateVersion7(), Guid.CreateVersion7());
        await _host.SeedWidgetAsync(companyA, TestContext.Current.CancellationToken);
        await _host.SeedWidgetAsync(companyB, TestContext.Current.CancellationToken);

        await using var scope = _host.CreateScope(companyA);
        var all = await scope.Db().Set<Widget>()
            .IgnoreQueryFilters()
            .Where(w => w.CompanyId == companyA || w.CompanyId == companyB)
            .ToListAsync(TestContext.Current.CancellationToken);

        all.Count.ShouldBe(2);
    }

    [Fact]
    public async Task Saving_an_entity_of_another_company_should_be_refused()
    {
        var (companyA, companyB) = (Guid.CreateVersion7(), Guid.CreateVersion7());

        await using var scope = _host.CreateScope(companyA);
        var context = scope.Db();
        context.Add(new Widget(companyB, "Intruder", WidgetStatus.Draft, DateTime.UtcNow));

        await Should.ThrowAsync<TenantViolationException>(() => context.SaveChangesAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Saving_an_entity_without_a_company_in_the_scope_should_be_refused()
    {
        await using var scope = _host.CreateScope();
        var context = scope.Db();
        context.Add(new Widget(Guid.CreateVersion7(), "Orphan", WidgetStatus.Draft, DateTime.UtcNow));

        await Should.ThrowAsync<TenantViolationException>(() => context.SaveChangesAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Changing_a_row_of_another_company_should_be_refused()
    {
        var (companyA, companyB) = (Guid.CreateVersion7(), Guid.CreateVersion7());
        var idOfB = await _host.SeedWidgetAsync(companyB, TestContext.Current.CancellationToken, "Original");

        await using var scope = _host.CreateScope(companyA);
        var context = scope.Db();

        // Loaded by an unfiltered query, as a buggy handler might do.
        var foreign = await context.Set<Widget>().IgnoreQueryFilters().SingleAsync(w => w.Id == idOfB, TestContext.Current.CancellationToken);
        foreign.Rename("Hacked");

        await Should.ThrowAsync<TenantViolationException>(() => context.SaveChangesAsync(TestContext.Current.CancellationToken));
    }
}
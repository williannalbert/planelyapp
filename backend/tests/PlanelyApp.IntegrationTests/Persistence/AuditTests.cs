using Microsoft.EntityFrameworkCore;
using PlanelyApp.Infrastructure.Persistence.Interceptors;
using PlanelyApp.IntegrationTests.Persistence.Support;
using Shouldly;

namespace PlanelyApp.IntegrationTests.Persistence;

public class AuditTests(MySqlFixture mysql) : IAsyncLifetime
{
    private WidgetHost _host = null!;

    public async ValueTask InitializeAsync() =>
        _host = await WidgetHost.CreateAsync(mysql, TestContext.Current.CancellationToken);

    public ValueTask DisposeAsync() => _host.DisposeAsync();

    [Fact]
    public async Task A_new_aggregate_should_get_created_and_updated_at_from_the_clock()
    {
        var company = Guid.CreateVersion7();
        var id = await _host.SeedWidgetAsync(company, TestContext.Current.CancellationToken);

        var (createdAt, updatedAt) = await ReadAuditAsync(company, id);

        createdAt.ShouldBe(_host.Clock.GetUtcNow().UtcDateTime);
        updatedAt.ShouldBe(createdAt);
    }

    [Fact]
    public async Task Changing_an_aggregate_should_move_updated_at_and_keep_created_at()
    {
        var company = Guid.CreateVersion7();
        var id = await _host.SeedWidgetAsync(company, TestContext.Current.CancellationToken);
        var created = _host.Clock.GetUtcNow().UtcDateTime;

        _host.Clock.Advance(TimeSpan.FromHours(2));

        await using (var scope = _host.CreateScope(company))
        {
            var context = scope.Db();
            var widget = await context.Set<Widget>().SingleAsync(w => w.Id == id, TestContext.Current.CancellationToken);
            widget.Rename("Renamed");
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var (createdAt, updatedAt) = await ReadAuditAsync(company, id);

        createdAt.ShouldBe(created);
        updatedAt.ShouldBe(created.AddHours(2));
    }

    [Fact]
    public async Task Saving_without_changes_should_not_touch_updated_at()
    {
        var company = Guid.CreateVersion7();
        var id = await _host.SeedWidgetAsync(company, TestContext.Current.CancellationToken);
        var created = _host.Clock.GetUtcNow().UtcDateTime;

        _host.Clock.Advance(TimeSpan.FromHours(2));

        await using (var scope = _host.CreateScope(company))
        {
            var context = scope.Db();
            await context.Set<Widget>().SingleAsync(w => w.Id == id, TestContext.Current.CancellationToken);
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var (_, updatedAt) = await ReadAuditAsync(company, id);

        updatedAt.ShouldBe(created);
    }

    private async Task<(DateTime CreatedAt, DateTime UpdatedAt)> ReadAuditAsync(Guid company, Guid id)
    {
        await using var scope = _host.CreateScope(company);
        var context = scope.Db();

        var widget = await context.Set<Widget>().SingleAsync(w => w.Id == id, TestContext.Current.CancellationToken);
        var entry = context.Entry(widget);

        return (
            entry.Property<DateTime>(AuditProperties.CreatedAt).CurrentValue,
            entry.Property<DateTime>(AuditProperties.UpdatedAt).CurrentValue);
    }
}
using Microsoft.EntityFrameworkCore;
using PlanelyApp.Infrastructure.Persistence.Conventions;
using PlanelyApp.IntegrationTests.Persistence.Support;
using Shouldly;

namespace PlanelyApp.IntegrationTests.Persistence;

public class ConventionTests(MySqlFixture mysql) : IAsyncLifetime
{
    private WidgetHost _host = null!;

    public async ValueTask InitializeAsync() =>
        _host = await WidgetHost.CreateAsync(mysql, TestContext.Current.CancellationToken);

    public ValueTask DisposeAsync() => _host.DisposeAsync();

    [Fact]
    public async Task Tables_should_be_named_after_the_class_in_snake_case()
    {
        var tables = await QueryAsync(
            $"SELECT table_name AS Value FROM information_schema.tables WHERE table_schema = DATABASE() AND table_name LIKE 'widget%'");

        string[] expected = ["widget", "widget_part"];
        tables.Order(StringComparer.Ordinal).ShouldBe(expected);
    }

    [Fact]
    public async Task Columns_should_be_named_after_the_properties_in_snake_case()
    {
        var widget = await ColumnsOfAsync("widget");
        var part = await ColumnsOfAsync("widget_part");

        string[] expectedWidget =
            ["archived_at_utc", "company_id", "created_at", "id", "name", "scheduled_at_utc", "status", "updated_at"];
        string[] expectedPart = ["id", "name", "widget_id"];

        widget.ShouldBe(expectedWidget);
        part.ShouldBe(expectedPart);
    }

    [Theory]
    [InlineData("id", "char(36)")]
    [InlineData("status", "varchar(30)")]
    [InlineData("scheduled_at_utc", "datetime(6)")]
    [InlineData("archived_at_utc", "datetime(6)")]
    [InlineData("created_at", "datetime(6)")]
    [InlineData("updated_at", "datetime(6)")]
    public async Task Columns_should_have_the_agreed_types(string column, string expectedType)
    {
        var types = await QueryAsync(
            $"SELECT column_type AS Value FROM information_schema.columns WHERE table_schema = DATABASE() AND table_name = 'widget' AND column_name = {column}");

        types.ShouldHaveSingleItem().ShouldBe(expectedType);
    }

    [Fact]
    public async Task An_enum_should_be_stored_as_its_name()
    {
        var id = await _host.SeedWidgetAsync(Guid.CreateVersion7(), TestContext.Current.CancellationToken, status: WidgetStatus.InReview);

        var stored = await QueryAsync($"SELECT status AS Value FROM widget WHERE id = {id.ToString()}");

        stored.ShouldHaveSingleItem().ShouldBe("InReview");
    }

    [Fact]
    public async Task Dates_should_round_trip_as_utc_with_microsecond_precision()
    {
        var company = Guid.CreateVersion7();
        var scheduled = new DateTime(2026, 3, 10, 12, 0, 0, DateTimeKind.Utc).AddTicks(1_234_560); // .123456 s
        var id = await _host.SeedWidgetAsync(company, TestContext.Current.CancellationToken, scheduledAtUtc: scheduled);

        await using var scope = _host.CreateScope(company);
        var loaded = await scope.Db().Set<Widget>().SingleAsync(w => w.Id == id, TestContext.Current.CancellationToken);

        loaded.ScheduledAtUtc.ShouldBe(scheduled);
        loaded.ScheduledAtUtc.Kind.ShouldBe(DateTimeKind.Utc);
        loaded.ArchivedAtUtc.ShouldBeNull();
    }

    [Theory]
    [InlineData(DateTimeKind.Local)]
    [InlineData(DateTimeKind.Unspecified)]
    public async Task A_date_that_is_not_utc_should_be_refused(DateTimeKind kind)
    {
        var company = Guid.CreateVersion7();
        await using var scope = _host.CreateScope(company);
        var context = scope.Db();
        context.Add(new Widget(company, "Local", WidgetStatus.Draft, new DateTime(2026, 3, 10, 12, 0, 0, kind)));

        var failure = await Should.ThrowAsync<Exception>(() => context.SaveChangesAsync(TestContext.Current.CancellationToken));

        // EF may wrap the exception; what matters is that ours is in the chain.
        Chain(failure).ShouldContain(e => e is NonUtcDateTimeException);
    }

    [Fact]
    public async Task A_part_added_to_a_loaded_aggregate_should_be_inserted_not_updated()
    {
        var company = Guid.CreateVersion7();
        var id = await _host.SeedWidgetAsync(company, TestContext.Current.CancellationToken);

        await using (var scope = _host.CreateScope(company))
        {
            var context = scope.Db();
            var widget = await context.Set<Widget>().Include(w => w.Parts).SingleAsync(w => w.Id == id, TestContext.Current.CancellationToken);

            widget.AddPart("handle");

            // With the Guid key generated "on add", EF would send an UPDATE and fail with DbUpdateConcurrencyException.
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using var check = _host.CreateScope(company);
        var reloaded = await check.Db().Set<Widget>().Include(w => w.Parts).SingleAsync(w => w.Id == id, TestContext.Current.CancellationToken);

        reloaded.Parts.ShouldHaveSingleItem().Name.ShouldBe("handle");
    }

    private async Task<List<string>> ColumnsOfAsync(string table)
    {
        var columns = await QueryAsync(
            $"SELECT column_name AS Value FROM information_schema.columns WHERE table_schema = DATABASE() AND table_name = {table}");

        return columns.Order(StringComparer.Ordinal).ToList();
    }

    private async Task<List<string>> QueryAsync(FormattableString sql)
    {
        await using var scope = _host.CreateScope();
        return await scope.Db().Database.SqlQuery<string>(sql).ToListAsync(TestContext.Current.CancellationToken);
    }

    private static IEnumerable<Exception> Chain(Exception? exception)
    {
        for (; exception is not null; exception = exception.InnerException)
        {
            yield return exception;
        }
    }
}
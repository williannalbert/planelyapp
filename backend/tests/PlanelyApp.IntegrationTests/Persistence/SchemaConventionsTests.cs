using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PlanelyApp.Infrastructure.Persistence;
using Shouldly;

namespace PlanelyApp.IntegrationTests.Persistence;

/// <summary>Rules over the whole schema that migrations applied, including the indexes written by hand.</summary>
public class SchemaConventionsTests(MySqlFixture mysql)
{
    [Fact]
    public async Task Tables_should_be_lowercase()
    {
        var names = await QueryAsync(
            $"SELECT table_name AS Value FROM information_schema.tables WHERE table_schema = DATABASE() AND table_name <> '__EFMigrationsHistory' AND CAST(table_name AS BINARY) <> CAST(LOWER(table_name) AS BINARY)");

        names.ShouldBeEmpty();
    }

    [Fact]
    public async Task Columns_should_be_lowercase()
    {
        var names = await QueryAsync(
            $"SELECT CONCAT(table_name, '.', column_name) AS Value FROM information_schema.columns WHERE table_schema = DATABASE() AND table_name <> '__EFMigrationsHistory' AND CAST(column_name AS BINARY) <> CAST(LOWER(column_name) AS BINARY)");

        names.ShouldBeEmpty();
    }

    [Fact]
    public async Task Indexes_should_be_lowercase()
    {
        var names = await QueryAsync(
            $"SELECT DISTINCT CONCAT(table_name, '.', index_name) AS Value FROM information_schema.statistics WHERE table_schema = DATABASE() AND table_name <> '__EFMigrationsHistory' AND index_name <> 'PRIMARY' AND CAST(index_name AS BINARY) <> CAST(LOWER(index_name) AS BINARY)");

        names.ShouldBeEmpty();
    }

    [Fact]
    public async Task Every_table_should_have_a_primary_key()
    {
        var tables = await QueryAsync(
            $"SELECT t.table_name AS Value FROM information_schema.tables t LEFT JOIN information_schema.table_constraints c ON c.table_schema = t.table_schema AND c.table_name = t.table_name AND c.constraint_type = 'PRIMARY KEY' WHERE t.table_schema = DATABASE() AND t.table_type = 'BASE TABLE' AND c.constraint_name IS NULL");

        tables.ShouldBeEmpty();
    }

    [Fact]
    public async Task Every_datetime_column_should_keep_microseconds()
    {
        var columns = await QueryAsync(
            $"SELECT CONCAT(table_name, '.', column_name) AS Value FROM information_schema.columns WHERE table_schema = DATABASE() AND data_type IN ('datetime', 'timestamp') AND column_type <> 'datetime(6)'");

        columns.ShouldBeEmpty();
    }

    private async Task<List<string>> QueryAsync(FormattableString sql)
    {
        await using var scope = mysql.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<PlanelyDbContext>();

        return await context.Database.SqlQuery<string>(sql).ToListAsync(TestContext.Current.CancellationToken);
    }
}
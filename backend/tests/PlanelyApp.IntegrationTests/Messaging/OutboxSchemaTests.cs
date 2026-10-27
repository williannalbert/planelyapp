using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PlanelyApp.Infrastructure.Persistence;
using Shouldly;

namespace PlanelyApp.IntegrationTests.Messaging;

public class OutboxSchemaTests(MySqlFixture mysql)
{
    [Fact]
    public async Task The_table_should_have_exactly_the_agreed_columns()
    {
        var actual = await QueryAsync(
            $"SELECT column_name AS Value FROM information_schema.columns WHERE table_schema = DATABASE() AND table_name = {"outbox_messages"}");

        // No created_at / updated_at: the outbox is not an aggregate.
        actual.Order(StringComparer.Ordinal).ShouldBe(
            new[]
            {
                "id", "type", "version", "company_id", "payload", "occurred_at_utc", "processed_at_utc", "attempts", "error",
            }.Order(StringComparer.Ordinal));
    }

    [Theory]
    [InlineData("id", "char(36)")]
    [InlineData("type", "varchar(100)")]
    [InlineData("version", "int")]
    [InlineData("company_id", "char(36)")]
    [InlineData("payload", "json")]
    [InlineData("occurred_at_utc", "datetime(6)")]
    [InlineData("processed_at_utc", "datetime(6)")]
    [InlineData("attempts", "int")]
    [InlineData("error", "varchar(2000)")]
    public async Task Columns_should_have_the_agreed_types(string column, string expectedType)
    {
        var types = await QueryAsync(
            $"SELECT column_type AS Value FROM information_schema.columns WHERE table_schema = DATABASE() AND table_name = {"outbox_messages"} AND column_name = {column}");

        types.ShouldHaveSingleItem().ShouldBe(expectedType);
    }

    [Fact]
    public async Task The_pending_index_should_have_its_columns_in_order()
    {
        var columns = await QueryAsync(
            $"SELECT column_name AS Value FROM information_schema.statistics WHERE table_schema = DATABASE() AND table_name = {"outbox_messages"} AND index_name = {"ix_outbox_messages_processed_at_utc_occurred_at_utc"} ORDER BY seq_in_index");

        string.Join(",", columns).ShouldBe("processed_at_utc,occurred_at_utc");
    }

    [Fact]
    public async Task The_table_should_have_no_foreign_keys()
    {
        var targets = await QueryAsync(
            $"SELECT DISTINCT referenced_table_name AS Value FROM information_schema.key_column_usage WHERE table_schema = DATABASE() AND table_name = {"outbox_messages"} AND referenced_table_name IS NOT NULL");

        targets.ShouldBeEmpty();
    }

    private async Task<List<string>> QueryAsync(FormattableString sql)
    {
        await using var scope = mysql.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<PlanelyDbContext>();

        return await context.Database.SqlQuery<string>(sql).ToListAsync(TestContext.Current.CancellationToken);
    }
}
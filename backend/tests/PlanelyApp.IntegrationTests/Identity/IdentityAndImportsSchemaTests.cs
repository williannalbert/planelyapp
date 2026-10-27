using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PlanelyApp.Infrastructure.Persistence;
using Shouldly;

namespace PlanelyApp.IntegrationTests.Identity;

public class IdentityAndImportsSchemaTests(MySqlFixture mysql)
{
    [Fact]
    public async Task The_tables_should_have_the_expected_columns()
    {
        await AssertColumnsAsync(
            "access_code",
            "id", "company_id", "channel", "destination", "code_hash", "created_at_utc", "expires_at_utc", "attempts",
            "used_at_utc", "created_at", "updated_at");
        await AssertColumnsAsync(
            "customer_session",
            "id", "company_id", "customer_id", "refresh_token_hash", "created_at_utc", "expires_at_utc", "revoked_at_utc",
            "created_at", "updated_at");
        await AssertColumnsAsync(
            "data_import",
            "id", "company_id", "type", "file_name", "storage_path", "status", "total_rows", "success_rows", "error_rows",
            "failure_reason", "requested_by", "created_at_utc", "completed_at_utc", "created_at", "updated_at");
        await AssertColumnsAsync("data_import_error", "id", "data_import_id", "line_number", "reason");
    }

    [Theory]
    [InlineData("access_code", "channel", "varchar(30)")]
    [InlineData("access_code", "destination", "varchar(254)")]
    [InlineData("access_code", "code_hash", "char(64)")]
    [InlineData("access_code", "attempts", "int")]
    [InlineData("access_code", "used_at_utc", "datetime(6)")]
    [InlineData("customer_session", "refresh_token_hash", "char(64)")]
    [InlineData("customer_session", "expires_at_utc", "datetime(6)")]
    [InlineData("customer_session", "revoked_at_utc", "datetime(6)")]
    [InlineData("data_import", "type", "varchar(30)")]
    [InlineData("data_import", "status", "varchar(30)")]
    [InlineData("data_import", "file_name", "varchar(255)")]
    [InlineData("data_import", "storage_path", "varchar(500)")]
    [InlineData("data_import", "failure_reason", "varchar(500)")]
    [InlineData("data_import", "completed_at_utc", "datetime(6)")]
    [InlineData("data_import_error", "line_number", "int")]
    [InlineData("data_import_error", "reason", "varchar(500)")]
    public async Task Columns_should_have_the_agreed_types(string table, string column, string expectedType)
    {
        var types = await QueryAsync(
            $"SELECT column_type AS Value FROM information_schema.columns WHERE table_schema = DATABASE() AND table_name = {table} AND column_name = {column}");

        types.ShouldHaveSingleItem().ShouldBe(expectedType);
    }

    [Fact]
    public async Task The_refresh_token_hash_should_be_unique()
    {
        var indexes = await QueryAsync(
            $"SELECT DISTINCT index_name AS Value FROM information_schema.statistics WHERE table_schema = DATABASE() AND table_name = {"customer_session"} AND non_unique = 0");

        indexes.ShouldContain("ix_customer_session_refresh_token_hash");
    }

    [Fact]
    public async Task The_access_code_index_should_have_its_columns_in_order()
    {
        var columns = await QueryAsync(
            $"SELECT column_name AS Value FROM information_schema.statistics WHERE table_schema = DATABASE() AND table_name = {"access_code"} AND index_name = {"ix_access_code_company_id_destination_created_at_utc"} ORDER BY seq_in_index");

        string.Join(",", columns).ShouldBe("company_id,destination,created_at_utc");
    }

    [Theory]
    [InlineData("access_code", "company")]
    [InlineData("customer_session", "company,customer")]
    [InlineData("data_import", "company")]
    [InlineData("data_import_error", "data_import")]
    public async Task Foreign_keys_should_point_only_where_the_design_says(string table, string expectedTargets)
    {
        var targets = await QueryAsync(
            $"SELECT DISTINCT referenced_table_name AS Value FROM information_schema.key_column_usage WHERE table_schema = DATABASE() AND table_name = {table} AND referenced_table_name IS NOT NULL");

        string.Join(",", targets.Order(StringComparer.Ordinal)).ShouldBe(expectedTargets);
    }

    private async Task AssertColumnsAsync(string table, params string[] expected)
    {
        var actual = await QueryAsync(
            $"SELECT column_name AS Value FROM information_schema.columns WHERE table_schema = DATABASE() AND table_name = {table}");

        actual.Order(StringComparer.Ordinal).ShouldBe(expected.Order(StringComparer.Ordinal));
    }

    private async Task<List<string>> QueryAsync(FormattableString sql)
    {
        await using var scope = mysql.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<PlanelyDbContext>();

        return await context.Database.SqlQuery<string>(sql).ToListAsync(TestContext.Current.CancellationToken);
    }
}
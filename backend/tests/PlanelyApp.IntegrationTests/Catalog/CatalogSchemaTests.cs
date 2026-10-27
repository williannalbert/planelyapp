using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PlanelyApp.Infrastructure.Persistence;
using Shouldly;

namespace PlanelyApp.IntegrationTests.Catalog;

public class CatalogSchemaTests(MySqlFixture mysql)
{
    [Fact]
    public async Task The_catalog_tables_should_have_the_expected_columns()
    {
        await AssertColumnsAsync("service_category", "id", "company_id", "name", "created_at", "updated_at");
        await AssertColumnsAsync(
            "service_offering",
            "id", "company_id", "category_id", "name", "description", "standard_duration_minutes", "pricing_type",
            "base_price", "is_active", "created_at", "updated_at");
        await AssertColumnsAsync(
            "promotion",
            "id", "company_id", "kind", "name", "description", "discount_type", "discount_value", "valid_from",
            "valid_until", "days_of_week_mask", "applies_to_all_branches", "branch_ids", "service_ids", "is_active",
            "created_at", "updated_at");
        await AssertColumnsAsync("branch_offering_override", "id", "branch_id", "service_offering_id", "custom_price", "is_available");
    }

    [Theory]
    [InlineData("service_offering", "standard_duration_minutes", "int")]
    [InlineData("service_offering", "pricing_type", "varchar(30)")]
    [InlineData("service_offering", "description", "varchar(2000)")]
    [InlineData("service_offering", "base_price", "decimal(12,2)")]
    [InlineData("promotion", "kind", "varchar(30)")]
    [InlineData("promotion", "description", "varchar(1000)")]
    [InlineData("promotion", "discount_type", "varchar(30)")]
    [InlineData("promotion", "discount_value", "decimal(12,2)")]
    [InlineData("promotion", "valid_from", "date")]
    [InlineData("promotion", "valid_until", "date")]
    [InlineData("promotion", "days_of_week_mask", "tinyint unsigned")]
    [InlineData("promotion", "branch_ids", "json")]
    [InlineData("promotion", "service_ids", "json")]
    [InlineData("branch_offering_override", "custom_price", "decimal(12,2)")]
    public async Task Columns_should_have_the_agreed_types(string table, string column, string expectedType)
    {
        var types = await QueryAsync(
            $"SELECT column_type AS Value FROM information_schema.columns WHERE table_schema = DATABASE() AND table_name = {table} AND column_name = {column}");

        types.ShouldHaveSingleItem().ShouldBe(expectedType);
    }

    [Theory]
    [InlineData("service_offering", "ix_service_offering_company_id_name")]
    [InlineData("branch_offering_override", "ix_branch_offering_override_branch_id_service_offering_id")]
    public async Task Unique_indexes_should_exist_with_snake_case_names(string table, string indexName)
    {
        var indexes = await QueryAsync(
            $"SELECT DISTINCT index_name AS Value FROM information_schema.statistics WHERE table_schema = DATABASE() AND table_name = {table} AND non_unique = 0");

        indexes.ShouldContain(indexName);
    }

    [Fact]
    public async Task The_promotion_index_should_exist_and_not_be_unique()
    {
        var indexes = await QueryAsync(
            $"SELECT DISTINCT index_name AS Value FROM information_schema.statistics WHERE table_schema = DATABASE() AND table_name = {"promotion"} AND non_unique = 1");

        indexes.ShouldContain("ix_promotion_company_id_is_active");
    }

    // Only ids inside the same module (or the company) are foreign keys; the service id of an override is not.
    [Theory]
    [InlineData("service_category", "company")]
    [InlineData("service_offering", "company,service_category")]
    [InlineData("promotion", "company")]
    [InlineData("branch_offering_override", "branch")]
    public async Task Foreign_keys_should_point_only_where_the_module_allows(string table, string expectedTargets)
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
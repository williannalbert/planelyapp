using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PlanelyApp.Infrastructure.Persistence;
using Shouldly;

namespace PlanelyApp.IntegrationTests.Organization;

public class OrganizationSchemaTests(MySqlFixture mysql)
{
    [Fact]
    public async Task The_company_table_should_have_the_expected_columns() =>
        await AssertColumnsAsync(
            "company",
            "id", "trade_name", "legal_name", "cnpj", "email", "phone", "default_language",
            "booking_policy_minimum_notice_hours", "booking_policy_maximum_window_days",
            "booking_policy_cancellation_deadline_hours", "booking_policy_interval_between_appointments_minutes",
            "booking_policy_travel_time_between_branches_minutes", "booking_policy_requires_confirmation",
            "is_active", "created_at", "updated_at");

    [Fact]
    public async Task The_branch_table_should_have_the_expected_columns() =>
        await AssertColumnsAsync(
            "branch",
            "id", "company_id", "name", "phone", "time_zone_id", "is_active", "created_at", "updated_at",
            "address_street", "address_number", "address_complement", "address_district",
            "address_city", "address_state", "address_postal_code");

    [Fact]
    public async Task The_schedule_tables_should_have_the_expected_columns()
    {
        await AssertColumnsAsync("branch_schedule", "id", "branch_id", "day", "start_time", "end_time");
        await AssertColumnsAsync("branch_schedule_break", "id", "branch_schedule_id", "start_time", "end_time", "description");
    }

    [Theory]
    [InlineData("company", "id", "char(36)")]
    [InlineData("company", "cnpj", "char(14)")]
    [InlineData("company", "email", "varchar(254)")]
    [InlineData("company", "phone", "varchar(16)")]
    [InlineData("company", "default_language", "varchar(10)")]
    [InlineData("company", "trade_name", "varchar(150)")]
    [InlineData("company", "booking_policy_requires_confirmation", "tinyint(1)")]
    [InlineData("branch", "address_state", "char(2)")]
    [InlineData("branch", "address_postal_code", "char(8)")]
    [InlineData("branch", "time_zone_id", "varchar(64)")]
    [InlineData("branch_schedule", "day", "varchar(30)")]
    [InlineData("branch_schedule", "start_time", "time(6)")]
    [InlineData("branch_schedule_break", "id", "int")]
    public async Task Columns_should_have_the_agreed_types(string table, string column, string expectedType)
    {
        var types = await QueryAsync(
            $"SELECT column_type AS Value FROM information_schema.columns WHERE table_schema = DATABASE() AND table_name = {table} AND column_name = {column}");

        types.ShouldHaveSingleItem().ShouldBe(expectedType);
    }

    [Theory]
    [InlineData("company", "ix_company_cnpj")]
    [InlineData("branch", "ix_branch_company_id_name")]
    [InlineData("branch_schedule", "ix_branch_schedule_branch_id_day")]
    public async Task Unique_indexes_should_exist_with_snake_case_names(string table, string indexName)
    {
        var indexes = await QueryAsync(
            $"SELECT DISTINCT index_name AS Value FROM information_schema.statistics WHERE table_schema = DATABASE() AND table_name = {table} AND non_unique = 0");

        indexes.ShouldContain(indexName);
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
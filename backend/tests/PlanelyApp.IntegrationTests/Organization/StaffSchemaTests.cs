using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PlanelyApp.Infrastructure.Persistence;
using Shouldly;

namespace PlanelyApp.IntegrationTests.Organization;

public class StaffSchemaTests(MySqlFixture mysql)
{
    [Fact]
    public async Task The_professional_tables_should_have_the_expected_columns()
    {
        await AssertColumnsAsync("professional",
            "id", "company_id", "user_id", "name", "specialty", "is_active", "schedule_version", "created_at", "updated_at");
        await AssertColumnsAsync("working_hours", "id", "professional_id", "branch_id", "day", "start_time", "end_time");
        await AssertColumnsAsync("working_hours_break", "id", "working_hours_id", "start_time", "end_time", "description");
        await AssertColumnsAsync("time_off", "id", "professional_id", "starts_at", "ends_at", "reason");
        await AssertColumnsAsync("professional_offering", "id", "professional_id", "service_offering_id", "custom_duration_minutes");
    }

    [Fact]
    public async Task The_user_table_should_have_the_expected_columns() =>
        await AssertColumnsAsync(
            "user",
            "id", "company_id", "identity_id", "name", "email", "role", "access_all_branches", "allowed_branch_ids",
            "preferred_language", "is_active", "created_at", "updated_at");

    [Fact]
    public async Task The_customer_table_should_have_the_expected_columns() =>
        await AssertColumnsAsync(
            "customer",
            "id", "company_id", "name", "email", "phone", "cpf", "birth_date", "last_access_at", "accepts_marketing",
            "preferred_language", "created_at", "updated_at");

    [Theory]
    [InlineData("professional", "schedule_version", "int")]
    [InlineData("working_hours", "day", "varchar(30)")]
    [InlineData("working_hours", "end_time", "time(6)")]
    [InlineData("time_off", "id", "char(36)")]
    [InlineData("time_off", "starts_at", "datetime(6)")]
    [InlineData("professional_offering", "custom_duration_minutes", "int")]
    [InlineData("user", "role", "varchar(30)")]
    [InlineData("user", "allowed_branch_ids", "json")]
    [InlineData("customer", "cpf", "char(11)")]
    [InlineData("customer", "birth_date", "date")]
    [InlineData("customer", "last_access_at", "datetime(6)")]
    public async Task Columns_should_have_the_agreed_types(string table, string column, string expectedType)
    {
        var types = await QueryAsync(
            $"SELECT column_type AS Value FROM information_schema.columns WHERE table_schema = DATABASE() AND table_name = {table} AND column_name = {column}");

        types.ShouldHaveSingleItem().ShouldBe(expectedType);
    }

    [Theory]
    [InlineData("professional", "ix_professional_user_id")]
    [InlineData("professional_offering", "ix_professional_offering_professional_id_service_offering_id")]
    [InlineData("user", "ix_user_identity_id")]
    [InlineData("user", "ix_user_company_id_email")]
    [InlineData("customer", "ix_customer_company_id_phone")]
    [InlineData("customer", "ix_customer_company_id_email")]
    [InlineData("customer", "ix_customer_company_id_cpf")]
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
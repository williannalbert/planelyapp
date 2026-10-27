using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PlanelyApp.Infrastructure.Persistence;
using Shouldly;

namespace PlanelyApp.IntegrationTests.Scheduling;

public class AppointmentSchemaTests(MySqlFixture mysql)
{
    [Fact]
    public async Task The_appointment_tables_should_have_the_expected_columns()
    {
        await AssertColumnsAsync(
            "appointment",
            "id", "company_id", "branch_id", "customer_id", "professional_id", "starts_at", "ends_at", "status",
            "total_price", "notes", "created_at", "updated_at");
        await AssertColumnsAsync(
            "appointment_item",
            "id", "appointment_id", "service_offering_id", "service_name", "duration_minutes", "is_on_request",
            "list_price", "discount", "final_price", "promotion_id", "is_from_bundle");
        await AssertColumnsAsync(
            "appointment_status_change",
            "id", "appointment_id", "from_status", "to_status", "occurred_at", "changed_by", "reason");
    }

    [Theory]
    [InlineData("appointment", "starts_at", "datetime(6)")]
    [InlineData("appointment", "ends_at", "datetime(6)")]
    [InlineData("appointment", "status", "varchar(30)")]
    [InlineData("appointment", "total_price", "decimal(12,2)")]
    [InlineData("appointment", "notes", "varchar(1000)")]
    [InlineData("appointment_item", "service_name", "varchar(150)")]
    [InlineData("appointment_item", "duration_minutes", "int")]
    [InlineData("appointment_item", "list_price", "decimal(12,2)")]
    [InlineData("appointment_item", "discount", "decimal(12,2)")]
    [InlineData("appointment_item", "final_price", "decimal(12,2)")]
    [InlineData("appointment_status_change", "from_status", "varchar(30)")]
    [InlineData("appointment_status_change", "to_status", "varchar(30)")]
    [InlineData("appointment_status_change", "occurred_at", "datetime(6)")]
    [InlineData("appointment_status_change", "reason", "varchar(500)")]
    public async Task Columns_should_have_the_agreed_types(string table, string column, string expectedType)
    {
        var types = await QueryAsync(
            $"SELECT column_type AS Value FROM information_schema.columns WHERE table_schema = DATABASE() AND table_name = {table} AND column_name = {column}");

        types.ShouldHaveSingleItem().ShouldBe(expectedType);
    }

    // The four indexes written by hand in the migration, with their columns in order.
    [Theory]
    [InlineData("ix_appointment_professional_id_starts_at_ends_at", "professional_id,starts_at,ends_at")]
    [InlineData("ix_appointment_company_id_branch_id_starts_at", "company_id,branch_id,starts_at")]
    [InlineData("ix_appointment_customer_id_starts_at", "customer_id,starts_at")]
    [InlineData("ix_appointment_status_starts_at", "status,starts_at")]
    public async Task The_hand_written_indexes_should_exist_with_their_columns_in_order(string indexName, string expectedColumns)
    {
        var columns = await QueryAsync(
            $"SELECT column_name AS Value FROM information_schema.statistics WHERE table_schema = DATABASE() AND table_name = {"appointment"} AND index_name = {indexName} ORDER BY seq_in_index");

        string.Join(",", columns).ShouldBe(expectedColumns);
    }

    // Participants have foreign keys; the service and the promotion of an item are copies and do not.
    [Theory]
    [InlineData("appointment", "branch,company,customer,professional")]
    [InlineData("appointment_item", "appointment")]
    [InlineData("appointment_status_change", "appointment")]
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
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PlanelyApp.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCheckConstraints : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddCheckConstraint(
                name: "ck_working_hours_break_range",
                table: "working_hours_break",
                sql: "end_time > start_time");

            migrationBuilder.AddCheckConstraint(
                name: "ck_working_hours_range",
                table: "working_hours",
                sql: "end_time > start_time");

            migrationBuilder.AddCheckConstraint(
                name: "ck_time_off_period",
                table: "time_off",
                sql: "ends_at > starts_at");

            migrationBuilder.AddCheckConstraint(
                name: "ck_service_offering_pricing",
                table: "service_offering",
                sql: "(pricing_type = 'Fixed' AND base_price IS NOT NULL) OR (pricing_type = 'OnRequest' AND base_price IS NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_promotion_discount_positive",
                table: "promotion",
                sql: "discount_value > 0");

            migrationBuilder.AddCheckConstraint(
                name: "ck_promotion_percentage_up_to_100",
                table: "promotion",
                sql: "discount_type <> 'Percentage' OR discount_value <= 100");

            migrationBuilder.AddCheckConstraint(
                name: "ck_promotion_validity",
                table: "promotion",
                sql: "valid_until >= valid_from");

            migrationBuilder.AddCheckConstraint(
                name: "ck_branch_schedule_break_range",
                table: "branch_schedule_break",
                sql: "end_time > start_time");

            migrationBuilder.AddCheckConstraint(
                name: "ck_branch_schedule_range",
                table: "branch_schedule",
                sql: "end_time > start_time");

            migrationBuilder.AddCheckConstraint(
                name: "ck_appointment_period",
                table: "appointment",
                sql: "ends_at > starts_at");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_working_hours_break_range",
                table: "working_hours_break");

            migrationBuilder.DropCheckConstraint(
                name: "ck_working_hours_range",
                table: "working_hours");

            migrationBuilder.DropCheckConstraint(
                name: "ck_time_off_period",
                table: "time_off");

            migrationBuilder.DropCheckConstraint(
                name: "ck_service_offering_pricing",
                table: "service_offering");

            migrationBuilder.DropCheckConstraint(
                name: "ck_promotion_discount_positive",
                table: "promotion");

            migrationBuilder.DropCheckConstraint(
                name: "ck_promotion_percentage_up_to_100",
                table: "promotion");

            migrationBuilder.DropCheckConstraint(
                name: "ck_promotion_validity",
                table: "promotion");

            migrationBuilder.DropCheckConstraint(
                name: "ck_branch_schedule_break_range",
                table: "branch_schedule_break");

            migrationBuilder.DropCheckConstraint(
                name: "ck_branch_schedule_range",
                table: "branch_schedule");

            migrationBuilder.DropCheckConstraint(
                name: "ck_appointment_period",
                table: "appointment");
        }
    }
}

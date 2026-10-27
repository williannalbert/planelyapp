using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PlanelyApp.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAppointment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "appointment",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    company_id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    branch_id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    customer_id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    professional_id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    starts_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    ends_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    status = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    total_price = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: true),
                    notes = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_appointment", x => x.id);
                    table.ForeignKey(
                        name: "FK_appointment_branch_branch_id",
                        column: x => x.branch_id,
                        principalTable: "branch",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_appointment_company_company_id",
                        column: x => x.company_id,
                        principalTable: "company",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_appointment_customer_customer_id",
                        column: x => x.customer_id,
                        principalTable: "customer",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_appointment_professional_professional_id",
                        column: x => x.professional_id,
                        principalTable: "professional",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "appointment_item",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    service_offering_id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    service_name = table.Column<string>(type: "varchar(150)", maxLength: 150, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    duration_minutes = table.Column<int>(type: "int", nullable: false),
                    is_on_request = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    list_price = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: true),
                    discount = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: false),
                    final_price = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: true),
                    promotion_id = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    is_from_bundle = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    appointment_id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_appointment_item", x => x.id);
                    table.ForeignKey(
                        name: "FK_appointment_item_appointment_appointment_id",
                        column: x => x.appointment_id,
                        principalTable: "appointment",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "appointment_status_change",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    from_status = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    to_status = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    occurred_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    changed_by = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    reason = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    appointment_id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_appointment_status_change", x => x.id);
                    table.ForeignKey(
                        name: "FK_appointment_status_change_appointment_appointment_id",
                        column: x => x.appointment_id,
                        principalTable: "appointment",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "ix_appointment_branch_id",
                table: "appointment",
                column: "branch_id");

            migrationBuilder.CreateIndex(
                name: "ix_appointment_company_id",
                table: "appointment",
                column: "company_id");

            migrationBuilder.CreateIndex(
                name: "ix_appointment_customer_id",
                table: "appointment",
                column: "customer_id");

            migrationBuilder.CreateIndex(
                name: "ix_appointment_professional_id",
                table: "appointment",
                column: "professional_id");

            migrationBuilder.CreateIndex(
                name: "ix_appointment_item_appointment_id",
                table: "appointment_item",
                column: "appointment_id");

            migrationBuilder.CreateIndex(
                name: "ix_appointment_status_change_appointment_id",
                table: "appointment_status_change",
                column: "appointment_id");
            // Hand-written (see the README): these indexes cross the owned type Period and its owner,
            // which EF Core 10 cannot express in the model. With EF Core 11 they move to the configuration.
            migrationBuilder.CreateIndex(
                name: "ix_appointment_professional_id_starts_at_ends_at",
                table: "appointment",
                columns: new[] { "professional_id", "starts_at", "ends_at" });

            migrationBuilder.CreateIndex(
                name: "ix_appointment_company_id_branch_id_starts_at",
                table: "appointment",
                columns: new[] { "company_id", "branch_id", "starts_at" });

            migrationBuilder.CreateIndex(
                name: "ix_appointment_customer_id_starts_at",
                table: "appointment",
                columns: new[] { "customer_id", "starts_at" });

            migrationBuilder.CreateIndex(
                name: "ix_appointment_status_starts_at",
                table: "appointment",
                columns: new[] { "status", "starts_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "appointment_item");

            migrationBuilder.DropTable(
                name: "appointment_status_change");

            migrationBuilder.DropTable(
                name: "appointment");
        }
    }
}

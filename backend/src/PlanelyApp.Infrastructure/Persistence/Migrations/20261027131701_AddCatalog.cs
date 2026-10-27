using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PlanelyApp.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCatalog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "branch_offering_override",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    service_offering_id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    custom_price = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: true),
                    is_available = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    branch_id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_branch_offering_override", x => x.id);
                    table.ForeignKey(
                        name: "FK_branch_offering_override_branch_branch_id",
                        column: x => x.branch_id,
                        principalTable: "branch",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "promotion",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    company_id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    kind = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    name = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    description = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    discount_type = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    discount_value = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: false),
                    valid_from = table.Column<DateOnly>(type: "date", nullable: false),
                    valid_until = table.Column<DateOnly>(type: "date", nullable: false),
                    applies_to_all_branches = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    is_active = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    branch_ids = table.Column<string>(type: "json", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    service_ids = table.Column<string>(type: "json", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    days_of_week_mask = table.Column<byte>(type: "tinyint unsigned", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_promotion", x => x.id);
                    table.ForeignKey(
                        name: "FK_promotion_company_company_id",
                        column: x => x.company_id,
                        principalTable: "company",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "service_category",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    company_id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    name = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_service_category", x => x.id);
                    table.ForeignKey(
                        name: "FK_service_category_company_company_id",
                        column: x => x.company_id,
                        principalTable: "company",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "service_offering",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    company_id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    category_id = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    name = table.Column<string>(type: "varchar(150)", maxLength: 150, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    description = table.Column<string>(type: "varchar(2000)", maxLength: 2000, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    standard_duration_minutes = table.Column<int>(type: "int", nullable: false),
                    pricing_type = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    base_price = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: true),
                    is_active = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_service_offering", x => x.id);
                    table.ForeignKey(
                        name: "FK_service_offering_company_company_id",
                        column: x => x.company_id,
                        principalTable: "company",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_service_offering_service_category_category_id",
                        column: x => x.category_id,
                        principalTable: "service_category",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "ix_branch_offering_override_branch_id_service_offering_id",
                table: "branch_offering_override",
                columns: new[] { "branch_id", "service_offering_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_promotion_company_id_is_active",
                table: "promotion",
                columns: new[] { "company_id", "is_active" });

            migrationBuilder.CreateIndex(
                name: "ix_service_category_company_id",
                table: "service_category",
                column: "company_id");

            migrationBuilder.CreateIndex(
                name: "ix_service_offering_category_id",
                table: "service_offering",
                column: "category_id");

            migrationBuilder.CreateIndex(
                name: "ix_service_offering_company_id_name",
                table: "service_offering",
                columns: new[] { "company_id", "name" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "branch_offering_override");

            migrationBuilder.DropTable(
                name: "promotion");

            migrationBuilder.DropTable(
                name: "service_offering");

            migrationBuilder.DropTable(
                name: "service_category");
        }
    }
}

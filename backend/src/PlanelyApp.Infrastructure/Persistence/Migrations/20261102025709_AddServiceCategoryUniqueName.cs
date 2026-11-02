using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PlanelyApp.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddServiceCategoryUniqueName : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // The new index first: MySQL needs an index on company_id for the foreign key at every moment,
            // so the old one can only go once this one (which starts with company_id) exists.
            migrationBuilder.CreateIndex(
                name: "ix_service_category_company_id_name",
                table: "service_category",
                columns: new[] { "company_id", "name" },
                unique: true);

            migrationBuilder.DropIndex(
                name: "ix_service_category_company_id",
                table: "service_category");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "ix_service_category_company_id",
                table: "service_category",
                column: "company_id");

            migrationBuilder.DropIndex(
                name: "ix_service_category_company_id_name",
                table: "service_category");
        }
    }
}

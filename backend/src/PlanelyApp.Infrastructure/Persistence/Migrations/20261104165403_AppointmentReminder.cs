using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PlanelyApp.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AppointmentReminder : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "reminder_sent_at_utc",
                table: "appointment",
                type: "datetime(6)",
                nullable: true);

            // Hand-written, like the other indexes of this table. Equality columns first, the range (starts_at) last.
            migrationBuilder.CreateIndex(
                name: "ix_appointment_status_reminder_sent_at_utc_starts_at",
                table: "appointment",
                columns: ["status", "reminder_sent_at_utc", "starts_at"]);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_appointment_status_reminder_sent_at_utc_starts_at",
                table: "appointment");

            migrationBuilder.DropColumn(
                name: "reminder_sent_at_utc",
                table: "appointment");

            
        }
    }
}

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace umbral_backend.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RenameSubstageTimerToMissionTimer : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "substage_timer_total_duration",
                table: "live_sessions",
                newName: "mission_timer_total_duration");

            migrationBuilder.RenameColumn(
                name: "substage_timer_remaining_duration",
                table: "live_sessions",
                newName: "mission_timer_remaining_duration");

            migrationBuilder.RenameColumn(
                name: "substage_timer_expired_at",
                table: "live_sessions",
                newName: "mission_timer_expired_at");

            migrationBuilder.RenameColumn(
                name: "substage_timer_advancing_since",
                table: "live_sessions",
                newName: "mission_timer_advancing_since");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "mission_timer_total_duration",
                table: "live_sessions",
                newName: "substage_timer_total_duration");

            migrationBuilder.RenameColumn(
                name: "mission_timer_remaining_duration",
                table: "live_sessions",
                newName: "substage_timer_remaining_duration");

            migrationBuilder.RenameColumn(
                name: "mission_timer_expired_at",
                table: "live_sessions",
                newName: "substage_timer_expired_at");

            migrationBuilder.RenameColumn(
                name: "mission_timer_advancing_since",
                table: "live_sessions",
                newName: "substage_timer_advancing_since");
        }
    }
}

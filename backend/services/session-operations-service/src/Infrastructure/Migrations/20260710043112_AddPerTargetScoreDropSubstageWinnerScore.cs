using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace umbral_backend.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPerTargetScoreDropSubstageWinnerScore : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "winner_score",
                table: "live_session_mission_runtime_snapshot_substages");

            migrationBuilder.AddColumn<int>(
                name: "score",
                table: "live_session_mission_runtime_snapshot_targets",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "score",
                table: "live_session_mission_runtime_snapshot_targets");

            migrationBuilder.AddColumn<int>(
                name: "winner_score",
                table: "live_session_mission_runtime_snapshot_substages",
                type: "integer",
                nullable: true);
        }
    }
}

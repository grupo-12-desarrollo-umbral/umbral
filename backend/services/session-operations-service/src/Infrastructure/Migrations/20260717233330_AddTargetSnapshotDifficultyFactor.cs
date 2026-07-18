using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace umbral_backend.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTargetSnapshotDifficultyFactor : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "difficulty_factor",
                table: "live_session_mission_runtime_snapshot_targets",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            // Backfill snapshots taken before this column existed: a target's score is the mission's
            // base score (50) weighted by its difficulty factor, so the factor is recovered as
            // score / 50 (target scores are always clean multiples of the base). GREATEST guards any
            // legacy non-multiple row from collapsing to 0.
            migrationBuilder.Sql(
                "UPDATE live_session_mission_runtime_snapshot_targets SET difficulty_factor = GREATEST(1, score / 50);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "difficulty_factor",
                table: "live_session_mission_runtime_snapshot_targets");
        }
    }
}

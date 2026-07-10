using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace umbral_backend.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class DropSubstageWinnerScoreMakeTargetScoreRequired : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Defensive re-backfill (mirrors the F1 expand migration): copy the substage
            // WinnerScore onto any target added to a winner-scored substage since F1 that
            // still lacks a per-target Score. Must run BEFORE WinnerScore is dropped.
            migrationBuilder.Sql("""
                UPDATE "MissionTargets" AS target
                SET "Score" = substage."WinnerScore"
                FROM "MissionSubstages" AS substage
                WHERE target."SubstageId" = substage."Id"
                  AND target."Score" IS NULL
                  AND substage."WinnerScore" IS NOT NULL;
                """);

            migrationBuilder.DropColumn(
                name: "WinnerScore",
                table: "MissionSubstages");

            // DES-86 F3, fail-fast strategy: no straggler fabrication. A target still NULL
            // here belongs to a substage that never had a WinnerScore — an invalid draft
            // that never passed readiness. SET NOT NULL errors loudly rather than inventing
            // a score (no defaultValue), so a dirty DB is surfaced, not silently mutated.
            migrationBuilder.AlterColumn<int>(
                name: "Score",
                table: "MissionTargets",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "Score",
                table: "MissionTargets",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AddColumn<int>(
                name: "WinnerScore",
                table: "MissionSubstages",
                type: "integer",
                nullable: true);
        }
    }
}

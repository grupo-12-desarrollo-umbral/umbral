using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace umbral_backend.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ConstrainTargetSnapshotScoreNotNull : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Fail loudly if any NULLs remain. The producer (mission-design-service) has always
            // emitted non-null scores; NULLs can only exist from the expand phase of DES-86.
            // If this migration fails, backfill NULLs to a valid score before re-running.
            migrationBuilder.Sql(
                "DO $$ BEGIN " +
                "IF EXISTS (SELECT 1 FROM live_session_mission_runtime_snapshot_targets WHERE score IS NULL) THEN " +
                "RAISE EXCEPTION 'Column score contains NULLs — backfill before applying NOT NULL constraint'; " +
                "END IF; END $$;");

            migrationBuilder.AlterColumn<int>(
                name: "score",
                table: "live_session_mission_runtime_snapshot_targets",
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
                name: "score",
                table: "live_session_mission_runtime_snapshot_targets",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");
        }
    }
}

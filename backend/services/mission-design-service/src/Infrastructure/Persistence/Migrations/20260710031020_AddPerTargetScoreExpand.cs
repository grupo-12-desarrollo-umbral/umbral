using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace umbral_backend.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPerTargetScoreExpand : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Score",
                table: "MissionTargets",
                type: "integer",
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE "MissionTargets" AS target
                SET "Score" = substage."WinnerScore"
                FROM "MissionSubstages" AS substage
                WHERE target."SubstageId" = substage."Id"
                  AND target."Score" IS NULL
                  AND substage."WinnerScore" IS NOT NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Score",
                table: "MissionTargets");
        }
    }
}

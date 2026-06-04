using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace umbral_backend.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTeamCapacityColumn : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Some environments applied the original runtime-recovery migration before the
            // team capacity column existed, while fresh databases create it there already.
            // Keep this follow-up migration safe in both cases.
            migrationBuilder.Sql("""
                ALTER TABLE live_session_teams
                ADD COLUMN IF NOT EXISTS team_capacity integer NOT NULL DEFAULT 0;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "team_capacity",
                table: "live_session_teams");
        }
    }
}

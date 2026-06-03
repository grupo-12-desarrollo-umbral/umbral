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
            // Team.Capacity was mapped to team_capacity in LiveSessionConfiguration
            // but the column was never created — the original migration was edited
            // after being applied to the database.
            migrationBuilder.AddColumn<int>(
                name: "team_capacity",
                table: "live_session_teams",
                type: "integer",
                nullable: false,
                defaultValue: 0);
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

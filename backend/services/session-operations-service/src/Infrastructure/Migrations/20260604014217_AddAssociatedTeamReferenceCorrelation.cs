using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace umbral_backend.Infrastructure.Migrations;

public partial class AddAssociatedTeamReferenceCorrelation : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<Guid>(
            name: "reference_team_id",
            table: "live_session_teams",
            type: "uuid",
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "IX_live_session_teams_live_session_id_reference_team_id",
            table: "live_session_teams",
            columns: new[] { "live_session_id", "reference_team_id" },
            unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_live_session_teams_live_session_id_reference_team_id",
            table: "live_session_teams");

        migrationBuilder.DropColumn(
            name: "reference_team_id",
            table: "live_session_teams");
    }
}

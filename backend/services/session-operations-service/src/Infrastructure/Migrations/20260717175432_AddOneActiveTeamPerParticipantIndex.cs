using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace umbral_backend.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddOneActiveTeamPerParticipantIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "ux_team_member_one_active_team_per_participant",
                table: "live_session_team_members",
                column: "session_participant_id",
                unique: true,
                filter: "membership_status = 'Active'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ux_team_member_one_active_team_per_participant",
                table: "live_session_team_members");
        }
    }
}

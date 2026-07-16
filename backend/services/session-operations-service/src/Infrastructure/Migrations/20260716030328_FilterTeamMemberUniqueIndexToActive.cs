using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace umbral_backend.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class FilterTeamMemberUniqueIndexToActive : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_live_session_team_members_team_id_session_participant_id",
                table: "live_session_team_members");

            migrationBuilder.CreateIndex(
                name: "IX_live_session_team_members_team_id_session_participant_id",
                table: "live_session_team_members",
                columns: new[] { "team_id", "session_participant_id" },
                unique: true,
                filter: "membership_status = 'Active'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_live_session_team_members_team_id_session_participant_id",
                table: "live_session_team_members");

            migrationBuilder.CreateIndex(
                name: "IX_live_session_team_members_team_id_session_participant_id",
                table: "live_session_team_members",
                columns: new[] { "team_id", "session_participant_id" },
                unique: true);
        }
    }
}

using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace umbral_backend.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RenameTeamsToRegisteredTeams : Migration
    {
        // In-place rename: teams -> registered_teams, team_memberships -> registered_team_memberships,
        // preserving existing rows. The whitelist has no assignment timestamp, so assigned_at is dropped.
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "assigned_at",
                table: "team_memberships");

            migrationBuilder.RenameTable(
                name: "teams",
                newName: "registered_teams");

            migrationBuilder.RenameTable(
                name: "team_memberships",
                newName: "registered_team_memberships");

            migrationBuilder.RenameIndex(
                name: "IX_teams_team_code",
                table: "registered_teams",
                newName: "IX_registered_teams_team_code");

            migrationBuilder.RenameIndex(
                name: "IX_team_memberships_team_id_user_id",
                table: "registered_team_memberships",
                newName: "IX_registered_team_memberships_team_id_user_id");

            migrationBuilder.RenameIndex(
                name: "IX_team_memberships_user_id",
                table: "registered_team_memberships",
                newName: "IX_registered_team_memberships_user_id");

            migrationBuilder.Sql(
                "ALTER TABLE registered_teams RENAME CONSTRAINT \"PK_teams\" TO \"PK_registered_teams\";");
            migrationBuilder.Sql(
                "ALTER TABLE registered_team_memberships RENAME CONSTRAINT \"PK_team_memberships\" TO \"PK_registered_team_memberships\";");
            migrationBuilder.Sql(
                "ALTER TABLE registered_team_memberships RENAME CONSTRAINT \"FK_team_memberships_teams_team_id\" TO \"FK_registered_team_memberships_registered_teams_team_id\";");
            migrationBuilder.Sql(
                "ALTER TABLE registered_team_memberships RENAME CONSTRAINT \"FK_team_memberships_users_user_id\" TO \"FK_registered_team_memberships_users_user_id\";");
            migrationBuilder.Sql(
                "ALTER TABLE session_team_associations RENAME CONSTRAINT \"FK_session_team_associations_teams_team_id\" TO \"FK_session_team_associations_registered_teams_team_id\";");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "ALTER TABLE session_team_associations RENAME CONSTRAINT \"FK_session_team_associations_registered_teams_team_id\" TO \"FK_session_team_associations_teams_team_id\";");
            migrationBuilder.Sql(
                "ALTER TABLE registered_team_memberships RENAME CONSTRAINT \"FK_registered_team_memberships_users_user_id\" TO \"FK_team_memberships_users_user_id\";");
            migrationBuilder.Sql(
                "ALTER TABLE registered_team_memberships RENAME CONSTRAINT \"FK_registered_team_memberships_registered_teams_team_id\" TO \"FK_team_memberships_teams_team_id\";");
            migrationBuilder.Sql(
                "ALTER TABLE registered_team_memberships RENAME CONSTRAINT \"PK_registered_team_memberships\" TO \"PK_team_memberships\";");
            migrationBuilder.Sql(
                "ALTER TABLE registered_teams RENAME CONSTRAINT \"PK_registered_teams\" TO \"PK_teams\";");

            migrationBuilder.RenameIndex(
                name: "IX_registered_team_memberships_user_id",
                table: "registered_team_memberships",
                newName: "IX_team_memberships_user_id");

            migrationBuilder.RenameIndex(
                name: "IX_registered_team_memberships_team_id_user_id",
                table: "registered_team_memberships",
                newName: "IX_team_memberships_team_id_user_id");

            migrationBuilder.RenameIndex(
                name: "IX_registered_teams_team_code",
                table: "registered_teams",
                newName: "IX_teams_team_code");

            migrationBuilder.RenameTable(
                name: "registered_team_memberships",
                newName: "team_memberships");

            migrationBuilder.RenameTable(
                name: "registered_teams",
                newName: "teams");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "assigned_at",
                table: "team_memberships",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()");
        }
    }
}

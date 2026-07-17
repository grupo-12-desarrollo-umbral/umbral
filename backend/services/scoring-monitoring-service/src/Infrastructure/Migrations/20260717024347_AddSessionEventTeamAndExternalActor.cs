using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace umbral_backend.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSessionEventTeamAndExternalActor : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DO $$
                BEGIN
                    IF EXISTS (SELECT 1 FROM session_events) THEN
                        RAISE EXCEPTION 'Session event attribution migration requires an empty session_events table; use the documented dual-write/backfill transition for populated environments.';
                    END IF;
                END
                $$;
                """);

            migrationBuilder.DropColumn(
                name: "responsible_user_id",
                table: "session_events");

            migrationBuilder.AddColumn<Guid>(
                name: "responsible_user_external_id",
                table: "session_events",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "team_id",
                table: "session_events",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_session_events_live_session_id_team_id_occurred_at",
                table: "session_events",
                columns: new[] { "live_session_id", "team_id", "occurred_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_session_events_live_session_id_team_id_occurred_at",
                table: "session_events");

            migrationBuilder.DropColumn(
                name: "responsible_user_external_id",
                table: "session_events");

            migrationBuilder.DropColumn(
                name: "team_id",
                table: "session_events");

            migrationBuilder.AddColumn<int>(
                name: "responsible_user_id",
                table: "session_events",
                type: "integer",
                nullable: true);
        }
    }
}

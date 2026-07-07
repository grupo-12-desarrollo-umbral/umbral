using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace umbral_backend.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RemoveSessionParticipationFromUsers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "session_team_associations");

            migrationBuilder.DropTable(
                name: "live_sessions");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "live_sessions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    session_code = table.Column<string>(type: "character varying(6)", maxLength: 6, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_live_sessions", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "session_team_associations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    live_session_id = table.Column<Guid>(type: "uuid", nullable: false),
                    team_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_session_team_associations", x => x.id);
                    table.ForeignKey(
                        name: "FK_session_team_associations_live_sessions_live_session_id",
                        column: x => x.live_session_id,
                        principalTable: "live_sessions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_session_team_associations_registered_teams_team_id",
                        column: x => x.team_id,
                        principalTable: "registered_teams",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_live_sessions_session_code",
                table: "live_sessions",
                column: "session_code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_session_team_associations_live_session_id_team_id",
                table: "session_team_associations",
                columns: new[] { "live_session_id", "team_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_session_team_associations_team_id",
                table: "session_team_associations",
                column: "team_id");
        }
    }
}

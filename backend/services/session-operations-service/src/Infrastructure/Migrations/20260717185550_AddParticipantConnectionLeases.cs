using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace umbral_backend.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddParticipantConnectionLeases : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "live_session_participant_connections",
                columns: table => new
                {
                    connection_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    session_participant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    opened_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    last_seen_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_live_session_participant_connections", x => x.connection_id);
                    table.ForeignKey(
                        name: "FK_live_session_participant_connections_live_session_participa~",
                        column: x => x.session_participant_id,
                        principalTable: "live_session_participants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_live_session_participant_connections_session_participant_id",
                table: "live_session_participant_connections",
                column: "session_participant_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "live_session_participant_connections");
        }
    }
}

using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace umbral_backend.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSessionEvents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "live_session_events",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    live_session_id = table.Column<Guid>(type: "uuid", nullable: false),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    actor_type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    actor_id = table.Column<int>(type: "integer", nullable: true),
                    event_type = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    payload_summary = table.Column<string>(type: "character varying(600)", maxLength: 600, nullable: false),
                    correlation_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_live_session_events", x => x.id);
                    table.ForeignKey(
                        name: "FK_live_session_events_live_sessions_live_session_id",
                        column: x => x.live_session_id,
                        principalTable: "live_sessions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_live_session_events_live_session_id",
                table: "live_session_events",
                column: "live_session_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "live_session_events");
        }
    }
}

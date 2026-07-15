using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace umbral_backend.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSessionEventHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "session_events",
                columns: table => new
                {
                    session_event_id = table.Column<Guid>(type: "uuid", nullable: false),
                    source_event_key = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    live_session_id = table.Column<Guid>(type: "uuid", nullable: false),
                    event_type = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    payload_summary = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    responsible_user_id = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_session_events", x => x.session_event_id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_session_events_live_session_id_occurred_at",
                table: "session_events",
                columns: new[] { "live_session_id", "occurred_at" });

            migrationBuilder.CreateIndex(
                name: "ux_session_events_source_event_key",
                table: "session_events",
                column: "source_event_key",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "session_events");
        }
    }
}

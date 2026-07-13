using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace umbral_backend.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddClueReleaseRecords : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "live_session_clue_releases",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    live_session_id = table.Column<Guid>(type: "uuid", nullable: false),
                    team_id = table.Column<Guid>(type: "uuid", nullable: false),
                    target_id = table.Column<Guid>(type: "uuid", nullable: false),
                    clue_id = table.Column<Guid>(type: "uuid", nullable: true),
                    release_mode = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    released_by_user_id = table.Column<int>(type: "integer", nullable: true),
                    released_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_live_session_clue_releases", x => x.id);
                    table.ForeignKey(
                        name: "FK_live_session_clue_releases_live_sessions_live_session_id",
                        column: x => x.live_session_id,
                        principalTable: "live_sessions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_live_session_clue_releases_live_session_id_team_id_target_id",
                table: "live_session_clue_releases",
                columns: new[] { "live_session_id", "team_id", "target_id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "live_session_clue_releases");
        }
    }
}

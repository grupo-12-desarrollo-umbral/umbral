using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace umbral_backend.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSubstageClueSnapshot : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "live_session_mission_runtime_snapshot_clues",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    substage_snapshot_id = table.Column<Guid>(type: "uuid", nullable: false),
                    text = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    visibility_policy = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    sequence_order = table.Column<int>(type: "integer", nullable: false),
                    live_session_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_live_session_mission_runtime_snapshot_clues", x => x.id);
                    table.ForeignKey(
                        name: "FK_live_session_mission_runtime_snapshot_clues_live_session_mi~",
                        column: x => x.live_session_id,
                        principalTable: "live_session_mission_runtime_snapshots",
                        principalColumn: "live_session_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_live_session_mission_runtime_snapshot_clues_live_session_id~",
                table: "live_session_mission_runtime_snapshot_clues",
                columns: new[] { "live_session_id", "substage_snapshot_id", "sequence_order" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "live_session_mission_runtime_snapshot_clues");
        }
    }
}

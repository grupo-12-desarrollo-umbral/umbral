using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace umbral_backend.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTreasureEvidenceSubmission : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "live_session_treasure_evidence_submissions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    scanned_value = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    target_snapshot_id = table.Column<Guid>(type: "uuid", nullable: true),
                    resolution_rejection_reason = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    live_session_id = table.Column<Guid>(type: "uuid", nullable: false),
                    team_id = table.Column<Guid>(type: "uuid", nullable: false),
                    active_substage_id = table.Column<Guid>(type: "uuid", nullable: false),
                    submission_type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    submitted_by_participant_id = table.Column<Guid>(type: "uuid", nullable: true),
                    submitted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    validation_state = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    rejection_reason = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_live_session_treasure_evidence_submissions", x => x.id);
                    table.ForeignKey(
                        name: "FK_live_session_treasure_evidence_submissions_live_sessions_li~",
                        column: x => x.live_session_id,
                        principalTable: "live_sessions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_live_session_treasure_evidence_submissions_live_session_id",
                table: "live_session_treasure_evidence_submissions",
                column: "live_session_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "live_session_treasure_evidence_submissions");
        }
    }
}

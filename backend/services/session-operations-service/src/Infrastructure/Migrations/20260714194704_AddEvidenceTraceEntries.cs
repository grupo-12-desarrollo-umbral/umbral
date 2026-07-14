using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace umbral_backend.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddEvidenceTraceEntries : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "evidence_trace_entries",
                columns: table => new
                {
                    evidence_submission_id = table.Column<Guid>(type: "uuid", nullable: false),
                    live_session_id = table.Column<Guid>(type: "uuid", nullable: false),
                    team_id = table.Column<Guid>(type: "uuid", nullable: false),
                    active_substage_id = table.Column<Guid>(type: "uuid", nullable: false),
                    submission_type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    submitted_by_participant_id = table.Column<Guid>(type: "uuid", nullable: true),
                    origin_reference = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    submitted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    validation_state = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    rejection_reason = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    resolved_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_evidence_trace_entries", x => x.evidence_submission_id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_evidence_trace_entries_live_session_id_team_id",
                table: "evidence_trace_entries",
                columns: new[] { "live_session_id", "team_id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "evidence_trace_entries");
        }
    }
}

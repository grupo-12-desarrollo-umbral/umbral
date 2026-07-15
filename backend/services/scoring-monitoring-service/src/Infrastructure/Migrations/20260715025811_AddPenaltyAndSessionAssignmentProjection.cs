using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace umbral_backend.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPenaltyAndSessionAssignmentProjection : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "penalties",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    score_entry_id = table.Column<Guid>(type: "uuid", nullable: false),
                    penalty_reason = table.Column<string>(type: "text", nullable: false),
                    applied_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    applied_by_user_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_penalties", x => x.id);
                    table.ForeignKey(
                        name: "FK_penalties_score_entries_score_entry_id",
                        column: x => x.score_entry_id,
                        principalTable: "score_entries",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "session_operator_assignments",
                columns: table => new
                {
                    live_session_id = table.Column<Guid>(type: "uuid", nullable: false),
                    assigned_operator_user_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_session_operator_assignments", x => x.live_session_id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_penalties_score_entry_id",
                table: "penalties",
                column: "score_entry_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "penalties");

            migrationBuilder.DropTable(
                name: "session_operator_assignments");
        }
    }
}

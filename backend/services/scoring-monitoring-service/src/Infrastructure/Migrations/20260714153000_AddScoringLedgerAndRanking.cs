using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace umbral_backend.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddScoringLedgerAndRanking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "rankings",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    live_session_id = table.Column<Guid>(type: "uuid", nullable: false),
                    generated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    calculation_version = table.Column<long>(type: "bigint", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "text", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_rankings", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "score_entries",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    live_session_id = table.Column<Guid>(type: "uuid", nullable: false),
                    team_id = table.Column<Guid>(type: "uuid", nullable: false),
                    entry_type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    reason_code = table.Column<string>(type: "text", nullable: false),
                    score_value = table.Column<int>(type: "integer", nullable: false),
                    recorded_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    source_entity_type = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    source_entity_id = table.Column<Guid>(type: "uuid", nullable: false),
                    recorded_by_user_id = table.Column<int>(type: "integer", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "text", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_score_entries", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "ranking_rows",
                columns: table => new
                {
                    ranking_id = table.Column<Guid>(type: "uuid", nullable: false),
                    team_id = table.Column<Guid>(type: "uuid", nullable: false),
                    position = table.Column<int>(type: "integer", nullable: false),
                    total_score = table.Column<int>(type: "integer", nullable: false),
                    resolution_time = table.Column<TimeSpan>(type: "interval", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ranking_rows", x => new { x.ranking_id, x.team_id });
                    table.ForeignKey(
                        name: "FK_ranking_rows_rankings_ranking_id",
                        column: x => x.ranking_id,
                        principalTable: "rankings",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ranking_rows_ranking_id_position",
                table: "ranking_rows",
                columns: new[] { "ranking_id", "position" });

            migrationBuilder.CreateIndex(
                name: "IX_rankings_live_session_id",
                table: "rankings",
                column: "live_session_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_score_entries_live_session_id_team_id",
                table: "score_entries",
                columns: new[] { "live_session_id", "team_id" });

            migrationBuilder.CreateIndex(
                name: "IX_score_entries_source_entity_type_source_entity_id",
                table: "score_entries",
                columns: new[] { "source_entity_type", "source_entity_id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ranking_rows");

            migrationBuilder.DropTable(
                name: "score_entries");

            migrationBuilder.DropTable(
                name: "rankings");
        }
    }
}

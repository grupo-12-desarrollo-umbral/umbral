using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace umbral_backend.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitSessionOperations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "source_trivia_quiz_id",
                table: "live_sessions",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "live_session_trivia_snapshots",
                columns: table => new
                {
                    live_session_id = table.Column<Guid>(type: "uuid", nullable: false),
                    quiz_title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_live_session_trivia_snapshots", x => x.live_session_id);
                    table.ForeignKey(
                        name: "FK_live_session_trivia_snapshots_live_sessions_live_session_id",
                        column: x => x.live_session_id,
                        principalTable: "live_sessions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "live_session_trivia_snapshot_questions",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    prompt = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    sequence_order = table.Column<int>(type: "integer", nullable: false),
                    score_value = table.Column<int>(type: "integer", nullable: false),
                    time_limit_seconds = table.Column<int>(type: "integer", nullable: false),
                    explanation = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    live_session_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_live_session_trivia_snapshot_questions", x => x.id);
                    table.ForeignKey(
                        name: "FK_live_session_trivia_snapshot_questions_live_session_trivia_~",
                        column: x => x.live_session_id,
                        principalTable: "live_session_trivia_snapshots",
                        principalColumn: "live_session_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "live_session_trivia_snapshot_options",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    option_text = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    sequence_order = table.Column<int>(type: "integer", nullable: false),
                    is_correct = table.Column<bool>(type: "boolean", nullable: false),
                    trivia_question_snapshot_id = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_live_session_trivia_snapshot_options", x => x.id);
                    table.ForeignKey(
                        name: "FK_live_session_trivia_snapshot_options_live_session_trivia_sn~",
                        column: x => x.trivia_question_snapshot_id,
                        principalTable: "live_session_trivia_snapshot_questions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_live_session_trivia_snapshot_options_trivia_question_snapsh~",
                table: "live_session_trivia_snapshot_options",
                columns: new[] { "trivia_question_snapshot_id", "sequence_order" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_live_session_trivia_snapshot_questions_live_session_id_sequ~",
                table: "live_session_trivia_snapshot_questions",
                columns: new[] { "live_session_id", "sequence_order" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "live_session_trivia_snapshot_options");

            migrationBuilder.DropTable(
                name: "live_session_trivia_snapshot_questions");

            migrationBuilder.DropTable(
                name: "live_session_trivia_snapshots");

            migrationBuilder.DropColumn(
                name: "source_trivia_quiz_id",
                table: "live_sessions");
        }
    }
}

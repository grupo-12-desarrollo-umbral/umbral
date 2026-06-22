using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace umbral_backend.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMissionRuntimeSnapshot : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "live_session_trivia_snapshot_options");

            migrationBuilder.DropTable(
                name: "live_session_trivia_snapshot_questions");

            migrationBuilder.DropTable(
                name: "live_session_trivia_snapshots");

            migrationBuilder.DropColumn(
                name: "session_mode",
                table: "live_sessions");

            migrationBuilder.DropColumn(
                name: "source_trivia_quiz_id",
                table: "live_sessions");

            migrationBuilder.CreateTable(
                name: "live_session_mission_runtime_snapshots",
                columns: table => new
                {
                    live_session_id = table.Column<Guid>(type: "uuid", nullable: false),
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    source_mission_id = table.Column<Guid>(type: "uuid", nullable: false),
                    mission_title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    maximum_time_minutes = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_live_session_mission_runtime_snapshots", x => x.live_session_id);
                    table.ForeignKey(
                        name: "FK_live_session_mission_runtime_snapshots_live_sessions_live_s~",
                        column: x => x.live_session_id,
                        principalTable: "live_sessions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "live_session_mission_runtime_snapshot_stages",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    sequence_order = table.Column<int>(type: "integer", nullable: false),
                    live_session_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_live_session_mission_runtime_snapshot_stages", x => x.id);
                    table.ForeignKey(
                        name: "FK_live_session_mission_runtime_snapshot_stages_live_session_m~",
                        column: x => x.live_session_id,
                        principalTable: "live_session_mission_runtime_snapshots",
                        principalColumn: "live_session_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "live_session_mission_runtime_snapshot_targets",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    substage_snapshot_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    qr_code = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    sequence_order = table.Column<int>(type: "integer", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    clue_text = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    clue_visibility_policy = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    live_session_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_live_session_mission_runtime_snapshot_targets", x => x.id);
                    table.ForeignKey(
                        name: "FK_live_session_mission_runtime_snapshot_targets_live_session_~",
                        column: x => x.live_session_id,
                        principalTable: "live_session_mission_runtime_snapshots",
                        principalColumn: "live_session_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "live_session_mission_runtime_snapshot_trivia_questions",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    substage_snapshot_id = table.Column<Guid>(type: "uuid", nullable: false),
                    prompt = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    sequence_order = table.Column<int>(type: "integer", nullable: false),
                    score_value = table.Column<int>(type: "integer", nullable: false),
                    time_limit_seconds = table.Column<int>(type: "integer", nullable: false),
                    explanation = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    live_session_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_live_session_mission_runtime_snapshot_trivia_questions", x => x.id);
                    table.ForeignKey(
                        name: "FK_live_session_mission_runtime_snapshot_trivia_questions_live~",
                        column: x => x.live_session_id,
                        principalTable: "live_session_mission_runtime_snapshots",
                        principalColumn: "live_session_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "live_session_mission_runtime_snapshot_substages",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    sequence_order = table.Column<int>(type: "integer", nullable: false),
                    play_mode = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    winner_score = table.Column<int>(type: "integer", nullable: true),
                    stage_snapshot_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_live_session_mission_runtime_snapshot_substages", x => x.id);
                    table.ForeignKey(
                        name: "FK_live_session_mission_runtime_snapshot_substages_live_sessio~",
                        column: x => x.stage_snapshot_id,
                        principalTable: "live_session_mission_runtime_snapshot_stages",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "live_session_mission_runtime_snapshot_trivia_options",
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
                    table.PrimaryKey("PK_live_session_mission_runtime_snapshot_trivia_options", x => x.id);
                    table.ForeignKey(
                        name: "FK_live_session_mission_runtime_snapshot_trivia_options_live_s~",
                        column: x => x.trivia_question_snapshot_id,
                        principalTable: "live_session_mission_runtime_snapshot_trivia_questions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_live_session_mission_runtime_snapshot_stages_live_session_i~",
                table: "live_session_mission_runtime_snapshot_stages",
                columns: new[] { "live_session_id", "sequence_order" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_live_session_mission_runtime_snapshot_substages_stage_snaps~",
                table: "live_session_mission_runtime_snapshot_substages",
                columns: new[] { "stage_snapshot_id", "sequence_order" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_live_session_mission_runtime_snapshot_targets_live_session_~",
                table: "live_session_mission_runtime_snapshot_targets",
                columns: new[] { "live_session_id", "qr_code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_live_session_mission_runtime_snapshot_targets_live_session~1",
                table: "live_session_mission_runtime_snapshot_targets",
                columns: new[] { "live_session_id", "substage_snapshot_id", "sequence_order" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_live_session_mission_runtime_snapshot_trivia_options_trivia~",
                table: "live_session_mission_runtime_snapshot_trivia_options",
                columns: new[] { "trivia_question_snapshot_id", "sequence_order" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_live_session_mission_runtime_snapshot_trivia_questions_live~",
                table: "live_session_mission_runtime_snapshot_trivia_questions",
                columns: new[] { "live_session_id", "substage_snapshot_id", "sequence_order" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "live_session_mission_runtime_snapshot_substages");

            migrationBuilder.DropTable(
                name: "live_session_mission_runtime_snapshot_targets");

            migrationBuilder.DropTable(
                name: "live_session_mission_runtime_snapshot_trivia_options");

            migrationBuilder.DropTable(
                name: "live_session_mission_runtime_snapshot_stages");

            migrationBuilder.DropTable(
                name: "live_session_mission_runtime_snapshot_trivia_questions");

            migrationBuilder.DropTable(
                name: "live_session_mission_runtime_snapshots");

            migrationBuilder.AddColumn<string>(
                name: "session_mode",
                table: "live_sessions",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "");

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
                    explanation = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    prompt = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    score_value = table.Column<int>(type: "integer", nullable: false),
                    sequence_order = table.Column<int>(type: "integer", nullable: false),
                    time_limit_seconds = table.Column<int>(type: "integer", nullable: false),
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
                    is_correct = table.Column<bool>(type: "boolean", nullable: false),
                    option_text = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    sequence_order = table.Column<int>(type: "integer", nullable: false),
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
    }
}

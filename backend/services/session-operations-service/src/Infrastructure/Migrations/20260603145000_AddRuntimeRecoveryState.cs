using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace umbral_backend.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddRuntimeRecoveryState : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "live_sessions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    session_mode = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    source_entity_type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    source_entity_id = table.Column<Guid>(type: "uuid", nullable: false),
                    session_code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    title_snapshot = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    state = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    scheduled_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    started_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    paused_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ended_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    cancelled_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    last_state_changed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    state_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    maximum_time_minutes = table.Column<int>(type: "integer", nullable: false),
                    assigned_operator_user_id = table.Column<int>(type: "integer", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_live_sessions", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "live_session_join_contexts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    live_session_id = table.Column<Guid>(type: "uuid", nullable: false),
                    team_id = table.Column<Guid>(type: "uuid", nullable: true),
                    join_token_id = table.Column<Guid>(type: "uuid", nullable: true),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    consumed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_live_session_join_contexts", x => x.id);
                    table.ForeignKey(
                        name: "FK_live_session_join_contexts_live_sessions_live_session_id",
                        column: x => x.live_session_id,
                        principalTable: "live_sessions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "live_session_participants",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    live_session_id = table.Column<Guid>(type: "uuid", nullable: false),
                    external_identity_id = table.Column<Guid>(type: "uuid", nullable: false),
                    display_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    participant_status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    joined_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    last_seen_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_live_session_participants", x => x.id);
                    table.ForeignKey(
                        name: "FK_live_session_participants_live_sessions_live_session_id",
                        column: x => x.live_session_id,
                        principalTable: "live_sessions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "live_session_teams",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    live_session_id = table.Column<Guid>(type: "uuid", nullable: false),
                    team_code = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    display_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    team_capacity = table.Column<int>(type: "integer", nullable: false),
                    current_score = table.Column<int>(type: "integer", nullable: true),
                    current_progress_node_id = table.Column<Guid>(type: "uuid", nullable: true),
                    current_clue_node_id = table.Column<Guid>(type: "uuid", nullable: true),
                    released_clue_count = table.Column<int>(type: "integer", nullable: false),
                    last_score_calculated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    join_status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_live_session_teams", x => x.id);
                    table.ForeignKey(
                        name: "FK_live_session_teams_live_sessions_live_session_id",
                        column: x => x.live_session_id,
                        principalTable: "live_sessions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "live_session_team_members",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    team_id = table.Column<Guid>(type: "uuid", nullable: false),
                    session_participant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    membership_status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    joined_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    left_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_live_session_team_members", x => x.id);
                    table.ForeignKey(
                        name: "FK_live_session_team_members_live_session_teams_team_id",
                        column: x => x.team_id,
                        principalTable: "live_session_teams",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_live_session_join_contexts_live_session_id_team_id",
                table: "live_session_join_contexts",
                columns: new[] { "live_session_id", "team_id" });

            migrationBuilder.CreateIndex(
                name: "IX_live_session_participants_live_session_id_external_identity~",
                table: "live_session_participants",
                columns: new[] { "live_session_id", "external_identity_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_live_session_team_members_team_id_session_participant_id",
                table: "live_session_team_members",
                columns: new[] { "team_id", "session_participant_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_live_session_teams_live_session_id_team_code",
                table: "live_session_teams",
                columns: new[] { "live_session_id", "team_code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_live_sessions_session_code",
                table: "live_sessions",
                column: "session_code",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "live_session_join_contexts");

            migrationBuilder.DropTable(
                name: "live_session_participants");

            migrationBuilder.DropTable(
                name: "live_session_team_members");

            migrationBuilder.DropTable(
                name: "live_session_teams");

            migrationBuilder.DropTable(
                name: "live_sessions");
        }
    }
}

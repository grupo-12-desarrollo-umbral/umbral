using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace umbral_backend.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ReleaseClueByClueId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_live_session_clue_releases_live_session_id_team_id_target_id",
                table: "live_session_clue_releases");

            migrationBuilder.AlterColumn<Guid>(
                name: "target_id",
                table: "live_session_clue_releases",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.CreateIndex(
                name: "IX_live_session_clue_releases_live_session_id_team_id_clue_id",
                table: "live_session_clue_releases",
                columns: new[] { "live_session_id", "team_id", "clue_id" },
                unique: true,
                filter: "clue_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_live_session_clue_releases_live_session_id_team_id_target_id",
                table: "live_session_clue_releases",
                columns: new[] { "live_session_id", "team_id", "target_id" },
                unique: true,
                filter: "target_id IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_live_session_clue_releases_exactly_one_subject",
                table: "live_session_clue_releases",
                sql: "(target_id IS NOT NULL) <> (clue_id IS NOT NULL)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_live_session_clue_releases_live_session_id_team_id_clue_id",
                table: "live_session_clue_releases");

            migrationBuilder.DropIndex(
                name: "IX_live_session_clue_releases_live_session_id_team_id_target_id",
                table: "live_session_clue_releases");

            migrationBuilder.DropCheckConstraint(
                name: "CK_live_session_clue_releases_exactly_one_subject",
                table: "live_session_clue_releases");

            migrationBuilder.AlterColumn<Guid>(
                name: "target_id",
                table: "live_session_clue_releases",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_live_session_clue_releases_live_session_id_team_id_target_id",
                table: "live_session_clue_releases",
                columns: new[] { "live_session_id", "team_id", "target_id" },
                unique: true);
        }
    }
}

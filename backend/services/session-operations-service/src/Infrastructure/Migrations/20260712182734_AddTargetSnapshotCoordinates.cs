using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace umbral_backend.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTargetSnapshotCoordinates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "latitude",
                table: "live_session_mission_runtime_snapshot_targets",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<double>(
                name: "longitude",
                table: "live_session_mission_runtime_snapshot_targets",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "latitude",
                table: "live_session_mission_runtime_snapshot_targets");

            migrationBuilder.DropColumn(
                name: "longitude",
                table: "live_session_mission_runtime_snapshot_targets");
        }
    }
}

using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace umbral_backend.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class DropSessionTimerColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "session_timer_advancing_since",
                table: "live_sessions");

            migrationBuilder.DropColumn(
                name: "session_timer_expired_at",
                table: "live_sessions");

            migrationBuilder.DropColumn(
                name: "session_timer_remaining_duration",
                table: "live_sessions");

            migrationBuilder.DropColumn(
                name: "session_timer_total_duration",
                table: "live_sessions");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "session_timer_advancing_since",
                table: "live_sessions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "session_timer_expired_at",
                table: "live_sessions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<TimeSpan>(
                name: "session_timer_remaining_duration",
                table: "live_sessions",
                type: "interval",
                nullable: false,
                defaultValue: new TimeSpan(0, 0, 0, 0, 0));

            migrationBuilder.AddColumn<TimeSpan>(
                name: "session_timer_total_duration",
                table: "live_sessions",
                type: "interval",
                nullable: false,
                defaultValue: new TimeSpan(0, 0, 0, 0, 0));
        }
    }
}

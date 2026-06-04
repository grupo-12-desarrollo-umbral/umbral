using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace umbral_backend.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAuthoritativeSessionTimerState : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<TimeSpan>(
                name: "session_timer_total_duration",
                table: "live_sessions",
                type: "interval",
                nullable: false,
                defaultValue: TimeSpan.Zero);

            migrationBuilder.AddColumn<TimeSpan>(
                name: "session_timer_remaining_duration",
                table: "live_sessions",
                type: "interval",
                nullable: false,
                defaultValue: TimeSpan.Zero);

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

            migrationBuilder.Sql("""
                UPDATE live_sessions
                SET
                    session_timer_total_duration = make_interval(mins => maximum_time_minutes),
                    session_timer_remaining_duration = make_interval(mins => maximum_time_minutes)
                WHERE session_timer_total_duration = interval '00:00:00'
                    AND session_timer_remaining_duration = interval '00:00:00';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "session_timer_total_duration",
                table: "live_sessions");

            migrationBuilder.DropColumn(
                name: "session_timer_remaining_duration",
                table: "live_sessions");

            migrationBuilder.DropColumn(
                name: "session_timer_advancing_since",
                table: "live_sessions");

            migrationBuilder.DropColumn(
                name: "session_timer_expired_at",
                table: "live_sessions");
        }
    }
}

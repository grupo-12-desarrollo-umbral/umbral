using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using umbral_backend.Infrastructure.Persistence;

#nullable disable

namespace umbral_backend.Infrastructure.Migrations
{
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260604143000_AddTriviaRoundState")]
    public partial class AddTriviaRoundState : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "active_question_index",
                table: "live_sessions",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<TimeSpan>(
                name: "question_timer_total_duration",
                table: "live_sessions",
                type: "interval",
                nullable: false,
                defaultValue: TimeSpan.Zero);

            migrationBuilder.AddColumn<TimeSpan>(
                name: "question_timer_remaining_duration",
                table: "live_sessions",
                type: "interval",
                nullable: false,
                defaultValue: TimeSpan.Zero);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "question_timer_advancing_since",
                table: "live_sessions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "question_timer_expired_at",
                table: "live_sessions",
                type: "timestamp with time zone",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "active_question_index",
                table: "live_sessions");

            migrationBuilder.DropColumn(
                name: "question_timer_total_duration",
                table: "live_sessions");

            migrationBuilder.DropColumn(
                name: "question_timer_remaining_duration",
                table: "live_sessions");

            migrationBuilder.DropColumn(
                name: "question_timer_advancing_since",
                table: "live_sessions");

            migrationBuilder.DropColumn(
                name: "question_timer_expired_at",
                table: "live_sessions");
        }
    }
}

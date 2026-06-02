using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace umbral_backend.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTriviaQuestionAuthoringMetadata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Explanation",
                table: "TriviaQuestions",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ScoreValue",
                table: "TriviaQuestions",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TimeLimitSeconds",
                table: "TriviaQuestions",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Explanation",
                table: "TriviaQuestions");

            migrationBuilder.DropColumn(
                name: "ScoreValue",
                table: "TriviaQuestions");

            migrationBuilder.DropColumn(
                name: "TimeLimitSeconds",
                table: "TriviaQuestions");
        }
    }
}

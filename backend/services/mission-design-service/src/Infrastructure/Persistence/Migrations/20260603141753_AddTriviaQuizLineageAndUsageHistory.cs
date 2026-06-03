using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace umbral_backend.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTriviaQuizLineageAndUsageHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "HasUsageHistory",
                table: "TriviaQuizzes",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "SourceTriviaQuizId",
                table: "TriviaQuizzes",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_TriviaQuizzes_SourceTriviaQuizId",
                table: "TriviaQuizzes",
                column: "SourceTriviaQuizId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TriviaQuizzes_SourceTriviaQuizId",
                table: "TriviaQuizzes");

            migrationBuilder.DropColumn(
                name: "HasUsageHistory",
                table: "TriviaQuizzes");

            migrationBuilder.DropColumn(
                name: "SourceTriviaQuizId",
                table: "TriviaQuizzes");
        }
    }
}

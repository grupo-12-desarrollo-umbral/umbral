using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace umbral_backend.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RemoveTriviaQuestionSequenceOrder : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TriviaQuestions_TriviaQuizId_SequenceOrder",
                table: "TriviaQuestions");

            migrationBuilder.DropColumn(
                name: "SequenceOrder",
                table: "TriviaQuestions");

            migrationBuilder.CreateIndex(
                name: "IX_TriviaQuestions_TriviaQuizId",
                table: "TriviaQuestions",
                column: "TriviaQuizId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TriviaQuestions_TriviaQuizId",
                table: "TriviaQuestions");

            migrationBuilder.AddColumn<int>(
                name: "SequenceOrder",
                table: "TriviaQuestions",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.Sql(
                """
                UPDATE "TriviaQuestions" AS question
                SET "SequenceOrder" = ordered."SequenceOrder"
                FROM (
                    SELECT "Id",
                           ROW_NUMBER() OVER (PARTITION BY "TriviaQuizId" ORDER BY "Id") AS "SequenceOrder"
                    FROM "TriviaQuestions"
                ) AS ordered
                WHERE question."Id" = ordered."Id";
                """);

            migrationBuilder.CreateIndex(
                name: "IX_TriviaQuestions_TriviaQuizId_SequenceOrder",
                table: "TriviaQuestions",
                columns: new[] { "TriviaQuizId", "SequenceOrder" },
                unique: true);
        }
    }
}

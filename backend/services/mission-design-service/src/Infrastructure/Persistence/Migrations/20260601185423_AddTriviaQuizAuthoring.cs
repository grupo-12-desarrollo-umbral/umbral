using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace umbral_backend.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTriviaQuizAuthoring : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TriviaQuizzes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Created = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastModified = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TriviaQuizzes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TriviaQuestions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Prompt = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    SequenceOrder = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    TriviaQuizId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TriviaQuestions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TriviaQuestions_TriviaQuizzes_TriviaQuizId",
                        column: x => x.TriviaQuizId,
                        principalTable: "TriviaQuizzes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TriviaOptions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    OptionText = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    SequenceOrder = table.Column<int>(type: "integer", nullable: false),
                    IsCorrect = table.Column<bool>(type: "boolean", nullable: false),
                    TriviaQuestionId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TriviaOptions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TriviaOptions_TriviaQuestions_TriviaQuestionId",
                        column: x => x.TriviaQuestionId,
                        principalTable: "TriviaQuestions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TriviaOptions_TriviaQuestionId_SequenceOrder",
                table: "TriviaOptions",
                columns: new[] { "TriviaQuestionId", "SequenceOrder" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TriviaQuestions_TriviaQuizId_SequenceOrder",
                table: "TriviaQuestions",
                columns: new[] { "TriviaQuizId", "SequenceOrder" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TriviaQuizzes_Status",
                table: "TriviaQuizzes",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TriviaOptions");

            migrationBuilder.DropTable(
                name: "TriviaQuestions");

            migrationBuilder.DropTable(
                name: "TriviaQuizzes");
        }
    }
}

using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace umbral_backend.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMissionCompositePersistence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MissionStages",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    MissionId = table.Column<int>(type: "integer", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    SequenceOrder = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MissionStages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MissionStages_Missions_MissionId",
                        column: x => x.MissionId,
                        principalTable: "Missions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MissionSubstages",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PlayMode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    WinnerScore = table.Column<int>(type: "integer", nullable: true),
                    TriviaQuizId = table.Column<int>(type: "integer", nullable: true),
                    StageId = table.Column<int>(type: "integer", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    SequenceOrder = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MissionSubstages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MissionSubstages_MissionStages_StageId",
                        column: x => x.StageId,
                        principalTable: "MissionStages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MissionClues",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Text = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    Visibility = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    SubstageId = table.Column<int>(type: "integer", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    SequenceOrder = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MissionClues", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MissionClues_MissionSubstages_SubstageId",
                        column: x => x.SubstageId,
                        principalTable: "MissionSubstages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MissionTargets",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    QrCode = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    SequenceOrder = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    ClueId = table.Column<int>(type: "integer", nullable: true),
                    SubstageId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MissionTargets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MissionTargets_MissionSubstages_SubstageId",
                        column: x => x.SubstageId,
                        principalTable: "MissionSubstages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MissionClues_SubstageId_SequenceOrder",
                table: "MissionClues",
                columns: new[] { "SubstageId", "SequenceOrder" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MissionStages_MissionId_SequenceOrder",
                table: "MissionStages",
                columns: new[] { "MissionId", "SequenceOrder" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MissionSubstages_StageId_SequenceOrder",
                table: "MissionSubstages",
                columns: new[] { "StageId", "SequenceOrder" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MissionTargets_SubstageId_SequenceOrder",
                table: "MissionTargets",
                columns: new[] { "SubstageId", "SequenceOrder" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MissionClues");

            migrationBuilder.DropTable(
                name: "MissionTargets");

            migrationBuilder.DropTable(
                name: "MissionSubstages");

            migrationBuilder.DropTable(
                name: "MissionStages");
        }
    }
}

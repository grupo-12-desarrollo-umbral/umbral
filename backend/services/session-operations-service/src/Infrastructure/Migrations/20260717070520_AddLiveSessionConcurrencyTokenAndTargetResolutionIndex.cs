using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace umbral_backend.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddLiveSessionConcurrencyTokenAndTargetResolutionIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // The scaffolded AddColumn<uint>("xmin") is deliberately omitted here, while the model
            // snapshot deliberately keeps the property. xmin is a Postgres system column present on
            // every table: adding it errors, and it needs no DDL to be readable. The mapping exists
            // only so EF emits `WHERE xmin = @original` on UPDATE. Npgsql's UseXminAsConcurrencyToken()
            // suppressed this scaffolding until it was dropped in v9.
            //
            // Consequence: any future migration touching live_sessions re-scaffolds the same
            // AddColumn. Delete it there too.
            migrationBuilder.CreateIndex(
                name: "ux_treasure_evidence_accepted_target",
                table: "live_session_treasure_evidence_submissions",
                columns: new[] { "live_session_id", "team_id", "target_snapshot_id" },
                unique: true,
                filter: "validation_state = 'Accepted'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ux_treasure_evidence_accepted_target",
                table: "live_session_treasure_evidence_submissions");

            // No DropColumn for xmin: Up never created it (see above), and dropping a system column
            // would error.
        }
    }
}

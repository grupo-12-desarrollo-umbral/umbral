using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace umbral_backend.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddRankingConcurrencyToken : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // The scaffolded AddColumn<uint>("xmin") is deliberately omitted here, while the model
            // snapshot deliberately keeps the property. xmin is a Postgres system column present on
            // every table: adding it errors, and it needs no DDL to be readable. The mapping exists
            // only so EF emits `WHERE xmin = @original` on UPDATE, serialising concurrent ranking
            // recalculations. Npgsql's UseXminAsConcurrencyToken() suppressed this scaffolding until it
            // was dropped in v9.
            //
            // Consequence: any future migration touching rankings re-scaffolds the same AddColumn.
            // Delete it there too.
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // No DropColumn for xmin: Up never created it (see above), and dropping a system column
            // would error.
        }
    }
}

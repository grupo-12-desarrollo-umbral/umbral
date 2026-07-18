using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace umbral_backend.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ClampMissionMaximumTimeToThirty : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // The mission maximum time is now capped at 30 minutes (see MaximumTime.MaximumMinutes).
            // Clamp any pre-existing rows above the cap so they remain editable under the new rule.
            migrationBuilder.Sql(
                """
                UPDATE "Missions"
                SET "MaximumTimeMinutes" = 30
                WHERE "MaximumTimeMinutes" > 30;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // The original above-cap values are not recoverable, so the down migration is a no-op.
            // Reverting the code cap is enough to accept larger values again going forward.
        }
    }
}

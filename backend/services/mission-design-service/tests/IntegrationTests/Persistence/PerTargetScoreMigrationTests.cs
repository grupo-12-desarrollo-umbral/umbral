using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using Testcontainers.PostgreSql;
using umbral_backend.Infrastructure.Persistence;

namespace umbral_backend.Infrastructure.IntegrationTests.Persistence;

/// <summary>
/// Exercises the DES-86 F3 migration against rows, not an empty schema. The shared
/// <see cref="PostgreSqlFixture"/> migrates a fresh database, so it never reaches the
/// backfill or the SET NOT NULL guard — both only do anything when targets already exist.
/// </summary>
public sealed class PerTargetScoreMigrationTests
{
    private const string ExpandMigration = "20260710031020_AddPerTargetScoreExpand";
    private const string ContractMigration = "20260710045617_DropSubstageWinnerScoreMakeTargetScoreRequired";

    [Fact]
    public async Task ContractMigration_BackfillsTargetScoreFromSubstageWinnerScore()
    {
        await using var database = await MigratedToExpandAsync();

        // A target added to a winner-scored substage after F1 ran: F1's own backfill has
        // already been and gone, so this row reaches F3 with a NULL score.
        await database.SeedAsync(substageWinnerScore: 70, targetScore: null);

        await database.MigrateToContractAsync();

        var score = await database.ScalarAsync("""SELECT "Score" FROM "MissionTargets";""");
        score.Should().Be(70);
    }

    [Fact]
    public async Task ContractMigration_PreservesAnExistingPerTargetScore()
    {
        await using var database = await MigratedToExpandAsync();

        await database.SeedAsync(substageWinnerScore: 70, targetScore: 25);

        await database.MigrateToContractAsync();

        var score = await database.ScalarAsync("""SELECT "Score" FROM "MissionTargets";""");
        score.Should().Be(25);
    }

    [Fact]
    public async Task ContractMigration_FailsLoudly_WhenATargetHasNoScoreToInheritFrom()
    {
        await using var database = await MigratedToExpandAsync();

        // A substage that never carried a winner score: an invalid draft that never passed
        // readiness. F3 refuses to fabricate a score for it.
        await database.SeedAsync(substageWinnerScore: null, targetScore: null);

        Func<Task> migrate = database.MigrateToContractAsync;

        var exception = await migrate.Should().ThrowAsync<PostgresException>();
        exception.Which.SqlState.Should().Be(PostgresErrorCodes.NotNullViolation);
    }

    private static async Task<MigrationHarness> MigratedToExpandAsync()
    {
        var harness = await MigrationHarness.StartAsync();
        await harness.MigrateAsync(ExpandMigration);
        return harness;
    }

    private sealed class MigrationHarness : IAsyncDisposable
    {
        private readonly PostgreSqlContainer _postgres;

        private MigrationHarness(PostgreSqlContainer postgres) => _postgres = postgres;

        public static async Task<MigrationHarness> StartAsync()
        {
            Environment.SetEnvironmentVariable("TESTCONTAINERS_RYUK_DISABLED", "true");

            var postgres = new PostgreSqlBuilder().WithImage("postgres:16").Build();
            await postgres.StartAsync();

            return new MigrationHarness(postgres);
        }

        public Task MigrateToContractAsync() => MigrateAsync(ContractMigration);

        public async Task MigrateAsync(string targetMigration)
        {
            await using var context = CreateContext();
            var migrator = context.GetInfrastructure().GetRequiredService<IMigrator>();
            await migrator.MigrateAsync(targetMigration);
        }

        public async Task SeedAsync(int? substageWinnerScore, int? targetScore)
        {
            await using var context = CreateContext();

            await context.Database.ExecuteSqlRawAsync(
                """
                INSERT INTO "Missions"
                    ("Id", "Name", "Description", "Difficulty", "MaximumTimeMinutes",
                     "IsActive", "ActivationState", "Created", "LastModified")
                VALUES (1, 'Mission', 'Description', 'Easy', 30, true, 'Draft', now(), now());

                INSERT INTO "MissionStages" ("Id", "MissionId", "Title", "SequenceOrder")
                VALUES (1, 1, 'Stage', 1);

                INSERT INTO "MissionSubstages"
                    ("Id", "StageId", "Title", "SequenceOrder", "PlayMode", "WinnerScore")
                VALUES (1, 1, 'Substage', 1, 'TreasureHunt', {0});

                INSERT INTO "MissionTargets"
                    ("Id", "SubstageId", "Name", "QrCode", "SequenceOrder", "IsActive", "Score")
                VALUES (1, 1, 'Target', 'QR-1', 1, true, {1});
                """
                    .Replace("{0}", ToSqlLiteral(substageWinnerScore))
                    .Replace("{1}", ToSqlLiteral(targetScore)));
        }

        public async Task<int?> ScalarAsync(string sql)
        {
            await using var context = CreateContext();
            await using var command = context.Database.GetDbConnection().CreateCommand();

            await context.Database.OpenConnectionAsync();
            command.CommandText = sql;

            return await command.ExecuteScalarAsync() as int?;
        }

        public async ValueTask DisposeAsync() => await _postgres.DisposeAsync();

        private ApplicationDbContext CreateContext()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseNpgsql(_postgres.GetConnectionString())
                .Options;

            return new ApplicationDbContext(options);
        }

        private static string ToSqlLiteral(int? value) =>
            value is null ? "NULL" : value.Value.ToString();
    }
}

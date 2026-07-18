using umbral_backend.Domain.Entities;
using umbral_backend.Domain.Enums;
using umbral_backend.Domain.ValueObjects;
using umbral_backend.Infrastructure.Persistence.Repositories;

namespace umbral_backend.ScoringMonitoring.IntegrationTests.Persistence;

[Collection(PostgreSqlCollection.Name)]
public sealed class ScoreEntryRepositoryIntegrationTests
{
    private readonly PersistenceTestContextFactory _contextFactory;

    public ScoreEntryRepositoryIntegrationTests(PostgreSqlFixture fixture)
    {
        _contextFactory = new PersistenceTestContextFactory(fixture.ConnectionString);
    }

    [Fact]
    public async Task AddAndList_RoundTripsLedgerEntry_AndConcurrentDuplicateSourceCollapsesToNoOp()
    {
        var liveSessionId = Guid.NewGuid();
        var teamId = Guid.NewGuid();
        var sourceEntityId = Guid.NewGuid();

        await using (var writeContext = _contextFactory.Create())
        {
            var repository = new ScoreEntryRepository(writeContext);

            var scoreEntry = ScoreEntry.Grant(
                liveSessionId,
                teamId,
                "Team A",
                "trivia-answer-correct",
                ScoreValue.Create(150),
                DateTimeOffset.UtcNow,
                ScoreSourceType.TriviaAnswerSubmission,
                sourceEntityId);

            await repository.AddAsync(scoreEntry, CancellationToken.None);
        }

        await using (var readContext = _contextFactory.Create())
        {
            var repository = new ScoreEntryRepository(readContext);

            var entries = await repository.ListByLiveSessionIdAsync(liveSessionId, CancellationToken.None);
            var exists = await repository.ExistsForSourceAsync(
                ScoreSourceType.TriviaAnswerSubmission,
                sourceEntityId,
                CancellationToken.None);

            entries.Should().ContainSingle();
            entries[0].TeamId.Should().Be(teamId);
            entries[0].ScoreValue.Value.Should().Be(150);
            exists.Should().BeTrue();
        }

        await using var duplicateContext = _contextFactory.Create();
        var duplicateRepository = new ScoreEntryRepository(duplicateContext);

        var duplicateEntry = ScoreEntry.Grant(
            liveSessionId,
            teamId,
            "Team A",
            "trivia-answer-correct",
            ScoreValue.Create(150),
            DateTimeOffset.UtcNow.AddSeconds(1),
            ScoreSourceType.TriviaAnswerSubmission,
            sourceEntityId);

        // A lost race on the (source_entity_type, source_entity_id) unique index is the idempotency
        // guard firing at the database: recording a source exactly once is the goal, so the repository
        // collapses the duplicate insert into a no-op instead of leaking a raw DbUpdateException.
        var act = async () => await duplicateRepository.AddAsync(duplicateEntry, CancellationToken.None);

        await act.Should().NotThrowAsync();

        await using var assertContext = _contextFactory.Create();
        var assertRepository = new ScoreEntryRepository(assertContext);
        var entriesAfterDuplicate = await assertRepository.ListByLiveSessionIdAsync(liveSessionId, CancellationToken.None);

        entriesAfterDuplicate.Should().ContainSingle();
    }
}

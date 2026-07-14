using Microsoft.EntityFrameworkCore;
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
    public async Task AddAndList_RoundTripsLedgerEntry_AndUniqueSourceIndexRejectsDuplicate()
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
            "trivia-answer-correct",
            ScoreValue.Create(150),
            DateTimeOffset.UtcNow.AddSeconds(1),
            ScoreSourceType.TriviaAnswerSubmission,
            sourceEntityId);

        var act = async () => await duplicateRepository.AddAsync(duplicateEntry, CancellationToken.None);

        await act.Should().ThrowAsync<DbUpdateException>();
    }
}

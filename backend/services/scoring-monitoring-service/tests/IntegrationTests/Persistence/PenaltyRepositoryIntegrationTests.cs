using Microsoft.EntityFrameworkCore;
using umbral_backend.Domain.Entities;
using umbral_backend.Domain.Enums;
using umbral_backend.Domain.ValueObjects;
using umbral_backend.Infrastructure.Persistence.Repositories;

namespace umbral_backend.ScoringMonitoring.IntegrationTests.Persistence;

[Collection(PostgreSqlCollection.Name)]
public sealed class PenaltyRepositoryIntegrationTests
{
    private readonly PersistenceTestContextFactory _contextFactory;

    public PenaltyRepositoryIntegrationTests(PostgreSqlFixture fixture)
    {
        _contextFactory = new PersistenceTestContextFactory(fixture.ConnectionString);
    }

    [Fact]
    public async Task AddAsync_RoundTripsPenaltyWithScoreEntry_AllFieldsPreserved()
    {
        var liveSessionId = Guid.NewGuid();
        var teamId = Guid.NewGuid();
        var appliedByUserId = Guid.NewGuid();

        var scoreEntry = ScoreEntry.Penalty(
            liveSessionId,
            teamId,
            "Excessive celebration",
            ScoreValue.Create(50),
            appliedByUserId);

        await using (var writeContext = _contextFactory.Create())
        {
            var scoreEntryRepository = new ScoreEntryRepository(writeContext);
            var penaltyRepository = new PenaltyRepository(writeContext);

            await scoreEntryRepository.AddAsync(scoreEntry, CancellationToken.None);
            await penaltyRepository.AddAsync(
                Domain.Entities.Penalty.Create(scoreEntry.ScoreEntryId, "Excessive celebration", appliedByUserId),
                CancellationToken.None);
        }

        await using (var readContext = _contextFactory.Create())
        {
            var persistedScoreEntry = await readContext.ScoreEntries
                .SingleOrDefaultAsync(e => e.ScoreEntryId == scoreEntry.ScoreEntryId);

            var persistedPenalty = await readContext.Set<Domain.Entities.Penalty>()
                .SingleOrDefaultAsync(p => p.ScoreEntryId == scoreEntry.ScoreEntryId);

            persistedScoreEntry.Should().NotBeNull();
            persistedScoreEntry!.EntryType.Should().Be(ScoreEntryType.Penalty);
            persistedScoreEntry.ScoreValue.Value.Should().Be(50);
            persistedScoreEntry.LiveSessionId.Should().Be(liveSessionId);
            persistedScoreEntry.TeamId.Should().Be(teamId);
            persistedScoreEntry.SourceEntityType.Should().Be(ScoreSourceType.Penalty);

            persistedPenalty.Should().NotBeNull();
            persistedPenalty!.ScoreEntryId.Should().Be(scoreEntry.ScoreEntryId);
            persistedPenalty.PenaltyReason.Value.Should().Be("Excessive celebration");
            persistedPenalty.AppliedByUserId.Should().Be(appliedByUserId);
            persistedPenalty.AppliedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5));
        }
    }
}

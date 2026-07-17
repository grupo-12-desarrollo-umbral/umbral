using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Domain.Common;
using umbral_backend.Domain.Entities;
using umbral_backend.Domain.Enums;
using umbral_backend.Domain.Events;
using umbral_backend.Domain.Exceptions;
using umbral_backend.Domain.Services;
using umbral_backend.Domain.ValueObjects;
using umbral_backend.Infrastructure.Persistence.Repositories;

namespace umbral_backend.ScoringMonitoring.IntegrationTests.Persistence;

// Proves the two DB-level guards on concurrent recalculations of one session's ranking actually fire,
// and that the repository reports both as the same recoverable ConcurrentRankingModificationException.
// They cover complementary halves: the xmin token catches two recalcs racing to update an existing
// ranking, and the live_session_id unique index catches two recalcs racing to create the first one.
// The last test follows the retry through to prove a stale projection can never overwrite a newer one.
[Collection(PostgreSqlCollection.Name)]
public sealed class RankingConcurrencyIntegrationTests
{
    private static readonly DateTimeOffset SessionStart = new(2026, 7, 17, 18, 0, 0, TimeSpan.Zero);

    private readonly PersistenceTestContextFactory _contextFactory;
    private readonly IRankingPolicy _rankingPolicy = new ResolutionTimeRankingPolicy();

    public RankingConcurrencyIntegrationTests(PostgreSqlFixture fixture)
    {
        _contextFactory = new PersistenceTestContextFactory(fixture.ConnectionString);
    }

    // Two recalcs load the same ranking (same xmin) and both refresh it. The token must let the first
    // commit and reject the second, rather than silently applying a last-write-wins overwrite that
    // could drop a team's score.
    [Fact]
    public async Task SaveAsync_WhenRankingUpdatedConcurrently_ThrowsConcurrentRankingModification()
    {
        var liveSessionId = Guid.NewGuid();
        var entries = new[] { GrantEntry(liveSessionId, Guid.NewGuid(), "Alpha", 200, SessionStart) };
        await SeedScoreEntriesAsync(entries);
        await SeedRankingAsync(liveSessionId, entries);

        await using var winnerContext = _contextFactory.Create();
        await using var loserContext = _contextFactory.Create();
        var winnerRepository = new RankingRepository(winnerContext);
        var loserRepository = new RankingRepository(loserContext);

        // Both read the same xmin before either writes.
        var winnerRanking = await winnerRepository.GetByLiveSessionIdAsync(liveSessionId, CancellationToken.None);
        var loserRanking = await loserRepository.GetByLiveSessionIdAsync(liveSessionId, CancellationToken.None);

        winnerRanking!.Refresh(entries, SessionStart.AddMinutes(1), winnerRanking.CalculationVersion + 1, _rankingPolicy);
        await winnerRepository.SaveAsync(winnerRanking, CancellationToken.None);

        loserRanking!.Refresh(entries, SessionStart.AddMinutes(1), loserRanking.CalculationVersion + 1, _rankingPolicy);
        var staleSave = () => loserRepository.SaveAsync(loserRanking, CancellationToken.None);

        await staleSave.Should().ThrowAsync<ConcurrentRankingModificationException>(
            "the second recalc read a stale xmin, so its UPDATE must match no row rather than overwrite the winner");
    }

    // Two consumers recalculate the very first ranking for one session at once: both find no row and
    // both INSERT. The live_session_id unique index must reject the second, and the repository must
    // translate the raw 23505 into the same recoverable conflict so the retry can refresh instead.
    [Fact]
    public async Task SaveAsync_WhenFirstRankingCreatedConcurrently_ThrowsConcurrentRankingModification()
    {
        var liveSessionId = Guid.NewGuid();
        var entries = new[] { GrantEntry(liveSessionId, Guid.NewGuid(), "Alpha", 200, SessionStart) };
        await SeedScoreEntriesAsync(entries);

        await using var winnerContext = _contextFactory.Create();
        await using var loserContext = _contextFactory.Create();
        var winnerRepository = new RankingRepository(winnerContext);
        var loserRepository = new RankingRepository(loserContext);

        // Both observe no existing ranking before either commits.
        (await winnerRepository.GetByLiveSessionIdAsync(liveSessionId, CancellationToken.None)).Should().BeNull();
        (await loserRepository.GetByLiveSessionIdAsync(liveSessionId, CancellationToken.None)).Should().BeNull();

        var winnerRanking = Ranking.Create(liveSessionId, entries, SessionStart.AddMinutes(1), 1, _rankingPolicy);
        await winnerRepository.SaveAsync(winnerRanking, CancellationToken.None);

        var loserRanking = Ranking.Create(liveSessionId, entries, SessionStart.AddMinutes(1), 1, _rankingPolicy);
        var staleCreate = () => loserRepository.SaveAsync(loserRanking, CancellationToken.None);

        await staleCreate.Should().ThrowAsync<ConcurrentRankingModificationException>(
            "rankings.live_session_id must reject the second first-creation, translated to a retryable conflict");

        await using var assertContext = _contextFactory.Create();
        var rows = await new RankingRepository(assertContext)
            .GetByLiveSessionIdAsync(liveSessionId, CancellationToken.None);
        rows!.RankingId.Should().Be(winnerRanking.RankingId, "concurrent first creation settles to exactly one row");
    }

    // Finding 4, the full follow-through. Two score events for one session are committed. A newer recalc
    // observes both and refreshes the ranking to two rows, committing first. A stale recalc — loaded from
    // the same xmin but having seen only the first entry — then loses the race, so its one-row projection
    // can never overwrite the newer two-row one. The retry the behaviour would run re-reads every
    // committed entry and the winner's row, folds all of them, and derives its version from that fresh
    // row — emitting the refresh exactly once, only on the committing attempt.
    [Fact]
    public async Task SaveAsync_WhenStaleProjectionLosesRace_RetryFoldsEveryCommittedEntryExactlyOnce()
    {
        var liveSessionId = Guid.NewGuid();
        var teamAlpha = Guid.NewGuid();
        var teamBeta = Guid.NewGuid();

        var entryOne = GrantEntry(liveSessionId, teamAlpha, "Alpha", 200, SessionStart);
        var entryTwo = GrantEntry(liveSessionId, teamBeta, "Beta", 100, SessionStart.AddSeconds(30));
        var committedEntries = new[] { entryOne, entryTwo };
        await SeedScoreEntriesAsync(committedEntries);

        // The projection as it stood when only the first entry had been recorded: version 1, one row.
        await SeedRankingAsync(liveSessionId, new[] { entryOne }, generatedAt: SessionStart.AddSeconds(1));

        var loserMediator = new RecordingMediator();
        var retryMediator = new RecordingMediator();
        var retryOutbox = new RecordingOutboxDomainEventDispatcher();

        await using var winnerContext = _contextFactory.Create();
        await using var loserContext = _contextFactory.Create(mediator: loserMediator);
        var winnerRepository = new RankingRepository(winnerContext);
        var loserRepository = new RankingRepository(loserContext);

        // Both recalcs load version 1 at the same xmin before either commits.
        var winnerRanking = await winnerRepository.GetByLiveSessionIdAsync(liveSessionId, CancellationToken.None);
        var loserRanking = await loserRepository.GetByLiveSessionIdAsync(liveSessionId, CancellationToken.None);

        // The newer recalc saw both entries and refreshes to two rows: this commits and moves the xmin.
        winnerRanking!.Refresh(committedEntries, SessionStart.AddMinutes(1), winnerRanking.CalculationVersion + 1, _rankingPolicy);
        await winnerRepository.SaveAsync(winnerRanking, CancellationToken.None);

        // The stale recalc, having seen only the first entry, tries to write a one-row projection from
        // the now-stale xmin. It must lose — never overwriting the newer two-row ranking.
        loserRanking!.Refresh(new[] { entryOne }, SessionStart.AddSeconds(45), loserRanking.CalculationVersion + 1, _rankingPolicy);
        var staleSave = () => loserRepository.SaveAsync(loserRanking, CancellationToken.None);
        await staleSave.Should().ThrowAsync<ConcurrentRankingModificationException>(
            "a stale one-entry projection must not overwrite the committed two-entry ranking");
        loserMediator.Published.OfType<RankingRefreshed>().Should().BeEmpty(
            "a rolled-back recalc reaches no post-commit dispatch, so it broadcasts no ranking-refreshed notification");

        // The retry: a fresh read sees the winner's two-row ranking and every committed entry, folds all
        // of them, and derives its version from the freshly loaded row.
        await using var retryContext = _contextFactory.Create(mediator: retryMediator, outboxDispatcher: retryOutbox);
        var retryRepository = new RankingRepository(retryContext);
        var retryRanking = await retryRepository.GetByLiveSessionIdAsync(liveSessionId, CancellationToken.None);
        retryRanking!.Refresh(committedEntries, SessionStart.AddMinutes(2), retryRanking.CalculationVersion + 1, _rankingPolicy);
        await retryRepository.SaveAsync(retryRanking, CancellationToken.None);

        retryMediator.Published.OfType<RankingRefreshed>().Should().ContainSingle(
            "the committing retry broadcasts the refreshed ranking exactly once");
        retryOutbox.Dispatched.OfType<RankingRefreshed>().Should().ContainSingle(
            "the committing retry enqueues exactly one ranking-refreshed outbox message");

        await using var assertContext = _contextFactory.Create();
        var reloaded = await new RankingRepository(assertContext)
            .GetByLiveSessionIdAsync(liveSessionId, CancellationToken.None);

        // The final rows equal a fresh fold over every committed score entry: both teams present, ordered.
        var expected = Ranking.Create(liveSessionId, committedEntries, SessionStart, 1, _rankingPolicy);
        reloaded!.Rows.Select(row => (row.TeamId, row.TotalScore))
            .Should()
            .Equal(expected.Rows.Select(row => (row.TeamId, row.TotalScore)),
                "the settled ranking must match a fold over every committed entry, not the stale one-row snapshot");
        reloaded.CalculationVersion.Should().Be(3, "the retry derives its version from the freshly loaded row (2 + 1)");
    }

    private ScoreEntry GrantEntry(Guid liveSessionId, Guid teamId, string teamName, int value, DateTimeOffset recordedAt) =>
        ScoreEntry.Grant(
            liveSessionId,
            teamId,
            teamName,
            "trivia-answer-correct",
            ScoreValue.Create(value),
            recordedAt,
            ScoreSourceType.TriviaAnswerSubmission,
            Guid.NewGuid());

    private async Task SeedScoreEntriesAsync(IEnumerable<ScoreEntry> entries)
    {
        await using var context = _contextFactory.Create();
        context.ScoreEntries.AddRange(entries);
        await context.SaveChangesAsync(CancellationToken.None);
    }

    private async Task SeedRankingAsync(Guid liveSessionId, IEnumerable<ScoreEntry> entries, DateTimeOffset? generatedAt = null)
    {
        await using var context = _contextFactory.Create();
        var repository = new RankingRepository(context);
        var ranking = Ranking.Create(
            liveSessionId,
            entries,
            generatedAt ?? SessionStart.AddMinutes(1),
            calculationVersion: 1,
            _rankingPolicy);
        await repository.SaveAsync(ranking, CancellationToken.None);
    }

    // Records the notifications the interceptor publishes after a successful save. Because that dispatch
    // runs only in SavedChanges, a rolled-back save publishes nothing — so a recorder on a losing context
    // faithfully shows the failed attempt emits no SignalR notification.
    private sealed class RecordingMediator : IMediator
    {
        public List<object> Published { get; } = [];

        public Task Publish(object notification, CancellationToken cancellationToken = default)
        {
            Published.Add(notification);
            return Task.CompletedTask;
        }

        public Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default)
            where TNotification : INotification
        {
            Published.Add(notification!);
            return Task.CompletedTask;
        }

        public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task Send<TRequest>(TRequest request, CancellationToken cancellationToken = default)
            where TRequest : IRequest =>
            throw new NotSupportedException();

        public Task<object?> Send(object request, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public IAsyncEnumerable<TResponse> CreateStream<TResponse>(
            IStreamRequest<TResponse> request,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public IAsyncEnumerable<object?> CreateStream(object request, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    // Captures the domain events the interceptor routes to the outbox during a SaveChanges. Because the
    // interceptor enqueues inside the same transaction as the business write, only a committed save's
    // events survive in production; the test therefore attaches a recorder only to the committing retry.
    private sealed class RecordingOutboxDomainEventDispatcher : IOutboxDomainEventDispatcher
    {
        public List<BaseEvent> Dispatched { get; } = [];

        public Task DispatchAsync(BaseEvent domainEvent, CancellationToken cancellationToken)
        {
            Dispatched.Add(domainEvent);
            return Task.CompletedTask;
        }
    }
}

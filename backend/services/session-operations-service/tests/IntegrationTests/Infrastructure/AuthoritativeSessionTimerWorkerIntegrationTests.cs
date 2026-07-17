using Microsoft.Extensions.Logging.Abstractions;
using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Application.Sessions.Common;
using umbral_backend.Domain.Entities;
using umbral_backend.Domain.Enums;
using umbral_backend.Domain.Services;
using umbral_backend.Domain.ValueObjects;
using umbral_backend.Infrastructure.IntegrationTests.Api;
using umbral_backend.Infrastructure.Persistence;
using umbral_backend.Infrastructure.Realtime;

namespace umbral_backend.Infrastructure.IntegrationTests.Infrastructure;

[Collection(PostgreSqlCollection.Name)]
public sealed class AuthoritativeSessionTimerWorkerIntegrationTests : IAsyncLifetime
{
    private readonly PostgreSqlFixture _fixture;
    private SessionOperationsApiWebApplicationFactory _factory = null!;

    public AuthoritativeSessionTimerWorkerIntegrationTests(PostgreSqlFixture fixture)
    {
        _fixture = fixture;
    }

    public async Task InitializeAsync()
    {
        _factory = new SessionOperationsApiWebApplicationFactory(_fixture.ConnectionString);
        await _factory.ResetDatabaseAsync();
    }

    public async Task DisposeAsync()
    {
        await _factory.DisposeAsync();
    }

    [Fact]
    public async Task TickAsync_WhenQuestionTimerElapses_OpensRevealThenActivatesNextAfterRevealWindow()
    {
        var scheduledAt = new DateTimeOffset(2026, 6, 4, 12, 0, 0, TimeSpan.Zero);
        var activateAt = scheduledAt.AddMinutes(2);
        var liveSessionId = await SeedActiveTriviaSessionAsync(scheduledAt, activateQuestionAt: activateAt);

        // First tick: the question timer has expired — close it and open the reveal window (HU-35).
        // The next question is NOT activated yet, so participants keep seeing the result.
        var closeAt = activateAt.AddSeconds(31);
        await RunWorkerTickAsync(closeAt);

        var revealing = await LoadSessionAsync(liveSessionId);
        revealing!.ActiveQuestionIndex.Should().BeNull();
        revealing.IsAwaitingQuestionReveal.Should().BeTrue();

        // Second tick after the reveal deadline: the deferred next-question activation fires.
        await RunWorkerTickAsync(closeAt.Add(TriviaRoundOrchestratorFacade.QuestionRevealDuration).AddSeconds(1));

        var advanced = await LoadSessionAsync(liveSessionId);
        advanced!.ActiveQuestionIndex.Should().Be(1);
        advanced.IsAwaitingQuestionReveal.Should().BeFalse();
        advanced.State.Should().Be(SessionState.Active);
    }

    [Fact]
    public async Task TickAsync_WhenLastQuestionTimerElapses_OpensBothRevealsThenFinishesOnTheRanking()
    {
        var scheduledAt = new DateTimeOffset(2026, 6, 4, 14, 0, 0, TimeSpan.Zero);
        var activateAt = scheduledAt.AddMinutes(2);
        var liveSessionId = await SeedActiveTriviaSessionAsync(
            scheduledAt,
            activateQuestionAt: activateAt,
            advanceToLastQuestion: true);

        // First tick: close the last question and open the reveal window — not finished yet.
        var closeAt = activateAt.AddSeconds(26);
        await RunWorkerTickAsync(closeAt);

        var revealing = await LoadSessionAsync(liveSessionId);
        revealing!.State.Should().Be(SessionState.Active);
        revealing.IsAwaitingQuestionReveal.Should().BeTrue();

        // Second tick after the answer-reveal deadline: the substage has ended, so the 10s ranking
        // opens (D-3). It does NOT finish here any more — the ranking is the terminal screen and the
        // session finishes on it.
        var rankingAt = closeAt.Add(TriviaRoundOrchestratorFacade.QuestionRevealDuration).AddSeconds(1);
        await RunWorkerTickAsync(rankingAt);

        var ranking = await LoadSessionAsync(liveSessionId);
        ranking!.State.Should().Be(SessionState.Active);
        ranking.IsAwaitingQuestionReveal.Should().BeFalse();
        ranking.IsAwaitingSubstageRankingReveal.Should().BeTrue();

        // Third tick after the ranking deadline: the substage completes and the session finishes.
        var finishAt = rankingAt.Add(LiveSession.SubstageRankingRevealDuration).AddSeconds(1);
        await RunWorkerTickAsync(finishAt);

        var session = await LoadSessionAsync(liveSessionId);
        session!.ActiveQuestionIndex.Should().BeNull();
        session.State.Should().Be(SessionState.Finished);
        session.EndedAt.Should().Be(finishAt);
    }

    private async Task<LiveSession?> LoadSessionAsync(Guid liveSessionId)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var repository = scope.ServiceProvider.GetRequiredService<ILiveSessionRepository>();
        return await repository.GetByIdAsync(liveSessionId, CancellationToken.None);
    }

    private async Task<Guid> SeedActiveTriviaSessionAsync(
        DateTimeOffset scheduledAt,
        DateTimeOffset activateQuestionAt,
        bool advanceToLastQuestion = false)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var transitionPolicy = new SessionStateTransitionPolicy();

        var sourceMissionId = Guid.NewGuid();
        var session = LiveSession.Create(
            SessionSource.Create(sourceMissionId),
            $"SES-{Guid.NewGuid():N}"[..12],
            "Worker Trivia",
            20,
            scheduledAt,
            CreateTriviaSnapshot(sourceMissionId));

        session.AssociateTeam(Guid.NewGuid(), "Aurora", "AUR-01", 3);
        session.MoveTo(SessionState.Preparing, scheduledAt.AddMinutes(1), transitionPolicy);
        session.MoveTo(SessionState.Active, scheduledAt.AddMinutes(2), transitionPolicy);
        session.ActivateQuestion(0, activateQuestionAt);

        if (advanceToLastQuestion)
        {
            session.CloseActiveQuestion(activateQuestionAt.AddSeconds(31));
            session.ActivateQuestion(1, activateQuestionAt);
        }

        dbContext.LiveSessions.Add(session);
        await dbContext.SaveChangesAsync();

        return session.LiveSessionId;
    }

    private static MissionRuntimeSnapshot CreateTriviaSnapshot(Guid sourceMissionId)
    {
        var triviaSubstage = SubstageSnapshot.CreateTrivia("Trivia Round", 1);

        return MissionRuntimeSnapshot.Create(
            sourceMissionId,
            "Worker Trivia Mission",
            MaximumTime.Create(20),
            [
                StageSnapshot.Create("Stage One", 1, [triviaSubstage])
            ],
            [],
            [
                TriviaQuestionSnapshot.Create(
                    triviaSubstage.SubstageSnapshotId,
                    "Capital of France?",
                    1,
                    50,
                    30,
                    "Paris is the capital city.",
                    [
                        TriviaOptionSnapshot.Create("Paris", 1, true),
                        TriviaOptionSnapshot.Create("Lyon", 2, false)
                    ]),
                TriviaQuestionSnapshot.Create(
                    triviaSubstage.SubstageSnapshotId,
                    "Capital of Spain?",
                    2,
                    50,
                    25,
                    "Madrid is the capital city.",
                    [
                        TriviaOptionSnapshot.Create("Madrid", 1, true),
                        TriviaOptionSnapshot.Create("Barcelona", 2, false)
                    ])
            ]);
    }

    private async Task RunWorkerTickAsync(DateTimeOffset utcNow)
    {
        var worker = new AuthoritativeSessionTimerWorker(
            _factory.Services.GetRequiredService<IServiceScopeFactory>(),
            new FixedTimeProvider(utcNow),
            NullLogger<AuthoritativeSessionTimerWorker>.Instance);

        await worker.TickAsync(CancellationToken.None);
    }

    private sealed class FixedTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset _utcNow;

        public FixedTimeProvider(DateTimeOffset utcNow)
        {
            _utcNow = utcNow;
        }

        public override DateTimeOffset GetUtcNow()
        {
            return _utcNow;
        }
    }
}

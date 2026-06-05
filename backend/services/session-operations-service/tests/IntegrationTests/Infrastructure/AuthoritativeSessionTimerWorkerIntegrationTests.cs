using Microsoft.Extensions.Logging.Abstractions;
using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Application.Sessions.DTOs;
using umbral_backend.Domain.Entities;
using umbral_backend.Domain.Enums;
using umbral_backend.Domain.Services;
using umbral_backend.Domain.ValueObjects;
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
    public async Task TickAsync_WhenQuestionTimerElapses_ClosesQuestionAndActivatesNext()
    {
        var scheduledAt = new DateTimeOffset(2026, 6, 4, 12, 0, 0, TimeSpan.Zero);
        var liveSessionId = await SeedActiveTriviaSessionAsync(scheduledAt, activateQuestionAt: scheduledAt.AddMinutes(2));

        await RunWorkerTickAsync(scheduledAt.AddMinutes(2).AddSeconds(31));

        await using var scope = _factory.Services.CreateAsyncScope();
        var repository = scope.ServiceProvider.GetRequiredService<ILiveSessionRepository>();
        var session = await repository.GetByIdAsync(liveSessionId, CancellationToken.None);

        session.Should().NotBeNull();
        session!.ActiveQuestionIndex.Should().Be(1);
        session.State.Should().Be(SessionState.Active);
    }

    [Fact]
    public async Task TickAsync_WhenLastQuestionTimerElapses_FinishesSession()
    {
        var scheduledAt = new DateTimeOffset(2026, 6, 4, 14, 0, 0, TimeSpan.Zero);
        var liveSessionId = await SeedActiveTriviaSessionAsync(
            scheduledAt,
            activateQuestionAt: scheduledAt.AddMinutes(2),
            advanceToLastQuestion: true);

        await RunWorkerTickAsync(scheduledAt.AddMinutes(2).AddSeconds(26));

        await using var scope = _factory.Services.CreateAsyncScope();
        var repository = scope.ServiceProvider.GetRequiredService<ILiveSessionRepository>();
        var session = await repository.GetByIdAsync(liveSessionId, CancellationToken.None);

        session.Should().NotBeNull();
        session!.ActiveQuestionIndex.Should().BeNull();
        session.State.Should().Be(SessionState.Finished);
        session.EndedAt.Should().Be(scheduledAt.AddMinutes(2).AddSeconds(26));
    }

    private async Task<Guid> SeedActiveTriviaSessionAsync(
        DateTimeOffset scheduledAt,
        DateTimeOffset activateQuestionAt,
        bool advanceToLastQuestion = false)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var transitionPolicy = new SessionStateTransitionPolicy();

        var session = LiveSession.CreateTrivia(
            SessionSource.CreateTriviaQuiz(42),
            $"SES-{Guid.NewGuid():N}"[..12],
            "Worker Trivia",
            20,
            scheduledAt,
            TriviaSessionSnapshot.Create(
                "Trivia Source",
                [
                    TriviaQuestionSnapshot.Create(
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
                        "Capital of Spain?",
                        2,
                        50,
                        25,
                        "Madrid is the capital city.",
                        [
                            TriviaOptionSnapshot.Create("Madrid", 1, true),
                            TriviaOptionSnapshot.Create("Barcelona", 2, false)
                        ])
                ]));

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

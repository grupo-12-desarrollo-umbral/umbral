using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Application.Sessions.Common;
using umbral_backend.Domain.Entities;
using umbral_backend.Domain.Enums;
using umbral_backend.Domain.Services;
using umbral_backend.Domain.ValueObjects;
using umbral_backend.Infrastructure.Realtime;

namespace umbral_backend.Infrastructure.IntegrationTests.Infrastructure;

public sealed class AuthoritativeSessionTimerWorkerBranchTests
{
    private static readonly DateTimeOffset Now = new(2026, 6, 4, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task TickAsync_WhenSessionHasNoAdvancingQuestion_BroadcastsOnly()
    {
        var session = CreateActiveTriviaSession();
        var repository = new Mock<ILiveSessionRepository>();
        repository
            .Setup(repo => repo.ListActiveTimersAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([session]);
        var broadcaster = new Mock<ISessionTimerBroadcaster>();
        broadcaster
            .Setup(current => current.BroadcastTimerUpdatedAsync(
                It.IsAny<SessionTimerUpdatedNotificationDto>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var facade = new Mock<ITriviaRoundOrchestratorFacade>();
        var worker = CreateWorker(repository, broadcaster, facade, Now);

        await worker.TickAsync(CancellationToken.None);

        broadcaster.Verify(
            current => current.BroadcastTimerUpdatedAsync(
                It.Is<SessionTimerUpdatedNotificationDto>(notification =>
                    notification.TotalMilliseconds == 0 && notification.RemainingMilliseconds == 0),
                It.IsAny<CancellationToken>()),
            Times.Once);
        repository.Verify(repo => repo.UpdateAsync(It.IsAny<LiveSession>(), It.IsAny<CancellationToken>()), Times.Never);
        facade.Verify(
            current => current.CloseAndAdvanceAsync(It.IsAny<LiveSession>(), It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task TickAsync_WhenQuestionTimerStillRunning_BroadcastsWithoutClosingQuestion()
    {
        var session = CreateActiveTriviaSession();
        var activatedAt = Now.AddSeconds(-5);
        session.ActivateQuestion(0, activatedAt);
        var repository = new Mock<ILiveSessionRepository>();
        repository
            .Setup(repo => repo.ListActiveTimersAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([session]);
        var broadcaster = new Mock<ISessionTimerBroadcaster>();
        broadcaster
            .Setup(current => current.BroadcastTimerUpdatedAsync(
                It.IsAny<SessionTimerUpdatedNotificationDto>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var facade = new Mock<ITriviaRoundOrchestratorFacade>();
        var worker = CreateWorker(repository, broadcaster, facade, Now);

        await worker.TickAsync(CancellationToken.None);

        broadcaster.Verify(
            current => current.BroadcastTimerUpdatedAsync(
                It.Is<SessionTimerUpdatedNotificationDto>(notification => !notification.IsExpired && notification.RemainingMilliseconds > 0),
                It.IsAny<CancellationToken>()),
            Times.Once);
        repository.Verify(repo => repo.UpdateAsync(It.IsAny<LiveSession>(), It.IsAny<CancellationToken>()), Times.Never);
        facade.Verify(
            current => current.CloseAndAdvanceAsync(It.IsAny<LiveSession>(), It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    private static AuthoritativeSessionTimerWorker CreateWorker(
        Mock<ILiveSessionRepository> repository,
        Mock<ISessionTimerBroadcaster> broadcaster,
        Mock<ITriviaRoundOrchestratorFacade> facade,
        DateTimeOffset utcNow)
    {
        var services = new ServiceCollection();
        services.AddScoped(_ => repository.Object);
        services.AddScoped(_ => broadcaster.Object);
        services.AddScoped(_ => facade.Object);
        var provider = services.BuildServiceProvider();

        return new AuthoritativeSessionTimerWorker(
            provider.GetRequiredService<IServiceScopeFactory>(),
            new FixedTimeProvider(utcNow),
            NullLogger<AuthoritativeSessionTimerWorker>.Instance);
    }

    private static LiveSession CreateActiveTriviaSession()
    {
        var sourceMissionId = Guid.NewGuid();
        var session = LiveSession.Create(
            SessionSource.Create(sourceMissionId),
            $"SES-{Guid.NewGuid():N}"[..12],
            "Worker Trivia",
            20,
            Now.AddMinutes(-10),
            CreateTriviaSnapshot(sourceMissionId));
        session.AssociateTeam(Guid.NewGuid(), "Alpha", "A-01", 4);
        var policy = new SessionStateTransitionPolicy();
        session.MoveTo(SessionState.Preparing, Now.AddMinutes(-2), policy);
        session.MoveTo(SessionState.Active, Now.AddMinutes(-1), policy);
        return session;
    }

    private static MissionRuntimeSnapshot CreateTriviaSnapshot(Guid sourceMissionId)
    {
        var triviaSubstage = SubstageSnapshot.CreateTrivia("Trivia Round", 1);

        return MissionRuntimeSnapshot.Create(
            sourceMissionId,
            "Worker Trivia Mission",
            MaximumTime.Create(20),
            [StageSnapshot.Create("Stage One", 1, [triviaSubstage])],
            [],
            [
                TriviaQuestionSnapshot.Create(
                    triviaSubstage.SubstageSnapshotId,
                    "Capital of France?",
                    1,
                    50,
                    30,
                    null,
                    [
                        TriviaOptionSnapshot.Create("Paris", 1, true),
                        TriviaOptionSnapshot.Create("Lyon", 2, false)
                    ])
            ]);
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}

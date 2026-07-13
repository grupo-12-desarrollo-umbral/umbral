using umbral_backend.Domain.Entities;
using umbral_backend.Domain.Enums;
using umbral_backend.Domain.Services;
using umbral_backend.Domain.ValueObjects;
using umbral_backend.Infrastructure.Persistence;
using umbral_backend.Infrastructure.Persistence.Repositories;

namespace umbral_backend.Infrastructure.IntegrationTests.Persistence;

[Collection(PostgreSqlCollection.Name)]
public sealed class LiveSessionRepositoryBranchTests
{
    private readonly PersistenceTestContextFactory _contextFactory;

    public LiveSessionRepositoryBranchTests(PostgreSqlFixture fixture)
    {
        _contextFactory = new PersistenceTestContextFactory(fixture.ConnectionString);
    }

    [Fact]
    public async Task ListActiveTimersAsync_WhenSessionHasNullMissionRuntimeSnapshot_ExcludesSession()
    {
        await using var resetContext = _contextFactory.Create();
        await ResetDatabaseAsync(resetContext);

        var scheduledAt = DateTimeOffset.UtcNow.AddMinutes(-30);
        var session = CreateSessionWithNullSnapshot(scheduledAt);
        session.AssociateTeam(Guid.NewGuid(), "Alpha", "A-01", 4);
        var policy = new SessionStateTransitionPolicy();
        session.MoveTo(SessionState.Preparing, scheduledAt.AddMinutes(1), policy);
        session.MoveTo(SessionState.Active, scheduledAt.AddMinutes(2), policy);

        await using (var seedContext = _contextFactory.Create())
        {
            seedContext.LiveSessions.Add(session);
            await seedContext.SaveChangesAsync(CancellationToken.None);
        }

        await using var assertContext = _contextFactory.Create();
        var repository = new LiveSessionRepository(assertContext);

        var result = await repository.ListActiveTimersAsync(CancellationToken.None);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task ListActiveTimersAsync_WhenActiveSubstageIsTrivia_ExcludesTreasureHuntTimerSessions()
    {
        await using var resetContext = _contextFactory.Create();
        await ResetDatabaseAsync(resetContext);

        var scheduledAt = DateTimeOffset.UtcNow.AddMinutes(-30);
        var sourceMissionId = Guid.NewGuid();
        var session = LiveSession.Create(
            SessionSource.Create(sourceMissionId),
            $"SES-{Guid.NewGuid():N}"[..12],
            "Trivia Session",
            20,
            scheduledAt,
            CreateTriviaSnapshot(sourceMissionId));
        session.AssociateTeam(Guid.NewGuid(), "Alpha", "A-01", 4);
        var policy = new SessionStateTransitionPolicy();
        session.MoveTo(SessionState.Preparing, scheduledAt.AddMinutes(1), policy);
        session.MoveTo(SessionState.Active, scheduledAt.AddMinutes(2), policy);
        session.ActivateQuestion(0, scheduledAt.AddMinutes(2));

        await using (var seedContext = _contextFactory.Create())
        {
            seedContext.LiveSessions.Add(session);
            await seedContext.SaveChangesAsync(CancellationToken.None);
        }

        await using var assertContext = _contextFactory.Create();
        var repository = new LiveSessionRepository(assertContext);

        var result = await repository.ListActiveTimersAsync(CancellationToken.None);

        result.Should().ContainSingle();
        result[0].LiveSessionId.Should().Be(session.LiveSessionId);
    }

    [Fact]
    public async Task ListActiveTimersAsync_WhenActiveSubstageIsTreasureHunt_IncludesSession()
    {
        await using var resetContext = _contextFactory.Create();
        await ResetDatabaseAsync(resetContext);

        var scheduledAt = DateTimeOffset.UtcNow.AddMinutes(-30);
        var sourceMissionId = Guid.NewGuid();
        var session = LiveSession.Create(
            SessionSource.Create(sourceMissionId),
            $"SES-{Guid.NewGuid():N}"[..12],
            "Treasure Hunt Session",
            45,
            scheduledAt,
            CreateTreasureHuntSnapshot(sourceMissionId));
        session.AssociateTeam(Guid.NewGuid(), "Alpha", "A-01", 4);
        var policy = new SessionStateTransitionPolicy();
        session.MoveTo(SessionState.Preparing, scheduledAt.AddMinutes(1), policy);
        session.MoveTo(SessionState.Active, scheduledAt.AddMinutes(2), policy);

        await using (var seedContext = _contextFactory.Create())
        {
            seedContext.LiveSessions.Add(session);
            await seedContext.SaveChangesAsync(CancellationToken.None);
        }

        await using var assertContext = _contextFactory.Create();
        var repository = new LiveSessionRepository(assertContext);

        var result = await repository.ListActiveTimersAsync(CancellationToken.None);

        result.Should().ContainSingle();
        result[0].LiveSessionId.Should().Be(session.LiveSessionId);
    }

    [Fact]
    public async Task ListActiveTimersAsync_WhenNoActiveSessions_ReturnsEmpty()
    {
        await using var resetContext = _contextFactory.Create();
        await ResetDatabaseAsync(resetContext);

        var scheduledAt = DateTimeOffset.UtcNow.AddMinutes(-30);
        var sourceMissionId = Guid.NewGuid();
        var session = LiveSession.Create(
            SessionSource.Create(sourceMissionId),
            $"SES-{Guid.NewGuid():N}"[..12],
            "Scheduled Session",
            20,
            scheduledAt,
            CreateTriviaSnapshot(sourceMissionId));
        session.AssociateTeam(Guid.NewGuid(), "Alpha", "A-01", 4);

        await using (var seedContext = _contextFactory.Create())
        {
            seedContext.LiveSessions.Add(session);
            await seedContext.SaveChangesAsync(CancellationToken.None);
        }

        await using var assertContext = _contextFactory.Create();
        var repository = new LiveSessionRepository(assertContext);

        var result = await repository.ListActiveTimersAsync(CancellationToken.None);

        result.Should().BeEmpty();
    }

    private static LiveSession CreateSessionWithNullSnapshot(DateTimeOffset scheduledAt)
    {
        var sourceMissionId = Guid.NewGuid();
        return LiveSession.Create(
            SessionSource.Create(sourceMissionId),
            $"SES-{Guid.NewGuid():N}"[..12],
            "Null Snapshot Session",
            20,
            scheduledAt,
            MissionRuntimeSnapshot.Create(
                sourceMissionId,
                "Test Mission",
                MaximumTime.Create(20),
                [StageSnapshot.Create("Stage One", 1, [SubstageSnapshot.CreateTrivia("Trivia", 1)])],
                [],
                [TriviaQuestionSnapshot.Create(
                    SubstageSnapshot.CreateTrivia("Trivia", 1).SubstageSnapshotId,
                    "Question?",
                    1,
                    50,
                    30,
                    null,
                    [TriviaOptionSnapshot.Create("A", 1, true), TriviaOptionSnapshot.Create("B", 2, false)])]));
    }

    private static MissionRuntimeSnapshot CreateTriviaSnapshot(Guid sourceMissionId)
    {
        var triviaSubstage = SubstageSnapshot.CreateTrivia("Trivia Round", 1);

        return MissionRuntimeSnapshot.Create(
            sourceMissionId,
            "Trivia Mission",
            MaximumTime.Create(20),
            [StageSnapshot.Create("Stage One", 1, [triviaSubstage])],
            [],
            [TriviaQuestionSnapshot.Create(
                triviaSubstage.SubstageSnapshotId,
                "Capital of France?",
                1,
                50,
                30,
                null,
                [TriviaOptionSnapshot.Create("Paris", 1, true), TriviaOptionSnapshot.Create("Lyon", 2, false)])]);
    }

    private static MissionRuntimeSnapshot CreateTreasureHuntSnapshot(Guid sourceMissionId)
    {
        var treasureHuntSubstage = SubstageSnapshot.CreateTreasureHunt("Treasure Route", 1);

        return MissionRuntimeSnapshot.Create(
            sourceMissionId,
            "Treasure Hunt Mission",
            MaximumTime.Create(45),
            [StageSnapshot.Create("Stage One", 1, [treasureHuntSubstage])],
            [TargetSnapshot.Create(
                treasureHuntSubstage.SubstageSnapshotId,
                "Target Alpha",
                "QR-ALPHA",
                1,
                true,
                100,
                4.711,
                -74.0721,
                "Look under the stairs",
                "AfterPreviousTarget")],
            []);
    }

    private static async Task ResetDatabaseAsync(DbContext context)
    {
        await context.Database.EnsureDeletedAsync();
        await context.Database.MigrateAsync();
    }
}

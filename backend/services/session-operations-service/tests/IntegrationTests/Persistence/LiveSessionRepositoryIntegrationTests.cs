using umbral_backend.Domain.Entities;
using umbral_backend.Domain.Enums;
using umbral_backend.Domain.Services;
using umbral_backend.Domain.ValueObjects;
using umbral_backend.Infrastructure.Persistence;
using umbral_backend.Infrastructure.Persistence.Repositories;

namespace umbral_backend.Infrastructure.IntegrationTests.Persistence;

[Collection(PostgreSqlCollection.Name)]
public sealed class LiveSessionRepositoryIntegrationTests
{
    private readonly PersistenceTestContextFactory _contextFactory;

    public LiveSessionRepositoryIntegrationTests(PostgreSqlFixture fixture)
    {
        _contextFactory = new PersistenceTestContextFactory(fixture.ConnectionString);
    }

    [Fact]
    public async Task GetByIdAsync_RestoresRuntimeParticipationState()
    {
        await using var resetContext = BuildContext();
        await ResetDatabaseAsync(resetContext);

        var createdAt = DateTimeOffset.UtcNow.AddMinutes(-30);
        var liveSession = CreateSession(createdAt);
        var team = liveSession.RegisterTeam("Blue", "BLUE-01", 4);
        var participant = liveSession.AdmitParticipant(
            Guid.NewGuid(),
            "Nora",
            team.TeamId,
            createdAt.AddMinutes(2),
            new JoinPolicy()).Participant;
        liveSession.OpenJoinContext(team.TeamId, Guid.NewGuid(), createdAt.AddMinutes(1), createdAt.AddMinutes(11));
        liveSession.DisconnectParticipant(participant.SessionParticipantId, createdAt.AddMinutes(5));

        await using (var seedContext = BuildContext())
        {
            seedContext.LiveSessions.Add(liveSession);
            await seedContext.SaveChangesAsync(CancellationToken.None);
        }

        await using var assertContext = BuildContext();
        var repository = new LiveSessionRepository(assertContext);
        var persistedSession = await repository.GetByIdAsync(liveSession.LiveSessionId, CancellationToken.None);

        persistedSession.Should().NotBeNull();
        persistedSession!.Teams.Should().ContainSingle();
        persistedSession.Participants.Should().ContainSingle();
        persistedSession.JoinContexts.Should().ContainSingle();
        persistedSession.Created.Should().BeAfter(DateTimeOffset.MinValue);
        persistedSession.LastModified.Should().BeAfter(DateTimeOffset.MinValue);

        var persistedTeam = persistedSession.Teams.Single();
        persistedTeam.TeamCode.Value.Should().Be("BLUE-01");
        persistedTeam.Capacity.Should().Be(4);
        persistedTeam.Members.Should().ContainSingle(member => member.SessionParticipantId == participant.SessionParticipantId);

        var persistedParticipant = persistedSession.Participants.Single();
        persistedParticipant.SessionParticipantId.Should().Be(participant.SessionParticipantId);
        persistedParticipant.IsDisconnected.Should().BeTrue();
        persistedParticipant.LastSeenAt.Should().BeCloseTo(createdAt.AddMinutes(5), TimeSpan.FromMicroseconds(1));
    }

    [Fact]
    public async Task UpdateAsync_PersistsReconnectRecoveryWithoutDuplicatingMembership()
    {
        await using var resetContext = BuildContext();
        await ResetDatabaseAsync(resetContext);

        var joinedAt = DateTimeOffset.UtcNow.AddMinutes(-10);
        var liveSession = CreateSession(joinedAt.AddMinutes(-5));
        var team = liveSession.RegisterTeam("Red", "RED-01", 3);
        var participant = liveSession.AdmitParticipant(
            Guid.NewGuid(),
            "Nova",
            team.TeamId,
            joinedAt,
            new JoinPolicy()).Participant;
        liveSession.DisconnectParticipant(participant.SessionParticipantId, joinedAt.AddMinutes(2));

        await using (var seedContext = BuildContext())
        {
            seedContext.LiveSessions.Add(liveSession);
            await seedContext.SaveChangesAsync(CancellationToken.None);
        }

        var reconnectedAt = joinedAt.AddMinutes(7);

        await using (var actContext = BuildContext())
        {
            var repository = new LiveSessionRepository(actContext);
            var persistedSession = await repository.GetByIdAsync(liveSession.LiveSessionId, CancellationToken.None);

            persistedSession.Should().NotBeNull();

            var admission = persistedSession!.AdmitParticipant(
                participant.ExternalIdentityId,
                participant.DisplayName,
                team.TeamId,
                reconnectedAt,
                new JoinPolicy());

            admission.IsReconnect.Should().BeTrue();
            admission.Participant.SessionParticipantId.Should().Be(participant.SessionParticipantId);

            await repository.UpdateAsync(persistedSession, CancellationToken.None);
        }

        await using var assertContext = BuildContext();
        var reloadedSession = await new LiveSessionRepository(assertContext)
            .GetByIdAsync(liveSession.LiveSessionId, CancellationToken.None);

        reloadedSession.Should().NotBeNull();

        var reloadedParticipant = reloadedSession!.Participants.Single();
        reloadedParticipant.IsDisconnected.Should().BeFalse();
        reloadedParticipant.LastSeenAt.Should().BeCloseTo(reconnectedAt, TimeSpan.FromMicroseconds(1));

        var reloadedTeam = reloadedSession.Teams.Single();
        reloadedTeam.Members.Should().ContainSingle(member => member.SessionParticipantId == participant.SessionParticipantId);
    }

    [Fact]
    public async Task GetByIdAsync_RestoresAssociatedTeamReferenceCorrelation()
    {
        await using var resetContext = BuildContext();
        await ResetDatabaseAsync(resetContext);

        var createdAt = DateTimeOffset.UtcNow.AddMinutes(-20);
        var liveSession = CreateSession(createdAt);
        var referenceTeamId = Guid.NewGuid();
        liveSession.AssociateTeam(referenceTeamId, "Aurora", "AUR-01", 3);

        await using (var seedContext = BuildContext())
        {
            seedContext.LiveSessions.Add(liveSession);
            await seedContext.SaveChangesAsync(CancellationToken.None);
        }

        await using var assertContext = BuildContext();
        var persistedSession = await new LiveSessionRepository(assertContext)
            .GetByIdAsync(liveSession.LiveSessionId, CancellationToken.None);

        persistedSession.Should().NotBeNull();
        persistedSession!.Teams.Should().ContainSingle();
        persistedSession.Teams.Single().TeamId.Should().NotBe(referenceTeamId);
        persistedSession.Teams.Single().ReferenceTeamId.Should().Be(referenceTeamId);
    }

    [Fact]
    public async Task GetByIdAsync_RestoresAssignedOperatorFromExistingPersistenceColumn()
    {
        await using var resetContext = BuildContext();
        await ResetDatabaseAsync(resetContext);

        var assignedAt = DateTimeOffset.UtcNow.AddMinutes(-15);
        var liveSession = CreateSession(assignedAt.AddMinutes(-10));
        liveSession.AssignOperator(27, assignedAt);

        await using (var seedContext = BuildContext())
        {
            seedContext.LiveSessions.Add(liveSession);
            await seedContext.SaveChangesAsync(CancellationToken.None);
        }

        await using var assertContext = BuildContext();
        var persistedSession = await new LiveSessionRepository(assertContext)
            .GetByIdAsync(liveSession.LiveSessionId, CancellationToken.None);

        persistedSession.Should().NotBeNull();
        persistedSession!.AssignedOperatorUserId.Should().Be(27);
    }

    [Fact]
    public async Task UpdateAsync_PersistsOperatorReassignmentToExistingPersistenceColumn()
    {
        await using var resetContext = BuildContext();
        await ResetDatabaseAsync(resetContext);

        var assignedAt = DateTimeOffset.UtcNow.AddMinutes(-12);
        var liveSession = CreateSession(assignedAt.AddMinutes(-8));
        liveSession.AssignOperator(27, assignedAt);

        await using (var seedContext = BuildContext())
        {
            seedContext.LiveSessions.Add(liveSession);
            await seedContext.SaveChangesAsync(CancellationToken.None);
        }

        await using (var actContext = BuildContext())
        {
            var repository = new LiveSessionRepository(actContext);
            var persistedSession = await repository.GetByIdAsync(liveSession.LiveSessionId, CancellationToken.None);

            persistedSession.Should().NotBeNull();
            persistedSession!.AssignOperator(31, assignedAt.AddMinutes(3));

            await repository.UpdateAsync(persistedSession, CancellationToken.None);
        }

        await using var assertContext = BuildContext();
        var reloadedSession = await new LiveSessionRepository(assertContext)
            .GetByIdAsync(liveSession.LiveSessionId, CancellationToken.None);

        reloadedSession.Should().NotBeNull();
        reloadedSession!.AssignedOperatorUserId.Should().Be(31);
    }

    [Fact]
    public async Task GetByIdAsync_RestoresMissionRuntimeSnapshotGraph()
    {
        await using var resetContext = BuildContext();
        await ResetDatabaseAsync(resetContext);

        var scheduledAt = DateTimeOffset.UtcNow.AddDays(1);
        var sourceMissionId = Guid.NewGuid();
        var runtimeSnapshot = CreateMixedRuntimeSnapshot(sourceMissionId, 20);
        var liveSession = LiveSession.Create(
            SessionSource.Create(sourceMissionId),
            $"SES-{Guid.NewGuid():N}"[..12],
            "Mission Night",
            20,
            scheduledAt,
            runtimeSnapshot);

        await using (var seedContext = BuildContext())
        {
            seedContext.LiveSessions.Add(liveSession);
            await seedContext.SaveChangesAsync(CancellationToken.None);
        }

        await using var assertContext = BuildContext();
        var persistedSession = await new LiveSessionRepository(assertContext)
            .GetByIdAsync(liveSession.LiveSessionId, CancellationToken.None);

        persistedSession.Should().NotBeNull();
        persistedSession!.Source.SourceType.Should().Be(SessionSourceType.Mission);
        persistedSession.Source.SourceEntityId.Should().Be(sourceMissionId);
        persistedSession.MissionRuntimeSnapshot.MissionRuntimeSnapshotId.Should().Be(runtimeSnapshot.MissionRuntimeSnapshotId);
        persistedSession.MissionRuntimeSnapshot.SourceMissionId.Should().Be(sourceMissionId);
        persistedSession.MissionRuntimeSnapshot.MissionTitle.Should().Be("Mission Runtime");
        persistedSession.MissionRuntimeSnapshot.StageSnapshots.Should().ContainSingle();
        persistedSession.MissionRuntimeSnapshot.TargetSnapshots.Should().ContainSingle();
        persistedSession.MissionRuntimeSnapshot.TriviaQuestionSnapshots.Should().ContainSingle();

        var stage = persistedSession.MissionRuntimeSnapshot.StageSnapshots.Single();
        stage.SubstageSnapshots.Should().HaveCount(2);

        var target = persistedSession.MissionRuntimeSnapshot.TargetSnapshots.Single();
        target.QrCode.Should().Be("QR-ALPHA");
        target.ClueText.Should().Be("Look under the stairs");

        var question = persistedSession.MissionRuntimeSnapshot.TriviaQuestionSnapshots.Single();
        question.Prompt.Should().Be("Capital of France?");
        question.Options.Should().HaveCount(2);
        question.Options.Should().ContainSingle(option => option.OptionText == "Paris" && option.IsCorrect);
    }

    [Fact]
    public async Task PersistedSchema_CarriesNoForeignSourceColumnOrTable()
    {
        await using var resetContext = BuildContext();
        await ResetDatabaseAsync(resetContext);

        // Persist a real mission-only session so the assertion runs against a populated graph.
        var sourceMissionId = Guid.NewGuid();
        var liveSession = LiveSession.Create(
            SessionSource.Create(sourceMissionId),
            $"SES-{Guid.NewGuid():N}"[..12],
            "Mission Night",
            20,
            DateTimeOffset.UtcNow.AddDays(1),
            CreateMixedRuntimeSnapshot(sourceMissionId, 20));

        await using (var seedContext = BuildContext())
        {
            seedContext.LiveSessions.Add(liveSession);
            await seedContext.SaveChangesAsync(CancellationToken.None);
        }

        await using var schemaContext = BuildContext();
        var connection = schemaContext.Database.GetDbConnection();
        await connection.OpenAsync();

        var columns = await QuerySchemaAsync(
            connection,
            "SELECT table_name || '.' || column_name FROM information_schema.columns WHERE table_schema = 'public'");
        var tables = await QuerySchemaAsync(
            connection,
            "SELECT table_name FROM information_schema.tables WHERE table_schema = 'public'");

        // HU-15's AddMissionRuntimeSnapshot dropped the two-source debris; HU-17 locks that it stays gone.
        columns.Should().NotContain(name => name.Contains("source_trivia_quiz_id", StringComparison.OrdinalIgnoreCase));
        columns.Should().NotContain(name => name.Contains("session_mode", StringComparison.OrdinalIgnoreCase));
        tables.Should().NotContain(name => name.Contains("trivia_session_snapshot", StringComparison.OrdinalIgnoreCase));
        tables.Should().NotContain(name => name.Contains("quiz", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task GetByIdAsync_RestoresActiveQuestionAndQuestionTimerState()
    {
        await using var resetContext = BuildContext();
        await ResetDatabaseAsync(resetContext);

        var activatedAt = DateTimeOffset.UtcNow.AddMinutes(-8);
        var liveSession = CreateTriviaSession(activatedAt.AddMinutes(-4));
        TransitionTriviaSessionToActive(liveSession, activatedAt.AddMinutes(-2));
        liveSession.ActivateQuestion(0, activatedAt);

        await using (var seedContext = BuildContext())
        {
            seedContext.LiveSessions.Add(liveSession);
            await seedContext.SaveChangesAsync(CancellationToken.None);
        }

        await using var assertContext = BuildContext();
        var persistedSession = await new LiveSessionRepository(assertContext)
            .GetByIdAsync(liveSession.LiveSessionId, CancellationToken.None);

        persistedSession.Should().NotBeNull();
        persistedSession!.ActiveQuestionIndex.Should().Be(0);

        var timerSnapshot = persistedSession.GetActiveQuestionTimerSnapshot(activatedAt.AddSeconds(10));
        timerSnapshot.TotalDuration.Should().Be(TimeSpan.FromSeconds(30));
        timerSnapshot.RemainingDuration.Should().BeCloseTo(TimeSpan.FromSeconds(20), TimeSpan.FromMilliseconds(1));
        timerSnapshot.IsExpired.Should().BeFalse();
    }

    [Fact]
    public async Task ListActiveTimersAsync_ReturnsSessionWithOnlyActiveQuestionTimer()
    {
        await using var resetContext = BuildContext();
        await ResetDatabaseAsync(resetContext);

        var startedAt = DateTimeOffset.UtcNow.AddMinutes(-5);
        var liveSession = CreateTriviaSession(startedAt.AddMinutes(-2));
        TransitionTriviaSessionToActive(liveSession, startedAt);
        liveSession.ActivateQuestion(0, startedAt.AddSeconds(5));
        liveSession.MarkSessionTimerExpiredIfElapsed(startedAt.AddMinutes(30));

        await using (var seedContext = BuildContext())
        {
            seedContext.LiveSessions.Add(liveSession);
            await seedContext.SaveChangesAsync(CancellationToken.None);
        }

        await using var assertContext = BuildContext();
        var sessions = await new LiveSessionRepository(assertContext)
            .ListActiveTimersAsync(CancellationToken.None);

        sessions.Should().ContainSingle(session => session.LiveSessionId == liveSession.LiveSessionId);
    }

    [Fact]
    public async Task GetByIdAsync_RestoresAdvancingAuthoritativeTimerState()
    {
        await using var resetContext = BuildContext();
        await ResetDatabaseAsync(resetContext);

        var activeAt = new DateTimeOffset(2026, 6, 4, 12, 0, 0, TimeSpan.Zero);
        var liveSession = CreateActiveSession(activeAt);

        await using (var seedContext = BuildContext())
        {
            seedContext.LiveSessions.Add(liveSession);
            await seedContext.SaveChangesAsync(CancellationToken.None);
        }

        await using var assertContext = BuildContext();
        var persistedSession = await new LiveSessionRepository(assertContext)
            .GetByIdAsync(liveSession.LiveSessionId, CancellationToken.None);

        persistedSession.Should().NotBeNull();
        var snapshot = persistedSession!.GetAuthoritativeSessionTimerSnapshot(activeAt.AddMinutes(5));

        snapshot.TotalDuration.Should().Be(TimeSpan.FromMinutes(45));
        snapshot.RemainingDuration.Should().Be(TimeSpan.FromMinutes(40));
        snapshot.IsAdvancing.Should().BeTrue();
        snapshot.AdvancingSince.Should().Be(activeAt);
    }

    [Fact]
    public async Task GetByIdAsync_RestoresPausedAuthoritativeTimerAsFrozen()
    {
        await using var resetContext = BuildContext();
        await ResetDatabaseAsync(resetContext);

        var activeAt = new DateTimeOffset(2026, 6, 4, 12, 0, 0, TimeSpan.Zero);
        var pausedAt = activeAt.AddMinutes(4);
        var liveSession = CreateActiveSession(activeAt);
        liveSession.MoveTo(SessionState.Paused, pausedAt, new SessionStateTransitionPolicy(), "Break");

        await using (var seedContext = BuildContext())
        {
            seedContext.LiveSessions.Add(liveSession);
            await seedContext.SaveChangesAsync(CancellationToken.None);
        }

        await using var assertContext = BuildContext();
        var persistedSession = await new LiveSessionRepository(assertContext)
            .GetByIdAsync(liveSession.LiveSessionId, CancellationToken.None);

        persistedSession.Should().NotBeNull();
        var snapshot = persistedSession!.GetAuthoritativeSessionTimerSnapshot(pausedAt.AddMinutes(10));

        snapshot.RemainingDuration.Should().Be(TimeSpan.FromMinutes(41));
        snapshot.IsAdvancing.Should().BeFalse();
        snapshot.AdvancingSince.Should().BeNull();
        persistedSession.State.Should().Be(SessionState.Paused);
    }

    [Fact]
    public async Task GetByIdAsync_RestoresResumedAuthoritativeTimerFromFrozenRemainder()
    {
        await using var resetContext = BuildContext();
        await ResetDatabaseAsync(resetContext);

        var activeAt = new DateTimeOffset(2026, 6, 4, 12, 0, 0, TimeSpan.Zero);
        var pausedAt = activeAt.AddMinutes(5);
        var resumedAt = pausedAt.AddMinutes(10);
        var liveSession = CreateActiveSession(activeAt);
        var transitionPolicy = new SessionStateTransitionPolicy();
        liveSession.MoveTo(SessionState.Paused, pausedAt, transitionPolicy, "Break");
        liveSession.MoveTo(SessionState.Active, resumedAt, transitionPolicy);

        await using (var seedContext = BuildContext())
        {
            seedContext.LiveSessions.Add(liveSession);
            await seedContext.SaveChangesAsync(CancellationToken.None);
        }

        await using var assertContext = BuildContext();
        var persistedSession = await new LiveSessionRepository(assertContext)
            .GetByIdAsync(liveSession.LiveSessionId, CancellationToken.None);

        persistedSession.Should().NotBeNull();
        var snapshot = persistedSession!.GetAuthoritativeSessionTimerSnapshot(resumedAt.AddMinutes(3));

        snapshot.RemainingDuration.Should().Be(TimeSpan.FromMinutes(37));
        snapshot.IsAdvancing.Should().BeTrue();
        snapshot.AdvancingSince.Should().Be(resumedAt);
    }

    private ApplicationDbContext BuildContext()
    {
        return _contextFactory.Create();
    }

    private static async Task<List<string>> QuerySchemaAsync(System.Data.Common.DbConnection connection, string sql)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;

        var results = new List<string>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            results.Add(reader.GetString(0));
        }

        return results;
    }

    private static LiveSession CreateTriviaSession(DateTimeOffset scheduledAt)
    {
        var sourceMissionId = Guid.NewGuid();

        return LiveSession.Create(
            SessionSource.Create(sourceMissionId),
            $"SES-{Guid.NewGuid():N}"[..12],
            "Trivia Night",
            20,
            scheduledAt,
            CreateTriviaRuntimeSnapshot(sourceMissionId, 20));
    }

    private static void TransitionTriviaSessionToActive(LiveSession liveSession, DateTimeOffset activeAt)
    {
        var transitionPolicy = new SessionStateTransitionPolicy();
        liveSession.AssociateTeam(Guid.NewGuid(), "Aurora", "AUR-01", 3);
        liveSession.MoveTo(SessionState.Preparing, activeAt.AddMinutes(-1), transitionPolicy);
        liveSession.MoveTo(SessionState.Active, activeAt, transitionPolicy);
    }

    private static async Task ResetDatabaseAsync(ApplicationDbContext context)
    {
        await context.LiveSessions.ExecuteDeleteAsync();
    }

    private static LiveSession CreateSession(DateTimeOffset scheduledAt)
    {
        var sourceMissionId = Guid.NewGuid();

        return LiveSession.Create(
            SessionSource.Create(sourceMissionId),
            $"SES-{Guid.NewGuid():N}"[..12],
            "Reconnect Session",
            45,
            scheduledAt,
            CreateTreasureHuntRuntimeSnapshot(sourceMissionId, 45));
    }

    private static LiveSession CreateActiveSession(DateTimeOffset activeAt)
    {
        var liveSession = CreateSession(activeAt.AddMinutes(-10));
        liveSession.AssociateTeam(Guid.NewGuid(), "Blue", "BLUE-01", 4);
        var transitionPolicy = new SessionStateTransitionPolicy();
        liveSession.MoveTo(SessionState.Preparing, activeAt.AddMinutes(-1), transitionPolicy);
        liveSession.MoveTo(SessionState.Active, activeAt, transitionPolicy);
        return liveSession;
    }

    private static MissionRuntimeSnapshot CreateTreasureHuntRuntimeSnapshot(Guid sourceMissionId, int maximumTimeMinutes)
    {
        var treasureHuntSubstage = SubstageSnapshot.CreateTreasureHunt("Treasure Hunt", 1, 100);

        return MissionRuntimeSnapshot.Create(
            sourceMissionId,
            "Mission Runtime",
            MaximumTime.Create(maximumTimeMinutes),
            [
                StageSnapshot.Create("Stage One", 1, [treasureHuntSubstage])
            ],
            [
                TargetSnapshot.Create(
                    treasureHuntSubstage.SubstageSnapshotId,
                    "Target Alpha",
                    "QR-ALPHA",
                    1,
                    true,
                    "Look under the stairs",
                    "AfterPreviousTarget")
            ],
            []);
    }

    private static MissionRuntimeSnapshot CreateTriviaRuntimeSnapshot(Guid sourceMissionId, int maximumTimeMinutes)
    {
        var triviaSubstage = SubstageSnapshot.CreateTrivia("Trivia Round", 1);

        return MissionRuntimeSnapshot.Create(
            sourceMissionId,
            "Mission Runtime",
            MaximumTime.Create(maximumTimeMinutes),
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

    private static MissionRuntimeSnapshot CreateMixedRuntimeSnapshot(Guid sourceMissionId, int maximumTimeMinutes)
    {
        var treasureHuntSubstage = SubstageSnapshot.CreateTreasureHunt("Treasure Hunt", 1, 100);
        var triviaSubstage = SubstageSnapshot.CreateTrivia("Trivia Round", 2);

        return MissionRuntimeSnapshot.Create(
            sourceMissionId,
            "Mission Runtime",
            MaximumTime.Create(maximumTimeMinutes),
            [
                StageSnapshot.Create("Stage One", 1, [treasureHuntSubstage, triviaSubstage])
            ],
            [
                TargetSnapshot.Create(
                    treasureHuntSubstage.SubstageSnapshotId,
                    "Target Alpha",
                    "QR-ALPHA",
                    1,
                    true,
                    "Look under the stairs",
                    "AfterPreviousTarget")
            ],
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
                    ])
            ]);
    }
}

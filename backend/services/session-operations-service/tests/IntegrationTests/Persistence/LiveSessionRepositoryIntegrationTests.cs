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
    public async Task GetByIdAsync_RestoresWholeTriviaQuizSnapshotWithScoreTimerAndCorrectFlagsInMissionOrder()
    {
        await using var resetContext = BuildContext();
        await ResetDatabaseAsync(resetContext);

        // CreateTriviaRuntimeSnapshot freezes the whole quiz: 2 questions with distinct timers/options in authored order.
        var liveSession = CreateTriviaSession(DateTimeOffset.UtcNow.AddDays(1));

        await using (var seedContext = BuildContext())
        {
            seedContext.LiveSessions.Add(liveSession);
            await seedContext.SaveChangesAsync(CancellationToken.None);
        }

        await using var assertContext = BuildContext();
        var persistedSession = await new LiveSessionRepository(assertContext)
            .GetByIdAsync(liveSession.LiveSessionId, CancellationToken.None);

        persistedSession.Should().NotBeNull();

        var questions = persistedSession!.MissionRuntimeSnapshot.TriviaQuestionSnapshots
            .OrderBy(question => question.SequenceOrder)
            .ToList();
        questions.Should().HaveCount(2);

        var first = questions[0];
        first.SequenceOrder.Should().Be(1);
        first.Prompt.Should().Be("Capital of France?");
        first.ScoreValue.Should().Be(50);
        first.TimeLimitSeconds.Should().Be(30);
        first.Explanation.Should().Be("Paris is the capital city.");
        first.Options.OrderBy(option => option.SequenceOrder)
            .Select(option => (option.OptionText, option.IsCorrect))
            .Should().Equal(("Paris", true), ("Lyon", false));

        var second = questions[1];
        second.SequenceOrder.Should().Be(2);
        second.Prompt.Should().Be("Capital of Spain?");
        second.ScoreValue.Should().Be(50);
        second.TimeLimitSeconds.Should().Be(25);
        second.Explanation.Should().Be("Madrid is the capital city.");
        second.Options.OrderBy(option => option.SequenceOrder)
            .Select(option => (option.OptionText, option.IsCorrect))
            .Should().Equal(("Madrid", true), ("Barcelona", false));
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

        // HU-22: the whole-session `_sessionTimer*` countdown columns are dropped; `maximum_time_minutes`
        // (authoring metadata, OD-3) and the trivia `question_timer_*` window survive.
        columns.Should().NotContain(name => name.Contains("session_timer_", StringComparison.OrdinalIgnoreCase));
        columns.Should().Contain(name => name.Contains("question_timer_", StringComparison.OrdinalIgnoreCase));
        columns.Should().Contain(name => name.Contains("maximum_time_minutes", StringComparison.OrdinalIgnoreCase));
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

        // Entering Active parks the pointer on the first substage (HU-33A); it must survive reload
        // alongside the active-substage question timer.
        persistedSession.ActiveSubstageId.Should().Be(liveSession.ActiveSubstageId);
        persistedSession.ActiveSubstageId.Should().NotBeNull();

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

    // HU-22 (OD-3): the authoritative snapshot is the active trivia-question window, not a whole-session
    // countdown. Question 0 carries a 30s limit; these round-trip its advance/freeze/resume through Postgres.
    [Fact]
    public async Task GetByIdAsync_RestoresAdvancingActiveQuestionTimer()
    {
        await using var resetContext = BuildContext();
        await ResetDatabaseAsync(resetContext);

        var activeAt = new DateTimeOffset(2026, 6, 4, 12, 0, 0, TimeSpan.Zero);
        var liveSession = CreateActiveTriviaQuestionSession(activeAt);

        await using (var seedContext = BuildContext())
        {
            seedContext.LiveSessions.Add(liveSession);
            await seedContext.SaveChangesAsync(CancellationToken.None);
        }

        await using var assertContext = BuildContext();
        var persistedSession = await new LiveSessionRepository(assertContext)
            .GetByIdAsync(liveSession.LiveSessionId, CancellationToken.None);

        persistedSession.Should().NotBeNull();
        var snapshot = persistedSession!.GetAuthoritativeSessionTimerSnapshot(activeAt.AddSeconds(10));

        snapshot.TotalDuration.Should().Be(TimeSpan.FromSeconds(30));
        snapshot.RemainingDuration.Should().BeCloseTo(TimeSpan.FromSeconds(20), TimeSpan.FromMilliseconds(1));
        snapshot.IsAdvancing.Should().BeTrue();
        snapshot.AdvancingSince.Should().Be(activeAt);
    }

    [Fact]
    public async Task GetByIdAsync_RestoresPausedActiveQuestionTimerAsFrozen()
    {
        await using var resetContext = BuildContext();
        await ResetDatabaseAsync(resetContext);

        var activeAt = new DateTimeOffset(2026, 6, 4, 12, 0, 0, TimeSpan.Zero);
        var pausedAt = activeAt.AddSeconds(10);
        var liveSession = CreateActiveTriviaQuestionSession(activeAt);
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

        snapshot.RemainingDuration.Should().Be(TimeSpan.FromSeconds(20));
        snapshot.IsAdvancing.Should().BeFalse();
        snapshot.AdvancingSince.Should().BeNull();
        persistedSession.State.Should().Be(SessionState.Paused);
    }

    [Fact]
    public async Task GetByIdAsync_RestoresResumedActiveQuestionTimerFromFrozenRemainder()
    {
        await using var resetContext = BuildContext();
        await ResetDatabaseAsync(resetContext);

        var activeAt = new DateTimeOffset(2026, 6, 4, 12, 0, 0, TimeSpan.Zero);
        var pausedAt = activeAt.AddSeconds(10);
        var resumedAt = pausedAt.AddMinutes(10);
        var liveSession = CreateActiveTriviaQuestionSession(activeAt);
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
        var snapshot = persistedSession!.GetAuthoritativeSessionTimerSnapshot(resumedAt.AddSeconds(5));

        snapshot.RemainingDuration.Should().BeCloseTo(TimeSpan.FromSeconds(15), TimeSpan.FromMilliseconds(1));
        snapshot.IsAdvancing.Should().BeTrue();
        snapshot.AdvancingSince.Should().Be(resumedAt);
    }

    // Round-trips a transitioned session through the real repository to prove its new state,
    // the timestamp the transition happened, and the operator's reason all persist and reload intact.
    [Fact]
    public async Task UpdateAsync_PersistsTransitionedStateReasonAndTimestamp()
    {
        await using var resetContext = BuildContext();
        await ResetDatabaseAsync(resetContext);

        var createdAt = DateTimeOffset.UtcNow.AddMinutes(-30);
        var liveSession = CreateSession(createdAt);

        await using (var seedContext = BuildContext())
        {
            seedContext.LiveSessions.Add(liveSession);
            await seedContext.SaveChangesAsync(CancellationToken.None);
        }

        var cancelledAt = createdAt.AddMinutes(10);
        const string reason = "Venue closed unexpectedly";

        await using (var actContext = BuildContext())
        {
            var repository = new LiveSessionRepository(actContext);
            var persistedSession = await repository.GetByIdAsync(liveSession.LiveSessionId, CancellationToken.None);

            persistedSession.Should().NotBeNull();
            persistedSession!.MoveTo(SessionState.Cancelled, cancelledAt, new SessionStateTransitionPolicy(), reason);

            await repository.UpdateAsync(persistedSession, CancellationToken.None);
        }

        await using var assertContext = BuildContext();
        var reloadedSession = await new LiveSessionRepository(assertContext)
            .GetByIdAsync(liveSession.LiveSessionId, CancellationToken.None);

        reloadedSession.Should().NotBeNull();
        reloadedSession!.State.Should().Be(SessionState.Cancelled);
        reloadedSession.StateReason.Should().Be(reason);
        reloadedSession.LastStateChangedAt.Should().BeCloseTo(cancelledAt, TimeSpan.FromMicroseconds(1));
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

    private static LiveSession CreateActiveTriviaQuestionSession(DateTimeOffset activeAt)
    {
        var liveSession = CreateTriviaSession(activeAt.AddMinutes(-10));
        TransitionTriviaSessionToActive(liveSession, activeAt);
        liveSession.ActivateQuestion(0, activeAt);
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

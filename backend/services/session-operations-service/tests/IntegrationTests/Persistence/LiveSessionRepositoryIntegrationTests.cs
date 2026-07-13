using umbral_backend.Domain.Entities;
using umbral_backend.Domain.Enums;
using umbral_backend.Domain.Exceptions;
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
        target.Latitude.Should().Be(4.711);
        target.Longitude.Should().Be(-74.0721);

        var question = persistedSession.MissionRuntimeSnapshot.TriviaQuestionSnapshots.Single();
        question.Prompt.Should().Be("Capital of France?");
        question.Options.Should().HaveCount(2);
        question.Options.Should().ContainSingle(option => option.OptionText == "Paris" && option.IsCorrect);

        // Substage-scoped clues survive the snapshot round-trip (#145), with text, policy, and order.
        var clues = persistedSession.MissionRuntimeSnapshot.ClueSnapshots
            .OrderBy(clue => clue.SequenceOrder)
            .ToArray();
        clues.Select(clue => clue.Text).Should().Equal("Shown at start.", "Released by operator.");
        clues.Select(clue => clue.VisibilityPolicy)
            .Should().Equal("VisibleWhenSubstageStarts", "HiddenUntilOperatorRelease");
        var triviaSubstageId = stage.SubstageSnapshots
            .Single(substage => substage.PlayMode == SubstagePlayMode.Trivia).SubstageSnapshotId;
        clues.Should().OnlyContain(clue => clue.SubstageSnapshotId == triviaSubstageId);
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

    // DES-93: an Active session whose first substage is TreasureHunt seeds an advancing substage timer on
    // activation; the worker must tick it, so ListActiveTimersAsync selects it via the substage branch.
    [Fact]
    public async Task ListActiveTimersAsync_ReturnsSessionWithAdvancingTreasureHuntSubstageTimer()
    {
        await using var resetContext = BuildContext();
        await ResetDatabaseAsync(resetContext);

        var activeAt = new DateTimeOffset(2026, 6, 4, 12, 0, 0, TimeSpan.Zero);
        var liveSession = CreateActiveTreasureHuntSession(activeAt);

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

    // DES-93 stale-field guard: SeedSubstageTimerIfTreasureHunt early-returns without clearing the
    // _substageTimer* fields when a TreasureHunt substage advances to a non-treasure-hunt one, so the
    // advancing/expired predicate alone would keep matching. The predicate is scoped to the active
    // substage actually being TreasureHunt, so an advanced-to-Trivia session (with no active question)
    // must NOT be returned — otherwise the report-only worker would tick it redundantly.
    [Fact]
    public async Task ListActiveTimersAsync_ExcludesSessionAdvancedFromTreasureHuntToTriviaSubstage()
    {
        await using var resetContext = BuildContext();
        await ResetDatabaseAsync(resetContext);

        var activeAt = new DateTimeOffset(2026, 6, 4, 12, 0, 0, TimeSpan.Zero);
        var sourceMissionId = Guid.NewGuid();
        var liveSession = LiveSession.Create(
            SessionSource.Create(sourceMissionId),
            $"SES-{Guid.NewGuid():N}"[..12],
            "Mixed Route",
            20,
            activeAt.AddMinutes(-10),
            CreateMixedRuntimeSnapshot(sourceMissionId, 20));
        var transitionPolicy = new SessionStateTransitionPolicy();
        liveSession.AssociateTeam(Guid.NewGuid(), "Aurora", "AUR-01", 3);
        liveSession.MoveTo(SessionState.Preparing, activeAt.AddMinutes(-1), transitionPolicy);
        liveSession.MoveTo(SessionState.Active, activeAt, transitionPolicy);
        // Active substage is now the leading TreasureHunt substage (timer seeded + advancing). Advance to
        // the trailing Trivia substage without activating a question: the substage timer fields stay stale.
        liveSession.CompleteActiveSubstageAndAdvance(activeAt.AddSeconds(30), transitionPolicy);
        liveSession.ActiveQuestionIndex.Should().BeNull();

        await using (var seedContext = BuildContext())
        {
            seedContext.LiveSessions.Add(liveSession);
            await seedContext.SaveChangesAsync(CancellationToken.None);
        }

        await using var assertContext = BuildContext();
        var sessions = await new LiveSessionRepository(assertContext)
            .ListActiveTimersAsync(CancellationToken.None);

        sessions.Should().NotContain(session => session.LiveSessionId == liveSession.LiveSessionId);
    }

    // DES-93: round-trips a seeded, advancing TreasureHunt substage timer through Postgres and proves the
    // authoritative snapshot is the substage window seeded from the session-level MaximumTime (45 min),
    // not the zero/expired question window that shipped before this slice.
    [Fact]
    public async Task GetByIdAsync_RestoresAdvancingTreasureHuntSubstageTimer()
    {
        await using var resetContext = BuildContext();
        await ResetDatabaseAsync(resetContext);

        var activeAt = new DateTimeOffset(2026, 6, 4, 12, 0, 0, TimeSpan.Zero);
        var liveSession = CreateActiveTreasureHuntSession(activeAt);

        await using (var seedContext = BuildContext())
        {
            seedContext.LiveSessions.Add(liveSession);
            await seedContext.SaveChangesAsync(CancellationToken.None);
        }

        await using var assertContext = BuildContext();
        var persistedSession = await new LiveSessionRepository(assertContext)
            .GetByIdAsync(liveSession.LiveSessionId, CancellationToken.None);

        persistedSession.Should().NotBeNull();
        var snapshot = persistedSession!.GetAuthoritativeSessionTimerSnapshot(activeAt.AddMinutes(1));

        snapshot.TotalDuration.Should().Be(TimeSpan.FromMinutes(45));
        snapshot.RemainingDuration.Should().BeCloseTo(TimeSpan.FromMinutes(44), TimeSpan.FromMilliseconds(1));
        snapshot.IsAdvancing.Should().BeTrue();
        snapshot.IsExpired.Should().BeFalse();
        snapshot.AdvancingSince.Should().Be(activeAt);
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

    // HU-34: an accepted trivia answer (base EvidenceSubmission umbrella + TriviaAnswerSubmission
    // specialization) must survive a save -> reload round-trip through the aggregate repository.
    [Fact]
    public async Task UpdateAsync_RoundTripsAcceptedTriviaAnswerThroughAggregate()
    {
        await using var resetContext = BuildContext();
        await ResetDatabaseAsync(resetContext);

        var activeAt = new DateTimeOffset(2026, 6, 4, 12, 0, 0, TimeSpan.Zero);
        var liveSession = CreateActiveTriviaQuestionSession(activeAt);
        var teamId = liveSession.Teams.Single().TeamId;
        var participantId = Guid.NewGuid();

        await using (var seedContext = BuildContext())
        {
            seedContext.LiveSessions.Add(liveSession);
            await seedContext.SaveChangesAsync(CancellationToken.None);
        }

        var submittedAt = activeAt.AddSeconds(5);

        await using (var actContext = BuildContext())
        {
            var repository = new LiveSessionRepository(actContext);
            var persistedSession = await repository.GetByIdAsync(liveSession.LiveSessionId, CancellationToken.None);

            persistedSession.Should().NotBeNull();

            // Option 1 ("Paris") is the correct option worth 50 points on the first active question.
            persistedSession!.RegisterTriviaAnswer(teamId, 1, participantId, submittedAt);

            await repository.UpdateAsync(persistedSession, CancellationToken.None);
        }

        await using var assertContext = BuildContext();
        var reloadedSession = await new LiveSessionRepository(assertContext)
            .GetByIdAsync(liveSession.LiveSessionId, CancellationToken.None);

        reloadedSession.Should().NotBeNull();
        reloadedSession!.TriviaAnswerSubmissions.Should().ContainSingle();

        var answer = reloadedSession.TriviaAnswerSubmissions.Single();

        // Base EvidenceSubmission umbrella node round-trips.
        answer.EvidenceSubmissionId.Should().NotBe(Guid.Empty);
        answer.LiveSessionId.Should().Be(liveSession.LiveSessionId);
        answer.TeamId.Should().Be(teamId);
        answer.ActiveSubstageId.Should().Be(reloadedSession.ActiveSubstageId!.Value);
        answer.SubmissionType.Should().Be(EvidenceSubmissionType.TriviaAnswer);
        answer.SubmittedByParticipantId.Should().Be(participantId);
        answer.SubmittedAt.Should().BeCloseTo(submittedAt, TimeSpan.FromMicroseconds(1));
        answer.ValidationState.Should().Be(EvidenceValidationState.Accepted);

        // Trivia specialization node round-trips with the snapshotted correctness/score.
        answer.QuestionSequenceOrder.Should().Be(1);
        answer.SelectedOptionSequenceOrder.Should().Be(1);
        answer.IsCorrect.Should().BeTrue();
        answer.ScoreValue.Should().Be(50);
    }

    // HU-34 first-write-wins is also guarded at the DB boundary: two aggregates loaded before either
    // committed (each blind to the other's answer, so the in-memory duplicate guard cannot see it)
    // must not both persist an answer for the same team + snapshotted question. The unique index
    // rejects the second write with a DbUpdateException.
    [Fact]
    public async Task UpdateAsync_RejectsSecondAnswerForSameTeamAndQuestionAtDatabase()
    {
        await using var resetContext = BuildContext();
        await ResetDatabaseAsync(resetContext);

        var activeAt = new DateTimeOffset(2026, 6, 4, 12, 0, 0, TimeSpan.Zero);
        var liveSession = CreateActiveTriviaQuestionSession(activeAt);
        var teamId = liveSession.Teams.Single().TeamId;

        await using (var seedContext = BuildContext())
        {
            seedContext.LiveSessions.Add(liveSession);
            await seedContext.SaveChangesAsync(CancellationToken.None);
        }

        var submittedAt = activeAt.AddSeconds(5);

        await using var firstContext = BuildContext();
        await using var secondContext = BuildContext();

        var firstSession = await new LiveSessionRepository(firstContext)
            .GetByIdAsync(liveSession.LiveSessionId, CancellationToken.None);
        var secondSession = await new LiveSessionRepository(secondContext)
            .GetByIdAsync(liveSession.LiveSessionId, CancellationToken.None);

        firstSession.Should().NotBeNull();
        secondSession.Should().NotBeNull();

        // Both copies register an answer for the same team/question — each blind to the other.
        firstSession!.RegisterTriviaAnswer(teamId, 1, Guid.NewGuid(), submittedAt);
        secondSession!.RegisterTriviaAnswer(teamId, 2, Guid.NewGuid(), submittedAt);

        await new LiveSessionRepository(firstContext).UpdateAsync(firstSession, CancellationToken.None);

        var secondWrite = async () =>
            await new LiveSessionRepository(secondContext).UpdateAsync(secondSession, CancellationToken.None);

        await secondWrite.Should().ThrowAsync<DbUpdateException>(
            "the unique index on team + snapshotted question must reject a second accepted answer");
    }

    // HU-36A: the restricted answered/not-answered monitor is a pure read over HU-34's persisted answers.
    // This proves the aggregate read path hydrates its Teams AND accepted TriviaAnswerSubmissions so that,
    // after a real DbContext save -> reload round-trip, ProjectActiveQuestionAnsweredStatus() marks the
    // teams that answered the active question as Answered (with AnsweredAt) and the rest as not-answered.
    // No new persisted state, no migration — it rides the existing GetByIdAsync includes.
    [Fact]
    public async Task GetByIdAsync_HydratesTeamsAndAcceptedAnswers_SoAnsweredMonitorRoundTrips()
    {
        await using var resetContext = BuildContext();
        await ResetDatabaseAsync(resetContext);

        var activeAt = new DateTimeOffset(2026, 6, 4, 12, 0, 0, TimeSpan.Zero);
        var liveSession = CreateActiveTriviaQuestionSessionWithTeams(
            activeAt,
            ("Alpha", "AAA-01"),
            ("Bravo", "BBB-01"),
            ("Charlie", "CCC-01"),
            ("Delta", "DDD-01"));

        var teamIdByCode = liveSession.Teams.ToDictionary(team => team.TeamCode.Value, team => team.TeamId);
        var alphaAnsweredAt = activeAt.AddSeconds(5);
        var charlieAnsweredAt = activeAt.AddSeconds(6);

        // Two of the four teams answer the active question; Bravo and Delta never do.
        liveSession.RegisterTriviaAnswer(teamIdByCode["AAA-01"], 1, Guid.NewGuid(), alphaAnsweredAt);
        liveSession.RegisterTriviaAnswer(teamIdByCode["CCC-01"], 2, Guid.NewGuid(), charlieAnsweredAt);

        await using (var seedContext = BuildContext())
        {
            seedContext.LiveSessions.Add(liveSession);
            await seedContext.SaveChangesAsync(CancellationToken.None);
        }

        await using var assertContext = BuildContext();
        var persistedSession = await new LiveSessionRepository(assertContext)
            .GetByIdAsync(liveSession.LiveSessionId, CancellationToken.None);

        persistedSession.Should().NotBeNull();
        persistedSession!.Teams.Should().HaveCount(4);
        persistedSession.TriviaAnswerSubmissions.Should().HaveCount(2);

        // Project over the RELOADED aggregate — this only yields correct answered/not-answered cells if the
        // read path deep-loaded both the teams and their accepted answers for the active question.
        var snapshot = persistedSession.ProjectActiveQuestionAnsweredStatus();

        // Active-question identity: question index 0 => sequence order 1 on the parked trivia substage.
        snapshot.QuestionSequenceOrder.Should().Be(1);
        snapshot.SubstageSnapshotId.Should().Be(persistedSession.ActiveSubstageId!.Value);
        snapshot.TeamStatuses.Should().HaveCount(4);

        var statusByCode = snapshot.TeamStatuses.ToDictionary(status => status.TeamCode);

        statusByCode["AAA-01"].Answered.Should().BeTrue();
        statusByCode["AAA-01"].AnsweredAt.Should().BeCloseTo(alphaAnsweredAt, TimeSpan.FromMicroseconds(1));
        statusByCode["CCC-01"].Answered.Should().BeTrue();
        statusByCode["CCC-01"].AnsweredAt.Should().BeCloseTo(charlieAnsweredAt, TimeSpan.FromMicroseconds(1));

        statusByCode["BBB-01"].Answered.Should().BeFalse();
        statusByCode["BBB-01"].AnsweredAt.Should().BeNull();
        statusByCode["DDD-01"].Answered.Should().BeFalse();
        statusByCode["DDD-01"].AnsweredAt.Should().BeNull();
    }

    // HU-26: a manual clue release appends an append-only ClueReleaseRecord and increments the released
    // team's ReleasedClueCount. This proves the owned collection round-trips through a real save -> reload:
    // the reloaded aggregate must re-hydrate the release record (team/target/releasedAt) via GetByIdAsync's
    // deep load, so the in-memory duplicate/no-leak guards still see the prior release after a reload.
    [Fact]
    public async Task UpdateAsync_RoundTripsManualClueReleaseRecordThroughAggregate()
    {
        await using var resetContext = BuildContext();
        await ResetDatabaseAsync(resetContext);

        var activeAt = new DateTimeOffset(2026, 6, 4, 12, 0, 0, TimeSpan.Zero);
        var liveSession = CreateActiveTreasureHuntSession(activeAt);
        var teamId = liveSession.Teams.Single().TeamId;
        var targetId = liveSession.MissionRuntimeSnapshot.TargetSnapshots.Single().TargetSnapshotId;

        await using (var seedContext = BuildContext())
        {
            seedContext.LiveSessions.Add(liveSession);
            await seedContext.SaveChangesAsync(CancellationToken.None);
        }

        var releasedAt = activeAt.AddSeconds(30);

        await using (var actContext = BuildContext())
        {
            var repository = new LiveSessionRepository(actContext);
            var persistedSession = await repository.GetByIdAsync(liveSession.LiveSessionId, CancellationToken.None);

            persistedSession.Should().NotBeNull();
            persistedSession!.ReleaseClue(targetId, teamId, 42, releasedAt);

            await repository.UpdateAsync(persistedSession, CancellationToken.None);
        }

        await using var assertContext = BuildContext();
        var reloadedSession = await new LiveSessionRepository(assertContext)
            .GetByIdAsync(liveSession.LiveSessionId, CancellationToken.None);

        reloadedSession.Should().NotBeNull();

        var records = reloadedSession!.GetClueReleaseRecords();
        records.Should().ContainSingle();

        var record = records.Single();
        record.LiveSessionId.Should().Be(liveSession.LiveSessionId);
        record.TeamId.Should().Be(teamId);
        record.TargetId.Should().Be(targetId);
        record.ClueId.Should().BeNull();
        record.ReleaseMode.Should().Be(ReleaseMode.Manual);
        record.ReleasedByUserId.Should().Be(42);
        record.ReleasedAt.Should().BeCloseTo(releasedAt, TimeSpan.FromMicroseconds(1));

        // The released team's counter increments and round-trips; releasing does not advance the substage.
        reloadedSession.Teams.Single().ReleasedClueCount.Should().Be(1);
        reloadedSession.ActiveSubstageId.Should().Be(liveSession.ActiveSubstageId);

        // The deep-loaded collection makes the duplicate guard fire after a reload — no leak across reloads.
        var releaseAgain = () => reloadedSession.ReleaseClue(targetId, teamId, 42, releasedAt.AddSeconds(1));
        releaseAgain.Should().Throw<ClueAlreadyReleasedToTeamException>();
    }

    // HU-26 DB-boundary guard: two aggregates loaded before either committed (each blind to the other's
    // release) must not both persist a release for the same team + target. The unique index rejects the
    // second write with a DbUpdateException.
    [Fact]
    public async Task UpdateAsync_RejectsSecondClueReleaseForSameTeamAndTargetAtDatabase()
    {
        await using var resetContext = BuildContext();
        await ResetDatabaseAsync(resetContext);

        var activeAt = new DateTimeOffset(2026, 6, 4, 12, 0, 0, TimeSpan.Zero);
        var liveSession = CreateActiveTreasureHuntSession(activeAt);
        var teamId = liveSession.Teams.Single().TeamId;
        var targetId = liveSession.MissionRuntimeSnapshot.TargetSnapshots.Single().TargetSnapshotId;

        await using (var seedContext = BuildContext())
        {
            seedContext.LiveSessions.Add(liveSession);
            await seedContext.SaveChangesAsync(CancellationToken.None);
        }

        var releasedAt = activeAt.AddSeconds(30);

        await using var firstContext = BuildContext();
        await using var secondContext = BuildContext();

        var firstSession = await new LiveSessionRepository(firstContext)
            .GetByIdAsync(liveSession.LiveSessionId, CancellationToken.None);
        var secondSession = await new LiveSessionRepository(secondContext)
            .GetByIdAsync(liveSession.LiveSessionId, CancellationToken.None);

        firstSession.Should().NotBeNull();
        secondSession.Should().NotBeNull();

        firstSession!.ReleaseClue(targetId, teamId, 42, releasedAt);
        secondSession!.ReleaseClue(targetId, teamId, 43, releasedAt);

        await new LiveSessionRepository(firstContext).UpdateAsync(firstSession, CancellationToken.None);

        var secondWrite = async () =>
            await new LiveSessionRepository(secondContext).UpdateAsync(secondSession, CancellationToken.None);

        await secondWrite.Should().ThrowAsync<DbUpdateException>(
            "the unique index on team + target must reject a second release for the same clue");
    }

    private static LiveSession CreateActiveTreasureHuntSession(DateTimeOffset activeAt)
    {
        var sourceMissionId = Guid.NewGuid();
        var liveSession = LiveSession.Create(
            SessionSource.Create(sourceMissionId),
            $"SES-{Guid.NewGuid():N}"[..12],
            "Treasure Hunt Night",
            45,
            activeAt.AddMinutes(-10),
            CreateReleasableTreasureHuntRuntimeSnapshot(sourceMissionId, 45));

        var transitionPolicy = new SessionStateTransitionPolicy();
        liveSession.AssociateTeam(Guid.NewGuid(), "Aurora", "AUR-01", 3);
        liveSession.MoveTo(SessionState.Preparing, activeAt.AddMinutes(-1), transitionPolicy);
        liveSession.MoveTo(SessionState.Active, activeAt, transitionPolicy);
        return liveSession;
    }

    // A treasure-hunt snapshot whose single active target carries an operator-gated hidden clue, so
    // ReleaseClue resolves it as releasable in the active substage.
    private static MissionRuntimeSnapshot CreateReleasableTreasureHuntRuntimeSnapshot(
        Guid sourceMissionId,
        int maximumTimeMinutes)
    {
        var treasureHuntSubstage = SubstageSnapshot.CreateTreasureHunt("Treasure Hunt", 1);

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
                    100,
                    4.711,
                    -74.0721,
                    "Look under the stairs",
                    "HiddenUntilOperatorRelease")
            ],
            []);
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

    // Like CreateActiveTriviaQuestionSession but associates several named teams while still Scheduled
    // (the only state that admits team association) before activating the first question — so the
    // answered/not-answered monitor has a multi-team roster to project.
    private static LiveSession CreateActiveTriviaQuestionSessionWithTeams(
        DateTimeOffset activeAt,
        params (string DisplayName, string TeamCode)[] teams)
    {
        var liveSession = CreateTriviaSession(activeAt.AddMinutes(-10));

        foreach (var team in teams)
        {
            liveSession.AssociateTeam(Guid.NewGuid(), team.DisplayName, team.TeamCode, 4);
        }

        var transitionPolicy = new SessionStateTransitionPolicy();
        liveSession.MoveTo(SessionState.Preparing, activeAt.AddMinutes(-1), transitionPolicy);
        liveSession.MoveTo(SessionState.Active, activeAt, transitionPolicy);
        liveSession.ActivateQuestion(0, activeAt);
        return liveSession;
    }

    private static LiveSession CreateActiveTreasureHuntSession(DateTimeOffset activeAt)
    {
        var liveSession = CreateSession(activeAt.AddMinutes(-10));
        var transitionPolicy = new SessionStateTransitionPolicy();
        liveSession.AssociateTeam(Guid.NewGuid(), "Aurora", "AUR-01", 3);
        liveSession.MoveTo(SessionState.Preparing, activeAt.AddMinutes(-1), transitionPolicy);
        liveSession.MoveTo(SessionState.Active, activeAt, transitionPolicy);
        return liveSession;
    }

    private static MissionRuntimeSnapshot CreateTreasureHuntRuntimeSnapshot(Guid sourceMissionId, int maximumTimeMinutes)
    {
        var treasureHuntSubstage = SubstageSnapshot.CreateTreasureHunt("Treasure Hunt", 1);

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
                    100,
                    4.711,
                    -74.0721,
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
        var treasureHuntSubstage = SubstageSnapshot.CreateTreasureHunt("Treasure Hunt", 1);
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
                    100,
                    4.711,
                    -74.0721,
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
            ],
            [
                ClueSnapshot.Create(triviaSubstage.SubstageSnapshotId, "Shown at start.", "VisibleWhenSubstageStarts", 1),
                ClueSnapshot.Create(triviaSubstage.SubstageSnapshotId, "Released by operator.", "HiddenUntilOperatorRelease", 2)
            ]);
    }
}

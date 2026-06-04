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
    public async Task GetByIdAsync_RestoresTriviaSnapshotGraph()
    {
        await using var resetContext = BuildContext();
        await ResetDatabaseAsync(resetContext);

        var scheduledAt = DateTimeOffset.UtcNow.AddDays(1);
        var liveSession = LiveSession.CreateTrivia(
            SessionSource.CreateTriviaQuiz(42),
            $"SES-{Guid.NewGuid():N}"[..12],
            "Trivia Night",
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
                        ])
                ]));

        await using (var seedContext = BuildContext())
        {
            seedContext.LiveSessions.Add(liveSession);
            await seedContext.SaveChangesAsync(CancellationToken.None);
        }

        await using var assertContext = BuildContext();
        var persistedSession = await new LiveSessionRepository(assertContext)
            .GetByIdAsync(liveSession.LiveSessionId, CancellationToken.None);

        persistedSession.Should().NotBeNull();
        persistedSession!.Source.SourceTriviaQuizId.Should().Be(42);
        persistedSession.TriviaSnapshot.Should().NotBeNull();
        persistedSession.TriviaSnapshot!.QuizTitle.Should().Be("Trivia Source");
        persistedSession.TriviaSnapshot.Questions.Should().ContainSingle();

        var question = persistedSession.TriviaSnapshot.Questions.Single();
        question.Prompt.Should().Be("Capital of France?");
        question.Explanation.Should().Be("Paris is the capital city.");
        question.Options.Should().HaveCount(2);
        question.Options.Should().ContainSingle(option => option.OptionText == "Paris" && option.IsCorrect);
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

    private static async Task ResetDatabaseAsync(ApplicationDbContext context)
    {
        // Deleting the aggregate root cascades to teams, participants, join contexts,
        // and team members (all FKs are ON DELETE CASCADE).
        await context.LiveSessions.ExecuteDeleteAsync();
    }

    private static LiveSession CreateSession(DateTimeOffset scheduledAt)
    {
        return LiveSession.Create(
            SessionMode.TreasureHunt,
            SessionSource.Create(SessionSourceType.Mission, Guid.NewGuid()),
            $"SES-{Guid.NewGuid():N}"[..12],
            "Reconnect Session",
            45,
            scheduledAt);
    }

    private static LiveSession CreateActiveSession(DateTimeOffset activeAt)
    {
        var liveSession = CreateSession(activeAt.AddMinutes(-10));
        liveSession.RegisterTeam("Blue", "BLUE-01", 4);
        var transitionPolicy = new SessionStateTransitionPolicy();
        liveSession.MoveTo(SessionState.Preparing, activeAt.AddMinutes(-1), transitionPolicy);
        liveSession.MoveTo(SessionState.Active, activeAt, transitionPolicy);
        return liveSession;
    }
}

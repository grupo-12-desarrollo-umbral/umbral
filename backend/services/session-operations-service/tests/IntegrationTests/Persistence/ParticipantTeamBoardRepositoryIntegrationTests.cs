using umbral_backend.Domain.Entities;
using umbral_backend.Domain.Enums;
using umbral_backend.Domain.Services;
using umbral_backend.Domain.ValueObjects;
using umbral_backend.Infrastructure.Persistence;
using umbral_backend.Infrastructure.Persistence.Repositories;

namespace umbral_backend.Infrastructure.IntegrationTests.Persistence;

[Collection(PostgreSqlCollection.Name)]
public sealed class ParticipantTeamBoardRepositoryIntegrationTests
{
    private readonly PersistenceTestContextFactory _contextFactory;

    public ParticipantTeamBoardRepositoryIntegrationTests(PostgreSqlFixture fixture)
    {
        _contextFactory = new PersistenceTestContextFactory(fixture.ConnectionString);
    }

    [Fact]
    public async Task GetByIdAsync_BoardRead_HydratesTeamsAndTreasureHuntTargetsWithClues()
    {
        await using var resetContext = BuildContext();
        await ResetDatabaseAsync(resetContext);

        var activeAt = new DateTimeOffset(2026, 7, 11, 12, 0, 0, TimeSpan.Zero);
        var liveSession = CreateActiveTreasureHuntSession(activeAt,
            ("Alpha", "AAA-01"),
            ("Bravo", "BBB-01"));

        await using (var seedContext = BuildContext())
        {
            seedContext.LiveSessions.Add(liveSession);
            await seedContext.SaveChangesAsync(CancellationToken.None);
        }

        await using var assertContext = BuildContext();
        var repository = new LiveSessionRepository(assertContext);
        var persistedSession = await repository.GetByIdAsync(liveSession.LiveSessionId, CancellationToken.None);

        persistedSession.Should().NotBeNull();
        var teamId = persistedSession!.Teams.First().TeamId;

        var board = persistedSession.ProjectParticipantTeamBoard(teamId, activeAt.AddSeconds(5));

        board.TeamId.Should().Be(teamId);
        board.CurrentScore.Should().Be(0);
        board.ActiveSubstageContext.Should().NotBeNull();
        board.ActiveSubstageContext!.PlayMode.Should().Be(SubstagePlayMode.TreasureHunt);
        board.ActiveSubstageContext.TotalActiveTargets.Should().Be(2);
        board.ActiveSubstageContext.ResolvedTargets.Should().Be(0);
        board.VisibleClues.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GetByIdAsync_BoardRead_HydratesTriviaActiveQuestionContext()
    {
        await using var resetContext = BuildContext();
        await ResetDatabaseAsync(resetContext);

        var activeAt = new DateTimeOffset(2026, 7, 11, 12, 0, 0, TimeSpan.Zero);
        var liveSession = CreateActiveTriviaQuestionSessionWithTeams(activeAt,
            ("Alpha", "AAA-01"),
            ("Bravo", "BBB-01"));

        await using (var seedContext = BuildContext())
        {
            seedContext.LiveSessions.Add(liveSession);
            await seedContext.SaveChangesAsync(CancellationToken.None);
        }

        await using var assertContext = BuildContext();
        var repository = new LiveSessionRepository(assertContext);
        var persistedSession = await repository.GetByIdAsync(liveSession.LiveSessionId, CancellationToken.None);

        persistedSession.Should().NotBeNull();
        persistedSession!.ActiveSubstageId.Should().NotBeNull();
        persistedSession.ActiveQuestionIndex.Should().NotBeNull();

        var teamId = persistedSession.Teams.First().TeamId;
        var board = persistedSession.ProjectParticipantTeamBoard(teamId, activeAt.AddSeconds(5));

        board.ActiveSubstageContext.Should().NotBeNull();
        board.ActiveSubstageContext!.PlayMode.Should().Be(SubstagePlayMode.Trivia);
        board.ActiveSubstageContext.ActiveQuestionSequenceOrder.Should().Be(1);
        board.ActiveSubstageContext.ActiveQuestionTimeLimitSeconds.Should().Be(30);
        board.ActiveSubstageContext.TotalActiveTargets.Should().Be(0);
        board.ActiveSubstageContext.ResolvedTargets.Should().Be(0);
    }

    [Fact]
    public async Task GetByIdAsync_BoardRead_HydratesTeamsAndMembersSoBoardResolvesOnlyRequestedTeam()
    {
        await using var resetContext = BuildContext();
        await ResetDatabaseAsync(resetContext);

        var activeAt = new DateTimeOffset(2026, 7, 11, 12, 0, 0, TimeSpan.Zero);
        var liveSession = CreateActiveTriviaQuestionSessionWithTeams(activeAt,
            ("Alpha", "AAA-01"),
            ("Bravo", "BBB-01"),
            ("Charlie", "CCC-01"));

        await using (var seedContext = BuildContext())
        {
            seedContext.LiveSessions.Add(liveSession);
            await seedContext.SaveChangesAsync(CancellationToken.None);
        }

        await using var assertContext = BuildContext();
        var repository = new LiveSessionRepository(assertContext);
        var persistedSession = await repository.GetByIdAsync(liveSession.LiveSessionId, CancellationToken.None);

        persistedSession.Should().NotBeNull();
        persistedSession!.Teams.Should().HaveCount(3);

        var alphaTeam = persistedSession.Teams.Single(t => t.TeamCode.Value == "AAA-01");
        var board = persistedSession.ProjectParticipantTeamBoard(alphaTeam.TeamId, activeAt.AddSeconds(5));

        board.TeamId.Should().Be(alphaTeam.TeamId);
        board.TeamDisplayName.Should().Be("Alpha");
        board.TeamCode.Should().Be("AAA-01");
    }

    [Fact]
    public async Task GetByIdAsync_BoardRead_PersistsTeamScoreAcrossReload()
    {
        await using var resetContext = BuildContext();
        await ResetDatabaseAsync(resetContext);

        var activeAt = new DateTimeOffset(2026, 7, 11, 12, 0, 0, TimeSpan.Zero);
        var liveSession = CreateActiveTriviaQuestionSessionWithTeams(activeAt, ("Alpha", "AAA-01"));

        var teamId = liveSession.Teams.First().TeamId;

        await using (var seedContext = BuildContext())
        {
            seedContext.LiveSessions.Add(liveSession);
            await seedContext.SaveChangesAsync(CancellationToken.None);
        }

        await using (var actContext = BuildContext())
        {
            var repository = new LiveSessionRepository(actContext);
            var session = await repository.GetByIdAsync(liveSession.LiveSessionId, CancellationToken.None);
            session.Should().NotBeNull();

            session!.RegisterTriviaAnswer(teamId, 1, Guid.NewGuid(), activeAt.AddSeconds(3));
            await repository.UpdateAsync(session, CancellationToken.None);
        }

        await using var assertContext = BuildContext();
        var reloaded = await new LiveSessionRepository(assertContext)
            .GetByIdAsync(liveSession.LiveSessionId, CancellationToken.None);

        reloaded.Should().NotBeNull();
        reloaded!.TriviaAnswerSubmissions.Should().ContainSingle();

        var board = reloaded.ProjectParticipantTeamBoard(teamId, activeAt.AddSeconds(10));
        board.CurrentScore.Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public async Task PersistedSchema_CarriesNoWinnerScoreColumnOnSubstageSnapshot()
    {
        await using var resetContext = BuildContext();
        await ResetDatabaseAsync(resetContext);

        var activeAt = new DateTimeOffset(2026, 7, 11, 12, 0, 0, TimeSpan.Zero);
        var liveSession = CreateActiveTreasureHuntSession(activeAt, ("Alpha", "AAA-01"));

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

        columns.Should().NotContain(name => name.Contains("winner_score", StringComparison.OrdinalIgnoreCase),
            "winner_score must not exist — per-target score refactor (DES-86) removed it");
    }

    [Fact]
    public async Task GetByIdAsync_BoardRead_TimerSnapshotSurvivesReloadForTriviaSession()
    {
        await using var resetContext = BuildContext();
        await ResetDatabaseAsync(resetContext);

        var activeAt = new DateTimeOffset(2026, 7, 11, 12, 0, 0, TimeSpan.Zero);
        var liveSession = CreateActiveTriviaQuestionSessionWithTeams(activeAt, ("Alpha", "AAA-01"));

        await using (var seedContext = BuildContext())
        {
            seedContext.LiveSessions.Add(liveSession);
            await seedContext.SaveChangesAsync(CancellationToken.None);
        }

        await using var assertContext = BuildContext();
        var repository = new LiveSessionRepository(assertContext);
        var persistedSession = await repository.GetByIdAsync(liveSession.LiveSessionId, CancellationToken.None);

        persistedSession.Should().NotBeNull();

        var teamId = persistedSession!.Teams.First().TeamId;
        var board = persistedSession.ProjectParticipantTeamBoard(teamId, activeAt.AddSeconds(10));

        board.TimerSnapshot.Should().NotBeNull();
        board.TimerSnapshot.TotalDuration.Should().Be(TimeSpan.FromSeconds(30));
        board.TimerSnapshot.RemainingDuration.Should().BeLessThanOrEqualTo(TimeSpan.FromSeconds(30));
    }

    private ApplicationDbContext BuildContext() => _contextFactory.Create();

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

    private static async Task ResetDatabaseAsync(ApplicationDbContext context)
    {
        await context.LiveSessions.ExecuteDeleteAsync();
    }

    private static LiveSession CreateActiveTreasureHuntSession(
        DateTimeOffset activeAt,
        params (string DisplayName, string TeamCode)[] teams)
    {
        var sourceMissionId = Guid.NewGuid();
        var treasureHuntSubstage = SubstageSnapshot.CreateTreasureHunt("Treasure Hunt", 1);

        var runtimeSnapshot = MissionRuntimeSnapshot.Create(
            sourceMissionId,
            "Mission Runtime",
            MaximumTime.Create(45),
            [StageSnapshot.Create("Stage One", 1, [treasureHuntSubstage])],
            [
                TargetSnapshot.Create(
                    treasureHuntSubstage.SubstageSnapshotId,
                    "Target Alpha",
                    "QR-ALPHA",
                    1,
                    true,
                    100,
                    "Look under the stairs",
                    "AfterPreviousTarget"),
                TargetSnapshot.Create(
                    treasureHuntSubstage.SubstageSnapshotId,
                    "Target Bravo",
                    "QR-BRAVO",
                    2,
                    true,
                    50,
                    "Check the garden",
                    "AfterPreviousTarget"),
            ],
            []);

        var liveSession = LiveSession.Create(
            SessionSource.Create(sourceMissionId),
            $"SES-{Guid.NewGuid():N}"[..12],
            "Treasure Hunt Board",
            45,
            activeAt.AddMinutes(-10),
            runtimeSnapshot);

        foreach (var team in teams)
        {
            liveSession.AssociateTeam(Guid.NewGuid(), team.DisplayName, team.TeamCode, 4);
        }

        var transitionPolicy = new SessionStateTransitionPolicy();
        liveSession.MoveTo(SessionState.Preparing, activeAt.AddMinutes(-1), transitionPolicy);
        liveSession.MoveTo(SessionState.Active, activeAt, transitionPolicy);

        return liveSession;
    }

    private static LiveSession CreateActiveTriviaQuestionSessionWithTeams(
        DateTimeOffset activeAt,
        params (string DisplayName, string TeamCode)[] teams)
    {
        var sourceMissionId = Guid.NewGuid();
        var triviaSubstage = SubstageSnapshot.CreateTrivia("Trivia Round", 1);

        var runtimeSnapshot = MissionRuntimeSnapshot.Create(
            sourceMissionId,
            "Mission Runtime",
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

        var liveSession = LiveSession.Create(
            SessionSource.Create(sourceMissionId),
            $"SES-{Guid.NewGuid():N}"[..12],
            "Trivia Board",
            20,
            activeAt.AddMinutes(-10),
            runtimeSnapshot);

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
}

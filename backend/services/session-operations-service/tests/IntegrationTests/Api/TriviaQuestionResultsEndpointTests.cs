using umbral_backend.Application.Sessions.Common;
using umbral_backend.Domain.Entities;
using umbral_backend.Domain.Enums;
using umbral_backend.Domain.Services;
using umbral_backend.Domain.ValueObjects;
using umbral_backend.Infrastructure.Persistence;

namespace umbral_backend.Infrastructure.IntegrationTests.Api;

[Collection(PostgreSqlCollection.Name)]
public sealed class TriviaQuestionResultsEndpointTests : IAsyncLifetime
{
    private const int OperatorUserId = 42;
    private const string OperatorExternalIdentityId = "kc-operator-42";

    private readonly PostgreSqlFixture _fixture;
    private SessionOperationsApiWebApplicationFactory _factory = null!;
    private HttpClient _client = null!;

    public TriviaQuestionResultsEndpointTests(PostgreSqlFixture fixture)
    {
        _fixture = fixture;
    }

    public async Task InitializeAsync()
    {
        _factory = new SessionOperationsApiWebApplicationFactory(_fixture.ConnectionString);
        _client = _factory.CreateClient();
        await _factory.ResetDatabaseAsync();
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
    }

    [Fact]
    public async Task ClosedQuestionReads_ReturnTeamResultAndOperatorReview()
    {
        var seeded = await SeedClosedQuestionAsync();

        await AssertClosedQuestionReadsAsync(seeded);
    }

    // Regression guard: while a just-closed question is inside its reveal window the next question has
    // not activated yet (ActiveQuestionIndex is null). The reads must still resolve — this is exactly
    // when the mobile reveal + operator review fire on the QuestionClosed push.
    [Fact]
    public async Task ClosedQuestionReads_DuringRevealWindow_ReturnTeamResultAndOperatorReview()
    {
        var seeded = await SeedClosedQuestionAsync(leaveRevealWindowOpen: true);

        await AssertClosedQuestionReadsAsync(seeded);
    }

    private async Task AssertClosedQuestionReadsAsync(SeededSession seeded)
    {
        AddTrustedHeaders(seeded.ParticipantExternalIdentityId.ToString(), "Participant", "alice@example.com");
        var participantResponse = await _client.GetAsync(
            $"/api/sessions/{seeded.LiveSessionId:D}/trivia/questions/1/my-result");

        participantResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var teamResult = await participantResponse.Content.ReadFromJsonAsync<TriviaTeamQuestionResultDto>();
        teamResult.Should().NotBeNull();
        teamResult!.SelectedOptionSequenceOrder.Should().Be(2);
        teamResult.IsCorrect.Should().BeFalse();
        teamResult.ScoreValue.Should().Be(0);
        teamResult.CorrectOptionSequenceOrder.Should().Be(1);
        teamResult.Explanation.Should().Be("Mercury is the closest planet.");

        _factory.AuthenticatedActorProfileAccessClient.CurrentActor = new AuthenticatedActorProfileLookupDto(
            OperatorUserId,
            OperatorExternalIdentityId,
            "Operator",
            true);
        AddTrustedHeaders(OperatorExternalIdentityId, "Operator", "operator@example.com");
        var operatorResponse = await _client.GetAsync(
            $"/api/sessions/{seeded.LiveSessionId:D}/trivia/questions/1/answer-review");

        operatorResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var review = await operatorResponse.Content.ReadFromJsonAsync<TriviaAnswerReviewDto>();
        review.Should().NotBeNull();
        review!.QuestionSequenceOrder.Should().Be(1);
        review.Teams.Should().HaveCount(2);
        review.Teams.Single(team => team.TeamId == seeded.AnsweredTeamId)
            .SelectedOptionSequenceOrder.Should().Be(2);
        review.Teams.Single(team => team.TeamId == seeded.UnansweredTeamId)
            .SelectedOptionSequenceOrder.Should().BeNull();
    }

    private async Task<SeededSession> SeedClosedQuestionAsync(bool leaveRevealWindowOpen = false)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var now = DateTimeOffset.UtcNow;
        var snapshot = CreateTriviaSnapshot();
        var session = LiveSession.Create(
            SessionSource.Create(snapshot.SourceMissionId),
            $"RES-{Guid.NewGuid():N}"[..12],
            "Trivia Results",
            45,
            now.AddMinutes(-5),
            snapshot);
        var answeredTeam = session.AssociateTeam(Guid.NewGuid(), "Blue", "BLU-01", 4);
        var unansweredTeam = session.AssociateTeam(Guid.NewGuid(), "Red", "RED-01", 4);
        session.AssignOperator(OperatorUserId, now.AddMinutes(-5));
        var policy = new SessionStateTransitionPolicy();
        session.MoveTo(SessionState.Preparing, now.AddMinutes(-4), policy);
        var participantExternalIdentityId = Guid.NewGuid();
        var admission = session.AdmitParticipant(
            participantExternalIdentityId,
            "Alice",
            answeredTeam.TeamId,
            now.AddMinutes(-3),
            new JoinPolicy());
        session.MoveTo(SessionState.Active, now.AddMinutes(-2), policy);
        session.ActivateQuestion(0, now.AddMinutes(-1));
        session.RegisterTriviaAnswer(
            answeredTeam.TeamId,
            selectedOptionSequenceOrder: 2,
            admission.Participant.SessionParticipantId,
            now.AddSeconds(-50));

        // Persist while the question is still active, then close as an UPDATE. A just-closed question
        // has a null ActiveQuestionIndex, and a fresh INSERT of a null nullable int writes 0 (it only
        // round-trips as null on an update) — so a single insert of the already-closed graph would
        // mis-seed the state. This two-step mirrors production, where the question is persisted-active
        // before it closes.
        dbContext.LiveSessions.Add(session);
        await dbContext.SaveChangesAsync();

        if (leaveRevealWindowOpen)
        {
            // Close into the reveal window and stop — the next question stays deferred, mirroring the
            // ~5s dwell during which the clients read on the QuestionClosed push.
            var nextQuestionIndex = new SequentialQuestionActivationStrategy().Next(session);
            session.CloseActiveQuestionForReveal(
                now,
                TriviaRoundOrchestratorFacade.QuestionRevealDuration,
                nextQuestionIndex);
        }
        else
        {
            session.CloseActiveQuestion(now.AddSeconds(-30));
            session.ActivateQuestion(1, now.AddSeconds(-30));
        }

        await dbContext.SaveChangesAsync();

        return new SeededSession(
            session.LiveSessionId,
            participantExternalIdentityId,
            answeredTeam.TeamId,
            unansweredTeam.TeamId);
    }

    private static MissionRuntimeSnapshot CreateTriviaSnapshot()
    {
        var substage = SubstageSnapshot.CreateTrivia("Trivia Round", 1);
        return MissionRuntimeSnapshot.Create(
            Guid.NewGuid(),
            "Trivia Results Mission",
            MaximumTime.Create(45),
            [StageSnapshot.Create("Stage One", 1, [substage])],
            [],
            [
                TriviaQuestionSnapshot.Create(
                    substage.SubstageSnapshotId,
                    "Closest planet?",
                    1,
                    100,
                    300,
                    "Mercury is the closest planet.",
                    [
                        TriviaOptionSnapshot.Create("Mercury", 1, true),
                        TriviaOptionSnapshot.Create("Venus", 2, false)
                    ]),
                TriviaQuestionSnapshot.Create(
                    substage.SubstageSnapshotId,
                    "Largest planet?",
                    2,
                    100,
                    300,
                    null,
                    [
                        TriviaOptionSnapshot.Create("Jupiter", 1, true),
                        TriviaOptionSnapshot.Create("Mars", 2, false)
                    ])
            ]);
    }

    private void AddTrustedHeaders(string userId, string role, string email)
    {
        _client.DefaultRequestHeaders.Remove("X-User-Id");
        _client.DefaultRequestHeaders.Remove("X-User-Role");
        _client.DefaultRequestHeaders.Remove("X-User-Email");
        _client.DefaultRequestHeaders.Add("X-User-Id", userId);
        _client.DefaultRequestHeaders.Add("X-User-Role", role);
        _client.DefaultRequestHeaders.Add("X-User-Email", email);
    }

    private sealed record SeededSession(
        Guid LiveSessionId,
        Guid ParticipantExternalIdentityId,
        Guid AnsweredTeamId,
        Guid UnansweredTeamId);
}

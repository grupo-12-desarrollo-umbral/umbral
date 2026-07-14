using umbral_backend.Application.Common.Exceptions;
using umbral_backend.Application.Sessions.Common.EvidenceValidation;
using umbral_backend.Application.Sessions.Common.EvidenceValidation.Validators;
using umbral_backend.Application.UnitTests.TestData;
using umbral_backend.Domain.Entities;
using umbral_backend.Domain.Enums;
using umbral_backend.Domain.Services;

namespace umbral_backend.Application.UnitTests.Sessions.Facades;

public sealed class EvidenceValidationLinkTests
{
    private static readonly DateTimeOffset ActivatedAt =
        new(2026, 6, 3, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ActiveSubstageBindingLink_WhenSubmissionTargetsDifferentSubstage_ReturnsBindingReason()
    {
        var session = LiveSessionTestFactory.CreateActiveTriviaWithActiveQuestion(out var teamId, out var activeSubstageId);
        var submission = CreateSubmission(session, teamId, Guid.NewGuid(), EvidenceSubmissionType.TriviaAnswer);

        var act = () => new ActiveSubstageBindingLink().ValidateAsync(
            CreateContext(submission, session, teamId, activeSubstageId), CancellationToken.None);

        var exception = await act.Should().ThrowAsync<EvidenceContextRejectedException>();
        exception.Which.Reason.Should().Be(EvidenceRejectionReason.SubstageBindingMismatch);
    }

    [Fact]
    public async Task ActiveSubstageBindingLink_WhenSessionNoLongerPointsAtDeclaredSubstage_ReturnsBindingReason()
    {
        var session = LiveSessionTestFactory.CreateActiveTriviaWithActiveQuestion(out var teamId, out _);
        var declaredSubstageId = Guid.NewGuid();
        var submission = CreateSubmission(session, teamId, declaredSubstageId, EvidenceSubmissionType.TriviaAnswer);

        // The declared active substage no longer matches the session pointer, so the first predicate
        // of the OR short-circuits and rejects before the submission binding is even inspected.
        var act = () => new ActiveSubstageBindingLink().ValidateAsync(
            CreateContext(submission, session, teamId, declaredSubstageId), CancellationToken.None);

        var exception = await act.Should().ThrowAsync<EvidenceContextRejectedException>();
        exception.Which.Reason.Should().Be(EvidenceRejectionReason.SubstageBindingMismatch);
    }

    [Fact]
    public async Task ActiveSubstageBindingLink_WhenSubmissionTargetsActiveSubstage_Passes()
    {
        var session = LiveSessionTestFactory.CreateActiveTriviaWithActiveQuestion(out var teamId, out var activeSubstageId);
        var submission = CreateSubmission(session, teamId, activeSubstageId, EvidenceSubmissionType.TriviaAnswer);

        var act = () => new ActiveSubstageBindingLink().ValidateAsync(
            CreateContext(submission, session, teamId, activeSubstageId), CancellationToken.None);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task SubmissionWindowLink_WhenTreasureHuntSubstageWindowExpired_ReturnsWindowReason()
    {
        var session = CreateActiveTreasureHunt(out var teamId, out var activeSubstageId);
        var afterWindow = ActivatedAt.AddMinutes(2);
        var submission = new TestEvidenceSubmission(session.LiveSessionId, teamId, activeSubstageId, afterWindow);
        var context = new EvidenceValidationContext(
            submission, session, teamId, activeSubstageId, origin: null, afterWindow);

        var act = () => new SubmissionWindowLink().ValidateAsync(context, CancellationToken.None);

        var exception = await act.Should().ThrowAsync<EvidenceContextRejectedException>();
        exception.Which.Reason.Should().Be(EvidenceRejectionReason.OutsideSubmissionWindow);
    }

    [Fact]
    public async Task SubmissionWindowLink_WhenLateTriviaAnswer_DoesNotReplaceFormSpecificWindowRule()
    {
        var session = LiveSessionTestFactory.CreateActiveTriviaWithActiveQuestion(out var teamId, out var activeSubstageId);
        var afterQuestionWindow = LiveSessionTestFactory.TriviaQuestionActivatedAt.AddMinutes(2);
        var submission = new TestEvidenceSubmission(
            session.LiveSessionId, teamId, activeSubstageId, afterQuestionWindow, EvidenceSubmissionType.TriviaAnswer);

        var act = () => new SubmissionWindowLink().ValidateAsync(
            new EvidenceValidationContext(
                submission, session, teamId, activeSubstageId, origin: null, afterQuestionWindow),
            CancellationToken.None);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task SubmissionOriginLink_WhenSuppliedOriginDoesNotMatchForm_ReturnsOriginReason()
    {
        var act = () => new SubmissionOriginLink().ValidateAsync(
            CreateTriviaContext(nameof(EvidenceSubmissionType.TreasureHuntQrScan)), CancellationToken.None);

        var exception = await act.Should().ThrowAsync<EvidenceContextRejectedException>();
        exception.Which.Reason.Should().Be(EvidenceRejectionReason.UnauthorizedOrigin);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("TriviaAnswer")]
    [InlineData("triviaanswer")]
    public async Task SubmissionOriginLink_WhenOriginAbsentOrMatchesForm_Passes(string? origin)
    {
        var act = () => new SubmissionOriginLink().ValidateAsync(
            CreateTriviaContext(origin), CancellationToken.None);

        await act.Should().NotThrowAsync();
    }

    private static EvidenceValidationContext CreateTriviaContext(string? origin)
    {
        var session = LiveSessionTestFactory.CreateActiveTriviaWithActiveQuestion(out var teamId, out var activeSubstageId);
        var submission = CreateSubmission(session, teamId, activeSubstageId, EvidenceSubmissionType.TriviaAnswer);
        return CreateContext(submission, session, teamId, activeSubstageId, origin);
    }

    private static TestEvidenceSubmission CreateSubmission(
        LiveSession session, Guid teamId, Guid activeSubstageId, EvidenceSubmissionType type)
    {
        return new TestEvidenceSubmission(session.LiveSessionId, teamId, activeSubstageId, ActivatedAt, type);
    }

    private static EvidenceValidationContext CreateContext(
        TestEvidenceSubmission submission,
        LiveSession session,
        Guid teamId,
        Guid activeSubstageId,
        string? origin = null)
    {
        return new EvidenceValidationContext(
            submission, session, teamId, activeSubstageId, origin, submission.SubmittedAt);
    }

    private static LiveSession CreateActiveTreasureHunt(out Guid teamId, out Guid activeSubstageId)
    {
        var session = LiveSessionTestFactory.CreateScheduledTreasureHunt(maximumTimeMinutes: 1);
        var team = session.AssociateTeam(Guid.NewGuid(), "Alpha", "A-01", 4);
        var policy = new SessionStateTransitionPolicy();
        session.MoveTo(SessionState.Preparing, ActivatedAt.AddMinutes(-1), policy);
        session.MoveTo(SessionState.Active, ActivatedAt, policy);

        teamId = team.TeamId;
        activeSubstageId = session.ActiveSubstageId!.Value;
        return session;
    }
}

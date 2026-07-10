using umbral_backend.Application.Sessions.Common.TriviaAnswerValidation;
using umbral_backend.Application.Sessions.Common.TriviaAnswerValidation.Validators;
using umbral_backend.Domain.Entities;
using umbral_backend.Domain.Exceptions;

namespace umbral_backend.Application.UnitTests.Sessions.Common.TriviaAnswerValidation;

// Drives the individual link branches the chain-order proof does not isolate: DuplicateTriviaAnswerLink's
// unknown-team short-circuit and duplicate detection, and ActiveTriviaQuestionLink's no-active-substage arm.
public sealed class TriviaAnswerLinkBranchTests
{
    private static TriviaAnswerValidationContext Context(
        LiveSession session, Guid teamId, Guid substageId, int order = 1, DateTimeOffset? submittedAt = null) =>
        new(session, teamId, substageId, order, token: null,
            submittedAt ?? LiveSessionTestFactory.TriviaQuestionActivatedAt.AddSeconds(5));

    [Fact]
    public async Task Duplicate_UnknownTeam_IsLeftToDomain()
    {
        var session = LiveSessionTestFactory.CreateActiveTriviaWithActiveQuestion(out _, out var substageId);
        // A team id that resolves to no team → the link returns without inspecting answer state.
        var context = Context(session, Guid.NewGuid(), substageId);

        await new DuplicateTriviaAnswerLink().ValidateAsync(context, CancellationToken.None);
    }

    [Fact]
    public async Task Duplicate_FirstAnswer_Passes()
    {
        var session = LiveSessionTestFactory.CreateActiveTriviaWithActiveQuestion(out var teamId, out var substageId);
        var context = Context(session, teamId, substageId);

        await new DuplicateTriviaAnswerLink().ValidateAsync(context, CancellationToken.None);
    }

    [Fact]
    public async Task Duplicate_SecondAnswer_Throws()
    {
        var session = LiveSessionTestFactory.CreateActiveTriviaWithActiveQuestion(out var teamId, out var substageId);
        session.RegisterTriviaAnswer(teamId, selectedOptionSequenceOrder: 1, submittedByParticipantId: Guid.NewGuid(),
            LiveSessionTestFactory.TriviaQuestionActivatedAt.AddSeconds(3));
        var context = Context(session, teamId, substageId);

        await FluentActions.Awaiting(() => new DuplicateTriviaAnswerLink().ValidateAsync(context, CancellationToken.None))
            .Should().ThrowAsync<DuplicateTriviaAnswerException>();
    }

    [Fact]
    public async Task Duplicate_TeamResolvedByReferenceId_Passes()
    {
        // Addressing the team by its reference id (not its runtime TeamId) exercises the second arm of
        // the team-resolution OR; with no prior submission the link passes.
        var session = LiveSessionTestFactory.CreateActiveTriviaWithActiveQuestion(out _, out var substageId);
        var referenceTeamId = session.Teams.Single().ReferenceTeamId!.Value;
        var context = Context(session, referenceTeamId, substageId);

        await new DuplicateTriviaAnswerLink().ValidateAsync(context, CancellationToken.None);
    }

    [Fact]
    public async Task Duplicate_SameTeamDifferentQuestion_Passes()
    {
        // A recorded answer for question 1 must not block a different question: the QuestionSequenceOrder
        // arm of the duplicate predicate evaluates false, so the link passes.
        var session = LiveSessionTestFactory.CreateActiveTriviaWithActiveQuestion(out var teamId, out var substageId);
        session.RegisterTriviaAnswer(teamId, selectedOptionSequenceOrder: 1, submittedByParticipantId: Guid.NewGuid(),
            LiveSessionTestFactory.TriviaQuestionActivatedAt.AddSeconds(3));
        var context = Context(session, teamId, substageId, order: 2);

        await new DuplicateTriviaAnswerLink().ValidateAsync(context, CancellationToken.None);
    }

    [Fact]
    public async Task Duplicate_SameTeamAndQuestionOnDifferentSubstage_Passes()
    {
        var session = LiveSessionTestFactory.CreateActiveTriviaWithActiveQuestion(out var teamId, out var substageId);
        session.RegisterTriviaAnswer(teamId, selectedOptionSequenceOrder: 1, submittedByParticipantId: Guid.NewGuid(),
            LiveSessionTestFactory.TriviaQuestionActivatedAt.AddSeconds(3));
        var differentSubstageId = Guid.NewGuid();
        differentSubstageId.Should().NotBe(substageId);
        var context = Context(session, teamId, differentSubstageId);

        await new DuplicateTriviaAnswerLink().ValidateAsync(context, CancellationToken.None);
    }

    [Fact]
    public async Task ActiveQuestion_NoActiveSubstage_Throws()
    {
        // A Scheduled session has no active substage → the first gate rejects before question lookup.
        var session = LiveSessionTestFactory.CreateScheduledTrivia();
        var context = Context(session, Guid.NewGuid(), Guid.NewGuid());

        await FluentActions.Awaiting(() => new ActiveTriviaQuestionLink().ValidateAsync(context, CancellationToken.None))
            .Should().ThrowAsync<TriviaAnswerRequiresTriviaSubstageException>();
    }
}

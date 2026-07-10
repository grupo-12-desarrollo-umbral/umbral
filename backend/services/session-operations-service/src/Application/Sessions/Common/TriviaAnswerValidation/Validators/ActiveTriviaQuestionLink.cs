using umbral_backend.Domain.Enums;
using umbral_backend.Domain.Exceptions;
using umbral_backend.Domain.ValueObjects;

namespace umbral_backend.Application.Sessions.Common.TriviaAnswerValidation.Validators;

/// <summary>
/// Link 2 of 4: an answer is only meaningful against the one synchronized active trivia question
/// shared by all teams (HU-33A seam). Rejects, in the same order the domain skeleton does, a
/// non-trivia active substage, then the absence of an active question, then a declared question key
/// that is not the currently active one (answering a stale/advanced question). Read-only inspection
/// of the aggregate — the domain skeleton remains the final authority.
/// </summary>
public sealed class ActiveTriviaQuestionLink : TriviaAnswerValidationLink
{
    protected override Task CheckAsync(TriviaAnswerValidationContext context, CancellationToken cancellationToken)
    {
        var session = context.Session;

        var activeSubstage = session.ActiveSubstageId is null
            ? null
            : OrderedSubstages(session).SingleOrDefault(substage => substage.SubstageSnapshotId == session.ActiveSubstageId.Value);

        if (activeSubstage is null || activeSubstage.PlayMode != SubstagePlayMode.Trivia)
        {
            throw new TriviaAnswerRequiresTriviaSubstageException();
        }

        if (session.ActiveQuestionIndex is null)
        {
            throw new TriviaAnswerRequiresActiveQuestionException();
        }

        var activeQuestion = OrderedTriviaQuestions(session, activeSubstage.SubstageSnapshotId)
            .ElementAt(session.ActiveQuestionIndex.Value);

        var declaresActiveQuestion =
            context.TriviaSubstageSnapshotId == activeQuestion.SubstageSnapshotId &&
            context.QuestionSequenceOrder == activeQuestion.SequenceOrder;

        if (!declaresActiveQuestion)
        {
            // The declared question is not the live one (already advanced/closed) — treat as a stale
            // attempt against a non-active question, the same reason the runtime uses for "no active".
            throw new TriviaAnswerRequiresActiveQuestionException();
        }

        return Task.CompletedTask;
    }

    private static IEnumerable<SubstageSnapshot> OrderedSubstages(Domain.Entities.LiveSession session)
    {
        return session.MissionRuntimeSnapshot.StageSnapshots
            .OrderBy(stage => stage.SequenceOrder)
            .SelectMany(stage => stage.SubstageSnapshots.OrderBy(substage => substage.SequenceOrder));
    }

    private static IEnumerable<TriviaQuestionSnapshot> OrderedTriviaQuestions(Domain.Entities.LiveSession session, Guid substageSnapshotId)
    {
        return session.MissionRuntimeSnapshot.TriviaQuestionSnapshots
            .Where(question => question.SubstageSnapshotId == substageSnapshotId)
            .OrderBy(question => question.SequenceOrder);
    }
}

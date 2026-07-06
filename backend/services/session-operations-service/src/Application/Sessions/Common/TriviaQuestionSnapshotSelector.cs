using umbral_backend.Domain.Entities;
using umbral_backend.Domain.ValueObjects;

namespace umbral_backend.Application.Sessions.Common;

public static class TriviaQuestionSnapshotSelector
{
    // Snapshots and options are stored unordered; both the broadcast path and the timer-snapshot
    // path must read them by SequenceOrder, not storage order. QuestionIndex is a position within
    // the ACTIVE substage's questions (ADR-0005) — scope to ActiveSubstageId, matching the domain's
    // GetOrderedTriviaQuestions and the activation strategy.
    public static (TriviaQuestionSnapshot Question, string[] Options) GetOrderedTriviaQuestion(
        LiveSession session,
        int questionIndex)
    {
        var question = session.MissionRuntimeSnapshot.TriviaQuestionSnapshots
            .Where(snapshot => snapshot.SubstageSnapshotId == session.ActiveSubstageId)
            .OrderBy(snapshot => snapshot.SequenceOrder)
            .ElementAt(questionIndex);
        var options = question.Options
            .OrderBy(option => option.SequenceOrder)
            .Select(option => option.OptionText)
            .ToArray();

        return (question, options);
    }
}

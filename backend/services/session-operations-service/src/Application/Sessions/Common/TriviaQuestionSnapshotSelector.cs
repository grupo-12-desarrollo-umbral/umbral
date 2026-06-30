using umbral_backend.Domain.Entities;
using umbral_backend.Domain.ValueObjects;

namespace umbral_backend.Application.Sessions.Common;

public static class TriviaQuestionSnapshotSelector
{
    // Snapshots and options are stored unordered; both the broadcast path and the
    // timer-snapshot path must read them by SequenceOrder, not storage order.
    public static (TriviaQuestionSnapshot Question, string[] Options) GetOrderedTriviaQuestion(
        LiveSession session,
        int questionIndex)
    {
        var question = session.MissionRuntimeSnapshot.TriviaQuestionSnapshots
            .OrderBy(snapshot => snapshot.SequenceOrder)
            .ElementAt(questionIndex);
        var options = question.Options
            .OrderBy(option => option.SequenceOrder)
            .Select(option => option.OptionText)
            .ToArray();

        return (question, options);
    }
}

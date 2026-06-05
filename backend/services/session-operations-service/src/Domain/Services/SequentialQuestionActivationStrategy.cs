using umbral_backend.Domain.Entities;

namespace umbral_backend.Domain.Services;

public sealed class SequentialQuestionActivationStrategy : IQuestionActivationStrategy
{
    public int? Next(LiveSession session)
    {
        ArgumentNullException.ThrowIfNull(session);

        if (session.TriviaSnapshot is null)
        {
            return null;
        }

        var orderedQuestions = session.TriviaSnapshot.Questions
            .Select((Question, Index) => new QuestionOrder(Index, Question.SequenceOrder))
            .OrderBy(question => question.SequenceOrder)
            .ThenBy(question => question.Index)
            .ToArray();

        if (orderedQuestions.Length == 0)
        {
            return null;
        }

        if (session.ActiveQuestionIndex is null)
        {
            return orderedQuestions[0].Index;
        }

        var currentPosition = Array.FindIndex(
            orderedQuestions,
            question => question.Index == session.ActiveQuestionIndex.Value);

        if (currentPosition < 0 || currentPosition == orderedQuestions.Length - 1)
        {
            return null;
        }

        return orderedQuestions[currentPosition + 1].Index;
    }

    private readonly record struct QuestionOrder(int Index, int SequenceOrder);
}

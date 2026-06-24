using umbral_backend.Domain.Entities;

namespace umbral_backend.Application.Trivias.Common.Authoring;

/// <summary>
/// Maps authoring inputs to domain entities. Shared by the quiz- and question-authoring
/// handlers so the create/update slices reuse one mapping rather than duplicating it.
/// </summary>
internal static class TriviaAuthoringInputMapper
{
    public static IReadOnlyCollection<TriviaQuestion> MapQuestions(IReadOnlyCollection<TriviaQuestionInput> questions)
    {
        return questions
            .OrderBy(question => question.SequenceOrder)
            .Select(question => TriviaQuestion.Create(
                question.Prompt,
                question.SequenceOrder,
                question.ScoreValue,
                question.TimeLimitSeconds,
                question.Explanation,
                MapOptions(question.Options),
                question.IsActive))
            .ToArray();
    }

    public static IReadOnlyCollection<TriviaOption> MapOptions(IReadOnlyCollection<TriviaOptionInput> options)
    {
        return options
            .OrderBy(option => option.SequenceOrder)
            .Select(option => TriviaOption.Create(option.OptionText, option.SequenceOrder, option.IsCorrect))
            .ToArray();
    }
}

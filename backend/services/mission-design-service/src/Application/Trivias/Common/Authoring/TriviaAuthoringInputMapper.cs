using umbral_backend.Domain.Entities;

namespace umbral_backend.Application.Trivias.Common.Authoring;

/// <summary>
/// Maps authoring inputs to domain entities. Shared by the quiz- and question-authoring
/// handlers so the create/update slices reuse one mapping rather than duplicating it.
/// </summary>
internal static class TriviaAuthoringInputMapper
{
    // Null flows through to the domain, which reads it as "no questions supplied" — create
    // starts empty, update keeps what the quiz already has.
    public static IReadOnlyCollection<TriviaQuestion>? MapQuestions(IReadOnlyCollection<TriviaQuestionInput>? questions)
    {
        return questions?
            .Select(question => TriviaQuestion.Create(
                question.Prompt,
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

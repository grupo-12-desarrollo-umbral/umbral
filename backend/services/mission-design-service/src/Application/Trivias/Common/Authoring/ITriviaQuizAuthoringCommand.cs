namespace umbral_backend.Application.Trivias.Common.Authoring;

public interface ITriviaQuizAuthoringCommand
{
    string Title { get; }

    string Description { get; }

    IReadOnlyCollection<TriviaQuestionInput> Questions { get; }
}

public sealed record TriviaQuestionInput(
    string Prompt,
    int SequenceOrder,
    bool IsActive,
    IReadOnlyCollection<TriviaOptionInput> Options,
    int? ScoreValue = null,
    int? TimeLimitSeconds = null,
    string? Explanation = null);

public sealed record TriviaOptionInput(
    string OptionText,
    int SequenceOrder,
    bool IsCorrect);

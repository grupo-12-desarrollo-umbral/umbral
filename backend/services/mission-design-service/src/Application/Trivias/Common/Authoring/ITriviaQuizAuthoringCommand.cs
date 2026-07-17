namespace umbral_backend.Application.Trivias.Common.Authoring;

public interface ITriviaQuizAuthoringCommand
{
    string Title { get; }

    string Description { get; }

    // Null means "questions were not part of this request": the update slice leaves the
    // existing questions untouched. An empty collection is an explicit "no questions".
    IReadOnlyCollection<TriviaQuestionInput>? Questions { get; }
}

public sealed record TriviaQuestionInput(
    string Prompt,
    bool IsActive,
    IReadOnlyCollection<TriviaOptionInput> Options,
    int? ScoreValue = null,
    int? TimeLimitSeconds = null,
    string? Explanation = null);

public sealed record TriviaOptionInput(
    string OptionText,
    int SequenceOrder,
    bool IsCorrect);

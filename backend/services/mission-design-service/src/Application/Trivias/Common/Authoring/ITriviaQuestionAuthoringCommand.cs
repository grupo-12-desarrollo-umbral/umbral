using umbral_backend.Application.Trivias.DTOs;

namespace umbral_backend.Application.Trivias.Common.Authoring;

public interface ITriviaQuestionAuthoringCommand : IRequest<TriviaQuizDto>
{
    int TriviaQuizId { get; }

    string Prompt { get; }

    int SequenceOrder { get; }

    int ScoreValue { get; }

    int TimeLimitSeconds { get; }

    string? Explanation { get; }

    bool IsActive { get; }

    IReadOnlyCollection<TriviaOptionInput> Options { get; }
}

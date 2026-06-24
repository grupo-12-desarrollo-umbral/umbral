using umbral_backend.Application.Common.Security;
using umbral_backend.Application.Trivias.Common.Authoring;
using umbral_backend.Application.Trivias.Common;
using umbral_backend.Domain.Constants;

namespace umbral_backend.Application.Trivias.Commands.UpdateTriviaQuestion;

[Authorize(Roles = Roles.Administrator)]
public sealed record UpdateTriviaQuestionCommand(
    int TriviaQuizId,
    int QuestionId,
    string Prompt,
    int SequenceOrder,
    int ScoreValue,
    int TimeLimitSeconds,
    string? Explanation,
    bool IsActive,
    IReadOnlyCollection<TriviaOptionInput> Options) : ITriviaQuestionAuthoringCommand;

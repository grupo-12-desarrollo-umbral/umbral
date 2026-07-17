using umbral_backend.Application.Common.Security;
using umbral_backend.Application.Trivias.Common.Authoring;
using umbral_backend.Application.Trivias.Common;
using umbral_backend.Domain.Constants;

namespace umbral_backend.Application.Trivias.Commands.UpdateTriviaQuiz;

[Authorize(Roles = Roles.Operator)]
public sealed record UpdateTriviaQuizCommand(
    int Id,
    string Title,
    string Description,
    IReadOnlyCollection<TriviaQuestionInput>? Questions) : IRequest<TriviaQuizDto>, ITriviaQuizAuthoringCommand;

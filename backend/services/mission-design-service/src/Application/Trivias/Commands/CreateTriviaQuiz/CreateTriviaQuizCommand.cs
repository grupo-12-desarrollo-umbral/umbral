using umbral_backend.Application.Common.Security;
using umbral_backend.Application.Trivias.Common.Authoring;
using umbral_backend.Application.Trivias.DTOs;
using umbral_backend.Domain.Constants;

namespace umbral_backend.Application.Trivias.Commands.CreateTriviaQuiz;

[Authorize(Roles = Roles.Administrator)]
public sealed record CreateTriviaQuizCommand(
    string Title,
    string Description,
    IReadOnlyCollection<TriviaQuestionInput> Questions) : IRequest<TriviaQuizDto>, ITriviaQuizAuthoringCommand;

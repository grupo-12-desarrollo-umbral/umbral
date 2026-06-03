using umbral_backend.Application.Common.Security;
using umbral_backend.Application.Trivias.Common.Reuse;
using umbral_backend.Application.Trivias.DTOs;
using umbral_backend.Domain.Constants;

namespace umbral_backend.Application.Trivias.Commands.DuplicateTriviaQuiz;

[Authorize(Roles = Roles.Administrator)]
public sealed record DuplicateTriviaQuizCommand(int Id) : IRequest<TriviaQuizDto>, ITriviaQuizReuseCommand;

using umbral_backend.Application.Common.Security;
using umbral_backend.Application.Trivias.Common.Reuse;
using umbral_backend.Application.Trivias.DTOs;
using umbral_backend.Domain.Constants;

namespace umbral_backend.Application.Trivias.Commands.RetireTriviaQuiz;

[Authorize(Roles = Roles.Administrator)]
public sealed record RetireTriviaQuizCommand(int Id) : IRequest<TriviaQuizDto>, ITriviaQuizReuseCommand;

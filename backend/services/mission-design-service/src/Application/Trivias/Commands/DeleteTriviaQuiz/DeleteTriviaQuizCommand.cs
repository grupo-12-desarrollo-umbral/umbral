using umbral_backend.Application.Common.Security;
using umbral_backend.Domain.Constants;

namespace umbral_backend.Application.Trivias.Commands.DeleteTriviaQuiz;

[Authorize(Roles = Roles.Administrator)]
public sealed record DeleteTriviaQuizCommand(int Id) : IRequest;

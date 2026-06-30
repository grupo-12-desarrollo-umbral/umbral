using umbral_backend.Application.Common.Security;
using umbral_backend.Application.Trivias.Common;
using umbral_backend.Domain.Constants;

namespace umbral_backend.Application.Trivias.Commands.PublishTriviaQuiz;

[Authorize(Roles = Roles.Administrator)]
public sealed record PublishTriviaQuizCommand(int Id) : IRequest<TriviaQuizDto>;

using umbral_backend.Application.Common.Security;
using umbral_backend.Application.Trivias.Common;
using umbral_backend.Domain.Constants;

namespace umbral_backend.Application.Trivias.Commands.RemoveTriviaQuestion;

[Authorize(Roles = Roles.Operator)]
public sealed record RemoveTriviaQuestionCommand(
    int TriviaQuizId,
    int QuestionId) : IRequest<TriviaQuizDto>;

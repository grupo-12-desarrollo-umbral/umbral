using umbral_backend.Application.Common.Security;
using umbral_backend.Application.Dtos.Sessions;

namespace umbral_backend.Application.Sessions.Queries.GetTeamTriviaQuestionResult;

[Authorize(Roles = "Participant")]
public sealed record GetTeamTriviaQuestionResultQuery(
    Guid LiveSessionId,
    int QuestionSequenceOrder) : IRequest<TriviaTeamQuestionResultDto>;

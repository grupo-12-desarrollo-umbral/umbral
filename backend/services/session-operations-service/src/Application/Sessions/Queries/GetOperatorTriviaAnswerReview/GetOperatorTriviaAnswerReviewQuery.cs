using umbral_backend.Application.Common.Security;
using umbral_backend.Application.Dtos.Sessions;

namespace umbral_backend.Application.Sessions.Queries.GetOperatorTriviaAnswerReview;

[Authorize(Roles = "Operator")]
public sealed record GetOperatorTriviaAnswerReviewQuery(
    Guid LiveSessionId,
    int QuestionSequenceOrder) : IRequest<TriviaAnswerReviewDto>;

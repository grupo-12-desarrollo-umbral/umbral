using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Application.Dtos.Sessions;
using umbral_backend.Application.Sessions.Common;

namespace umbral_backend.Application.Sessions.Queries.GetOperatorTriviaAnswerReview;

public sealed class GetOperatorTriviaAnswerReviewQueryHandler
    : IRequestHandler<GetOperatorTriviaAnswerReviewQuery, TriviaAnswerReviewDto>
{
    private readonly ISessionAdministrationAccessResolver _accessResolver;

    public GetOperatorTriviaAnswerReviewQueryHandler(ISessionAdministrationAccessResolver accessResolver)
    {
        _accessResolver = accessResolver;
    }

    public async Task<TriviaAnswerReviewDto> Handle(
        GetOperatorTriviaAnswerReviewQuery request,
        CancellationToken cancellationToken)
    {
        var session = await _accessResolver.GetAuthorizedSessionAsync(
            request.LiveSessionId,
            cancellationToken);
        var question = ClosedTriviaQuestionResultReader.GetClosedQuestion(
            session,
            request.QuestionSequenceOrder);

        var teams = session.Teams
            .OrderBy(team => team.TeamCode.Value, StringComparer.Ordinal)
            .Select(team =>
            {
                var answer = session.TriviaAnswerSubmissions.SingleOrDefault(candidate =>
                    candidate.TeamId == team.TeamId &&
                    candidate.ActiveSubstageId == question.SubstageSnapshotId &&
                    candidate.QuestionSequenceOrder == question.SequenceOrder);

                return new TriviaTeamAnswerReviewDto(
                    team.TeamId,
                    team.TeamCode.Value,
                    team.DisplayName,
                    answer?.SelectedOptionSequenceOrder,
                    answer?.IsCorrect,
                    answer?.ScoreValue,
                    answer?.SubmittedAt);
            })
            .ToList();

        return new TriviaAnswerReviewDto(
            session.LiveSessionId,
            question.SequenceOrder,
            teams);
    }
}

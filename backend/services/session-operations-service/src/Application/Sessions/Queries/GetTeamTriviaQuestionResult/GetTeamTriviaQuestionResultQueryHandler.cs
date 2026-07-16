using umbral_backend.Application.Common.Exceptions;
using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Application.Dtos.Sessions;
using umbral_backend.Application.Sessions.Common;
using umbral_backend.Domain.Entities;

namespace umbral_backend.Application.Sessions.Queries.GetTeamTriviaQuestionResult;

public sealed class GetTeamTriviaQuestionResultQueryHandler
    : IRequestHandler<GetTeamTriviaQuestionResultQuery, TriviaTeamQuestionResultDto>
{
    private readonly ILiveSessionRepository _liveSessionRepository;
    private readonly ICurrentUser _currentUser;

    public GetTeamTriviaQuestionResultQueryHandler(
        ILiveSessionRepository liveSessionRepository,
        ICurrentUser currentUser)
    {
        _liveSessionRepository = liveSessionRepository;
        _currentUser = currentUser;
    }

    public async Task<TriviaTeamQuestionResultDto> Handle(
        GetTeamTriviaQuestionResultQuery request,
        CancellationToken cancellationToken)
    {
        var session = await _liveSessionRepository.GetByIdAsync(request.LiveSessionId, cancellationToken)
            ?? throw new NotFoundException(nameof(LiveSession), request.LiveSessionId);

        if (!Guid.TryParse(_currentUser.Id, out var externalIdentityId))
        {
            throw new UnauthorizedAccessException();
        }

        var participant = session.Participants
            .SingleOrDefault(candidate => candidate.ExternalIdentityId == externalIdentityId);
        if (participant is null || participant.IsBlocked || participant.IsRemoved)
        {
            throw new ForbiddenAccessException();
        }

        var team = session.FindTeamForExternalParticipant(externalIdentityId)
            ?? throw new ForbiddenAccessException();
        var question = ClosedTriviaQuestionResultReader.GetClosedQuestion(
            session,
            request.QuestionSequenceOrder);
        var answer = session.TriviaAnswerSubmissions.SingleOrDefault(candidate =>
            candidate.TeamId == team.TeamId &&
            candidate.ActiveSubstageId == question.SubstageSnapshotId &&
            candidate.QuestionSequenceOrder == question.SequenceOrder);

        return new TriviaTeamQuestionResultDto(
            answer?.SelectedOptionSequenceOrder,
            answer?.IsCorrect,
            answer?.ScoreValue,
            question.Options.Single(option => option.IsCorrect).SequenceOrder,
            question.Explanation);
    }
}

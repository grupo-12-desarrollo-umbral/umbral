using umbral_backend.Application.Common.Exceptions;
using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Application.Sessions.Common;
using umbral_backend.Domain.Entities;
using umbral_backend.Domain.Services;

namespace umbral_backend.Application.Sessions.Commands.ReconnectAuthenticatedParticipant;

public sealed class ReconnectAuthenticatedParticipantCommandHandler
    : IRequestHandler<ReconnectAuthenticatedParticipantCommand, ReconnectParticipantResultDto>
{
    private readonly ILiveSessionRepository _liveSessionRepository;
    private readonly IRuntimeParticipationGuard _runtimeParticipationGuard;
    private readonly ICurrentUser _currentUser;
    private readonly JoinPolicy _joinPolicy;
    private readonly TimeProvider _timeProvider;

    public ReconnectAuthenticatedParticipantCommandHandler(
        ILiveSessionRepository liveSessionRepository,
        IRuntimeParticipationGuard runtimeParticipationGuard,
        ICurrentUser currentUser,
        JoinPolicy joinPolicy,
        TimeProvider timeProvider)
    {
        _liveSessionRepository = liveSessionRepository;
        _runtimeParticipationGuard = runtimeParticipationGuard;
        _currentUser = currentUser;
        _joinPolicy = joinPolicy;
        _timeProvider = timeProvider;
    }

    public async Task<ReconnectParticipantResultDto> Handle(
        ReconnectAuthenticatedParticipantCommand request,
        CancellationToken cancellationToken)
    {
        await _runtimeParticipationGuard.EnsureAllowedAsync(
            request.LiveSessionId,
            request.TeamId,
            request.Token,
            cancellationToken);

        var liveSession = await _liveSessionRepository.GetByIdAsync(request.LiveSessionId, cancellationToken)
            ?? throw new NotFoundException(nameof(LiveSession), request.LiveSessionId);

        if (!Guid.TryParse(_currentUser.Id, out var externalIdentityId))
        {
            throw new UnauthorizedAccessException();
        }

        var occurredAt = _timeProvider.GetUtcNow();
        var admission = liveSession.AdmitParticipant(
            externalIdentityId,
            request.DisplayName,
            request.TeamId,
            occurredAt,
            _joinPolicy);
        var timerSnapshot = liveSession.GetAuthoritativeSessionTimerSnapshot(occurredAt);

        await _liveSessionRepository.UpdateAsync(liveSession, cancellationToken);

        return new ReconnectParticipantResultDto(
            liveSession.LiveSessionId,
            admission.Team.TeamId,
            admission.Team.DisplayName,
            admission.Participant.SessionParticipantId,
            admission.Participant.DisplayName,
            liveSession.State.ToString(),
            admission.IsReconnect,
            admission.Participant.JoinedAt,
            admission.Participant.LastSeenAt,
            SessionTimerSnapshotDtoFactory.Create(liveSession, admission.Team.TeamId, timerSnapshot));
    }
}

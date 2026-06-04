using umbral_backend.Application.Common.Exceptions;
using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Application.Sessions.DTOs;
using umbral_backend.Domain.Entities;
using umbral_backend.Domain.Services;

namespace umbral_backend.Application.Sessions.Commands.ReconnectAuthenticatedParticipant;

public sealed class ReconnectAuthenticatedParticipantService : IReconnectAuthenticatedParticipantExecutor
{
    private readonly ILiveSessionRepository _liveSessionRepository;
    private readonly IParticipantMembershipAccessClient _participantMembershipAccessClient;
    private readonly ICurrentUser _currentUser;
    private readonly JoinPolicy _joinPolicy;
    private readonly TimeProvider _timeProvider;

    public ReconnectAuthenticatedParticipantService(
        ILiveSessionRepository liveSessionRepository,
        IParticipantMembershipAccessClient participantMembershipAccessClient,
        ICurrentUser currentUser,
        JoinPolicy joinPolicy,
        TimeProvider timeProvider)
    {
        _liveSessionRepository = liveSessionRepository;
        _participantMembershipAccessClient = participantMembershipAccessClient;
        _currentUser = currentUser;
        _joinPolicy = joinPolicy;
        _timeProvider = timeProvider;
    }

    public async Task<ReconnectParticipantResultDto> ReconnectAsync(
        ReconnectAuthenticatedParticipantCommand command,
        CancellationToken cancellationToken)
    {
        var accessDecision = await _participantMembershipAccessClient.ValidateAsync(
            command.LiveSessionId,
            command.TeamId,
            command.Token,
            cancellationToken);

        if (!accessDecision.IsAllowed)
        {
            throw new ForbiddenAccessException();
        }

        var liveSession = await _liveSessionRepository.GetByIdAsync(command.LiveSessionId, cancellationToken)
            ?? throw new NotFoundException(nameof(LiveSession), command.LiveSessionId);

        if (!Guid.TryParse(_currentUser.Id, out var externalIdentityId))
        {
            throw new UnauthorizedAccessException();
        }

        var occurredAt = _timeProvider.GetUtcNow();
        var admission = liveSession.AdmitParticipant(
            externalIdentityId,
            command.DisplayName,
            command.TeamId,
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

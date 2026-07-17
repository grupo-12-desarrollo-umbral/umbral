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
    private readonly OpenTeamSelectionPolicy _openTeamSelectionPolicy;
    private readonly TimeProvider _timeProvider;

    public ReconnectAuthenticatedParticipantCommandHandler(
        ILiveSessionRepository liveSessionRepository,
        IRuntimeParticipationGuard runtimeParticipationGuard,
        ICurrentUser currentUser,
        JoinPolicy joinPolicy,
        OpenTeamSelectionPolicy openTeamSelectionPolicy,
        TimeProvider timeProvider)
    {
        _liveSessionRepository = liveSessionRepository;
        _runtimeParticipationGuard = runtimeParticipationGuard;
        _currentUser = currentUser;
        _joinPolicy = joinPolicy;
        _openTeamSelectionPolicy = openTeamSelectionPolicy;
        _timeProvider = timeProvider;
    }

    public async Task<ReconnectParticipantResultDto> Handle(
        ReconnectAuthenticatedParticipantCommand request,
        CancellationToken cancellationToken)
    {
        var whitelist = await _runtimeParticipationGuard.EnsureAllowedAsync(
            request.LiveSessionId,
            cancellationToken);

        var liveSession = await _liveSessionRepository.GetByIdAsync(request.LiveSessionId, cancellationToken)
            ?? throw new NotFoundException(nameof(LiveSession), request.LiveSessionId);

        if (!Guid.TryParse(_currentUser.Id, out var externalIdentityId))
        {
            throw new UnauthorizedAccessException();
        }

        // Reference/catalog team ids; empty => unassigned participant, domain treats it as "all attached
        // selectable". Only the first-join branch consults it — a returning participant is already bound to
        // their assigned team by JoinPolicy.EnsureCanReconnect.
        var authorizedReferenceTeamIds = whitelist.Teams.Select(team => team.TeamId).ToHashSet();

        var occurredAt = _timeProvider.GetUtcNow();
        var admission = liveSession.AdmitParticipant(
            externalIdentityId,
            request.DisplayName,
            request.TeamId,
            occurredAt,
            _joinPolicy,
            _openTeamSelectionPolicy,
            authorizedReferenceTeamIds);
        // Register the socket as a presence lease inside the same serialized write. Keying presence on
        // the ConnectionId here is what lets the matching disconnect be decrement-guarded: an old
        // socket dropping after this commit removes only its own lease and never disconnects the
        // participant (Finding 5). The HTTP reconnect path carries no ConnectionId and only refreshes.
        if (!string.IsNullOrWhiteSpace(request.ConnectionId))
        {
            liveSession.RegisterParticipantConnection(
                admission.Participant.SessionParticipantId,
                request.ConnectionId,
                occurredAt);
        }

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

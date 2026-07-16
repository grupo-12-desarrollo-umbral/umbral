using umbral_backend.Application.Common.Exceptions;
using umbral_backend.Application.Common.Interfaces;

namespace umbral_backend.Application.Sessions.Common;

// Enforces the cross-context Participation Block (#91) synchronously and fail-closed. Blocking is an
// access decision on the critical gameplay path, so it must not depend on RabbitMQ (roadmap §2.3):
// the authoritative gate is this per-call re-check of the identity-access eligibility fact. On deny we
// record the block on the SessionParticipant and evict the live connection, then reject. Recovery is
// automatic — a later Users re-allow passes here and reconnect refreshes the participant to Active.
public sealed class RuntimeParticipationGuard : IRuntimeParticipationGuard
{
    private readonly IParticipantEligibleTeamsClient _participantEligibleTeamsClient;
    private readonly ILiveSessionRepository _liveSessionRepository;
    private readonly ICurrentUser _currentUser;
    private readonly IParticipantBlockNotifier _participantBlockNotifier;
    private readonly TimeProvider _timeProvider;

    public RuntimeParticipationGuard(
        IParticipantEligibleTeamsClient participantEligibleTeamsClient,
        ILiveSessionRepository liveSessionRepository,
        ICurrentUser currentUser,
        IParticipantBlockNotifier participantBlockNotifier,
        TimeProvider timeProvider)
    {
        _participantEligibleTeamsClient = participantEligibleTeamsClient;
        _liveSessionRepository = liveSessionRepository;
        _currentUser = currentUser;
        _participantBlockNotifier = participantBlockNotifier;
        _timeProvider = timeProvider;
    }

    public async Task<ParticipantEligibleTeamsDto> EnsureAllowedAsync(
        Guid liveSessionId,
        CancellationToken cancellationToken)
    {
        var whitelist = await _participantEligibleTeamsClient.GetAsync(cancellationToken);
        if (whitelist.IsEligible)
        {
            return whitelist;
        }

        await ApplyParticipationBlockAsync(liveSessionId, cancellationToken);
        throw new ForbiddenAccessException();
    }

    private async Task ApplyParticipationBlockAsync(Guid liveSessionId, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(_currentUser.Id, out var externalIdentityId))
        {
            return;
        }

        var liveSession = await _liveSessionRepository.GetByIdAsync(liveSessionId, cancellationToken);
        var blocked = liveSession?.BlockExternalParticipant(externalIdentityId, _timeProvider.GetUtcNow());
        if (blocked is null)
        {
            return;
        }

        await _liveSessionRepository.UpdateAsync(liveSession!, cancellationToken);
        await _participantBlockNotifier.NotifyBlockedAsync(blocked.SessionParticipantId, cancellationToken);
    }
}

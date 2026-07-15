using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Application.Dtos.Sessions;

namespace umbral_backend.Application.Sessions.Queries.ValidateParticipantSessionMembership;

public sealed class ValidateParticipantSessionMembershipQueryHandler
    : IRequestHandler<ValidateParticipantSessionMembershipQuery, ParticipantSessionMembershipDto>
{
    private readonly ILiveSessionRepository _liveSessionRepository;
    private readonly ICurrentUser _currentUser;
    private readonly IParticipantSessionMembershipChecker _membershipChecker;

    public ValidateParticipantSessionMembershipQueryHandler(
        ILiveSessionRepository liveSessionRepository,
        ICurrentUser currentUser,
        IParticipantSessionMembershipChecker membershipChecker)
    {
        _liveSessionRepository = liveSessionRepository;
        _currentUser = currentUser;
        _membershipChecker = membershipChecker;
    }

    public async Task<ParticipantSessionMembershipDto> Handle(
        ValidateParticipantSessionMembershipQuery request,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(_currentUser.Id, out _))
        {
            return Deny(request.LiveSessionId, request.TeamId, "unauthenticated");
        }

        var liveSession = await _liveSessionRepository.GetByIdAsync(request.LiveSessionId, cancellationToken);
        if (liveSession is null)
        {
            return Deny(request.LiveSessionId, request.TeamId, "session-not-found");
        }

        var result = _membershipChecker.Check(liveSession, request.TeamId);

        return new ParticipantSessionMembershipDto(
            result.IsAllowed,
            request.LiveSessionId,
            request.TeamId,
            result.ReasonCode);
    }

    private static ParticipantSessionMembershipDto Deny(Guid liveSessionId, Guid teamId, string reasonCode)
    {
        return new ParticipantSessionMembershipDto(false, liveSessionId, teamId, reasonCode);
    }
}

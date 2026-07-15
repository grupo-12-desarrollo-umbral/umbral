using umbral_backend.Application.Common.Exceptions;
using umbral_backend.Application.Common.Interfaces;

namespace umbral_backend.Application.Rankings.Common;

public sealed class RankingSessionMembershipGuard : IRankingSessionMembershipGuard
{
    private readonly IParticipantSessionMembershipClient _client;

    public RankingSessionMembershipGuard(IParticipantSessionMembershipClient client)
    {
        _client = client;
    }

    public async Task EnsureAllowedAsync(Guid liveSessionId, Guid teamId, CancellationToken cancellationToken)
    {
        var decision = await _client.ValidateAsync(liveSessionId, teamId, cancellationToken);
        if (decision.IsAllowed)
        {
            return;
        }

        throw new ForbiddenAccessException();
    }
}

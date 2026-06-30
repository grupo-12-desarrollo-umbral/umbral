using umbral_backend.Application.Sessions.Common;

namespace umbral_backend.Application.Common.Interfaces;

public interface IParticipantMembershipAccessClient
{
    Task<ParticipantMembershipAccessDecisionDto> ValidateAsync(
        Guid liveSessionId,
        Guid teamId,
        string? token,
        CancellationToken cancellationToken);
}

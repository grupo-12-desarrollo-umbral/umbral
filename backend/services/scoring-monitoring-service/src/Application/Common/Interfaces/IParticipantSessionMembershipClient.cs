namespace umbral_backend.Application.Common.Interfaces;

public interface IParticipantSessionMembershipClient
{
    Task<ParticipantSessionMembershipDecisionDto> ValidateAsync(
        Guid liveSessionId,
        Guid teamId,
        CancellationToken cancellationToken);
}

public sealed record ParticipantSessionMembershipDecisionDto(
    bool IsAllowed,
    Guid LiveSessionId,
    Guid TeamId,
    string ReasonCode);

using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Application.Sessions.Common;

namespace umbral_backend.Infrastructure.IntegrationTests.Api;

/// <summary>
/// In-process replacement for the HTTP access-fact client. Each test configures
/// the decision it wants the identity-access-service to have returned.
/// </summary>
public sealed class FakeParticipantMembershipAccessClient : IParticipantMembershipAccessClient
{
    public bool IsAllowed { get; set; } = true;

    public string Capability { get; set; } = "ParticipantExperience";

    public string Reason { get; set; } = "Membership validated.";

    public Task<ParticipantMembershipAccessDecisionDto> ValidateAsync(
        Guid liveSessionId,
        Guid teamId,
        string? token,
        CancellationToken cancellationToken)
    {
        return Task.FromResult(new ParticipantMembershipAccessDecisionDto(
            Capability,
            IsAllowed,
            Reason,
            liveSessionId,
            teamId));
    }
}

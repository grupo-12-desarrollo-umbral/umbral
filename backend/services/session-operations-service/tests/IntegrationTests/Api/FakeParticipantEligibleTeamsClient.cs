using umbral_backend.Application.Common.Interfaces;

namespace umbral_backend.Infrastructure.IntegrationTests.Api;

/// <summary>
/// In-process replacement for the participant eligibility access-fact client.
/// Each test configures the decision it wants users-service to have returned.
/// </summary>
public sealed class FakeParticipantEligibleTeamsClient : IParticipantEligibleTeamsClient
{
    public bool IsEligible { get; set; } = true;

    public string ReasonCode { get; set; } = "eligible";

    public IReadOnlyList<EligibleTeamDto> Teams { get; set; } = [];

    public Task<ParticipantEligibleTeamsDto> GetAsync(CancellationToken cancellationToken)
    {
        return Task.FromResult(new ParticipantEligibleTeamsDto(IsEligible, ReasonCode, Teams));
    }
}

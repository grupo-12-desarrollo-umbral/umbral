using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Application.Sessions.Common;

namespace umbral_backend.Infrastructure.IntegrationTests.Api;

/// <summary>
/// In-process replacement for the identity-backed operator eligibility client.
/// Each test configures the actor facts it needs for the endpoint path under test.
/// </summary>
public sealed class FakeAssignableSessionOperatorAccessClient : IAssignableSessionOperatorAccessClient
{
    public bool IsEligible { get; set; } = true;

    public string Source { get; set; } = "identity-access-service";

    public string? Role { get; set; } = "Operator";

    public string? Reason { get; set; } = "User is eligible for operator assignment.";

    public Task<SessionOperatorEligibilityDecisionDto> GetEligibilityAsync(
        int operatorUserId,
        CancellationToken cancellationToken)
    {
        return Task.FromResult(new SessionOperatorEligibilityDecisionDto(
            Source,
            IsEligible,
            operatorUserId,
            Role,
            Reason));
    }
}

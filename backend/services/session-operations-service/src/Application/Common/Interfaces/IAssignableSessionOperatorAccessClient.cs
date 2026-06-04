using umbral_backend.Application.Sessions.DTOs;

namespace umbral_backend.Application.Common.Interfaces;

public interface IAssignableSessionOperatorAccessClient
{
    Task<SessionOperatorEligibilityDecisionDto> GetEligibilityAsync(
        int operatorUserId,
        CancellationToken cancellationToken);
}

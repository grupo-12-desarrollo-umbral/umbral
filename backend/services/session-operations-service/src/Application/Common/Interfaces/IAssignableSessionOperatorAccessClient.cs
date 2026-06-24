using umbral_backend.Application.Sessions.Common;

namespace umbral_backend.Application.Common.Interfaces;

public interface IAssignableSessionOperatorAccessClient
{
    Task<SessionOperatorEligibilityDecisionDto> GetEligibilityAsync(
        int operatorUserId,
        CancellationToken cancellationToken);
}

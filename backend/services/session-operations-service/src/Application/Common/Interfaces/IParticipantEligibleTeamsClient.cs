using umbral_backend.Application.Sessions.Common;

namespace umbral_backend.Application.Common.Interfaces;

// Resolves the current participant's session-independent eligible-teams whitelist from users-service (#107).
// Shared by the lobby read (#108) and the self-join write (#110).
public interface IParticipantEligibleTeamsClient
{
    Task<ParticipantEligibleTeamsDto> GetAsync(CancellationToken cancellationToken);
}

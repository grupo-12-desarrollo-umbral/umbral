using umbral_backend.Application.Sessions.DTOs;

namespace umbral_backend.Application.Common.Interfaces;

public interface IAuthenticatedActorProfileAccessClient
{
    Task<AuthenticatedActorProfileLookupDto> GetCurrentAsync(CancellationToken cancellationToken);
}

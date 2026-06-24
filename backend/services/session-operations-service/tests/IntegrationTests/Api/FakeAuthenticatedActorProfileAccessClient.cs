using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Application.Sessions.Common;

namespace umbral_backend.Infrastructure.IntegrationTests.Api;

public sealed class FakeAuthenticatedActorProfileAccessClient : IAuthenticatedActorProfileAccessClient
{
    public AuthenticatedActorProfileLookupDto CurrentActor { get; set; } =
        new(55, "kc-operator-55", "Operator", true);

    public Task<AuthenticatedActorProfileLookupDto> GetCurrentAsync(CancellationToken cancellationToken)
    {
        return Task.FromResult(CurrentActor);
    }
}

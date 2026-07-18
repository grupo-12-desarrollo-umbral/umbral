using umbral_backend.Application.Common.Security;
using umbral_backend.Application.Dtos.Users;

namespace umbral_backend.Application.Users.Queries.GetAuthenticatedActorProfile;

// Any authenticated role, stated rather than implied: this is a self-profile read, so it carries no
// role requirement — but it must still reject an unauthenticated caller on its own rather than
// relying solely on the gateway's "default" policy.
[Authorize]
public sealed record GetAuthenticatedActorProfileQuery : IRequest<AuthenticatedActorProfileDto>;

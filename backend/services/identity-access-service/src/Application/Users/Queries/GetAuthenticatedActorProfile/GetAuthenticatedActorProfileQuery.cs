using umbral_backend.Application.Users.Common;

namespace umbral_backend.Application.Users.Queries.GetAuthenticatedActorProfile;

public sealed record GetAuthenticatedActorProfileQuery : IRequest<AuthenticatedActorProfileDto>;

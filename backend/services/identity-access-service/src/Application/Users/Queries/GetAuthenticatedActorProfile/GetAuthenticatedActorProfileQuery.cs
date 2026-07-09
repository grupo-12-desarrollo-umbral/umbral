using umbral_backend.Application.Dtos.Users;

namespace umbral_backend.Application.Users.Queries.GetAuthenticatedActorProfile;

public sealed record GetAuthenticatedActorProfileQuery : IRequest<AuthenticatedActorProfileDto>;

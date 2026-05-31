using umbral_backend.Application.Users.DTOs;

namespace umbral_backend.Application.Users.Queries.GetAuthenticatedActorProfile;

public sealed record GetAuthenticatedActorProfileQuery : IRequest<AuthenticatedActorProfileDto>;

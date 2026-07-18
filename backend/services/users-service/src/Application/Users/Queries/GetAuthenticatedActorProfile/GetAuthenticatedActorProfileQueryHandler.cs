using umbral_backend.Application.Common.Exceptions;
using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Application.Dtos.Users;
using umbral_backend.Application.Users.Queries.GetAuthenticatedActorProfile;
using umbral_backend.Domain.Entities;

namespace umbral_backend.Application.Users.Queries.GetAuthenticatedActorProfile;

public sealed class GetAuthenticatedActorProfileQueryHandler : IRequestHandler<GetAuthenticatedActorProfileQuery, AuthenticatedActorProfileDto>
{
    private readonly ICurrentActor _currentActor;

    public GetAuthenticatedActorProfileQueryHandler(ICurrentActor currentActor)
    {
        _currentActor = currentActor;
    }

    public async Task<AuthenticatedActorProfileDto> Handle(
        GetAuthenticatedActorProfileQuery request,
        CancellationToken cancellationToken)
    {
        var user = await _currentActor.GetActorAsync(cancellationToken);

        return new AuthenticatedActorProfileDto(
            user.Id,
            user.ExternalIdentityId,
            user.DisplayName,
            user.Email,
            user.Role.ToString(),
            user.IsActive);
    }
}

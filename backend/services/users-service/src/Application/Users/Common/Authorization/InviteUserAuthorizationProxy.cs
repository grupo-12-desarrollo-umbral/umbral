using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Application.Dtos.Users;
using umbral_backend.Application.Users.Commands.InviteUser;
using umbral_backend.Domain.Enums;
using umbral_backend.Domain.Services;

namespace umbral_backend.Application.Users.Common.Authorization;

public sealed class InviteUserAuthorizationProxy : IRequestHandler<InviteUserCommand, InviteUserResultDto>
{
    private readonly ICurrentActor _currentActor;
    private readonly AccessPolicy _accessPolicy;
    private readonly InviteUserCommandHandler _inner;

    public InviteUserAuthorizationProxy(
        ICurrentActor currentActor,
        AccessPolicy accessPolicy,
        InviteUserCommandHandler inner)
    {
        _currentActor = currentActor;
        _accessPolicy = accessPolicy;
        _inner = inner;
    }

    public async Task<InviteUserResultDto> Handle(InviteUserCommand request, CancellationToken cancellationToken)
    {
        var actor = await _currentActor.GetActorAsync(cancellationToken);

        _accessPolicy.EnsureCanAccess(actor, ProtectedCapability.AdministratorPanel);

        return await _inner.Handle(request, cancellationToken);
    }
}

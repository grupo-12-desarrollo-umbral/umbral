using umbral_backend.Application.Common.Exceptions;
using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Application.Users.Commands.DeactivateUser;
using umbral_backend.Domain.Entities;
using umbral_backend.Domain.Enums;
using umbral_backend.Domain.Services;

namespace umbral_backend.Application.Users.Commands.DeactivateUser;

public sealed class DeactivateUserCommandHandler : IRequestHandler<DeactivateUserCommand>
{
    private readonly IUserRepository _userRepository;
    private readonly ICurrentActor _currentActor;
    private readonly AccessPolicy _accessPolicy;

    public DeactivateUserCommandHandler(
        IUserRepository userRepository,
        ICurrentActor currentActor,
        AccessPolicy accessPolicy)
    {
        _userRepository = userRepository;
        _currentActor = currentActor;
        _accessPolicy = accessPolicy;
    }

    public async Task Handle(DeactivateUserCommand request, CancellationToken cancellationToken)
    {
        var actor = await _currentActor.GetActorAsync(cancellationToken);

        _accessPolicy.EnsureCanAccess(actor, ProtectedCapability.AdministratorPanel);

        var user = await _userRepository.GetByIdAsync(request.UserId, cancellationToken)
            ?? throw new NotFoundException(nameof(User), request.UserId);

        user.DeactivateAccess();

        await _userRepository.UpdateAsync(user, cancellationToken);
    }
}

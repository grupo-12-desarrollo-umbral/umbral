using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Application.Sessions.Commands.AssociateTeamToSessionReference;
using umbral_backend.Domain.Entities;
using umbral_backend.Domain.Exceptions;

namespace umbral_backend.Application.Sessions.Commands.AssociateTeamToSessionReference;

public sealed class AssociateTeamToSessionReferenceCommandHandler
    : IRequestHandler<AssociateTeamToSessionReferenceCommand>
{
    private readonly ILiveSessionReferenceRepository _liveSessionReferenceRepository;

    public AssociateTeamToSessionReferenceCommandHandler(
        ILiveSessionReferenceRepository liveSessionReferenceRepository)
    {
        _liveSessionReferenceRepository = liveSessionReferenceRepository;
    }

    public async Task Handle(
        AssociateTeamToSessionReferenceCommand request,
        CancellationToken cancellationToken)
    {
        var reference = await _liveSessionReferenceRepository.GetByIdAsync(
            request.LiveSessionId,
            cancellationToken);

        if (reference is null)
        {
            reference = LiveSessionReference.Create(request.LiveSessionId, request.SessionCode);
            reference.AssociateTeam(request.TeamId);
            await _liveSessionReferenceRepository.AddAsync(reference, cancellationToken);
            return;
        }

        try
        {
            reference.AssociateTeam(request.TeamId);
        }
        catch (TeamAlreadyAssociatedWithSessionException)
        {
            // Operator associate is idempotent: the lobby index already reflects this team.
            return;
        }

        await _liveSessionReferenceRepository.UpdateAsync(reference, cancellationToken);
    }
}

using umbral_backend.Application.Common.Exceptions;
using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Domain.Entities;
using umbral_backend.Domain.Services;

namespace umbral_backend.Application.Teams.Commands.JoinTeamAsParticipant;

public sealed class ParticipantTeamSelfJoinService : IJoinTeamAsParticipantService
{
    private readonly ILiveSessionReferenceRepository _liveSessionReferenceRepository;
    private readonly ITeamRepository _teamRepository;
    private readonly ICurrentActor _currentActor;
    private readonly ParticipantSessionMembershipPolicy _membershipPolicy;

    public ParticipantTeamSelfJoinService(
        ILiveSessionReferenceRepository liveSessionReferenceRepository,
        ITeamRepository teamRepository,
        ICurrentActor currentActor,
        ParticipantSessionMembershipPolicy membershipPolicy)
    {
        _liveSessionReferenceRepository = liveSessionReferenceRepository;
        _teamRepository = teamRepository;
        _currentActor = currentActor;
        _membershipPolicy = membershipPolicy;
    }

    public async Task<Guid> JoinAsync(JoinTeamAsParticipantCommand command, CancellationToken cancellationToken)
    {
        var participant = await _currentActor.GetActorAsync(cancellationToken);

        var liveSessionReference = await _liveSessionReferenceRepository.GetByIdAsync(
            command.LiveSessionId,
            cancellationToken);

        if (liveSessionReference is null)
        {
            throw new NotFoundException(nameof(LiveSessionReference), command.LiveSessionId);
        }

        var isAssociated = await _liveSessionReferenceRepository.IsTeamAssociatedAsync(
            liveSessionReference.LiveSessionId,
            command.TeamId,
            cancellationToken);

        if (!isAssociated)
        {
            throw new NotFoundException(nameof(SessionTeamAssociation), $"{command.LiveSessionId}:{command.TeamId}");
        }

        var existingMembership = await _liveSessionReferenceRepository.GetParticipantMembershipAsync(
            liveSessionReference.LiveSessionId,
            participant.Id,
            cancellationToken);

        _membershipPolicy.EnsureCanSelfAssign(command.TeamId, participant.Id, existingMembership?.TeamId);

        if (existingMembership?.TeamId == command.TeamId)
        {
            return existingMembership.TeamMembershipId;
        }

        var team = await _teamRepository.GetByIdWithMembershipsAsync(command.TeamId, cancellationToken)
            ?? throw new NotFoundException(nameof(Team), command.TeamId);

        var membership = team.AssignParticipant(participant.Id);
        await _teamRepository.UpdateAsync(team, cancellationToken);

        return membership.TeamMembershipId;
    }
}

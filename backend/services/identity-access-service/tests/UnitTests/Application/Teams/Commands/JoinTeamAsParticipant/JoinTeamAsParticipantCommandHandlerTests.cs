using umbral_backend.Application.Common.Exceptions;
using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Application.Common.Models;
using umbral_backend.Application.Teams.Commands.JoinTeamAsParticipant;
using umbral_backend.Domain.Entities;
using umbral_backend.Domain.Enums;
using umbral_backend.Domain.Events;
using umbral_backend.Domain.Exceptions;
using umbral_backend.Domain.Services;

namespace umbral_backend.Application.UnitTests.Application.Teams.Commands.JoinTeamAsParticipant;

public sealed class JoinTeamAsParticipantCommandHandlerTests
{
    [Fact]
    public async Task Handle_AssignsParticipantWhenSessionTeamIsJoinable()
    {
        var actor = CreateUser(20, "kc-participant", Role.Participant);
        var liveSessionReference = LiveSessionReference.Create(Guid.NewGuid(), "RSF231");
        var team = Team.Register("Blue Owls", "BLUE-01");
        var handler = CreateHandler(actor, liveSessionReference, team, existingMembership: null, isAssociated: true);

        var membershipId = await handler.Handle(
            new JoinTeamAsParticipantCommand(liveSessionReference.LiveSessionId, team.TeamId),
            CancellationToken.None);

        membershipId.Should().NotBe(Guid.Empty);
        team.Memberships.Should().ContainSingle(membership => membership.UserId == actor.Id);
        team.DomainEvents.OfType<ParticipantAssignedToTeamEvent>().Should().ContainSingle();
    }

    [Fact]
    public async Task Handle_ReturnsExistingMembershipWhenParticipantReEntersOwnTeam()
    {
        var actor = CreateUser(21, "kc-participant-reenter", Role.Participant);
        var liveSessionReference = LiveSessionReference.Create(Guid.NewGuid(), "RSF231");
        var team = Team.Register("Blue Owls", "BLUE-01");
        var existingMembership = new ParticipantSessionMembershipLookup(team.TeamId, Guid.NewGuid());
        var handler = CreateHandler(actor, liveSessionReference, team, existingMembership, isAssociated: true);

        var membershipId = await handler.Handle(
            new JoinTeamAsParticipantCommand(liveSessionReference.LiveSessionId, team.TeamId),
            CancellationToken.None);

        membershipId.Should().Be(existingMembership.TeamMembershipId);
        team.Memberships.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_RejectsParticipantLockedToAnotherSessionTeam()
    {
        var actor = CreateUser(22, "kc-participant-locked", Role.Participant);
        var liveSessionReference = LiveSessionReference.Create(Guid.NewGuid(), "RSF231");
        var team = Team.Register("Blue Owls", "BLUE-01");
        var existingMembership = new ParticipantSessionMembershipLookup(Guid.NewGuid(), Guid.NewGuid());
        var handler = CreateHandler(actor, liveSessionReference, team, existingMembership, isAssociated: true);

        var act = async () => await handler.Handle(
            new JoinTeamAsParticipantCommand(liveSessionReference.LiveSessionId, team.TeamId),
            CancellationToken.None);

        await act.Should().ThrowAsync<ParticipantLockedToAnotherSessionTeamException>();
    }

    [Fact]
    public async Task Handle_RejectsNonParticipantActor()
    {
        var actor = CreateUser(23, "kc-operator", Role.Operator);
        var liveSessionReference = LiveSessionReference.Create(Guid.NewGuid(), "RSF231");
        var team = Team.Register("Blue Owls", "BLUE-01");
        var handler = CreateHandler(actor, liveSessionReference, team, existingMembership: null, isAssociated: true);

        var act = async () => await handler.Handle(
            new JoinTeamAsParticipantCommand(liveSessionReference.LiveSessionId, team.TeamId),
            CancellationToken.None);

        await act.Should().ThrowAsync<UserRoleNotAuthorizedException>();
    }

    [Fact]
    public async Task Handle_ThrowsNotFoundWhenSessionDoesNotExist()
    {
        var actor = CreateUser(24, "kc-participant-missing-session", Role.Participant);
        var team = Team.Register("Blue Owls", "BLUE-01");
        var handler = CreateHandler(actor, liveSessionReference: null, team, existingMembership: null, isAssociated: true);

        var act = async () => await handler.Handle(
            new JoinTeamAsParticipantCommand(Guid.NewGuid(), team.TeamId),
            CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_ThrowsNotFoundWhenTeamIsNotAssociatedWithSession()
    {
        var actor = CreateUser(25, "kc-participant-missing-association", Role.Participant);
        var liveSessionReference = LiveSessionReference.Create(Guid.NewGuid(), "RSF231");
        var team = Team.Register("Blue Owls", "BLUE-01");
        var handler = CreateHandler(actor, liveSessionReference, team, existingMembership: null, isAssociated: false);

        var act = async () => await handler.Handle(
            new JoinTeamAsParticipantCommand(liveSessionReference.LiveSessionId, team.TeamId),
            CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    private static JoinTeamAsParticipantCommandHandler CreateHandler(
        User actor,
        LiveSessionReference? liveSessionReference,
        Team team,
        ParticipantSessionMembershipLookup? existingMembership,
        bool isAssociated)
    {
        var currentActor = new Mock<ICurrentActor>();
        currentActor
            .Setup(a => a.GetActorAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(actor);

        var liveSessionReferenceRepository = new Mock<ILiveSessionReferenceRepository>();
        liveSessionReferenceRepository
            .Setup(repository => repository.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(liveSessionReference);

        if (liveSessionReference is not null)
        {
            liveSessionReferenceRepository
                .Setup(repository => repository.IsTeamAssociatedAsync(
                    liveSessionReference.LiveSessionId,
                    team.TeamId,
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(isAssociated);

            liveSessionReferenceRepository
                .Setup(repository => repository.GetParticipantMembershipAsync(
                    liveSessionReference.LiveSessionId,
                    actor.Id,
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(existingMembership);
        }

        var teamRepository = new Mock<ITeamRepository>();
        teamRepository
            .Setup(repository => repository.GetByIdWithMembershipsAsync(team.TeamId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(team);
        teamRepository
            .Setup(repository => repository.UpdateAsync(team, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var executor = new ParticipantTeamSelfJoinService(
            liveSessionReferenceRepository.Object,
            teamRepository.Object,
            currentActor.Object,
            new ParticipantSessionMembershipPolicy());

        var proxy = new ParticipantTeamSelfJoinAuthorizationProxy(
            currentActor.Object,
            new AccessPolicy(),
            executor);

        return new JoinTeamAsParticipantCommandHandler(proxy);
    }

    private static User CreateUser(int id, string externalIdentityId, Role role)
    {
        var user = User.Provision(externalIdentityId, $"{role} User", $"{externalIdentityId}@example.com", role);
        user.Id = id;
        return user;
    }
}

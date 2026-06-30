using umbral_backend.Application.Common.Exceptions;
using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Application.Sessions.Queries.GetSessionTeamsForParticipant;
using umbral_backend.Application.Sessions.Queries.GetSessionTeamsForParticipant;
using umbral_backend.Domain.Entities;
using umbral_backend.Domain.Enums;
using umbral_backend.Domain.Services;

namespace umbral_backend.Application.UnitTests.Application.Sessions.Queries.GetSessionTeamsForParticipant;

public sealed class GetSessionTeamsForParticipantQueryHandlerTests
{
    [Fact]
    public async Task Handle_ReturnsJoinableTeamsWhenParticipantHasNoSessionMembership()
    {
        var actor = CreateUser(20, "kc-participant", Role.Participant);
        var liveSessionReference = LiveSessionReference.Create(Guid.NewGuid(), "RSF231");
        var entries = new[]
        {
            new SessionTeamLobbyEntry(Guid.NewGuid(), "Blue Owls", false),
            new SessionTeamLobbyEntry(Guid.NewGuid(), "Red Foxes", false)
        };

        var handler = CreateHandler(actor, liveSessionReference, entries);

        var result = await handler.Handle(
            new GetSessionTeamsForParticipantQuery("rsf231"),
            CancellationToken.None);

        result.LiveSessionId.Should().Be(liveSessionReference.LiveSessionId);
        result.SessionCode.Should().Be("RSF231");
        result.Teams.Select(team => team.JoinState).Should().OnlyContain(state => state == "joinable");
    }

    [Fact]
    public async Task Handle_ReturnsMineAndLockedStatesWhenParticipantIsAlreadyAssigned()
    {
        var actor = CreateUser(21, "kc-preassigned", Role.Participant);
        var liveSessionReference = LiveSessionReference.Create(Guid.NewGuid(), "RSF231");
        var mineTeamId = Guid.NewGuid();
        var entries = new[]
        {
            new SessionTeamLobbyEntry(mineTeamId, "Blue Owls", true),
            new SessionTeamLobbyEntry(Guid.NewGuid(), "Red Foxes", false)
        };

        var handler = CreateHandler(actor, liveSessionReference, entries);

        var result = await handler.Handle(
            new GetSessionTeamsForParticipantQuery("RSF231"),
            CancellationToken.None);

        result.Teams.Should().Contain(team => team.TeamId == mineTeamId && team.JoinState == "mine");
        result.Teams.Should().Contain(team => team.TeamId != mineTeamId && team.JoinState == "locked");
    }

    [Fact]
    public async Task Handle_RejectsNonParticipantActor()
    {
        var actor = CreateUser(22, "kc-operator", Role.Operator);
        var liveSessionReference = LiveSessionReference.Create(Guid.NewGuid(), "RSF231");
        var handler = CreateHandler(actor, liveSessionReference, []);

        var act = async () => await handler.Handle(
            new GetSessionTeamsForParticipantQuery("RSF231"),
            CancellationToken.None);

        await act.Should().ThrowAsync<umbral_backend.Domain.Exceptions.UserRoleNotAuthorizedException>();
    }

    [Fact]
    public async Task Handle_ThrowsNotFoundWhenSessionCodeDoesNotExist()
    {
        var actor = CreateUser(23, "kc-participant-missing", Role.Participant);
        var handler = CreateHandler(actor, liveSessionReference: null, []);

        var act = async () => await handler.Handle(
            new GetSessionTeamsForParticipantQuery("RSF231"),
            CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    private static ParticipantSessionTeamLobbyAuthorizationProxy CreateHandler(
        User actor,
        LiveSessionReference? liveSessionReference,
        IReadOnlyList<SessionTeamLobbyEntry> entries)
    {
        var currentActor = new Mock<ICurrentActor>();
        currentActor
            .Setup(a => a.GetActorAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(actor);

        var liveSessionReferenceRepository = new Mock<ILiveSessionReferenceRepository>();
        liveSessionReferenceRepository
            .Setup(repository => repository.GetBySessionCodeAsync("RSF231", It.IsAny<CancellationToken>()))
            .ReturnsAsync(liveSessionReference);

        if (liveSessionReference is not null)
        {
            liveSessionReferenceRepository
                .Setup(repository => repository.ListTeamLobbyEntriesAsync(
                    liveSessionReference.LiveSessionId,
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(entries);
        }

        var inner = new GetSessionTeamsForParticipantQueryHandler(liveSessionReferenceRepository.Object);

        return new ParticipantSessionTeamLobbyAuthorizationProxy(
            currentActor.Object,
            new AccessPolicy(),
            inner);
    }

    private static User CreateUser(int id, string externalIdentityId, Role role)
    {
        var user = User.Provision(externalIdentityId, $"{role} User", $"{externalIdentityId}@example.com", role);
        user.Id = id;
        return user;
    }
}

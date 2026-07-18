using umbral_backend.Application.Common.Exceptions;
using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Application.Teams.Queries.GetTeamParticipants;
using umbral_backend.Domain.Entities;
using umbral_backend.Domain.Enums;
using umbral_backend.Domain.Services;

namespace umbral_backend.Application.UnitTests.Application.Teams.Handlers;

public sealed class GetTeamParticipantsQueryHandlerTests
{
    [Theory]
    [InlineData(Role.Administrator)]
    [InlineData(Role.Operator)]
    public async Task Handle_ReturnsAllMembershipProjections(Role actorRole)
    {
        var actor = CreateUser(1, $"kc-{actorRole}", actorRole);
        var firstParticipant = CreateUser(10, "kc-participant-01", Role.Participant);
        var secondParticipant = CreateUser(11, "kc-participant-02", Role.Participant);
        var team = RegisteredTeam.Register("Red Team", "RED-01");
        var firstMembership = team.AuthorizeParticipant(firstParticipant.Id);
        var secondMembership = team.AuthorizeParticipant(secondParticipant.Id);

        var teamRepository = new Mock<ITeamRepository>();
        teamRepository
            .Setup(repo => repo.GetByIdWithMembershipsAsync(team.TeamId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(team);

        var handler = CreateHandler(teamRepository, CreateUserRepository(), actor);

        var result = await handler.Handle(new GetTeamParticipantsQuery(team.TeamId), CancellationToken.None);

        result.Should().HaveCount(2);
        result.Should().Contain(item => item.TeamMembershipId == firstMembership.TeamMembershipId && item.UserId == firstParticipant.Id);
        result.Should().Contain(item => item.TeamMembershipId == secondMembership.TeamMembershipId && item.UserId == secondParticipant.Id);
    }

    [Theory]
    [InlineData(Role.Administrator)]
    [InlineData(Role.Operator)]
    public async Task Handle_WhenTeamHasNoMembers_ReturnsEmptyList(Role actorRole)
    {
        var actor = CreateUser(1, $"kc-{actorRole}", actorRole);
        var team = RegisteredTeam.Register("Red Team", "RED-01");

        var teamRepository = new Mock<ITeamRepository>();
        teamRepository
            .Setup(repo => repo.GetByIdWithMembershipsAsync(team.TeamId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(team);

        var handler = CreateHandler(teamRepository, CreateUserRepository(), actor);

        var result = await handler.Handle(new GetTeamParticipantsQuery(team.TeamId), CancellationToken.None);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_ThrowsWhenTeamDoesNotExist()
    {
        var actor = CreateUser(1, "kc-admin", Role.Administrator);
        var teamRepository = new Mock<ITeamRepository>();
        teamRepository
            .Setup(repo => repo.GetByIdWithMembershipsAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((RegisteredTeam?)null);

        var handler = CreateHandler(teamRepository, CreateUserRepository(), actor);

        var act = async () => await handler.Handle(new GetTeamParticipantsQuery(Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    private static GetTeamParticipantsQueryHandler CreateHandler(
        Mock<ITeamRepository> teamRepository,
        Mock<IUserRepository> userRepository,
        User actor)
    {
        var currentActor = new Mock<ICurrentActor>();
        currentActor.Setup(a => a.GetActorAsync(It.IsAny<CancellationToken>())).ReturnsAsync(actor);

        return new GetTeamParticipantsQueryHandler(
            teamRepository.Object,
            userRepository.Object,
            currentActor.Object,
            new AccessPolicy());
    }

    private static Mock<IUserRepository> CreateUserRepository() => new();

    private static User CreateUser(int id, string externalIdentityId, Role role)
    {
        var user = User.Provision(externalIdentityId, $"{role} User", $"{externalIdentityId}@example.com", role);
        user.Id = id;
        return user;
    }
}

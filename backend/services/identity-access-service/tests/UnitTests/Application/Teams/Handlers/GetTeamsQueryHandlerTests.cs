using umbral_backend.Application.Common.Exceptions;
using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Application.Common.Models;
using umbral_backend.Application.Teams.Common;
using umbral_backend.Application.Teams.Queries.GetTeams;
using umbral_backend.Domain.Entities;
using umbral_backend.Domain.Enums;
using umbral_backend.Domain.Exceptions;
using umbral_backend.Domain.Services;

namespace umbral_backend.Application.UnitTests.Application.Teams.Handlers;

public sealed class GetTeamsQueryHandlerTests
{
    [Theory]
    [InlineData(Role.Administrator)]
    [InlineData(Role.Operator)]
    public async Task Handle_ReturnsPagedTeamsForAuthorizedActor(Role actorRole)
    {
        var actor = CreateUser(2, $"kc-{actorRole}", actorRole);
        var team = Team.Register("Red Team", "RED-01");
        team.Created = DateTimeOffset.Parse("2026-05-31T12:00:00Z");
        team.LastModified = DateTimeOffset.Parse("2026-05-31T12:30:00Z");

        var teamRepository = new Mock<ITeamRepository>();
        teamRepository
            .Setup(repo => repo.ListAsync(2, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PagedResult<Team>
            {
                Items = new[] { team },
                TotalCount = 7,
                Page = 2,
                PageSize = 10
            });

        var handler = CreateHandler(teamRepository, CreateUserRepository(actor), actor.ExternalIdentityId);

        var result = await handler.Handle(new GetTeamsQuery(2, 10), CancellationToken.None);

        result.Should().BeEquivalentTo(new PagedResult<TeamDto>
        {
            Items = new[]
            {
                new TeamDto(team.TeamId, "Red Team", "RED-01", true, team.Created, team.LastModified)
            },
            TotalCount = 7,
            Page = 2,
            PageSize = 10
        });
    }

    [Fact]
    public async Task Handle_RejectsParticipantCaller()
    {
        var actor = CreateUser(2, "kc-participant", Role.Participant);
        var handler = CreateHandler(new Mock<ITeamRepository>(), CreateUserRepository(actor), actor.ExternalIdentityId);

        var act = async () => await handler.Handle(new GetTeamsQuery(), CancellationToken.None);

        await act.Should().ThrowAsync<UserRoleNotAuthorizedException>();
    }

    [Fact]
    public async Task Handle_RejectsMissingCurrentUserIdentity()
    {
        var actor = CreateUser(2, "kc-admin", Role.Administrator);
        var handler = CreateHandler(new Mock<ITeamRepository>(), CreateUserRepository(actor), null);

        var act = async () => await handler.Handle(new GetTeamsQuery(), CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    private static GetTeamsQueryHandler CreateHandler(
        Mock<ITeamRepository> teamRepository,
        Mock<IUserRepository> userRepository,
        string? currentUserId)
    {
        var currentUser = new Mock<ICurrentUser>();
        currentUser.SetupGet(user => user.Id).Returns(currentUserId);

        return new GetTeamsQueryHandler(
            teamRepository.Object,
            userRepository.Object,
            currentUser.Object,
            new AccessPolicy());
    }

    private static Mock<IUserRepository> CreateUserRepository(User actor)
    {
        var repository = new Mock<IUserRepository>();
        repository
            .Setup(repo => repo.GetByExternalIdentityIdAsync(actor.ExternalIdentityId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(actor);

        return repository;
    }

    private static User CreateUser(int id, string externalIdentityId, Role role)
    {
        var user = User.Provision(externalIdentityId, $"{role} User", $"{externalIdentityId}@example.com", role);
        user.Id = id;
        return user;
    }
}

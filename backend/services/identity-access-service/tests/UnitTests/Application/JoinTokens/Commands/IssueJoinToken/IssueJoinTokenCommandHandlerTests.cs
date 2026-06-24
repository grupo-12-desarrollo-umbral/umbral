using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Application.JoinTokens.Commands.IssueJoinToken;
using umbral_backend.Domain.Entities;
using umbral_backend.Domain.Enums;
using umbral_backend.Domain.Events;
using umbral_backend.Domain.Exceptions;
using umbral_backend.Domain.Services;

namespace umbral_backend.Application.UnitTests.Application.JoinTokens.Commands.IssueJoinToken;

public sealed class IssueJoinTokenCommandHandlerTests
{
    [Theory]
    [InlineData(Role.Administrator)]
    [InlineData(Role.Operator)]
    public async Task Handle_IssuesJoinTokenForAuthorizedActor(Role actorRole)
    {
        var actor = CreateUser(7, $"kc-{actorRole}", actorRole);
        var joinTokenRepository = new Mock<IJoinTokenRepository>();
        var tokenService = CreateTokenService();
        JoinToken? persistedJoinToken = null;

        joinTokenRepository
            .Setup(repository => repository.AddAsync(It.IsAny<JoinToken>(), It.IsAny<CancellationToken>()))
            .Callback<JoinToken, CancellationToken>((joinToken, _) => persistedJoinToken = joinToken)
            .Returns(Task.CompletedTask);

        var handler = CreateHandler(
            actor,
            joinTokenRepository,
            tokenService,
            new TestTimeProvider(new DateTimeOffset(2026, 6, 2, 12, 0, 0, TimeSpan.Zero)));

        var command = new IssueJoinTokenCommand(
            Guid.NewGuid(),
            Guid.NewGuid(),
            new DateTimeOffset(2026, 6, 2, 12, 10, 0, TimeSpan.Zero));

        var result = await handler.Handle(command, CancellationToken.None);

        result.Token.Should().Be("plain-token");
        result.LiveSessionId.Should().Be(command.LiveSessionId);
        result.TeamId.Should().Be(command.TeamId);
        result.ExpiresAt.Should().Be(command.ExpiresAt);
        persistedJoinToken.Should().NotBeNull();
        persistedJoinToken!.TokenHash.Should().Be("hashed-token");
        persistedJoinToken.IssuedByUserId.Should().Be(actor.Id);
        persistedJoinToken.DomainEvents.OfType<JoinTokenIssuedEvent>().Should().ContainSingle();
    }

    [Fact]
    public async Task Handle_RejectsParticipantActor()
    {
        var actor = CreateUser(9, "kc-participant", Role.Participant);
        var joinTokenRepository = new Mock<IJoinTokenRepository>();
        var tokenService = CreateTokenService();
        var handler = CreateHandler(actor, joinTokenRepository, tokenService, TimeProvider.System);

        var act = async () => await handler.Handle(
            new IssueJoinTokenCommand(Guid.NewGuid(), Guid.NewGuid(), DateTimeOffset.UtcNow.AddMinutes(5)),
            CancellationToken.None);

        await act.Should().ThrowAsync<UserRoleNotAuthorizedException>();
        joinTokenRepository.Verify(
            repository => repository.AddAsync(It.IsAny<JoinToken>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    private static IssueJoinTokenCommandHandler CreateHandler(
        User actor,
        Mock<IJoinTokenRepository> joinTokenRepository,
        Mock<IJoinTokenTokenService> tokenService,
        TimeProvider timeProvider)
    {
        var userRepository = new Mock<IUserRepository>();
        userRepository
            .Setup(repository => repository.GetByExternalIdentityIdAsync(actor.ExternalIdentityId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(actor);

        var currentUser = new Mock<ICurrentUser>();
        currentUser.SetupGet(user => user.Id).Returns(actor.ExternalIdentityId);

        var executor = new JoinTokenIssuanceService(
            joinTokenRepository.Object,
            userRepository.Object,
            currentUser.Object,
            tokenService.Object,
            new JoinTokenPolicy(),
            timeProvider);

        var proxy = new JoinTokenIssuanceAuthorizationProxy(
            userRepository.Object,
            currentUser.Object,
            new AccessPolicy(),
            executor);

        return new IssueJoinTokenCommandHandler(proxy);
    }

    private static Mock<IJoinTokenTokenService> CreateTokenService()
    {
        var tokenService = new Mock<IJoinTokenTokenService>();
        tokenService.Setup(service => service.GenerateToken()).Returns("plain-token");
        tokenService.Setup(service => service.HashToken("plain-token")).Returns("hashed-token");
        return tokenService;
    }

    private static User CreateUser(int id, string externalIdentityId, Role role)
    {
        var user = User.Provision(externalIdentityId, $"{role} User", $"{externalIdentityId}@example.com", role);
        user.Id = id;
        return user;
    }

    private sealed class TestTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset _utcNow;

        public TestTimeProvider(DateTimeOffset utcNow)
        {
            _utcNow = utcNow;
        }

        public override DateTimeOffset GetUtcNow()
        {
            return _utcNow;
        }
    }
}

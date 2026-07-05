using umbral_backend.Application.Common.Behaviours;
using umbral_backend.Application.Common.Exceptions;
using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Application.Sessions.Commands.TransitionSessionState;
using umbral_backend.Domain.Enums;

namespace umbral_backend.Application.UnitTests.Sessions.Commands.TransitionSessionState;

/// Locks that the state-transition command is Operator-only. The standard
/// AuthorizationBehaviour reads the command's required role and enforces it:
/// anonymous callers are rejected as unauthorized, non-operators as forbidden,
/// and operators are allowed through.
public sealed class TransitionSessionStateAuthorizationBehaviourTests
{
    [Fact]
    public async Task Handle_WhenCallerIsAnonymous_ThrowsUnauthorizedException()
    {
        var behaviour = Behaviour(CreateCurrentUser(null, null));
        var command = new TransitionSessionStateCommand(Guid.NewGuid(), SessionState.Preparing, null);

        var act = async () => await behaviour.Handle(command, Continue, CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task Handle_WhenCallerIsNotOperator_ThrowsForbiddenException()
    {
        var behaviour = Behaviour(CreateCurrentUser("99", "Administrator"));
        var command = new TransitionSessionStateCommand(Guid.NewGuid(), SessionState.Preparing, null);

        var act = async () => await behaviour.Handle(command, Continue, CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenAccessException>();
    }

    [Fact]
    public async Task Handle_WhenCallerIsOperator_AllowsCommand()
    {
        var behaviour = Behaviour(CreateCurrentUser("42", "Operator"));
        var command = new TransitionSessionStateCommand(Guid.NewGuid(), SessionState.Preparing, null);

        var result = await behaviour.Handle(command, Continue, CancellationToken.None);

        result.CurrentState.Should().Be(nameof(SessionState.Preparing));
    }

    private static AuthorizationBehaviour<TransitionSessionStateCommand, TransitionSessionStateResultDto> Behaviour(
        Mock<ICurrentUser> currentUser)
        => new(currentUser.Object);

    private static Task<TransitionSessionStateResultDto> Continue()
        => Task.FromResult(new TransitionSessionStateResultDto(
            Guid.NewGuid(),
            nameof(SessionState.Scheduled),
            nameof(SessionState.Preparing),
            DateTimeOffset.UtcNow));

    private static Mock<ICurrentUser> CreateCurrentUser(string? id, string? role)
    {
        var currentUser = new Mock<ICurrentUser>();
        currentUser.SetupGet(user => user.Id).Returns(id);
        currentUser.SetupGet(user => user.Role).Returns(role);
        return currentUser;
    }
}

using umbral_backend.Application.Common.Behaviours;
using umbral_backend.Application.Common.Exceptions;
using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Application.Sessions.Commands.AssignOperatorToSession;
using umbral_backend.Application.Sessions.Common;

namespace umbral_backend.Application.UnitTests.Sessions.Commands.AssignOperatorToSession;

public sealed class AssignOperatorToSessionAuthorizationBehaviourTests
{
    [Fact]
    public async Task Handle_WhenCallerIsAnonymous_ThrowsUnauthorizedException()
    {
        var behaviour = new AuthorizationBehaviour<AssignOperatorToSessionCommand, AssignOperatorToSessionResultDto>(
            CreateCurrentUser(null, null).Object);
        var command = new AssignOperatorToSessionCommand(Guid.NewGuid(), 27);

        var act = async () => await behaviour.Handle(command, () => Task.FromResult(new AssignOperatorToSessionResultDto(command.LiveSessionId, command.OperatorUserId)), CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task Handle_WhenCallerIsOperator_ThrowsForbiddenException()
    {
        var behaviour = new AuthorizationBehaviour<AssignOperatorToSessionCommand, AssignOperatorToSessionResultDto>(
            CreateCurrentUser("27", "Operator").Object);
        var command = new AssignOperatorToSessionCommand(Guid.NewGuid(), 27);

        var act = async () => await behaviour.Handle(command, () => Task.FromResult(new AssignOperatorToSessionResultDto(command.LiveSessionId, command.OperatorUserId)), CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenAccessException>();
    }

    [Fact]
    public async Task Handle_WhenCallerIsAdministrator_AllowsCommand()
    {
        var behaviour = new AuthorizationBehaviour<AssignOperatorToSessionCommand, AssignOperatorToSessionResultDto>(
            CreateCurrentUser("99", "Administrator").Object);
        var command = new AssignOperatorToSessionCommand(Guid.NewGuid(), 27);

        var result = await behaviour.Handle(
            command,
            () => Task.FromResult(new AssignOperatorToSessionResultDto(command.LiveSessionId, command.OperatorUserId)),
            CancellationToken.None);

        result.AssignedOperatorUserId.Should().Be(27);
    }

    private static Mock<ICurrentUser> CreateCurrentUser(string? id, string? role)
    {
        var currentUser = new Mock<ICurrentUser>();
        currentUser.SetupGet(user => user.Id).Returns(id);
        currentUser.SetupGet(user => user.Role).Returns(role);
        return currentUser;
    }
}

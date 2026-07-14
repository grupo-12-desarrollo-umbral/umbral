using umbral_backend.Application.Common.Behaviours;
using umbral_backend.Application.Common.Exceptions;
using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Application.Common.Security;
using umbral_backend.Application.Sessions.Commands.AddOperativeClue;

namespace umbral_backend.Application.UnitTests.Sessions.Commands.AddOperativeClue;

public sealed class AddOperativeClueAuthorizationBehaviourTests
{
    [Fact]
    public void Command_RequiresOperatorRole()
    {
        var attribute = typeof(AddOperativeClueCommand)
            .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
            .Cast<AuthorizeAttribute>()
            .Should()
            .ContainSingle()
            .Subject;

        attribute.Roles.Should().Be("Operator");
    }

    [Fact]
    public async Task Handle_WhenCallerIsOperator_AllowsCommand()
    {
        var behaviour = new AuthorizationBehaviour<AddOperativeClueCommand, AddOperativeClueResultDto>(
            CreateCurrentUser("external-42", "Operator").Object);
        var command = new AddOperativeClueCommand(Guid.NewGuid(), "Check the clock.", [Guid.NewGuid()]);
        var expected = new AddOperativeClueResultDto([Guid.NewGuid()], command.TeamIds, command.ClueText);

        var result = await behaviour.Handle(
            command,
            () => Task.FromResult(expected),
            CancellationToken.None);

        result.Should().Be(expected);
    }

    [Fact]
    public async Task Handle_WhenCallerIsAdministrator_ThrowsForbiddenAccessException()
    {
        var behaviour = new AuthorizationBehaviour<AddOperativeClueCommand, AddOperativeClueResultDto>(
            CreateCurrentUser("external-99", "Administrator").Object);
        var command = new AddOperativeClueCommand(Guid.NewGuid(), "Check the clock.", [Guid.NewGuid()]);

        var act = async () => await behaviour.Handle(
            command,
            () => Task.FromResult(new AddOperativeClueResultDto([], [], command.ClueText)),
            CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenAccessException>();
    }

    private static Mock<ICurrentUser> CreateCurrentUser(string? id, string? role)
    {
        var currentUser = new Mock<ICurrentUser>();
        currentUser.SetupGet(user => user.Id).Returns(id);
        currentUser.SetupGet(user => user.Role).Returns(role);
        return currentUser;
    }
}

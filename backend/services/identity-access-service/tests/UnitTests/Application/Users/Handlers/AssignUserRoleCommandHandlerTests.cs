using umbral_backend.Application.Users.Commands.AssignUserRole;

namespace umbral_backend.Application.UnitTests.Application.Users.Handlers;

public sealed class AssignUserRoleCommandHandlerTests
{
    [Fact]
    public async Task Handle_DelegatesAssignmentToService()
    {
        var service = new Mock<IUserRoleAssignmentService>();
        var command = new AssignUserRoleCommand(42, "Participant");
        var handler = new AssignUserRoleCommandHandler(service.Object);

        await handler.Handle(command, CancellationToken.None);

        service.Verify(assignment => assignment.AssignAsync(command, It.IsAny<CancellationToken>()), Times.Once);
    }
}

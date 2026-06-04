using umbral_backend.Application.Sessions.DTOs;
using umbral_backend.Application.Sessions.Facades;
using umbral_backend.Application.Sessions.Handlers;
using umbral_backend.Application.Sessions.Queries.GetAssociatedTeamsForSession;

namespace umbral_backend.Application.UnitTests.Sessions.Queries.GetAssociatedTeamsForSession;

public sealed class GetAssociatedTeamsForSessionQueryHandlerTests
{
    [Fact]
    public async Task Handle_DelegatesToFacade()
    {
        var facade = new Mock<ISessionTeamAssociationFacade>();
        var query = new GetAssociatedTeamsForSessionQuery(Guid.NewGuid());
        var expected = new SessionAssociatedTeamsDto(query.LiveSessionId, []);

        facade
            .Setup(instance => instance.GetAssociatedTeamsAsync(query, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var handler = new GetAssociatedTeamsForSessionQueryHandler(facade.Object);

        var result = await handler.Handle(query, CancellationToken.None);

        result.Should().Be(expected);
    }
}

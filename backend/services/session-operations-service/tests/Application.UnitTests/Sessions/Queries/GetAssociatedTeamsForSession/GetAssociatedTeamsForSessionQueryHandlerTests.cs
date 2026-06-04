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
        var query = new GetAssociatedTeamsForSessionQuery(Guid.NewGuid());
        var expected = new SessionAssociatedTeamsDto(
            query.LiveSessionId,
            new List<AssociatedSessionTeamDto>
            {
                new(Guid.NewGuid(), Guid.NewGuid(), "Alpha", "A-01", "Open")
            });
        var facade = new Mock<ISessionTeamAssociationFacade>();
        facade
            .Setup(service => service.GetAssociatedTeamsAsync(query, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var handler = new GetAssociatedTeamsForSessionQueryHandler(facade.Object);

        var result = await handler.Handle(query, CancellationToken.None);

        result.Should().Be(expected);
        facade.Verify(service => service.GetAssociatedTeamsAsync(query, It.IsAny<CancellationToken>()), Times.Once);
    }
}

using umbral_backend.Application.Missions.DTOs;
using umbral_backend.Application.Missions.Handlers;
using umbral_backend.Application.Missions.Queries.GetMissionCatalog;
using umbral_backend.Application.UnitTests.Application.Missions.TestDoubles;

namespace umbral_backend.Application.UnitTests.Application.Missions.Handlers;

public sealed class GetMissionCatalogQueryHandlerTests
{
    [Fact]
    public async Task Handle_ReturnsMissionCatalog()
    {
        var catalog = new[]
        {
            new MissionSummaryDto(1, "Mission One", "Briefing One", "Advanced", "Draft"),
            new MissionSummaryDto(2, "Mission Two", "Briefing Two", "Beginner", "Inactive")
        };
        var repository = new InMemoryMissionReadModelRepository(catalog);
        var handler = new GetMissionCatalogQueryHandler(repository);

        var result = await handler.Handle(new GetMissionCatalogQuery(), CancellationToken.None);

        result.Should().BeEquivalentTo(catalog);
    }
}

using umbral_backend.Application.Common.Exceptions;
using umbral_backend.Application.Missions.DTOs;
using umbral_backend.Application.Missions.Handlers;
using umbral_backend.Application.Missions.Queries.GetMissionDetail;
using umbral_backend.Application.UnitTests.Application.Missions.TestDoubles;

namespace umbral_backend.Application.UnitTests.Application.Missions.Handlers;

public sealed class GetMissionDetailQueryHandlerTests
{
    [Fact]
    public async Task Handle_WhenMissionExists_ReturnsMissionDetail()
    {
        var mission = new MissionDto(7, "Mission", "Briefing", "Advanced", 45, "Draft");
        var repository = new InMemoryMissionReadModelRepository(
            details: new Dictionary<int, MissionDto> { [mission.Id] = mission });
        var handler = new GetMissionDetailQueryHandler(repository);

        var result = await handler.Handle(new GetMissionDetailQuery(mission.Id), CancellationToken.None);

        result.Should().Be(mission);
    }

    [Fact]
    public async Task Handle_WhenMissionDoesNotExist_ThrowsNotFound()
    {
        var handler = new GetMissionDetailQueryHandler(new InMemoryMissionReadModelRepository());

        var act = () => handler.Handle(new GetMissionDetailQuery(42), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage("Entity \"Mission\" (42) was not found.");
    }
}

using Microsoft.EntityFrameworkCore;
using Moq;
using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Application.Missions.Commands.CreateMission;
using umbral_backend.Application.Missions.Handlers;
using umbral_backend.Domain.Entities;

namespace umbral_backend.Application.UnitTests.Application.Missions.Handlers;

public class CreateMissionCommandHandlerTests
{
    [Fact]
    public async Task Handle_CreatesMissionAndReturnsDraftDto()
    {
        Mission? captured = null;

        var mockSet = new Mock<DbSet<Mission>>();
        mockSet
            .Setup(s => s.Add(It.IsAny<Mission>()))
            .Callback<Mission>(m => captured = m);

        var mockContext = new Mock<IApplicationDbContext>();
        mockContext.Setup(c => c.Missions).Returns(mockSet.Object);
        mockContext
            .Setup(c => c.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var handler = new CreateMissionCommandHandler(mockContext.Object);
        var command = new CreateMissionCommand("Test Mission", "Test Description", "Advanced", 45);

        var result = await handler.Handle(command, CancellationToken.None);

        result.Status.Should().Be("Draft");
        result.Name.Should().Be("Test Mission");
        result.Description.Should().Be("Test Description");
        result.Difficulty.Should().Be("Advanced");
        result.MaximumTimeMinutes.Should().Be(45);
        mockContext.Verify(c => c.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}

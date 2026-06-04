using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Application.Sessions.DTOs;
using umbral_backend.Application.Sessions.Handlers;
using umbral_backend.Application.Sessions.Queries.ListAssignableSessions;

namespace umbral_backend.Application.UnitTests.Sessions.Queries.ListAssignableSessions;

public sealed class ListAssignableSessionsQueryHandlerTests
{
    [Fact]
    public async Task Handle_ReturnsRepositoryProjection()
    {
        var expected = new[]
        {
            new SessionOperatorSummaryDto(
                Guid.NewGuid(),
                "SES-123456",
                "Museum Hunt",
                "Scheduled",
                27,
                new DateTimeOffset(2026, 6, 4, 10, 0, 0, TimeSpan.Zero))
        };

        var repository = new Mock<ILiveSessionRepository>();
        repository
            .Setup(repo => repo.ListAssignableSummariesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var handler = new ListAssignableSessionsQueryHandler(repository.Object);

        var result = await handler.Handle(new ListAssignableSessionsQuery(), CancellationToken.None);

        result.Should().BeEquivalentTo(expected);
        repository.Verify(repo => repo.ListAssignableSummariesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}

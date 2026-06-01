using umbral_backend.Application.Trivias.DTOs;
using umbral_backend.Application.Trivias.Handlers;
using umbral_backend.Application.Trivias.Queries.GetTriviaCatalog;
using umbral_backend.Application.UnitTests.Application.Trivias.TestDoubles;

namespace umbral_backend.Application.UnitTests.Application.Trivias.Handlers;

public sealed class GetTriviaCatalogQueryHandlerTests
{
    [Fact]
    public async Task Handle_ReturnsTriviaCatalog()
    {
        TriviaQuizSummaryDto[] catalog =
        [
            new TriviaQuizSummaryDto(1, "Quiz One", "Warm-up", "Draft"),
            new TriviaQuizSummaryDto(2, "Quiz Two", "Advanced", "Published")
        ];

        var repository = new InMemoryTriviaQuizReadModelRepository(catalog);
        var handler = new GetTriviaCatalogQueryHandler(repository);

        var result = await handler.Handle(new GetTriviaCatalogQuery(), CancellationToken.None);

        result.Should().BeEquivalentTo(catalog);
    }
}

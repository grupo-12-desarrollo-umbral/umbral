using umbral_backend.Domain.Enums;

namespace umbral_backend.Application.UnitTests.Domain.Enums;

public class TriviaQuizStatusTests
{
    [Fact]
    public void TriviaQuizStatus_DefinesDraftPublishedAndArchivedStates()
    {
        Enum.GetValues<TriviaQuizStatus>().Should().ContainInOrder(
            TriviaQuizStatus.Draft,
            TriviaQuizStatus.Published,
            TriviaQuizStatus.Archived);
    }
}

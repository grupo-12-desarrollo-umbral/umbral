using umbral_backend.Domain.Exceptions;

namespace umbral_backend.ScoringMonitoring.UnitTests;

public sealed class ScoreEntryIsAppendOnlyExceptionTests
{
    [Fact]
    public void Exception_ShouldExplainAppendOnlyInvariant()
    {
        var exception = new ScoreEntryIsAppendOnlyException();

        exception.Message.Should().Contain("append-only");
    }
}

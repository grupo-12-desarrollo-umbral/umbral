using umbral_backend.Domain.Enums;
using umbral_backend.Domain.Exceptions;
using umbral_backend.Domain.ValueObjects;

namespace umbral_backend.SessionOperations.UnitTests.Domain.ValueObjects;

public sealed class SessionSourceTests
{
    [Fact]
    public void Create_WithEmptySourceId_ThrowsException()
    {
        var act = () => SessionSource.Create(SessionSourceType.Mission, Guid.Empty);

        act.Should().Throw<SessionSourceEntityRequiredException>();
    }

    [Fact]
    public void Create_WithTriviaSourceType_ThrowsException()
    {
        var act = () => SessionSource.Create(SessionSourceType.TriviaQuiz, Guid.NewGuid());

        act.Should().Throw<SessionSourceTriviaQuizIdRequiredException>();
    }

    [Fact]
    public void CreateTriviaQuiz_WithNonPositiveId_ThrowsException()
    {
        var act = () => SessionSource.CreateTriviaQuiz(0);

        act.Should().Throw<SessionSourceTriviaQuizIdRequiredException>();
    }

    [Fact]
    public void CreateTriviaQuiz_PreservesExplicitIntegerIdentity()
    {
        var source = SessionSource.CreateTriviaQuiz(42);

        source.SourceType.Should().Be(SessionSourceType.TriviaQuiz);
        source.SourceTriviaQuizId.Should().Be(42);
        source.SourceEntityId.Should().Be(Guid.Empty);
    }
}

using umbral_backend.Domain.Exceptions;
using umbral_backend.Domain.ValueObjects;
using umbral_backend.SessionOperations.UnitTests.Domain.TestData;

namespace umbral_backend.SessionOperations.UnitTests.Domain.ValueObjects;

public sealed class TriviaQuestionSnapshotTests
{
    [Fact]
    public void Create_WithFewerThanTwoOptions_ThrowsException()
    {
        var act = () => TriviaQuestionSnapshot.Create(
            "What is the closest planet to the Sun?",
            1,
            100,
            30,
            "Mercury is the closest planet.",
            [TriviaOptionSnapshot.Create("Mercury", 1, true)]);

        act.Should().Throw<TriviaQuestionSnapshotRequiresAtLeastTwoOptionsException>();
    }

    [Fact]
    public void Create_WithoutCorrectOption_ThrowsException()
    {
        var act = () => TriviaQuestionSnapshot.Create(
            "What is the closest planet to the Sun?",
            1,
            100,
            30,
            "Mercury is the closest planet.",
            [
                TriviaOptionSnapshot.Create("Mercury", 1, false),
                TriviaOptionSnapshot.Create("Venus", 2, false)
            ]);

        act.Should().Throw<TriviaQuestionSnapshotRequiresCorrectOptionException>();
    }

    [Fact]
    public void Create_CopiesOptionsImmutably()
    {
        var options = TriviaSessionSnapshotFactory.CreateOptions().ToList();

        var question = TriviaQuestionSnapshot.Create(
            "What is the closest planet to the Sun?",
            1,
            100,
            30,
            "Mercury is the closest planet.",
            options);

        options.Add(TriviaOptionSnapshot.Create("Earth", 3, false));

        question.Options.Should().HaveCount(2);
    }
}

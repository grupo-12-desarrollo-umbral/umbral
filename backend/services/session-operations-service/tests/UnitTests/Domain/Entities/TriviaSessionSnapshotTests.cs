using umbral_backend.Domain.Entities;
using umbral_backend.Domain.Exceptions;
using umbral_backend.Domain.ValueObjects;
using umbral_backend.SessionOperations.UnitTests.Domain.TestData;

namespace umbral_backend.SessionOperations.UnitTests.Domain.Entities;

public sealed class TriviaSessionSnapshotTests
{
    [Fact]
    public void Create_WithoutQuestions_ThrowsException()
    {
        var act = () => TriviaSessionSnapshot.Create("Foundations of Science", []);

        act.Should().Throw<TriviaSessionSnapshotMustContainQuestionsException>();
    }

    [Fact]
    public void Create_CopiesQuestionsImmutably()
    {
        var questions = new List<TriviaQuestionSnapshot>
        {
            TriviaSessionSnapshotFactory.CreateQuestion()
        };

        var snapshot = TriviaSessionSnapshot.Create("Foundations of Science", questions);
        questions.Add(TriviaSessionSnapshotFactory.CreateQuestion(sequenceOrder: 2));

        snapshot.Questions.Should().ContainSingle();
    }
}

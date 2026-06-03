using umbral_backend.Domain.Entities;
using umbral_backend.Domain.ValueObjects;

namespace umbral_backend.SessionOperations.UnitTests.Domain.TestData;

internal static class TriviaSessionSnapshotFactory
{
    internal static TriviaSessionSnapshot CreateSingleQuestion()
    {
        return TriviaSessionSnapshot.Create("Foundations of Science", [CreateQuestion()]);
    }

    internal static TriviaQuestionSnapshot CreateQuestion(int sequenceOrder = 1)
    {
        return TriviaQuestionSnapshot.Create(
            "What is the closest planet to the Sun?",
            sequenceOrder,
            100,
            30,
            "Mercury is the closest planet.",
            CreateOptions());
    }

    internal static IReadOnlyCollection<TriviaOptionSnapshot> CreateOptions()
    {
        return
        [
            TriviaOptionSnapshot.Create("Mercury", 1, true),
            TriviaOptionSnapshot.Create("Venus", 2, false)
        ];
    }
}

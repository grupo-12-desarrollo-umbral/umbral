using umbral_backend.Application.Sessions.Common;
using umbral_backend.Domain.Entities;
using umbral_backend.Domain.ValueObjects;

namespace umbral_backend.Application.UnitTests.Sessions.Common;

public sealed class TriviaQuestionSnapshotSelectorTests
{
    [Fact]
    public void GetOrderedTriviaQuestion_OrdersQuestionsAndOptionsBySequenceOrder()
    {
        // Both questions and options are stored out of SequenceOrder so a naive read
        // by storage position would return the wrong question / option order.
        var substage = SubstageSnapshot.CreateTrivia("Trivia Round", 1);
        var firstQuestion = TriviaQuestionSnapshot.Create(
            substage.SubstageSnapshotId,
            "First",
            sequenceOrder: 1,
            scoreValue: 100,
            timeLimitSeconds: 30,
            explanation: null,
            options:
            [
                TriviaOptionSnapshot.Create("Beta", 2, false),
                TriviaOptionSnapshot.Create("Alpha", 1, true)
            ]);
        var secondQuestion = TriviaQuestionSnapshot.Create(
            substage.SubstageSnapshotId,
            "Second",
            sequenceOrder: 2,
            scoreValue: 100,
            timeLimitSeconds: 30,
            explanation: null,
            options:
            [
                TriviaOptionSnapshot.Create("Delta", 2, false),
                TriviaOptionSnapshot.Create("Gamma", 1, true)
            ]);
        var runtimeSnapshot = MissionRuntimeSnapshot.Create(
            Guid.NewGuid(),
            "Foundations of Science",
            MaximumTime.Create(10),
            [StageSnapshot.Create("Stage One", 1, [substage])],
            [],
            [secondQuestion, firstQuestion]);
        var session = LiveSession.Create(
            SessionSource.Create(runtimeSnapshot.SourceMissionId),
            "tri-123",
            "Trivia Session",
            10,
            new DateTimeOffset(2026, 6, 3, 10, 0, 0, TimeSpan.Zero),
            runtimeSnapshot);

        var (question, options) = TriviaQuestionSnapshotSelector.GetOrderedTriviaQuestion(session, 0);

        question.SequenceOrder.Should().Be(1);
        question.Prompt.Should().Be("First");
        options.Should().Equal("Alpha", "Beta");
    }
}

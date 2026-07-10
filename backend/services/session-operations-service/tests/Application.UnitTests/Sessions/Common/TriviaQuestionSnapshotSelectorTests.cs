using umbral_backend.Application.Sessions.Common;
using umbral_backend.Domain.Entities;
using umbral_backend.Domain.Enums;
using umbral_backend.Domain.Services;
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
        // Selection is scoped to the active substage, so the session must be Active with the
        // substage pointer set before reading a question.
        var policy = new SessionStateTransitionPolicy();
        var startedAt = new DateTimeOffset(2026, 6, 3, 11, 0, 0, TimeSpan.Zero);
        session.AssociateTeam(Guid.NewGuid(), "Red", "RED-01", 4);
        session.MoveTo(SessionState.Preparing, startedAt, policy);
        session.MoveTo(SessionState.Active, startedAt, policy);

        var (question, options) = TriviaQuestionSnapshotSelector.GetOrderedTriviaQuestion(session, 0);

        question.SequenceOrder.Should().Be(1);
        question.Prompt.Should().Be("First");
        options.Should().Equal("Alpha", "Beta");
    }

    [Fact]
    public void GetOrderedTriviaQuestion_FiltersQuestionsToActiveSubstage()
    {
        var firstSubstage = SubstageSnapshot.CreateTrivia("Round One", 1);
        var secondSubstage = SubstageSnapshot.CreateTrivia("Round Two", 2);
        var firstSubstageQuestion = TriviaQuestionSnapshot.Create(
            firstSubstage.SubstageSnapshotId,
            "First substage question",
            sequenceOrder: 1,
            scoreValue: 100,
            timeLimitSeconds: 30,
            explanation: null,
            options:
            [
                TriviaOptionSnapshot.Create("A", 1, true),
                TriviaOptionSnapshot.Create("B", 2, false)
            ]);
        var secondSubstageQuestion = TriviaQuestionSnapshot.Create(
            secondSubstage.SubstageSnapshotId,
            "Second substage question",
            sequenceOrder: 1,
            scoreValue: 100,
            timeLimitSeconds: 30,
            explanation: null,
            options:
            [
                TriviaOptionSnapshot.Create("C", 1, true),
                TriviaOptionSnapshot.Create("D", 2, false)
            ]);
        var runtimeSnapshot = MissionRuntimeSnapshot.Create(
            Guid.NewGuid(),
            "Foundations of Science",
            MaximumTime.Create(10),
            [StageSnapshot.Create("Stage One", 1, [firstSubstage, secondSubstage])],
            [],
            [secondSubstageQuestion, firstSubstageQuestion]);
        var session = LiveSession.Create(
            SessionSource.Create(runtimeSnapshot.SourceMissionId),
            "tri-456",
            "Trivia Session",
            10,
            new DateTimeOffset(2026, 6, 3, 10, 0, 0, TimeSpan.Zero),
            runtimeSnapshot);
        var policy = new SessionStateTransitionPolicy();
        var startedAt = new DateTimeOffset(2026, 6, 3, 11, 0, 0, TimeSpan.Zero);
        session.AssociateTeam(Guid.NewGuid(), "Red", "RED-02", 4);
        session.MoveTo(SessionState.Preparing, startedAt, policy);
        session.MoveTo(SessionState.Active, startedAt, policy);
        session.ActivateQuestion(0, startedAt);

        var (question, _) = TriviaQuestionSnapshotSelector.GetOrderedTriviaQuestion(session, 0);

        question.SubstageSnapshotId.Should().Be(firstSubstage.SubstageSnapshotId);
        question.Prompt.Should().Be("First substage question");
    }

    [Fact]
    public void GetOrderedTriviaQuestion_WhenSecondSubstageIsActive_SkipsEarlierSubstageQuestions()
    {
        var session = LiveSessionTestFactory.CreateScheduledMultiSubstageTrivia();
        var policy = new SessionStateTransitionPolicy();
        var startedAt = new DateTimeOffset(2026, 6, 3, 11, 0, 0, TimeSpan.Zero);
        session.AssociateTeam(Guid.NewGuid(), "Red", "RED-03", 4);
        session.MoveTo(SessionState.Preparing, startedAt.AddMinutes(-1), policy);
        session.MoveTo(SessionState.Active, startedAt, policy);
        var substages = session.MissionRuntimeSnapshot.StageSnapshots
            .OrderBy(stage => stage.SequenceOrder)
            .SelectMany(stage => stage.SubstageSnapshots.OrderBy(substage => substage.SequenceOrder))
            .ToArray();
        session.ActivateQuestion(0, startedAt);
        session.CloseActiveQuestion(startedAt.AddSeconds(30));
        session.CompleteActiveSubstageAndAdvance(startedAt.AddSeconds(30), policy);

        var (question, _) = TriviaQuestionSnapshotSelector.GetOrderedTriviaQuestion(session, 0);

        session.ActiveSubstageId.Should().Be(substages[1].SubstageSnapshotId);
        question.SubstageSnapshotId.Should().Be(substages[1].SubstageSnapshotId);
    }
}

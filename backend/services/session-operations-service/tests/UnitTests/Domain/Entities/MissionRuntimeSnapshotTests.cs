using umbral_backend.Domain.Entities;
using umbral_backend.Domain.Exceptions;
using umbral_backend.Domain.ValueObjects;
using umbral_backend.SessionOperations.UnitTests.Domain.TestData;

namespace umbral_backend.SessionOperations.UnitTests.Domain.Entities;

public sealed class MissionRuntimeSnapshotTests
{
    [Fact]
    public void Create_WithoutStages_ThrowsException()
    {
        var act = () => MissionRuntimeSnapshot.Create(
            Guid.NewGuid(),
            "Runtime Plan",
            MaximumTime.Create(30),
            [],
            [],
            []);

        act.Should().Throw<MissionRuntimeSnapshotMustContainStagesException>();
    }

    [Fact]
    public void Create_WithDuplicateTargetQrCodes_ThrowsException()
    {
        var treasureSubstage = SubstageSnapshot.CreateTreasureHunt("Treasure Route", 1, winnerScore: 100);
        var stage = StageSnapshot.Create("Stage One", 1, [treasureSubstage]);

        var act = () => MissionRuntimeSnapshot.Create(
            Guid.NewGuid(),
            "Runtime Plan",
            MaximumTime.Create(30),
            [stage],
            [
                MissionRuntimeSnapshotFactory.CreateTarget(treasureSubstage.SubstageSnapshotId, qrCode: "QR-001"),
                MissionRuntimeSnapshotFactory.CreateTarget(treasureSubstage.SubstageSnapshotId, qrCode: "QR-001", sequenceOrder: 2)
            ],
            []);

        act.Should().Throw<MissionRuntimeSnapshotTargetQrCodesMustBeUniqueException>();
    }

    [Fact]
    public void Create_TreasureHuntSubstageWithoutTarget_ThrowsException()
    {
        var treasureSubstage = SubstageSnapshot.CreateTreasureHunt("Treasure Route", 1, winnerScore: 100);
        var stage = StageSnapshot.Create("Stage One", 1, [treasureSubstage]);

        var act = () => MissionRuntimeSnapshot.Create(
            Guid.NewGuid(),
            "Runtime Plan",
            MaximumTime.Create(30),
            [stage],
            [],
            []);

        act.Should().Throw<TreasureHuntSubstageSnapshotMustContainTargetsException>();
    }

    [Fact]
    public void Create_TriviaSubstageWithoutQuestions_ThrowsException()
    {
        var triviaSubstage = SubstageSnapshot.CreateTrivia("Trivia Round", 1);
        var stage = StageSnapshot.Create("Stage One", 1, [triviaSubstage]);

        var act = () => MissionRuntimeSnapshot.Create(
            Guid.NewGuid(),
            "Runtime Plan",
            MaximumTime.Create(30),
            [stage],
            [],
            []);

        act.Should().Throw<TriviaSubstageSnapshotMustContainQuestionsException>();
    }

    [Fact]
    public void Create_CopiesCollectionsImmutably()
    {
        var treasureSubstage = SubstageSnapshot.CreateTreasureHunt("Treasure Route", 1, winnerScore: 100);
        var triviaSubstage = SubstageSnapshot.CreateTrivia("Trivia Round", 2);
        var stages = new List<StageSnapshot> { StageSnapshot.Create("Stage One", 1, [treasureSubstage, triviaSubstage]) };
        var targets = new List<TargetSnapshot> { MissionRuntimeSnapshotFactory.CreateTarget(treasureSubstage.SubstageSnapshotId) };
        var questions = MissionRuntimeSnapshotFactory.CreateQuestions(triviaSubstage.SubstageSnapshotId, 1).ToList();

        var snapshot = MissionRuntimeSnapshot.Create(
            Guid.NewGuid(),
            "Runtime Plan",
            MaximumTime.Create(30),
            stages,
            targets,
            questions);

        stages.Clear();
        targets.Clear();
        questions.Clear();

        snapshot.StageSnapshots.Should().ContainSingle();
        snapshot.TargetSnapshots.Should().ContainSingle();
        snapshot.TriviaQuestionSnapshots.Should().ContainSingle();
    }
}

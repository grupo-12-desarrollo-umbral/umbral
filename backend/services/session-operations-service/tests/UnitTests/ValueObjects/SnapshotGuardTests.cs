using umbral_backend.Domain.Entities;
using umbral_backend.Domain.Enums;
using umbral_backend.Domain.Exceptions;
using umbral_backend.Domain.ValueObjects;

namespace umbral_backend.Domain.UnitTests.ValueObjects;

// Covers the construction-guard and equality branches of the runtime-snapshot value objects that the
// factory's always-valid happy path never reaches: empty/out-of-order collections, empty ids,
// null optional clue fields, timer range checks, and GetEqualityComponents enumeration.
public sealed class SnapshotGuardTests
{
    private static SubstageSnapshot TriviaSubstage(int order = 1) =>
        SubstageSnapshot.CreateTrivia($"Round {order}", order);

    private static TriviaQuestionSnapshot Question(Guid substageId, int order = 1) =>
        TriviaQuestionSnapshot.Create(
            substageId, "Prompt?", order, 100, 30, null,
            [TriviaOptionSnapshot.Create("A", 1, true), TriviaOptionSnapshot.Create("B", 2, false)]);

    // ── AuthoritativeSessionTimerSnapshot ────────────────────────────────────
    [Fact]
    public void Timer_NegativeTotal_Throws()
    {
        var act = () => AuthoritativeSessionTimerSnapshot.Create(
            TimeSpan.FromSeconds(-1), TimeSpan.Zero, false, DateTimeOffset.UtcNow, null, null);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Timer_NegativeRemaining_Throws()
    {
        var act = () => AuthoritativeSessionTimerSnapshot.Create(
            TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(-1), false, DateTimeOffset.UtcNow, null, null);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Timer_RemainingExceedsTotal_Throws()
    {
        var act = () => AuthoritativeSessionTimerSnapshot.Create(
            TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(31), false, DateTimeOffset.UtcNow, null, null);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Timer_Valid_EqualByValue()
    {
        var now = DateTimeOffset.UtcNow;
        var a = AuthoritativeSessionTimerSnapshot.Create(TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(10), true, now, now, null);
        var b = AuthoritativeSessionTimerSnapshot.Create(TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(10), true, now, now, null);

        a.Should().Be(b);
        a.GetHashCode().Should().Be(b.GetHashCode());
    }

    // ── StageSnapshot ────────────────────────────────────────────────────────
    [Fact]
    public void Stage_NullSubstages_ThrowsMustContainSubstages()
    {
        var act = () => StageSnapshot.Create("Stage", 1, null!);
        act.Should().Throw<StageSnapshotMustContainSubstagesException>();
    }

    [Fact]
    public void Stage_EfConstructorGuard_WhenIdIsEmpty_ThrowsRequiredException()
    {
        var act = () => InvokePrivateConstructor<StageSnapshot>(
            Guid.Empty,
            "Stage",
            1,
            new[] { TriviaSubstage() });

        act.Should().Throw<StageSnapshotIdRequiredException>();
    }

    [Fact]
    public void Stage_SubstagesOutOfOrder_Throws()
    {
        var act = () => StageSnapshot.Create("Stage", 1, [TriviaSubstage(1), TriviaSubstage(3)]);
        act.Should().Throw<MissionRuntimeSnapshotSubstageOrderInvalidException>();
    }

    [Fact]
    public void Stage_Valid_EqualByValueEnumeratesComponents()
    {
        var substage = TriviaSubstage(1);
        var a = StageSnapshot.Create("Stage", 1, [substage]);

        a.Should().Be(a);                 // forces GetEqualityComponents over the substage list
        a.SubstageSnapshots.Should().ContainSingle();
    }

    // ── TriviaQuestionSnapshot ───────────────────────────────────────────────
    [Fact]
    public void Question_EmptySubstageId_Throws()
    {
        var act = () => Question(Guid.Empty);
        act.Should().Throw<SubstageSnapshotIdRequiredException>();
    }

    [Fact]
    public void Question_FewerThanTwoOptions_Throws()
    {
        var substageId = TriviaSubstage().SubstageSnapshotId;
        var act = () => TriviaQuestionSnapshot.Create(
            substageId, "Prompt?", 1, 100, 30, null,
            [TriviaOptionSnapshot.Create("A", 1, true)]);
        act.Should().Throw<TriviaQuestionSnapshotRequiresAtLeastTwoOptionsException>();
    }

    [Fact]
    public void Question_NullOptions_ThrowsRequiresAtLeastTwoOptions()
    {
        var substageId = TriviaSubstage().SubstageSnapshotId;
        // Null options exercises the `options?.ToArray() ?? []` null-coalescing arm → empty → < 2.
        var act = () => TriviaQuestionSnapshot.Create(substageId, "Prompt?", 1, 100, 30, null, null!);
        act.Should().Throw<TriviaQuestionSnapshotRequiresAtLeastTwoOptionsException>();
    }

    [Fact]
    public void Question_NoCorrectOption_Throws()
    {
        var substageId = TriviaSubstage().SubstageSnapshotId;
        var act = () => TriviaQuestionSnapshot.Create(
            substageId, "Prompt?", 1, 100, 30, null,
            [TriviaOptionSnapshot.Create("A", 1, false), TriviaOptionSnapshot.Create("B", 2, false)]);
        act.Should().Throw<TriviaQuestionSnapshotRequiresCorrectOptionException>();
    }

    [Fact]
    public void Question_Valid_EqualByValue()
    {
        var substageId = TriviaSubstage().SubstageSnapshotId;
        var question = Question(substageId, 1);

        question.Should().Be(question);   // enumerates option components
        question.Options.Should().HaveCount(2);
    }

    // ── TargetSnapshot ───────────────────────────────────────────────────────
    [Fact]
    public void Target_NullClueFields_NormalizeToNull()
    {
        var target = TargetSnapshot.Create(
            Guid.NewGuid(), "Name", "QR", 1, isActive: true, score: 100,
 latitude: 4.711,
 longitude: -74.0721,
            clueText: null, clueVisibilityPolicy: null);

        target.ClueText.Should().BeNull();
        target.ClueVisibilityPolicy.Should().BeNull();
    }

    [Fact]
    public void Substage_EfConstructorGuard_WhenIdIsEmpty_ThrowsRequiredException()
    {
        var act = () => InvokePrivateConstructor<SubstageSnapshot>(
            Guid.Empty,
            "Round",
            1,
            SubstagePlayMode.Trivia);

        act.Should().Throw<SubstageSnapshotIdRequiredException>();
    }

    [Fact]
    public void Target_EfConstructorGuard_WhenIdIsEmpty_ThrowsRequiredException()
    {
        var act = () => InvokePrivateConstructor<TargetSnapshot>(
            Guid.Empty,
            Guid.NewGuid(),
            "Target",
            "QR",
            1,
            true,
            100,
            4.711,
            -74.0721,
            null,
            null);

        act.Should().Throw<TargetSnapshotIdRequiredException>();
    }

    [Fact]
    public void Target_WithClueFields_AreTrimmed()
    {
        var target = TargetSnapshot.Create(
            Guid.NewGuid(), "Name", "QR", 1, isActive: true, score: 100,
 latitude: 4.711,
 longitude: -74.0721,
            clueText: "  hint  ", clueVisibilityPolicy: "  VisibleAtStart  ");

        target.ClueText.Should().Be("hint");
        target.ClueVisibilityPolicy.Should().Be("VisibleAtStart");
    }

    // ── ClueSnapshot ─────────────────────────────────────────────────────────
    [Fact]
    public void Clue_EmptySubstageId_Throws()
    {
        var act = () => ClueSnapshot.Create(Guid.Empty, "Hint", "VisibleWhenSubstageStarts", 1);
        act.Should().Throw<ClueSnapshotSubstageRequiredException>();
    }

    [Fact]
    public void Clue_EfConstructorGuard_WhenIdIsEmpty_ThrowsRequiredException()
    {
        var act = () => InvokePrivateConstructor<ClueSnapshot>(
            Guid.Empty,
            Guid.NewGuid(),
            "Hint",
            "VisibleWhenSubstageStarts",
            1);

        act.Should().Throw<ClueSnapshotIdRequiredException>();
    }

    [Fact]
    public void Clue_TrimsTextAndPolicy_AndHonorsVisiblePolicy()
    {
        var visible = ClueSnapshot.Create(Guid.NewGuid(), "  Hint  ", "  VisibleWhenSubstageStarts  ", 1);
        visible.Text.Should().Be("Hint");
        visible.VisibilityPolicy.Should().Be("VisibleWhenSubstageStarts");
        visible.IsVisibleWhenSubstageStarts.Should().BeTrue();

        var hidden = ClueSnapshot.Create(Guid.NewGuid(), "Hint", "HiddenUntilOperatorRelease", 2);
        hidden.IsVisibleWhenSubstageStarts.Should().BeFalse();
        hidden.IsHiddenUntilOperatorRelease.Should().BeTrue();
    }

    [Fact]
    public void Clue_NullTextAndPolicy_NormalizeToEmpty()
    {
        var clue = ClueSnapshot.Create(Guid.NewGuid(), null!, null!, 1);
        clue.Text.Should().BeEmpty();
        clue.VisibilityPolicy.Should().BeEmpty();
        clue.IsVisibleWhenSubstageStarts.Should().BeFalse();
    }

    [Fact]
    public void Clue_Valid_EqualByValue()
    {
        var substageId = Guid.NewGuid();
        var id = Guid.NewGuid();
        var a = (ClueSnapshot)InvokePrivateConstructor<ClueSnapshot>(id, substageId, "Hint", "VisibleWhenSubstageStarts", 1);
        var b = (ClueSnapshot)InvokePrivateConstructor<ClueSnapshot>(id, substageId, "Hint", "VisibleWhenSubstageStarts", 1);

        a.Should().Be(b);
        a.GetHashCode().Should().Be(b.GetHashCode());
    }

    [Fact]
    public void ActiveSubstageContext_WithTargets_IsEqualByEveryTargetValue()
    {
        var substageId = Guid.NewGuid();
        var targetId = Guid.NewGuid();
        var first = ActiveSubstageContext.CreateTreasureHunt(
            substageId,
            "Treasure Route",
            resolvedTargets: 0,
            [new ActiveSubstageTarget(targetId, "Main Exhibit", 1, true)]);
        var second = ActiveSubstageContext.CreateTreasureHunt(
            substageId,
            "Treasure Route",
            resolvedTargets: 0,
            [new ActiveSubstageTarget(targetId, "Main Exhibit", 1, true)]);

        first.Should().Be(second);
        first.GetHashCode().Should().Be(second.GetHashCode());
    }

    // ── MissionRuntimeSnapshot ───────────────────────────────────────────────
    [Fact]
    public void Mission_EmptySourceId_Throws()
    {
        var substage = TriviaSubstage();
        var act = () => MissionRuntimeSnapshot.Create(
            Guid.Empty, "Title", MaximumTime.Create(10),
            [StageSnapshot.Create("Stage", 1, [substage])], [], [Question(substage.SubstageSnapshotId)]);
        act.Should().Throw<SessionSourceEntityRequiredException>();
    }

    [Fact]
    public void Mission_NullStages_ThrowsMustContainStages()
    {
        var act = () => MissionRuntimeSnapshot.Create(
            Guid.NewGuid(), "Title", MaximumTime.Create(10), null!, null!, null!);
        act.Should().Throw<MissionRuntimeSnapshotMustContainStagesException>();
    }

    [Fact]
    public void Mission_StagesOutOfOrder_Throws()
    {
        var s1 = TriviaSubstage(1);
        var s2 = TriviaSubstage(1);
        var act = () => MissionRuntimeSnapshot.Create(
            Guid.NewGuid(), "Title", MaximumTime.Create(10),
            [StageSnapshot.Create("Stage 1", 1, [s1]), StageSnapshot.Create("Stage 2", 3, [s2])],
            [],
            [Question(s1.SubstageSnapshotId), Question(s2.SubstageSnapshotId)]);
        act.Should().Throw<MissionRuntimeSnapshotStageOrderInvalidException>();
    }

    [Fact]
    public void LiveSessionStateFactory_WhenStateIsUnknown_ThrowsOutOfRange()
    {
        var factoryType = typeof(LiveSession).Assembly.GetType(
            "umbral_backend.Domain.Services.SessionStates.LiveSessionStateFactory")!;
        var method = factoryType.GetMethod(
            "For",
            System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)!;

        var act = () => method.Invoke(null, [(SessionState)999]);

        act.Should().Throw<System.Reflection.TargetInvocationException>()
            .WithInnerException<ArgumentOutOfRangeException>();
    }

    private static T InvokePrivateConstructor<T>(params object?[] parameters)
    {
        try
        {
            return (T)Activator.CreateInstance(
                typeof(T),
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic,
                binder: null,
                args: parameters,
                culture: null)!;
        }
        catch (System.Reflection.TargetInvocationException exception) when (exception.InnerException is not null)
        {
            throw exception.InnerException;
        }
    }
}

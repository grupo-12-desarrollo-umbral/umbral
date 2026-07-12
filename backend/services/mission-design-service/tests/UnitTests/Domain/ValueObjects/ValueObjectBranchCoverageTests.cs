using umbral_backend.Domain.Common;
using umbral_backend.Domain.Exceptions;
using umbral_backend.Domain.ValueObjects;

namespace umbral_backend.Application.UnitTests.Domain.ValueObjects;

public sealed class ValueObjectBranchCoverageTests
{
    // ── ValueObject.EqualOperator branches ────────────────────────────────────

    [Fact]
    public void EqualOperator_WhenLeftIsNullAndRightIsNot_ReturnsFalse()
    {
        Difficulty? left = null;
        var right = Difficulty.Create("Advanced");

        (left! == right).Should().BeFalse();
    }

    [Fact]
    public void EqualOperator_WhenRightIsNullAndLeftIsNot_ReturnsFalse()
    {
        var left = Difficulty.Create("Advanced");
        Difficulty? right = null;

        (left == right!).Should().BeFalse();
    }

    [Fact]
    public void EqualOperator_WhenBothAreSameInstance_ReturnsTrue()
    {
        var left = Difficulty.Create("Advanced");

        (left == left).Should().BeTrue();
    }

    [Fact]
    public void NotEqualOperator_WhenDifferent_ReturnsTrue()
    {
        var a = Difficulty.Create("Advanced");
        var b = Difficulty.Create("Beginner");

        (a != b).Should().BeTrue();
    }

    [Fact]
    public void NotEqualOperator_WhenSame_ReturnsFalse()
    {
        var a = Difficulty.Create("Advanced");
        var b = Difficulty.Create("Advanced");

        (a != b).Should().BeFalse();
    }

    // ── Difficulty.ScoreFactor for invalid value ──────────────────────────────

    [Fact]
    public void ScoreFactor_WhenDifficultyValueIsNotInAllowedList_ThrowsInvalidDifficultyValueException()
    {
        // Create a valid difficulty, then mutate its Value to an invalid one
        var difficulty = Difficulty.Create("Beginner");
        typeof(Difficulty)
            .GetProperty(nameof(Difficulty.Value))!
            .SetValue(difficulty, "SuperHard");

        var act = () => _ = difficulty.ScoreFactor;

        act.Should().Throw<InvalidDifficultyValueException>();
    }

    [Fact]
    public void Create_WhenValueIsEmpty_ThrowsDifficultyValueRequiredException()
    {
        var act = () => Difficulty.Create(string.Empty);

        act.Should().Throw<DifficultyValueRequiredException>();
    }

    [Fact]
    public void Create_WhenValueIsInvalid_ThrowsInvalidDifficultyValueException()
    {
        var act = () => Difficulty.Create("Expert");

        act.Should().Throw<InvalidDifficultyValueException>();
    }

    // ── MaximumTime equality branches ─────────────────────────────────────────

    [Fact]
    public void MaximumTime_Equals_WhenSameMinutes_ReturnsTrue()
    {
        var a = MaximumTime.Create(30);
        var b = MaximumTime.Create(30);

        a.Equals(b).Should().BeTrue();
    }

    [Fact]
    public void MaximumTime_Equals_WhenDifferentMinutes_ReturnsFalse()
    {
        var a = MaximumTime.Create(30);
        var b = MaximumTime.Create(60);

        a.Equals(b).Should().BeFalse();
    }

    // ── ScoreValue equality branches ──────────────────────────────────────────

    [Fact]
    public void ScoreValue_Equals_WhenSamePoints_ReturnsTrue()
    {
        var a = ScoreValue.Create(50);
        var b = ScoreValue.Create(50);

        a.Equals(b).Should().BeTrue();
    }

    [Fact]
    public void ScoreValue_Equals_WhenDifferentPoints_ReturnsFalse()
    {
        var a = ScoreValue.Create(50);
        var b = ScoreValue.Create(100);

        a.Equals(b).Should().BeFalse();
    }

    [Fact]
    public void ScoreValue_Equals_WhenComparedToNull_ReturnsFalse()
    {
        var score = ScoreValue.Create(50);

        score.Equals(null).Should().BeFalse();
    }

    [Fact]
    public void ScoreValue_Equals_WhenComparedToDifferentType_ReturnsFalse()
    {
        var score = ScoreValue.Create(50);

        score.Equals(50).Should().BeFalse();
    }

    // ── QuestionTimer equality branches ───────────────────────────────────────

    [Fact]
    public void QuestionTimer_Equals_WhenSameSeconds_ReturnsTrue()
    {
        var a = QuestionTimer.Create(30);
        var b = QuestionTimer.Create(30);

        a.Equals(b).Should().BeTrue();
    }

    [Fact]
    public void QuestionTimer_Equals_WhenDifferentSeconds_ReturnsFalse()
    {
        var a = QuestionTimer.Create(30);
        var b = QuestionTimer.Create(60);

        a.Equals(b).Should().BeFalse();
    }

    [Fact]
    public void QuestionTimer_Equals_WhenComparedToNull_ReturnsFalse()
    {
        var timer = QuestionTimer.Create(30);

        timer.Equals(null).Should().BeFalse();
    }

    [Fact]
    public void QuestionTimer_Equals_WhenComparedToDifferentType_ReturnsFalse()
    {
        var timer = QuestionTimer.Create(30);

        timer.Equals(30).Should().BeFalse();
    }

    // ── Difficulty equality operator branches ─────────────────────────────────

    [Fact]
    public void Difficulty_EqualityOperator_WhenBothNull_ReturnsTrue()
    {
        Difficulty? a = null;
        Difficulty? b = null;

        (a! == b!).Should().BeTrue();
    }

    [Fact]
    public void Difficulty_EqualityOperator_WhenLeftNull_ReturnsFalse()
    {
        Difficulty? nil = null;
        var a = Difficulty.Create("Advanced");

        (nil! == a).Should().BeFalse();
    }

    // ── MaximumTime equality operator branches ────────────────────────────────

    [Fact]
    public void MaximumTime_EqualityOperator_WhenBothNull_ReturnsTrue()
    {
        MaximumTime? a = null;
        MaximumTime? b = null;

        (a! == b!).Should().BeTrue();
    }

    [Fact]
    public void MaximumTime_EqualityOperator_WhenLeftNull_ReturnsFalse()
    {
        MaximumTime? nil = null;
        var a = MaximumTime.Create(45);

        (nil! == a).Should().BeFalse();
    }

    // ── ScoreValue equality operator branches ─────────────────────────────────

    [Fact]
    public void ScoreValue_EqualityOperator_WhenBothNull_ReturnsTrue()
    {
        ScoreValue? a = null;
        ScoreValue? b = null;

        (a! == b!).Should().BeTrue();
    }

    [Fact]
    public void ScoreValue_EqualityOperator_WhenLeftNullRightNotNull_ReturnsFalse()
    {
        ScoreValue? nil = null;
        var a = ScoreValue.Create(50);

        (nil! == a).Should().BeFalse();
    }

    [Fact]
    public void ScoreValue_EqualityOperator_WhenRightNullLeftNotNull_ReturnsFalse()
    {
        var a = ScoreValue.Create(50);
        ScoreValue? nil = null;

        (a == nil!).Should().BeFalse();
    }

    [Fact]
    public void ScoreValue_InequalityOperator_WhenDifferent_ReturnsTrue()
    {
        var a = ScoreValue.Create(50);
        var b = ScoreValue.Create(100);

        (a != b).Should().BeTrue();
    }

    // ── QuestionTimer equality operator branches ──────────────────────────────

    [Fact]
    public void QuestionTimer_EqualityOperator_WhenBothNull_ReturnsTrue()
    {
        QuestionTimer? a = null;
        QuestionTimer? b = null;

        (a! == b!).Should().BeTrue();
    }

    [Fact]
    public void QuestionTimer_EqualityOperator_WhenLeftNullRightNotNull_ReturnsFalse()
    {
        QuestionTimer? nil = null;
        var a = QuestionTimer.Create(30);

        (nil! == a).Should().BeFalse();
    }

    [Fact]
    public void QuestionTimer_EqualityOperator_WhenRightNullLeftNotNull_ReturnsFalse()
    {
        var a = QuestionTimer.Create(30);
        QuestionTimer? nil = null;

        (a == nil!).Should().BeFalse();
    }

    [Fact]
    public void QuestionTimer_InequalityOperator_WhenDifferent_ReturnsTrue()
    {
        var a = QuestionTimer.Create(30);
        var b = QuestionTimer.Create(60);

        (a != b).Should().BeTrue();
    }
}

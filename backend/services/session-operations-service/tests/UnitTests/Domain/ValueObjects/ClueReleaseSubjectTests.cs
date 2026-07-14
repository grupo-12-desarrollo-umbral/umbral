using umbral_backend.Domain.Exceptions;
using umbral_backend.Domain.ValueObjects;

namespace umbral_backend.SessionOperations.UnitTests.Domain.ValueObjects;

public sealed class ClueReleaseSubjectTests
{
    [Fact]
    public void ForTarget_CreatesTargetOnlySubject()
    {
        var targetId = Guid.NewGuid();

        var subject = ClueReleaseSubject.ForTarget(targetId);

        subject.TargetId.Should().Be(targetId);
        subject.ClueId.Should().BeNull();
    }

    [Fact]
    public void ForSubstageClue_CreatesClueOnlySubject()
    {
        var clueId = Guid.NewGuid();

        var subject = ClueReleaseSubject.ForSubstageClue(clueId);

        subject.TargetId.Should().BeNull();
        subject.ClueId.Should().Be(clueId);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, true)]
    public void Create_WhenSubjectDoesNotHaveExactlyOneId_Throws(bool hasTarget, bool hasClue)
    {
        var act = () => ClueReleaseSubject.Create(
            hasTarget ? Guid.NewGuid() : null,
            hasClue ? Guid.NewGuid() : null);

        act.Should().Throw<ClueReleaseSubjectInvalidException>()
            .Which.Category.Should().Be(ErrorCategory.Validation);
    }

    [Fact]
    public void Create_WhenSelectedIdIsEmpty_Throws()
    {
        var act = () => ClueReleaseSubject.Create(Guid.Empty, null);

        act.Should().Throw<ClueReleaseSubjectInvalidException>();
    }
}

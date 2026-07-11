using umbral_backend.Domain.Exceptions;
using umbral_backend.Domain.ValueObjects;

namespace umbral_backend.SessionOperations.UnitTests.Domain.ValueObjects;

// HU-36A X.1: the monitor snapshot carries the active-question identity + the ordered team roster, no more.
public sealed class TriviaAnsweredMonitorSnapshotTests
{
    [Fact]
    public void Create_CarriesActiveQuestionIdentityAndOrderedRoster()
    {
        var substageSnapshotId = Guid.NewGuid();
        var alpha = TeamAnsweredStatus.CreateAnswered(Guid.NewGuid(), "A-01", "Alpha", DateTimeOffset.UnixEpoch);
        var bravo = TeamAnsweredStatus.CreateNotAnswered(Guid.NewGuid(), "B-01", "Bravo");

        var snapshot = TriviaAnsweredMonitorSnapshot.Create(substageSnapshotId, 2, [alpha, bravo]);

        snapshot.SubstageSnapshotId.Should().Be(substageSnapshotId);
        snapshot.QuestionSequenceOrder.Should().Be(2);
        snapshot.TeamStatuses.Should().ContainInOrder(alpha, bravo);
    }

    [Fact]
    public void Create_WithEmptySubstageSnapshotId_Throws()
    {
        var act = () => TriviaAnsweredMonitorSnapshot.Create(Guid.Empty, 1, []);

        act.Should().Throw<SubstageSnapshotIdRequiredException>();
    }

    // No-leak invariant: the snapshot exposes no option/correctness/score member.
    [Fact]
    public void Snapshot_ExposesNoOptionCorrectnessOrScoreProperty()
    {
        var propertyNames = typeof(TriviaAnsweredMonitorSnapshot).GetProperties().Select(property => property.Name);

        propertyNames.Should().NotContain(name =>
            name.Contains("Option", StringComparison.Ordinal) ||
            name.Contains("Correct", StringComparison.Ordinal) ||
            name.Contains("Score", StringComparison.Ordinal) ||
            name.Contains("Point", StringComparison.Ordinal));
    }
}

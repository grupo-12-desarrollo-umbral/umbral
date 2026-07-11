using umbral_backend.Domain.ValueObjects;

namespace umbral_backend.SessionOperations.UnitTests.Domain.ValueObjects;

// HU-36A X.1: the per-team cell of the pre-close answered monitor exposes answered/not-answered only.
public sealed class TeamAnsweredStatusTests
{
    [Fact]
    public void CreateAnswered_CarriesIdentityFlagAndTimestamp()
    {
        var teamId = Guid.NewGuid();
        var answeredAt = new DateTimeOffset(2026, 6, 3, 10, 1, 4, TimeSpan.Zero);

        var status = TeamAnsweredStatus.CreateAnswered(teamId, "A-01", "Alpha", answeredAt);

        status.TeamId.Should().Be(teamId);
        status.TeamCode.Should().Be("A-01");
        status.DisplayName.Should().Be("Alpha");
        status.Answered.Should().BeTrue();
        status.AnsweredAt.Should().Be(answeredAt);
    }

    [Fact]
    public void CreateNotAnswered_HasNoAnswerTimestamp()
    {
        var status = TeamAnsweredStatus.CreateNotAnswered(Guid.NewGuid(), "B-01", "Bravo");

        status.Answered.Should().BeFalse();
        status.AnsweredAt.Should().BeNull();
    }

    // No-leak invariant: the type structurally carries no option/correctness/score member.
    [Fact]
    public void Status_ExposesNoOptionCorrectnessOrScoreProperty()
    {
        var propertyNames = typeof(TeamAnsweredStatus).GetProperties().Select(property => property.Name);

        propertyNames.Should().NotContain(name =>
            name.Contains("Option", StringComparison.Ordinal) ||
            name.Contains("Correct", StringComparison.Ordinal) ||
            name.Contains("Score", StringComparison.Ordinal) ||
            name.Contains("Point", StringComparison.Ordinal));
    }

    [Fact]
    public void Equality_WithSameData_IsEqual()
    {
        var teamId = Guid.NewGuid();
        var answeredAt = DateTimeOffset.UnixEpoch;

        var left = TeamAnsweredStatus.CreateAnswered(teamId, "A-01", "Alpha", answeredAt);
        var right = TeamAnsweredStatus.CreateAnswered(teamId, "A-01", "Alpha", answeredAt);

        left.Should().Be(right);
    }
}

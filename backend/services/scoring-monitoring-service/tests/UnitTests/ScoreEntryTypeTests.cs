using umbral_backend.Domain.Enums;

namespace umbral_backend.ScoringMonitoring.UnitTests;

public sealed class ScoreEntryTypeTests
{
    [Fact]
    public void Enum_ShouldExposeLedgerEntryKinds()
    {
        Enum.GetValues<ScoreEntryType>().Should().Equal(
            ScoreEntryType.Grant,
            ScoreEntryType.Penalty,
            ScoreEntryType.Correction);
    }
}

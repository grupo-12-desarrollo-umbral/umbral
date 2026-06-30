using System.Reflection;
using umbral_backend.Domain.Entities;
using umbral_backend.Domain.Enums;
using umbral_backend.Domain.Exceptions;
using umbral_backend.Domain.ValueObjects;
using umbral_backend.SessionOperations.UnitTests.Domain.TestData;

namespace umbral_backend.SessionOperations.UnitTests.Domain.ValueObjects;

// Locks the HU-17 single-source invariant (DES-24): Mission is the only SessionSource.
// Anchors the teardown so DES-75/76/77/78 cannot resurrect a session-level mode or a
// non-mission source. Guard lives at LiveSession.ValidateSource / SessionSource.Create.
public sealed class SessionSourceSingleSourceInvariantTests
{
    [Fact]
    public void SessionSourceType_ExposesOnlyMission()
    {
        Enum.GetValues<SessionSourceType>()
            .Should().ContainSingle()
            .Which.Should().Be(SessionSourceType.Mission);
    }

    [Fact]
    public void Create_AlwaysYieldsMissionSourceType()
    {
        var source = SessionSource.Create(Guid.NewGuid());

        source.SourceType.Should().Be(SessionSourceType.Mission);
    }

    [Fact]
    public void LiveSessionCreate_WithNonMissionSource_ThrowsException()
    {
        // SessionSourceType has no non-mission member, so forge one through the
        // private ctor to exercise LiveSession.ValidateSource directly.
        var ctor = typeof(SessionSource).GetConstructor(
            BindingFlags.Instance | BindingFlags.NonPublic,
            binder: null,
            new[] { typeof(SessionSourceType), typeof(Guid) },
            modifiers: null)!;
        var nonMissionSource = (SessionSource)ctor.Invoke(
            new object[] { (SessionSourceType)999, Guid.NewGuid() });

        var act = () => LiveSession.Create(
            nonMissionSource,
            "ses-123",
            "Mission Session",
            30,
            DateTimeOffset.UtcNow,
            MissionRuntimeSnapshotFactory.CreateTreasureHuntSnapshot());

        act.Should().Throw<SessionSourceEntityRequiredException>();
    }
}

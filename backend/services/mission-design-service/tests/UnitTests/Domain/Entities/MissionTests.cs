using umbral_backend.Domain.Entities;
using umbral_backend.Domain.Enums;
using umbral_backend.Domain.Events;
using umbral_backend.Domain.Exceptions;

namespace umbral_backend.Application.UnitTests.Domain.Entities;

public class MissionTests
{
    [Fact]
    public void Create_SetsDraftBaselineAndRaisesCreatedEvent()
    {
        var mission = Mission.Create(" Mission One ", " Briefing ", "Advanced", 45);

        mission.Name.Should().Be("Mission One");
        mission.Description.Should().Be("Briefing");
        mission.Difficulty.Value.Should().Be("Advanced");
        mission.MaximumTime.Minutes.Should().Be(45);
        mission.IsActive.Should().BeTrue();
        mission.ArchivedAt.Should().BeNull();
        mission.ActivationState.Should().Be(MissionActivation.Draft);
        mission.DomainEvents.Should().ContainSingle(e => e is MissionCreatedEvent);
    }

    [Fact]
    public void UpdateDetails_RefreshesMissionAndRaisesUpdatedEvent()
    {
        var mission = Mission.Create("Mission One", "Briefing", "Advanced", 45);
        mission.ClearDomainEvents();

        mission.UpdateDetails(" Mission Two ", " Updated Briefing ", "Beginner", 30);

        mission.Name.Should().Be("Mission Two");
        mission.Description.Should().Be("Updated Briefing");
        mission.Difficulty.Value.Should().Be("Beginner");
        mission.MaximumTime.Minutes.Should().Be(30);
        mission.IsActive.Should().BeTrue();
        mission.ActivationState.Should().Be(MissionActivation.Draft);
        mission.DomainEvents.Should().ContainSingle(e => e is MissionDetailsUpdatedEvent);
    }

    [Fact]
    public void RecordStructureChanged_RaisesStructureEventWithoutRevalidatingDetails()
    {
        var mission = Mission.Create("Mission One", "Briefing", "Advanced", 45);
        var stage = mission.AddStage("Stage 1", 1);
        stage.Id = 10;
        var substage = mission.AddSubstage(stage.Id, Substage.CreateTreasureHunt("Substage 1", 1));
        substage.Id = 20;
        mission.AddTarget(stage.Id, substage.Id, "Target 1", "QR-1", 1);
        mission.Activate();
        mission.ClearDomainEvents();

        mission.RecordStructureChanged();

        mission.ActivationState.Should().Be(MissionActivation.Ready);
        mission.DomainEvents.Should().ContainSingle(e => e is MissionStructureChangedEvent);
        mission.DomainEvents.Should().NotContain(e => e is MissionDetailsUpdatedEvent);
    }

    [Fact]
    public void Deactivate_MarksMissionInactiveAndRaisesDeactivatedEvent()
    {
        var mission = Mission.Create("Mission One", "Briefing", "Advanced", 45);
        var archivedAt = new DateTimeOffset(2026, 5, 31, 12, 0, 0, TimeSpan.Zero);
        mission.ClearDomainEvents();

        mission.Deactivate(archivedAt);

        mission.IsActive.Should().BeFalse();
        mission.ArchivedAt.Should().Be(archivedAt);
        mission.ActivationState.Should().Be(MissionActivation.Inactive);
        mission.DomainEvents.Should().ContainSingle(e => e is MissionDeactivatedEvent);
    }

    [Fact]
    public void Deactivate_WhenMissionIsAlreadyInactive_Throws()
    {
        var mission = Mission.Create("Mission One", "Briefing", "Advanced", 45);
        mission.Deactivate(new DateTimeOffset(2026, 5, 31, 12, 0, 0, TimeSpan.Zero));

        var act = () => mission.Deactivate(new DateTimeOffset(2026, 5, 31, 12, 30, 0, TimeSpan.Zero));

        act.Should().Throw<MissionAlreadyDeactivatedException>();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void Create_WhenNameIsInvalid_ThrowsMissionNameRequiredException(string? name)
    {
        var act = () => Mission.Create(name!, "Briefing", "Advanced", 45);

        act.Should().Throw<MissionNameRequiredException>();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void Create_WhenDescriptionIsInvalid_ThrowsMissionDescriptionRequiredException(string? description)
    {
        var act = () => Mission.Create("Mission One", description!, "Advanced", 45);

        act.Should().Throw<MissionDescriptionRequiredException>();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void UpdateDetails_WhenNameIsInvalid_ThrowsMissionNameRequiredException(string? name)
    {
        var mission = Mission.Create("Mission One", "Briefing", "Advanced", 45);

        var act = () => mission.UpdateDetails(name!, "Updated Briefing", "Beginner", 30);

        act.Should().Throw<MissionNameRequiredException>();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void UpdateDetails_WhenDescriptionIsInvalid_ThrowsMissionDescriptionRequiredException(string? description)
    {
        var mission = Mission.Create("Mission One", "Briefing", "Advanced", 45);

        var act = () => mission.UpdateDetails("Mission Two", description!, "Beginner", 30);

        act.Should().Throw<MissionDescriptionRequiredException>();
    }
}

using umbral_backend.Domain.Entities;
using umbral_backend.Domain.Events;
using umbral_backend.Domain.Exceptions;

namespace umbral_backend.Application.UnitTests.Domain.Entities;

public sealed class RegisteredTeamTests
{
    [Fact]
    public void Register_WithValidValues_CreatesActiveTeamAndRaisesRegisteredEvent()
    {
        var team = RegisteredTeam.Register(" Red Foxes ", " RF-01 ");

        team.TeamId.Should().NotBe(Guid.Empty);
        team.DisplayName.Should().Be("Red Foxes");
        team.TeamCode.Should().Be("RF-01");
        team.IsActive.Should().BeTrue();

        var domainEvent = team.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<TeamRegisteredEvent>().Subject;
        domainEvent.TeamId.Should().Be(team.TeamId);
        domainEvent.DisplayName.Should().Be("Red Foxes");
        domainEvent.TeamCode.Should().Be("RF-01");
    }

    [Fact]
    public void Register_WithBlankDisplayName_ThrowsException()
    {
        FluentActions.Invoking(() => RegisteredTeam.Register(" ", "RF-01"))
            .Should().Throw<TeamDisplayNameRequiredException>();
    }

    [Fact]
    public void Register_WithBlankTeamCode_ThrowsException()
    {
        FluentActions.Invoking(() => RegisteredTeam.Register("Red Foxes", " "))
            .Should().Throw<TeamCodeRequiredException>();
    }

    [Fact]
    public void UpdateDetails_OnInactiveTeam_IsAllowedAndRaisesUpdatedEvent()
    {
        var team = RegisteredTeam.Register("Red Foxes", "RF-01");
        team.ClearDomainEvents();
        team.Deactivate();
        team.ClearDomainEvents();

        team.UpdateDetails(" Blue Owls ", " BO-02 ");

        team.DisplayName.Should().Be("Blue Owls");
        team.TeamCode.Should().Be("BO-02");
        team.IsActive.Should().BeFalse();

        var domainEvent = team.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<TeamDetailsUpdatedEvent>().Subject;
        domainEvent.TeamId.Should().Be(team.TeamId);
        domainEvent.DisplayName.Should().Be("Blue Owls");
        domainEvent.TeamCode.Should().Be("BO-02");
    }

    [Fact]
    public void UpdateDetails_WithBlankDisplayName_ThrowsException()
    {
        var team = RegisteredTeam.Register("Red Foxes", "RF-01");

        FluentActions.Invoking(() => team.UpdateDetails(" ", "BO-02"))
            .Should().Throw<TeamDisplayNameRequiredException>();
    }

    [Fact]
    public void UpdateDetails_WithBlankTeamCode_ThrowsException()
    {
        var team = RegisteredTeam.Register("Red Foxes", "RF-01");

        FluentActions.Invoking(() => team.UpdateDetails("Blue Owls", " "))
            .Should().Throw<TeamCodeRequiredException>();
    }

    [Fact]
    public void Deactivate_OnActiveTeam_SucceedsAndRaisesEvent()
    {
        var team = RegisteredTeam.Register("Red Foxes", "RF-01");
        team.ClearDomainEvents();

        team.Deactivate();

        team.IsActive.Should().BeFalse();
        var domainEvent = team.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<TeamDeactivatedEvent>().Subject;
        domainEvent.TeamId.Should().Be(team.TeamId);
    }

    [Fact]
    public void Deactivate_OnAlreadyInactiveTeam_ThrowsException()
    {
        var team = RegisteredTeam.Register("Red Foxes", "RF-01");
        team.Deactivate();
        team.ClearDomainEvents();

        FluentActions.Invoking(team.Deactivate)
            .Should().Throw<TeamAlreadyDeactivatedException>();

        team.DomainEvents.Should().BeEmpty();
    }
}

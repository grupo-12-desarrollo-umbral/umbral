using umbral_backend.Domain.Entities;
using umbral_backend.Domain.Exceptions;

namespace umbral_backend.Application.UnitTests.Domain.Entities;

public sealed class LiveSessionReferenceTests
{
    [Fact]
    public void Create_WithValidCode_NormalizesCode()
    {
        var liveSessionId = Guid.NewGuid();

        var liveSessionReference = LiveSessionReference.Create(liveSessionId, " rsf231 ");

        liveSessionReference.LiveSessionId.Should().Be(liveSessionId);
        liveSessionReference.SessionCode.Should().Be("RSF231");
        liveSessionReference.TeamAssociations.Should().BeEmpty();
    }

    [Fact]
    public void Create_WithInvalidCode_ThrowsException()
    {
        FluentActions.Invoking(() => LiveSessionReference.Create(Guid.NewGuid(), "abc"))
            .Should().Throw<SessionCodeFormatInvalidException>();
    }

    [Fact]
    public void AssociateTeam_WithUniqueTeam_AddsAssociation()
    {
        var liveSessionReference = LiveSessionReference.Create(Guid.NewGuid(), "RSF231");
        var teamId = Guid.NewGuid();

        var association = liveSessionReference.AssociateTeam(teamId);

        association.LiveSessionId.Should().Be(liveSessionReference.LiveSessionId);
        association.TeamId.Should().Be(teamId);
        liveSessionReference.TeamAssociations.Should().ContainSingle();
    }

    [Fact]
    public void AssociateTeam_WithDuplicateTeam_ThrowsException()
    {
        var liveSessionReference = LiveSessionReference.Create(Guid.NewGuid(), "RSF231");
        var teamId = Guid.NewGuid();
        liveSessionReference.AssociateTeam(teamId);

        FluentActions.Invoking(() => liveSessionReference.AssociateTeam(teamId))
            .Should().Throw<TeamAlreadyAssociatedWithSessionException>();
    }
}

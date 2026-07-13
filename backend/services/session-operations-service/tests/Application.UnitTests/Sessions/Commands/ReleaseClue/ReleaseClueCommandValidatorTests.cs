using System.Reflection;
using umbral_backend.Application.Common.Security;
using umbral_backend.Application.Sessions.Commands.ReleaseClue;

namespace umbral_backend.Application.UnitTests.Sessions.Commands.ReleaseClue;

public sealed class ReleaseClueCommandValidatorTests
{
    private readonly ReleaseClueCommandValidator _validator = new();

    [Fact]
    public async Task Validate_WhenCommandTargetsOneTeam_Passes()
    {
        var result = await _validator.ValidateAsync(new ReleaseClueCommand(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()));
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task Validate_WhenTeamIdIsNullForAllTeams_Passes()
    {
        var result = await _validator.ValidateAsync(new ReleaseClueCommand(Guid.NewGuid(), Guid.NewGuid(), null));
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task Validate_WhenLiveSessionIdIsEmpty_Fails()
    {
        var result = await _validator.ValidateAsync(new ReleaseClueCommand(Guid.Empty, Guid.NewGuid(), null));
        result.Errors.Should().ContainSingle(error => error.PropertyName == nameof(ReleaseClueCommand.LiveSessionId));
    }

    [Fact]
    public async Task Validate_WhenTargetIdIsEmpty_Fails()
    {
        var result = await _validator.ValidateAsync(new ReleaseClueCommand(Guid.NewGuid(), Guid.Empty, null));
        result.Errors.Should().ContainSingle(error => error.PropertyName == nameof(ReleaseClueCommand.TargetId));
    }

    [Fact]
    public void Command_RequiresOperatorRole()
    {
        var attribute = typeof(ReleaseClueCommand).GetCustomAttribute<AuthorizeAttribute>();
        attribute.Should().NotBeNull();
        attribute!.Roles.Should().Be("Operator");
    }
}

using umbral_backend.Application.Sessions.Commands.AssociateTeamToSession;

namespace umbral_backend.Application.UnitTests.Sessions.Commands.AssociateTeamToSession;

public sealed class AssociateTeamToSessionCommandValidatorTests
{
    private readonly AssociateTeamToSessionCommandValidator _validator = new();

    [Fact]
    public async Task Validate_WhenCommandIsValid_Succeeds()
    {
        var command = new AssociateTeamToSessionCommand(Guid.NewGuid(), Guid.NewGuid());

        var result = await _validator.ValidateAsync(command);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task Validate_WhenLiveSessionIdIsEmpty_Fails()
    {
        var command = new AssociateTeamToSessionCommand(Guid.Empty, Guid.NewGuid());

        var result = await _validator.ValidateAsync(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(error => error.PropertyName == nameof(AssociateTeamToSessionCommand.LiveSessionId));
    }

    [Fact]
    public async Task Validate_WhenReferenceTeamIdIsEmpty_Fails()
    {
        var command = new AssociateTeamToSessionCommand(Guid.NewGuid(), Guid.Empty);

        var result = await _validator.ValidateAsync(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(error => error.PropertyName == nameof(AssociateTeamToSessionCommand.ReferenceTeamId));
    }
}

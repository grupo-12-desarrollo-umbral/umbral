using umbral_backend.Application.Sessions.Commands.AssociateTeamToSession;

namespace umbral_backend.Application.UnitTests.Sessions.Commands.AssociateTeamToSession;

public sealed class AssociateTeamToSessionCommandValidatorTests
{
    private readonly AssociateTeamToSessionCommandValidator _validator = new();

    [Fact]
    public async Task Validate_WithValidCommand_Passes()
    {
        var result = await _validator.ValidateAsync(
            new AssociateTeamToSessionCommand(Guid.NewGuid(), Guid.NewGuid()));

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task Validate_WithEmptyLiveSessionId_Fails()
    {
        var result = await _validator.ValidateAsync(
            new AssociateTeamToSessionCommand(Guid.Empty, Guid.NewGuid()));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(error => error.PropertyName == nameof(AssociateTeamToSessionCommand.LiveSessionId));
    }

    [Fact]
    public async Task Validate_WithEmptyReferenceTeamId_Fails()
    {
        var result = await _validator.ValidateAsync(
            new AssociateTeamToSessionCommand(Guid.NewGuid(), Guid.Empty));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(error => error.PropertyName == nameof(AssociateTeamToSessionCommand.ReferenceTeamId));
    }
}

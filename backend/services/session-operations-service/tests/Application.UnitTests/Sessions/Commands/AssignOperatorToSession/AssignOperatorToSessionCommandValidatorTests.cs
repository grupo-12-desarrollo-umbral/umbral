using umbral_backend.Application.Sessions.Commands.AssignOperatorToSession;

namespace umbral_backend.Application.UnitTests.Sessions.Commands.AssignOperatorToSession;

public sealed class AssignOperatorToSessionCommandValidatorTests
{
    private readonly AssignOperatorToSessionCommandValidator _validator = new();

    [Fact]
    public async Task Validate_WhenCommandIsValid_Passes()
    {
        var result = await _validator.ValidateAsync(new AssignOperatorToSessionCommand(Guid.NewGuid(), 27));

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task Validate_WhenLiveSessionIdIsEmpty_Fails()
    {
        var result = await _validator.ValidateAsync(new AssignOperatorToSessionCommand(Guid.Empty, 27));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(error => error.PropertyName == nameof(AssignOperatorToSessionCommand.LiveSessionId));
    }

    [Fact]
    public async Task Validate_WhenOperatorUserIdIsNotPositive_Fails()
    {
        var result = await _validator.ValidateAsync(new AssignOperatorToSessionCommand(Guid.NewGuid(), 0));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(error => error.PropertyName == nameof(AssignOperatorToSessionCommand.OperatorUserId));
    }
}

using umbral_backend.Application.Sessions.Commands.RegisterTargetScan;

namespace umbral_backend.Application.UnitTests.Sessions.Commands.RegisterTargetScan;

public sealed class RegisterTargetScanCommandValidatorTests
{
    private readonly RegisterTargetScanCommandValidator _validator = new();
    private static RegisterTargetScanCommand Valid() => new(Guid.NewGuid(), Guid.NewGuid(), "QR-001");

    [Fact]
    public void Validate_WithWellFormedCommand_Passes() =>
        _validator.Validate(Valid()).IsValid.Should().BeTrue();

    [Fact]
    public void Validate_WithEmptySessionId_Fails() =>
        _validator.Validate(Valid() with { LiveSessionId = Guid.Empty }).IsValid.Should().BeFalse();

    [Fact]
    public void Validate_WithEmptyTeamId_Fails() =>
        _validator.Validate(Valid() with { TeamId = Guid.Empty }).IsValid.Should().BeFalse();

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_WithEmptyScannedValue_Fails(string value) =>
        _validator.Validate(Valid() with { ScannedValue = value }).IsValid.Should().BeFalse();
}

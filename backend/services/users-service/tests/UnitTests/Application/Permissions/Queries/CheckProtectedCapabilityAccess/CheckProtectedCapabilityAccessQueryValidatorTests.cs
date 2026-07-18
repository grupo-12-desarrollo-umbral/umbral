using umbral_backend.Application.Permissions.Queries.CheckProtectedCapabilityAccess;
using umbral_backend.Domain.Enums;

namespace umbral_backend.Application.UnitTests.Application.Permissions.Queries.CheckProtectedCapabilityAccess;

public sealed class CheckProtectedCapabilityAccessQueryValidatorTests
{
    private readonly CheckProtectedCapabilityAccessQueryValidator _validator = new();

    [Fact]
    public void Validate_AcceptsKnownCapability()
    {
        var result = _validator.Validate(
            new CheckProtectedCapabilityAccessQuery(ProtectedCapability.OperatorPanel));

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_RejectsUnknownCapability()
    {
        var result = _validator.Validate(
            new CheckProtectedCapabilityAccessQuery((ProtectedCapability)999));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(error => error.PropertyName == nameof(CheckProtectedCapabilityAccessQuery.Capability));
    }
}

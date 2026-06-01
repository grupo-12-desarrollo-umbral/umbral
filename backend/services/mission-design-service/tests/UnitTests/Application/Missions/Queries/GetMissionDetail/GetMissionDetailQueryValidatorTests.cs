using umbral_backend.Application.Missions.Queries.GetMissionDetail;

namespace umbral_backend.Application.UnitTests.Application.Missions.Queries.GetMissionDetail;

public sealed class GetMissionDetailQueryValidatorTests
{
    private readonly GetMissionDetailQueryValidator _validator = new();

    [Fact]
    public void Validate_WhenQueryIsValid_HasNoErrors()
    {
        var result = _validator.Validate(new GetMissionDetailQuery(1));

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_WhenIdIsNotPositive_ReturnsError()
    {
        var result = _validator.Validate(new GetMissionDetailQuery(0));

        result.Errors.Should().ContainSingle(error => error.PropertyName == nameof(GetMissionDetailQuery.Id));
    }
}

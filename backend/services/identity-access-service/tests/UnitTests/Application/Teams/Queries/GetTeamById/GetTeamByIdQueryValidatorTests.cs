using umbral_backend.Application.Teams.Queries.GetTeamById;

namespace umbral_backend.Application.UnitTests.Application.Teams.Queries.GetTeamById;

public sealed class GetTeamByIdQueryValidatorTests
{
    private readonly GetTeamByIdQueryValidator _validator = new();

    [Fact]
    public void Validate_AcceptsNonEmptyIdentifier()
    {
        var result = _validator.Validate(new GetTeamByIdQuery(Guid.NewGuid()));

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_RejectsEmptyIdentifier()
    {
        var result = _validator.Validate(new GetTeamByIdQuery(Guid.Empty));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(error => error.PropertyName == nameof(GetTeamByIdQuery.TeamId));
    }
}

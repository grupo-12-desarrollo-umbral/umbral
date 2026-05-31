using umbral_backend.Application.Teams.Queries.GetTeams;

namespace umbral_backend.Application.UnitTests.Application.Teams.Queries.GetTeams;

public sealed class GetTeamsQueryValidatorTests
{
    private readonly GetTeamsQueryValidator _validator = new();

    [Fact]
    public void Validate_AcceptsPositivePaginationArguments()
    {
        var result = _validator.Validate(new GetTeamsQuery(2, 25));

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_RejectsNonPositivePaginationArguments()
    {
        var result = _validator.Validate(new GetTeamsQuery(0, 0));

        result.IsValid.Should().BeFalse();
        result.Errors.Select(error => error.PropertyName).Should().Contain(new[]
        {
            nameof(GetTeamsQuery.Page),
            nameof(GetTeamsQuery.PageSize)
        });
    }
}

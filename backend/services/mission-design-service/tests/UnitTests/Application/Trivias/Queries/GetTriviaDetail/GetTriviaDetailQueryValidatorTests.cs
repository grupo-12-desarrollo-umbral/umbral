using umbral_backend.Application.Trivias.Queries.GetTriviaDetail;

namespace umbral_backend.Application.UnitTests.Application.Trivias.Queries.GetTriviaDetail;

public sealed class GetTriviaDetailQueryValidatorTests
{
    private readonly GetTriviaDetailQueryValidator _validator = new();

    [Fact]
    public void Validate_WhenQueryIsValid_HasNoErrors()
    {
        var result = _validator.Validate(new GetTriviaDetailQuery(1));

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_WhenIdIsNotPositive_ReturnsError()
    {
        var result = _validator.Validate(new GetTriviaDetailQuery(0));

        result.Errors.Should().ContainSingle(error => error.PropertyName == nameof(GetTriviaDetailQuery.Id));
    }
}

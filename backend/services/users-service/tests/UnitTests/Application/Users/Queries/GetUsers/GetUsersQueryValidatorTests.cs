using umbral_backend.Application.Users.Queries.GetUsers;

namespace umbral_backend.Application.UnitTests.Application.Users.Queries.GetUsers;

public sealed class GetUsersQueryValidatorTests
{
    private readonly GetUsersQueryValidator _validator = new();

    [Fact]
    public void Validate_AcceptsPositivePaginationArguments()
    {
        var result = _validator.Validate(new GetUsersQuery(2, 25));

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_RejectsNonPositivePaginationArguments()
    {
        var result = _validator.Validate(new GetUsersQuery(0, 0));

        result.IsValid.Should().BeFalse();
        result.Errors.Select(error => error.PropertyName).Should().Contain(new[]
        {
            nameof(GetUsersQuery.Page),
            nameof(GetUsersQuery.PageSize)
        });
    }
}

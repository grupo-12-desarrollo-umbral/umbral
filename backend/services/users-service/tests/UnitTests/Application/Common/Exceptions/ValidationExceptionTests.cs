using FluentValidation.Results;
using ValidationException = umbral_backend.Application.Common.Exceptions.ValidationException;

namespace umbral_backend.Application.UnitTests.Application.Common.Exceptions;

public sealed class ValidationExceptionTests
{
    [Fact]
    public void Constructor_WithoutFailures_InitializesDefaultMessageAndEmptyErrors()
    {
        var exception = new ValidationException();

        exception.Message.Should().Be("One or more validation failures have occurred.");
        exception.Errors.Should().BeEmpty();
    }

    [Fact]
    public void Constructor_WithFailures_GroupsErrorsByPropertyName()
    {
        ValidationFailure[] failures =
        [
            new ValidationFailure("Email", "Email is required."),
            new ValidationFailure("Email", "Email must be valid."),
            new ValidationFailure("Role", "Role is required.")
        ];

        var exception = new ValidationException(failures);

        exception.Errors.Should().ContainKey("Email");
        exception.Errors["Email"].Should().Contain(["Email is required.", "Email must be valid."]);
        exception.Errors["Role"].Should().ContainSingle().Which.Should().Be("Role is required.");
    }
}

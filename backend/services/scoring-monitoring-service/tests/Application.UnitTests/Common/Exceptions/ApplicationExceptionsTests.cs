using FluentValidation.Results;
using umbral_backend.Application.Common.Exceptions;
using umbral_backend.Domain.Exceptions;
using ValidationException = umbral_backend.Application.Common.Exceptions.ValidationException;

namespace umbral_backend.Application.UnitTests.Common.Exceptions;

// Covers the three application-layer IErrorMetadata carriers: their category/error-code contract
// and, for ValidationException, both constructors plus the failure-grouping projection.
public sealed class ApplicationExceptionsTests
{
    [Fact]
    public void ValidationException_Default_HasEmptyErrorsAndMetadata()
    {
        var exception = new ValidationException();

        exception.Errors.Should().BeEmpty();
        exception.Category.Should().Be(ErrorCategory.Validation);
        exception.ErrorCode.Should().Be("validation-failed");
        exception.Message.Should().Be("One or more validation failures have occurred.");
    }

    [Fact]
    public void ValidationException_FromFailures_GroupsByProperty()
    {
        var exception = new ValidationException(
        [
            new ValidationFailure("Score", "must be positive"),
            new ValidationFailure("Score", "must be an integer"),
            new ValidationFailure("Team", "is required")
        ]);

        exception.Errors.Should().ContainKey("Score");
        exception.Errors["Score"].Should().BeEquivalentTo("must be positive", "must be an integer");
        exception.Errors["Team"].Should().ContainSingle().Which.Should().Be("is required");
    }

    [Fact]
    public void NotFoundException_CarriesNameAndKeyAndMetadata()
    {
        var exception = new NotFoundException("ScoreEntry", 42);

        exception.Message.Should().Contain("ScoreEntry").And.Contain("42");
        exception.Category.Should().Be(ErrorCategory.NotFound);
        exception.ErrorCode.Should().Be("not-found");
    }

    [Fact]
    public void ForbiddenAccessException_HasForbiddenMetadata()
    {
        var exception = new ForbiddenAccessException();

        exception.Category.Should().Be(ErrorCategory.Forbidden);
        exception.ErrorCode.Should().Be("forbidden-access");
    }
}

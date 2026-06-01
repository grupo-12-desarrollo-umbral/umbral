using umbral_backend.Application.Common.Behaviours;
using ValidationException = umbral_backend.Application.Common.Exceptions.ValidationException;
using FluentValidation;
using FluentValidation.Results;
using MediatR;
using Moq;

namespace umbral_backend.Application.UnitTests.Common.Behaviours;

public class ValidationBehaviourTests
{
    [Fact]
    public async Task Handle_WhenNoValidators_CallsNext()
    {
        var sut = new ValidationBehaviour<PingRequest, int>(Array.Empty<IValidator<PingRequest>>());
        var nextWasCalled = false;
        var result = await sut.Handle(new PingRequest(), () => { nextWasCalled = true; return Task.FromResult(0); }, CancellationToken.None);

        result.Should().Be(0);
        nextWasCalled.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_WhenValidatorsPass_CallsNext()
    {
        var validator = new Mock<IValidator<PingRequest>>();
        validator
            .Setup(v => v.ValidateAsync(It.IsAny<ValidationContext<PingRequest>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());

        var sut = new ValidationBehaviour<PingRequest, int>(new[] { validator.Object });
        var nextWasCalled = false;
        var result = await sut.Handle(new PingRequest(), () => { nextWasCalled = true; return Task.FromResult(42); }, CancellationToken.None);

        result.Should().Be(42);
        nextWasCalled.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_WhenValidatorsFail_ThrowsValidationException()
    {
        var validator = new Mock<IValidator<PingRequest>>();
        validator
            .Setup(v => v.ValidateAsync(It.IsAny<ValidationContext<PingRequest>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult(new[] { new ValidationFailure("Name", "Name is required") }));

        var sut = new ValidationBehaviour<PingRequest, int>(new[] { validator.Object });

        var act = () => sut.Handle(new PingRequest(), () => Task.FromResult(0), CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>();
    }

    public sealed record PingRequest : IRequest<int>;
}

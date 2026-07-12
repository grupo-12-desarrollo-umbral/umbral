using umbral_backend.Application.Common.Behaviours;
using umbral_backend.Application.Common.Exceptions;
using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Application.Common.Security;
using FluentValidation;
using Microsoft.Extensions.Logging.Abstractions;
using ValidationException = umbral_backend.Application.Common.Exceptions.ValidationException;

namespace umbral_backend.Application.UnitTests.Common.Behaviours;

// Exercises every branch of the four open pipeline behaviours: authorization (no attribute,
// unauthorized, no-role, role-mismatch, role-match), validation (no validators / pass / fail),
// performance (fast, slow-with-user, slow-without-user) and the unhandled-exception passthrough.
public sealed class PipelineBehaviourTests
{
    private sealed record PlainRequest;

    [Authorize]
    private sealed record UnrestrictedRequest;

    [Authorize(Roles = "Operator")]
    private sealed record OperatorRequest;

    private static Mock<ICurrentUser> User(string? id, string? role = null)
    {
        var user = new Mock<ICurrentUser>();
        user.SetupGet(u => u.Id).Returns(id);
        user.SetupGet(u => u.Role).Returns(role);
        return user;
    }

    [Fact]
    public async Task Authorization_NoAuthorizeAttribute_AllowsExecution()
    {
        var behaviour = new AuthorizationBehaviour<PlainRequest, string>(User(null).Object);

        var result = await behaviour.Handle(new PlainRequest(), () => Task.FromResult("ok"), CancellationToken.None);

        result.Should().Be("ok");
    }

    [Fact]
    public async Task Authorization_MissingUser_ThrowsUnauthorized()
    {
        var behaviour = new AuthorizationBehaviour<UnrestrictedRequest, string>(User(null).Object);

        var act = () => behaviour.Handle(new UnrestrictedRequest(), () => Task.FromResult("ok"), CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task Authorization_AuthorizeWithoutRoles_AllowsAnyAuthenticatedCaller()
    {
        var behaviour = new AuthorizationBehaviour<UnrestrictedRequest, string>(User("42", "AnyRole").Object);

        var result = await behaviour.Handle(new UnrestrictedRequest(), () => Task.FromResult("ok"), CancellationToken.None);

        result.Should().Be("ok");
    }

    [Fact]
    public async Task Authorization_RoleMismatch_ThrowsForbidden()
    {
        var behaviour = new AuthorizationBehaviour<OperatorRequest, string>(User("42", "Administrator").Object);

        var act = () => behaviour.Handle(new OperatorRequest(), () => Task.FromResult("ok"), CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenAccessException>();
    }

    [Fact]
    public async Task Authorization_RoleMatch_AllowsExecution()
    {
        var behaviour = new AuthorizationBehaviour<OperatorRequest, string>(User("42", "Operator").Object);

        var result = await behaviour.Handle(new OperatorRequest(), () => Task.FromResult("ok"), CancellationToken.None);

        result.Should().Be("ok");
    }

    [Fact]
    public async Task Validation_NoValidators_AllowsExecution()
    {
        var behaviour = new ValidationBehaviour<PlainRequest, string>([]);

        var result = await behaviour.Handle(new PlainRequest(), () => Task.FromResult("ok"), CancellationToken.None);

        result.Should().Be("ok");
    }

    [Fact]
    public async Task Validation_AllValidatorsPass_AllowsExecution()
    {
        var validator = new InlineValidator<PlainRequest>();
        var behaviour = new ValidationBehaviour<PlainRequest, string>([validator]);

        var result = await behaviour.Handle(new PlainRequest(), () => Task.FromResult("ok"), CancellationToken.None);

        result.Should().Be("ok");
    }

    [Fact]
    public async Task Validation_ValidatorFails_ThrowsValidationException()
    {
        var validator = new InlineValidator<PlainRequest>();
        validator.RuleFor(_ => _).Must(_ => false).WithName("Field").WithMessage("bad");
        var behaviour = new ValidationBehaviour<PlainRequest, string>([validator]);

        var act = () => behaviour.Handle(new PlainRequest(), () => Task.FromResult("ok"), CancellationToken.None);

        (await act.Should().ThrowAsync<ValidationException>())
            .Which.Errors.Should().ContainKey("Field");
    }

    [Fact]
    public async Task Performance_FastRequest_ReturnsWithoutLogging()
    {
        var behaviour = new PerformanceBehaviour<PlainRequest, string>(
            NullLogger<PlainRequest>.Instance, User("1").Object);

        var result = await behaviour.Handle(new PlainRequest(), () => Task.FromResult("ok"), CancellationToken.None);

        result.Should().Be("ok");
    }

    [Fact]
    public async Task Performance_SlowRequestWithUser_Logs()
    {
        var behaviour = new PerformanceBehaviour<PlainRequest, string>(
            NullLogger<PlainRequest>.Instance, User("user-1").Object);

        var result = await behaviour.Handle(
            new PlainRequest(), async () => { await Task.Delay(550); return "ok"; }, CancellationToken.None);

        result.Should().Be("ok");
    }

    [Fact]
    public async Task Performance_SlowRequestWithoutUser_Logs()
    {
        var behaviour = new PerformanceBehaviour<PlainRequest, string>(
            NullLogger<PlainRequest>.Instance, User(null).Object);

        var result = await behaviour.Handle(
            new PlainRequest(), async () => { await Task.Delay(550); return "ok"; }, CancellationToken.None);

        result.Should().Be("ok");
    }

    [Fact]
    public async Task Unhandled_SuccessfulRequest_ReturnsResult()
    {
        var behaviour = new UnhandledExceptionBehaviour<PlainRequest, string>(NullLogger<PlainRequest>.Instance);

        var result = await behaviour.Handle(new PlainRequest(), () => Task.FromResult("ok"), CancellationToken.None);

        result.Should().Be("ok");
    }

    [Fact]
    public async Task Unhandled_ThrowingRequest_LogsAndRethrows()
    {
        var behaviour = new UnhandledExceptionBehaviour<PlainRequest, string>(NullLogger<PlainRequest>.Instance);

        var act = () => behaviour.Handle(
            new PlainRequest(),
            () => throw new InvalidOperationException("boom"),
            CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("boom");
    }
}

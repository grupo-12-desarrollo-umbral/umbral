using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using umbral_backend.Api.Services;
using umbral_backend.Application.Common.Exceptions;
using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Application.Scores.Commands.RecordScoreEntry;
using umbral_backend.Domain.Enums;

namespace umbral_backend.Infrastructure.IntegrationTests.Api;

public sealed class DependencyInjectionTests
{
    [Fact]
    public void AddWebServices_RegistersCurrentUserAndApiBehaviorOptions()
    {
        var builder = Host.CreateApplicationBuilder();

        builder.AddWebServices();

        using var provider = builder.Services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        scope.ServiceProvider.GetRequiredService<ICurrentUser>().Should().NotBeNull();
        scope.ServiceProvider.GetRequiredService<IOptions<ApiBehaviorOptions>>()
            .Value.SuppressModelStateInvalidFilter.Should().BeTrue();
    }

    // Without this registration UseExceptionHandler has nothing to dispatch to, so a deliberate
    // ForbiddenAccessException from a guard surfaces as 500 instead of 403 — silently, since the
    // handler type still exists and compiles.
    [Fact]
    public void AddWebServices_RegistersProblemDetailsExceptionHandler()
    {
        var builder = Host.CreateApplicationBuilder();

        builder.AddWebServices();

        using var provider = builder.Services.BuildServiceProvider();

        provider.GetServices<IExceptionHandler>()
            .Should().ContainSingle()
            .Which.Should().BeOfType<ProblemDetailsExceptionHandler>();
    }

    [Fact]
    public async Task AddApplicationServices_MediatorExecutesRegisteredValidators()
    {
        var builder = Host.CreateApplicationBuilder();
        var repository = new Mock<IScoreEntryRepository>();

        builder.AddApplicationServices();
        builder.Services.AddSingleton(repository.Object);
        builder.Services.AddSingleton(Mock.Of<ICurrentUser>());
        // The innermost ConcurrencyRetryBehaviour is constructed with every pipeline, so IUnitOfWork
        // must resolve even for a request that never touches the ranking projection.
        builder.Services.AddSingleton(Mock.Of<IUnitOfWork>());

        using var provider = builder.Services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        var command = new RecordScoreEntryCommand(
            Guid.Empty,
            Guid.NewGuid(),
            "Team",
            "target-resolved",
            100,
            DateTimeOffset.UtcNow,
            ScoreSourceType.TargetResolution,
            Guid.NewGuid());

        var act = () => sender.Send(command);

        await act.Should().ThrowAsync<ValidationException>();
        repository.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task AddWebServices_AuthenticationSchemeReturnsNoResultWhenTrustedHeadersAreMissing()
    {
        var builder = Host.CreateApplicationBuilder();
        builder.AddWebServices();
        using var provider = builder.Services.BuildServiceProvider();

        var httpContext = new DefaultHttpContext
        {
            RequestServices = provider
        };

        var result = await provider
            .GetRequiredService<IAuthenticationService>()
            .AuthenticateAsync(httpContext, "TrustedHeaders");

        result.Succeeded.Should().BeFalse();
        result.None.Should().BeTrue();
    }

    [Fact]
    public async Task AddWebServices_AuthenticationSchemeBuildsAClaimsPrincipalFromTrustedHeaders()
    {
        var builder = Host.CreateApplicationBuilder();
        builder.AddWebServices();
        using var provider = builder.Services.BuildServiceProvider();

        var httpContext = new DefaultHttpContext
        {
            RequestServices = provider
        };
        httpContext.Request.Headers["X-User-Id"] = "user-123";
        httpContext.Request.Headers["X-User-Role"] = "Operator";
        httpContext.Request.Headers["X-User-Email"] = "operator@example.com";

        var result = await provider
            .GetRequiredService<IAuthenticationService>()
            .AuthenticateAsync(httpContext, "TrustedHeaders");

        result.Succeeded.Should().BeTrue();
        result.Principal!.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value.Should().Be("user-123");
        result.Principal.FindFirst(System.Security.Claims.ClaimTypes.Role)!.Value.Should().Be("Operator");
        result.Principal.FindFirst(System.Security.Claims.ClaimTypes.Email)!.Value.Should().Be("operator@example.com");
    }
}

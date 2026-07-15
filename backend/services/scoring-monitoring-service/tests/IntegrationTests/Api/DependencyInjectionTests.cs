using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using umbral_backend.Api.Services;
using umbral_backend.Application.Common.Interfaces;

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

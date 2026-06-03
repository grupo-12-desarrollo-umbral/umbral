using System.Diagnostics.CodeAnalysis;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using umbral_backend.Api.Services;
using umbral_backend.Application.Common.Interfaces;

namespace Microsoft.Extensions.DependencyInjection;

[ExcludeFromCodeCoverage]
public static class DependencyInjection
{
    public static void AddWebServices(this IHostApplicationBuilder builder)
    {
        builder.Services.AddDatabaseDeveloperPageExceptionFilter();
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddSignalR(options => options.EnableDetailedErrors = builder.Environment.IsDevelopment());
        builder.Services.AddScoped<CurrentUserContext>();
        builder.Services.AddScoped<ICurrentUser, CurrentUser>();
        builder.Services
            .AddAuthentication(TrustedHeadersAuthenticationDefaults.Scheme)
            .AddScheme<AuthenticationSchemeOptions, TrustedHeadersAuthenticationHandler>(
                TrustedHeadersAuthenticationDefaults.Scheme,
                _ => { });
        builder.Services.AddAuthorization(options =>
        {
            options.AddPolicy(AuthorizationPolicies.Operator, policy =>
            {
                policy.AddAuthenticationSchemes(TrustedHeadersAuthenticationDefaults.Scheme);
                policy.RequireAuthenticatedUser();
                policy.RequireRole("Operator");
            });
            options.AddPolicy(AuthorizationPolicies.Participant, policy =>
            {
                policy.AddAuthenticationSchemes(TrustedHeadersAuthenticationDefaults.Scheme);
                policy.RequireAuthenticatedUser();
                policy.RequireRole("Participant");
            });
        });
        builder.Services.AddExceptionHandler<ProblemDetailsExceptionHandler>();
        builder.Services.Configure<ApiBehaviorOptions>(options => options.SuppressModelStateInvalidFilter = true);
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddOpenApi();
    }
}

[ExcludeFromCodeCoverage]
file sealed class TrustedHeadersAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public TrustedHeadersAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder)
    {
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var userId = Request.Headers["X-User-Id"].ToString();
        var role = Request.Headers["X-User-Role"].ToString();
        var email = Request.Headers["X-User-Email"].ToString();

        if (string.IsNullOrWhiteSpace(userId) ||
            string.IsNullOrWhiteSpace(role) ||
            string.IsNullOrWhiteSpace(email))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId),
            new Claim(ClaimTypes.Role, role),
            new Claim(ClaimTypes.Email, email)
        };

        var identity = new ClaimsIdentity(claims, TrustedHeadersAuthenticationDefaults.Scheme);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, TrustedHeadersAuthenticationDefaults.Scheme);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}

[ExcludeFromCodeCoverage]
file static class TrustedHeadersAuthenticationDefaults
{
    public const string Scheme = "TrustedHeaders";
}

using System.Diagnostics.CodeAnalysis;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Azure.Identity;
using umbral_backend.Api.Services;
using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Domain.Constants;
using umbral_backend.Web.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
// Microsoft.AspNetCore.Authentication also exports a SystemClock; alias ours so the registration below
// stays unambiguous.
using SystemClock = umbral_backend.Web.Services.SystemClock;

namespace Microsoft.Extensions.DependencyInjection;

public static class DependencyInjection
{
    public static void AddWebServices(this IHostApplicationBuilder builder)
    {
        builder.AddObservability();
        builder.Services.AddDatabaseDeveloperPageExceptionFilter();

        builder.Services.AddScoped<ICurrentUser, CurrentUser>();
        builder.Services.AddSingleton<IClock, SystemClock>();

        builder.Services.AddHttpContextAccessor();

        // Authenticate the gateway's trusted identity headers so ASP.NET `[Authorize]` works on
        // controllers here as it does in the sibling services. This is defence-in-depth, not the
        // primary gate: requests are authorized by the MediatR AuthorizationBehaviour reading
        // Application/Common/Security/AuthorizeAttribute, which needs no middleware. Without this
        // registration an ASP.NET `[Authorize]` would silently pass every request.
        builder.Services
            .AddAuthentication(TrustedHeadersAuthenticationDefaults.Scheme)
            .AddScheme<AuthenticationSchemeOptions, TrustedHeadersAuthenticationHandler>(
                TrustedHeadersAuthenticationDefaults.Scheme,
                _ => { });
        builder.Services.AddAuthorization(options =>
        {
            options.AddPolicy(AuthorizationPolicies.Administrator, policy =>
            {
                policy.AddAuthenticationSchemes(TrustedHeadersAuthenticationDefaults.Scheme);
                policy.RequireAuthenticatedUser();
                policy.RequireRole(Roles.Administrator);
            });
            options.AddPolicy(AuthorizationPolicies.Operator, policy =>
            {
                policy.AddAuthenticationSchemes(TrustedHeadersAuthenticationDefaults.Scheme);
                policy.RequireAuthenticatedUser();
                policy.RequireRole(Roles.Operator);
            });
            options.AddPolicy(AuthorizationPolicies.AdministratorOrOperator, policy =>
            {
                policy.AddAuthenticationSchemes(TrustedHeadersAuthenticationDefaults.Scheme);
                policy.RequireAuthenticatedUser();
                policy.RequireRole(Roles.Administrator, Roles.Operator);
            });
        });

        // The authorization middleware rejects before MediatR, so its 401/403 never reach
        // ProblemDetailsExceptionHandler and would otherwise return an empty body. Register the
        // ProblemDetails service (surfaced by UseStatusCodePages in Program.cs) and align the two
        // arms it can produce with that handler's wording, so a caller cannot tell which layer
        // rejected it. Only these two are customised: every other status still reaches the handler.
        builder.Services.AddProblemDetails(options =>
            options.CustomizeProblemDetails = context =>
            {
                switch (context.ProblemDetails.Status)
                {
                    case StatusCodes.Status401Unauthorized:
                        context.ProblemDetails.Type = "unauthorized";
                        context.ProblemDetails.Title = "Unauthorized.";
                        context.ProblemDetails.Detail = "Unauthorized.";
                        break;
                    case StatusCodes.Status403Forbidden:
                        context.ProblemDetails.Type = "forbidden";
                        context.ProblemDetails.Title = "Forbidden.";
                        context.ProblemDetails.Detail = "You do not have permission to perform this action.";
                        break;
                }
            });
        builder.Services.AddExceptionHandler<ProblemDetailsExceptionHandler>();

        // Customise default API behaviour
        builder.Services.Configure<ApiBehaviorOptions>(options =>
            options.SuppressModelStateInvalidFilter = true);

        builder.Services.AddControllers();

        builder.Services.AddOpenApi();

        builder.Services.AddCors();
    }

    public static void AddKeyVaultIfConfigured(this IHostApplicationBuilder builder)
    {
        var keyVaultUri = builder.Configuration["AZURE_KEY_VAULT_ENDPOINT"];
        if (!string.IsNullOrWhiteSpace(keyVaultUri))
        {
            builder.Configuration.AddAzureKeyVault(
                new Uri(keyVaultUri),
                new DefaultAzureCredential());
        }
    }
}

// Turns the gateway's trusted identity headers into a ClaimsPrincipal. The gateway is the trust
// boundary: it validates the token, strips any client-supplied copies of these headers, and mints its
// own (api-gateway/src/Transforms/TrustedHeadersTransform.cs), so they are trusted implicitly here.
//
// Requires X-User-Id and X-User-Role only. X-User-Email is deliberately NOT required, unlike
// identity-access-service: the gateway forwards it only when the token carries an email claim, so
// requiring it would 401 a Keycloak user with no email. mission-design's ICurrentUser exposes no
// Email at all, so nothing here needs it. (identity-access does require it, because User.Email is a
// required domain field there.)
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

        if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(role))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId),
            new Claim(ClaimTypes.Role, role)
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

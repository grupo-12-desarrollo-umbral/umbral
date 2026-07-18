using System.Diagnostics.CodeAnalysis;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
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
        builder.AddObservability();
        builder.Services.AddDatabaseDeveloperPageExceptionFilter();
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddScoped<ICurrentUser, CurrentUser>();
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
                policy.RequireRole("Administrator");
            });

            options.AddPolicy(AuthorizationPolicies.AdminOrOperator, policy =>
            {
                policy.AddAuthenticationSchemes(TrustedHeadersAuthenticationDefaults.Scheme);
                policy.RequireAuthenticatedUser();
                policy.RequireRole("Administrator", "Operator");
            });

            // The default policy backs a bare `[Authorize]` — "any authenticated user", used by
            // /api/users/me and the reason-coded PermissionsController routes.
            options.DefaultPolicy = new AuthorizationPolicyBuilder(TrustedHeadersAuthenticationDefaults.Scheme)
                .RequireAuthenticatedUser()
                .Build();
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
        builder.Services.Configure<ApiBehaviorOptions>(options => options.SuppressModelStateInvalidFilter = true);
        builder.Services.AddControllers();
        builder.Services.AddOpenApi();
    }
}

file sealed class TrustedHeadersAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public TrustedHeadersAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder)
    {
    }

    // Requires X-User-Email, unlike mission-design and session-operations, which treat it as optional.
    // The difference is deliberate: email is a required domain field here (User.Email is IsRequired,
    // UserConfiguration.cs), and AuthenticateUserCommandHandler rejects a blank email before
    // provisioning a user. A caller with no email claim cannot be represented by this service, so
    // failing at the edge is clearer than failing deeper in the handler.
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

file static class TrustedHeadersAuthenticationDefaults
{
    public const string Scheme = "TrustedHeaders";
}

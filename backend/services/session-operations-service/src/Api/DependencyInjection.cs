using System.Diagnostics.CodeAnalysis;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;
using umbral_backend.Api.Hubs;
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
        builder.Services.AddSignalR(options =>
        {
            options.EnableDetailedErrors = builder.Environment.IsDevelopment();
            options.AddFilter<DomainExceptionHubFilter>();
        });
        builder.Services.AddSingleton<ConnectionTracker>();
        builder.Services.AddScoped<CurrentUserContext>();
        builder.Services.AddScoped<ICurrentUser, CurrentUser>();
        builder.Services.AddScoped<ISessionStateBroadcaster, SessionStateBroadcaster>();
        builder.Services.AddSingleton<ISessionQuestionBroadcaster, SignalRSessionQuestionBroadcaster>();
        builder.Services.AddSingleton<ISessionTimerBroadcaster, SignalRSessionTimerBroadcaster>();
        builder.Services.AddSingleton<ITeamAnsweredBroadcaster, SignalRTeamAnsweredBroadcaster>();
        builder.Services.AddSingleton<ITeamBoardBroadcaster, SignalRTeamBoardBroadcaster>();
        builder.Services.AddSingleton<IEvidenceSubmissionBroadcaster, SignalREvidenceSubmissionBroadcaster>();
        builder.Services.AddSingleton<IOperatorSessionPanelBroadcaster, SignalROperatorSessionPanelBroadcaster>();
        builder.Services.AddSingleton<IParticipantBlockNotifier, ParticipantBlockNotifier>();
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
            options.AddPolicy(AuthorizationPolicies.AdministratorOrOperator, policy =>
            {
                policy.AddAuthenticationSchemes(TrustedHeadersAuthenticationDefaults.Scheme);
                policy.RequireAuthenticatedUser();
                policy.RequireRole("Administrator", "Operator");
            });
            options.AddPolicy(AuthorizationPolicies.ParticipantOrOperator, policy =>
            {
                policy.AddAuthenticationSchemes(TrustedHeadersAuthenticationDefaults.Scheme);
                policy.RequireAuthenticatedUser();
                policy.RequireRole("Participant", "Operator");
            });
        });
        builder.Services.AddExceptionHandler<ProblemDetailsExceptionHandler>();
        builder.Services.Configure<ApiBehaviorOptions>(options => options.SuppressModelStateInvalidFilter = true);
        builder.Services.AddControllers();
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

    // Requires X-User-Id and X-User-Role only. X-User-Email is optional: the gateway forwards it only
    // when the token carries an email claim (api-gateway/src/Transforms/TrustedHeadersTransform.cs),
    // so requiring it 401'd every Keycloak user with no email. Nothing here consumes the email —
    // CurrentUser.DisplayName already falls back when it is absent. (users-service does
    // require it, because User.Email is a required domain field there.)
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var userId = Request.Headers["X-User-Id"].ToString();
        var role = Request.Headers["X-User-Role"].ToString();
        var email = Request.Headers["X-User-Email"].ToString();

        if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(role))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId),
            new(ClaimTypes.Role, role)
        };

        if (!string.IsNullOrWhiteSpace(email))
        {
            claims.Add(new Claim(ClaimTypes.Email, email));
        }

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

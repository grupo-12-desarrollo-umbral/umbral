using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.JwtBearer;

namespace umbral_backend.Infrastructure.Identity.Keycloak;

public class KeycloakJwtEvents : JwtBearerEvents
{
    public override Task TokenValidated(TokenValidatedContext context)
    {
        var roles = context.Principal?.FindAll("realm_access.roles")
            .SelectMany(r => r.Value.Trim('[', ']', '"').Split(','));

        if (roles != null)
        {
            var claims = roles.Select(r => new Claim(ClaimTypes.Role, r.Trim()));
            var identity = context.Principal?.Identity as ClaimsIdentity;
            identity?.AddClaims(claims);
        }

        return Task.CompletedTask;
    }
}

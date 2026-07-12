using System.Security.Claims;

namespace umbral_backend.Api.Services;

public sealed class CurrentUserContext
{
    public ClaimsPrincipal? Principal { get; set; }
}

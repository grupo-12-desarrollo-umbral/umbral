namespace ApiGateway.Transforms;

public sealed class TrustedHeadersTransform : RequestTransform
{
    public override ValueTask ApplyAsync(RequestTransformContext context)
    {
        var user = context.HttpContext.User;
        if (user.Identity?.IsAuthenticated == true)
        {
            AddIfPresent(context, "X-User-Id", user.FindFirstValue(ClaimTypes.NameIdentifier) ?? user.FindFirstValue("sub"));
            AddIfPresent(context, "X-User-Role", GetRealmRole(user));
            AddIfPresent(context, "X-User-Email", user.FindFirstValue(ClaimTypes.Email) ?? user.FindFirstValue("email"));
        }

        // Strip the original token; downstream services must not re-validate it.
        context.ProxyRequest.Headers.Remove("Authorization");

        return ValueTask.CompletedTask;
    }

    private static void AddIfPresent(RequestTransformContext context, string header, string? value)
    {
        if (!string.IsNullOrEmpty(value))
        {
            context.ProxyRequest.Headers.TryAddWithoutValidation(header, value);
        }
    }

    private static string? GetRealmRole(ClaimsPrincipal user)
    {
        var mappedRole = user.FindAll(ClaimTypes.Role).FirstOrDefault()?.Value
            ?? user.FindFirstValue("role")
            ?? user.FindFirstValue("roles");

        if (!string.IsNullOrWhiteSpace(mappedRole))
        {
            return mappedRole;
        }

        var realmAccess = user.FindFirstValue("realm_access");
        if (string.IsNullOrWhiteSpace(realmAccess))
        {
            return null;
        }

        using var document = JsonDocument.Parse(realmAccess);
        if (!document.RootElement.TryGetProperty("roles", out var roles) ||
            roles.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        return roles.EnumerateArray()
            .Select(role => role.GetString())
            .FirstOrDefault(role => !string.IsNullOrWhiteSpace(role));
    }
}

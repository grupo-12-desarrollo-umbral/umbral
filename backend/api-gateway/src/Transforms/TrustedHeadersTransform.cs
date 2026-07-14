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

        // Strip the original token; downstream services must not re-validate it. It arrives one of
        // two ways — the Authorization header, or SignalR's ?access_token query (ADR-0002) — and the
        // gateway has already consumed both by now. Leaving the query one in place would forward a
        // replayable JWT into the downstream request log.
        context.ProxyRequest.Headers.Remove("Authorization");
        context.Query.Collection.Remove(WebSocketTokenExtractionTransform.AccessTokenQueryKey);

        return ValueTask.CompletedTask;
    }

    private static void AddIfPresent(RequestTransformContext context, string header, string? value)
    {
        if (!string.IsNullOrEmpty(value))
        {
            context.ProxyRequest.Headers.TryAddWithoutValidation(header, value);
        }
    }

    // The three application roles, most-privileged first. Downstream services only understand these
    // exact names (identity-access-service's GatewayRoleParser rejects anything else), so the header
    // must carry one of them or nothing.
    private static readonly string[] ApplicationRolesByPrecedence =
    {
        "Administrator",
        "Operator",
        "Participant",
    };

    // A Keycloak token carries every realm role the user holds, and realm_access.roles has no
    // guaranteed order. Self-registered participants also receive Keycloak's baseline roles
    // (default-roles-umbral, offline_access, uma_authorization), so blindly taking the first entry
    // could forward "offline_access" as the role and make provisioning fail. Pick the one
    // application role we recognise, most-privileged first, ignoring the baseline roles.
    private static string? GetRealmRole(ClaimsPrincipal user)
    {
        var candidates = CollectRealmRoleCandidates(user);
        return ApplicationRolesByPrecedence.FirstOrDefault(candidates.Contains);
    }

    private static HashSet<string> CollectRealmRoleCandidates(ClaimsPrincipal user)
    {
        var candidates = new HashSet<string>(StringComparer.Ordinal);

        foreach (var mapped in user.FindAll(ClaimTypes.Role).Select(claim => claim.Value))
        {
            AddRoleCandidate(candidates, mapped);
        }
        AddRoleCandidate(candidates, user.FindFirstValue("role"));
        AddRoleCandidate(candidates, user.FindFirstValue("roles"));

        var realmAccess = user.FindFirstValue("realm_access");
        if (!string.IsNullOrWhiteSpace(realmAccess))
        {
            using var document = JsonDocument.Parse(realmAccess);
            if (document.RootElement.TryGetProperty("roles", out var roles) &&
                roles.ValueKind == JsonValueKind.Array)
            {
                foreach (var role in roles.EnumerateArray())
                {
                    AddRoleCandidate(candidates, role.GetString());
                }
            }
        }

        return candidates;
    }

    private static void AddRoleCandidate(HashSet<string> candidates, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            candidates.Add(value.Trim());
        }
    }
}

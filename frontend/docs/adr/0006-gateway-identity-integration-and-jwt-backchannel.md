# ADR-006: API Gateway to Identity Service Routing and JWT Backchannel Hostname Rewrite

**Status:** Accepted  
**Date:** 2026-05-30  
**Context:** HU-01 Frontend — Keycloak OIDC Flow, Identity Bootstrap

## 1. Gateway Routing Must Match Service Route Prefixes Exactly

### Problem
The API gateway's `appsettings.json` originally routed `/api/identity/{**catch-all}` to the `identity-access-service`. However, the service's endpoint groups map to `/api/users` and `/api/permissions`:

```csharp
// UsersEndpoints.cs
var users = groupBuilder.MapGroup("/api/users");
users.MapPost("/authenticated", BootstrapAuthenticatedUserAsync);

// PermissionsEndpoints.cs
var permissions = groupBuilder.MapGroup("/api/permissions");
permissions.MapGet("/authenticated-platform-access", ...);
```

This meant `POST /api/users/authenticated` from the frontend fell through to a 404 because no gateway route matched it.

### Decision
Split the single `identity` route into two explicit routes that mirror the service's own route groups:

| Route Name | Path | Cluster |
|---|---|---|
| `identity-users` | `/api/users/{**catch-all}` | `identity` |
| `identity-permissions` | `/api/permissions/{**catch-all}` | `identity` |

The `identity-access-service` cluster destination remains `http://identity-access-service:8080/`.

### Files Changed
- `backend/api-gateway/src/appsettings.json`
- `backend/docker-compose.yml` (added `ReverseProxy__Clusters__identity__Destinations__d1__Address`)

---

## 2. JWT Backchannel Requests Need Hostname Rewriting Inside Docker

### Problem
After fixing `KC_HOSTNAME=http://localhost:8080` (see ADR-001), Keycloak's OpenID metadata document contains URLs like:

```json
{
  "issuer": "http://localhost:8080/realms/umbral",
  "jwks_uri": "http://localhost:8080/realms/umbral/protocol/openid-connect/certs"
}
```

The API gateway's `Keycloak__Authority` is correctly set to `http://keycloak:8080/realms/umbral` for initial metadata fetch. However, the JWT bearer middleware follows the `jwks_uri` from the metadata document — which points to `localhost:8080`. Inside the Docker container, `localhost:8080` resolves to the gateway itself, not Keycloak. Result: `404` on `/realms/umbral/protocol/openid-connect/certs` and token validation fails with:

```
IDX10500: Signature validation failed. No security keys were provided
```

### Decision
Inject a custom `DelegatingHandler` into the JWT backchannel that rewrites `localhost:8080` to `keycloak:8080` before the request leaves the gateway:

```csharp
public sealed class RewriteLocalhostBackchannelHandler : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (request.RequestUri is { Host: "localhost", Port: 8080 })
        {
            request.RequestUri = new UriBuilder(request.RequestUri)
            {
                Host = "keycloak",
                Port = 8080
            }.Uri;
        }
        return base.SendAsync(request, cancellationToken);
    }
}
```

Configure it in the JWT bearer options:

```csharp
options.BackchannelHttpHandler = new RewriteLocalhostBackchannelHandler(new SocketsHttpHandler());
```

This keeps the public `KC_HOSTNAME` as `localhost:8080` (correct for browsers) while allowing the gateway inside Docker to resolve Keycloak's internal container hostname.

### Files Changed
- `backend/api-gateway/src/RewriteLocalhostBackchannelHandler.cs` (new)
- `backend/api-gateway/src/DependencyInjection.cs`

---

## 3. Service Containerization

### Problem
The `identity-access-service` existed as source code in `backend/services/identity-access-service/` but had **no Dockerfile** and was **not referenced in `docker-compose.yml`**. The API gateway routed to `http://identity-access-service:8080/`, but the service was never deployed.

### Decision
- Created a Dockerfile using the same multi-stage .NET build pattern as `mission-design-service`.
- Added the service to `backend/docker-compose.yml` with:
  - `depends_on: postgres (healthy)`
  - `ASPNETCORE_ENVIRONMENT: Development`
  - `ConnectionStrings__umbral_backendDb` pointing to `identity_access` database
- Added `api-gateway` `depends_on` for `identity-access-service` so the gateway waits for the service before starting.

### Files Changed
- `backend/services/identity-access-service/Dockerfile` (new)
- `backend/docker-compose.yml`

---

## 4. Logout Redirects to Keycloak's OIDC Logout Endpoint

### Problem
The initial logout only cleared the frontend session cookie (`deleteSession()`). The user's Keycloak SSO session remained active, so clicking "Sign in with Keycloak" again silently re-authenticated without showing the login form. This made it impossible to test the login UX repeatedly or switch users.

### Decision
The `logout` Server Action:
1. Clears the frontend `session` cookie
2. Redirects to Keycloak's OIDC logout endpoint with `client_id` and `post_logout_redirect_uri`:

```
http://localhost:8080/realms/umbral/protocol/openid-connect/logout
  ?client_id=umbral-web
  &post_logout_redirect_uri=http://localhost:3000/login
```

Using `client_id` (rather than `id_token_hint`) is acceptable because:
- We do not persist the `id_token` in the frontend session
- Keycloak accepts `client_id` as a fallback when `id_token_hint` is missing
- The `post_logout_redirect_uri` must be registered in the Keycloak client settings (already configured as `http://localhost:3000/*`)

Additionally, the authorization URL includes `prompt=login` so that even if a Keycloak session exists, the user is always prompted for credentials during the initial OAuth flow.

### Files Changed
- `frontend/app/actions/auth.ts`
- `frontend/app/lib/keycloak.ts`

---

## Summary Table

| Concern | Before | After |
|---|---|---|
| Gateway routes to identity service | `/api/identity/**` | `/api/users/**` + `/api/permissions/**` |
| JWT backchannel inside Docker | `localhost:8080` → 404 | `RewriteLocalhostBackchannelHandler` → `keycloak:8080` |
| Identity service deployment | Not in docker-compose | Dockerfile + service in compose stack |
| Logout | Clear cookie only | Clear cookie + Keycloak logout endpoint + `prompt=login` |

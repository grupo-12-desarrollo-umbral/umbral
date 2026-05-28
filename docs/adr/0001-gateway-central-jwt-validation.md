# Gateway-central JWT validation via YARP

The `api-gateway` is an ASP.NET Core app using YARP as the reverse-proxy engine. It is the single point that validates every inbound Keycloak JWT. On a valid token it strips the `Authorization` header and injects three trusted headers downstream: `X-User-Id` (Keycloak `sub`), `X-User-Role` (realm role), and `X-User-Email`. Individual microservices read these headers via `CurrentUserService` — they do not call Keycloak, hold JWKS config, or run `AddJwtBearer` middleware. The sole exception is `identity-access-service`, which holds Keycloak admin-client config for user provisioning, not for token validation.

## Considered Options

**Distributed validation (rejected):** each service validates the JWT independently against the Keycloak JWKS endpoint. Rejected because it duplicates Keycloak SDK wiring across four services and makes the trust boundary invisible in code — a future developer could accidentally remove gateway routing and services would still accept tokens, masking the bypass.

## Consequences

- `Infrastructure/Identity/Keycloak/` exists only in `identity-access-service`, not in the other three services.
- `ICurrentUser` implementations read `HttpContext` request headers, not JWT claims.
- The gateway must be the entry point for all client traffic; direct service-to-service calls within the cluster bypass it by design and are trusted without token validation.
- Keycloak 24+ moved the `sub` claim into the built-in `basic` client scope. Both `umbral-web` and `umbral-mobile` must list `basic` in their `defaultClientScopes`; without it access tokens carry no `sub` claim and `X-User-Id` is never set.
- The gateway's error contract: **401** means the token is absent, invalid, or expired — the client should re-authenticate. **503** means Keycloak is temporarily unreachable — the client should retry. Conflating the two would cause clients to silently burn refresh tokens during a Keycloak outage.

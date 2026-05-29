# API Gateway and Keycloak design — session summary

This document records the full set of architectural decisions made for the `api-gateway` and Keycloak integration before service implementation began. Individual decisions with deeper rationale are in ADR-0001 and ADR-0002.

## Decisions

**1. Gateway-central JWT validation (YARP)**
The `api-gateway` is an ASP.NET Core app backed by YARP. It is the only component that validates Keycloak-issued JWTs. On a valid token it strips the `Authorization` header and injects three trusted headers downstream. See ADR-0001.

**2. Trusted header contract**
Every downstream service receives:
- `X-User-Id` — Keycloak `sub` claim (stable actor identifier)
- `X-User-Role` — realm role: `Administrador`, `Operador`, or `Participante`
- `X-User-Email` — optional, for display and audit

`ICurrentUser` implementations read these headers via `CurrentUserService`. No `AddJwtBearer` or Keycloak JWKS config in any microservice except `identity-access-service`.

**3. Keycloak realm design**
One realm (`umbral`), two clients:
- `umbral-web` — Authorization Code + PKCE for `Administrador` and `Operador` on the web client
- `umbral-mobile` — Authorization Code + PKCE for `Participante` on the mobile client

Roles are realm-level assignments. The `X-User-Role` header maps directly to the Keycloak realm role.

Both clients must include `basic` in `defaultClientScopes`. Keycloak 24+ moved the `sub` claim into the built-in `basic` scope; omitting it causes access tokens to carry no `sub`, which means `X-User-Id` is never injected by the gateway.

The Compose `healthcheck` for the Keycloak container must probe the management port (9000, `GET /health/ready`) rather than the main HTTP port (8080). The `/health/ready` endpoint returns 200 only after realm imports complete; a check against port 8080 can pass while the `umbral` realm is still being imported, causing the gateway to start before its OIDC metadata endpoint is available.

**4. JoinToken is a post-authentication application guard**
Participants authenticate with Keycloak first (mobile OIDC flow). The `JoinToken` issued by `identity-access-service` is consumed after the gateway has already validated the actor's Keycloak JWT. It is an application-level check on top of authentication, not a bypass around it.

**5. AuthenticateUser is post-login provisioning**
`AuthenticateUser` in `identity-access-service` is not a re-implementation of Keycloak login. It is the step where the client, after receiving a Keycloak JWT, calls `identity-access-service` to synchronize or create the application-side `User` record from the Keycloak claims.

**6. WebSocket token extraction at the gateway**
For SignalR connections the gateway reads the token from the `?access_token` query parameter instead of the `Authorization` header. Validation and header injection are identical to the HTTP path. `session-operations-service` receives the same three trusted headers for hub connections. See ADR-0002.

**7. Keycloak wiring scope**
`Infrastructure/Identity/Keycloak/` exists only in `identity-access-service`. The other three services carry no Keycloak SDK dependency.

## What this unblocks

- `services/api-gateway/` can be scaffolded as an ASP.NET Core + YARP project
- Keycloak and the gateway can be wired in `deploy/` Docker Compose before any service Phase X.4 work begins
- All four services' `CurrentUserService` implementations are now well-defined: read the three trusted headers, no token parsing
- Backend-agent can start `mission-design-service` phase 1.1 immediately — Domain and Application layers have no gateway or auth dependency

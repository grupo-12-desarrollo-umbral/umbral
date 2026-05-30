# Identity Access Service

`identity-access-service` implements the **Identity** bounded context. It handles post-login provisioning, user identity, role assignment, access-policy evaluation, and identity-provider session tracking — delegating authentication to Keycloak.

## Why This Service Exists

Keycloak authenticates users and issues tokens, but it does not own Umbral's application domain. This service bridges that gap:

| Keycloak Does | identity-access-service Does |
|---------------|------------------------------|
| Authenticates credentials | **Post-Login Provisioning** — creates/syncs the application-side `User` record from Keycloak claims after a successful login |
| Issues JWTs with realm roles | **Access Policy** — evaluates which `ProtectedCapability` a given `User`+`Role` may access, independent of token structure |
| Manages OIDC sessions | **IdentityProviderSession** — persists a subset of IDP session state for Umbral's traceability, revocation, and correlation needs |
| Revokes tokens | **User Deactivation** — marks a `User` as inactive in the application domain, blocking access regardless of token validity |
| — | **Domain Events** — publishes `UserProvisioned`, `UserAccessDeactivated`, `UserRoleAssigned`, etc. for other bounded contexts to react to |
| — | **JoinToken** — owns and validates application-level tokens that gate entry into specific `LiveSession`/`Team` pairs |

Without this service, every downstream context would need to reason about roles, access rules, and user state from raw Keycloak data — violating the bounded-context boundary and scattering identity logic across the system.

## Architecture Overview

```
Keycloak (authenticates)
    ↓ JWT
api-gateway (validates JWT, strips it)
    ↓ X-User-Id, X-User-Role, X-User-Email
identity-access-service (reads trusted headers)
    ↓
Application-side User, Role, Access Facts
```

- **Keycloak** authenticates users and issues JWTs.
- **api-gateway** is the only component that validates JWTs. It strips the `Authorization` header and forwards three trusted headers: `X-User-Id`, `X-User-Role`, `X-User-Email`.
- **identity-access-service** reads those headers and provides post-login provisioning, user lookup, and access-policy decisions.

## Domain Model

### Entities

| Entity | Description |
|--------|-------------|
| `User` | Aggregate root. Represents an actor known to the platform. Has `ExternalIdentityId` (Keycloak `sub`), `DisplayName`, `Email`, `Role`, `IsActive`. |
| `IdentityProviderSession` | Records Keycloak session state for traceability/revocation. Binding: `UserId`, `ProviderName`, `ProviderSessionKey`, `StartedAt`, `ExpiresAt`, `RevokedAt`. |

### Roles

| Role | Enum Value |
|------|-----------|
| `Administrator` | 1 |
| `Operator` | 2 |
| `Participant` | 3 |

### Protected Capabilities & Access Matrix

| Capability | Allowed Roles |
|------------|--------------|
| `AuthenticatedPlatformAccess` | Any active user |
| `AdministratorPanel` | `Administrator` only |
| `OperatorPanel` | `Administrator`, `Operator` |
| `ParticipantExperience` | `Participant` |

### Domain Services

- **`AccessPolicy`** — Evaluates whether a `User` can access a `ProtectedCapability`. Returns `AccessDecision` (allowed/denied + reason). `EnsureCanAccess()` throws on denial.
- **`IdentityProvisioningPolicy`** — Synchronizes or creates a `User` from Keycloak claims ("Post-Login Provisioning"). Validates that existing users' `ExternalIdentityId` matches.
- **`AccessDecision`** — Value object: `Capability`, `IsAllowed`, `Reason`.

### Domain Events (6)

`UserProvisioned`, `UserRoleAssigned`, `UserAccessDeactivated`, `AccessDecisionRecorded`, `IdentityProviderSessionStarted`, `IdentityProviderSessionEnded`.

## Application Layer (CQRS + MediatR)

### Commands

| Command | Handler | Purpose |
|---------|---------|---------|
| `AuthenticateUserCommand` | `AuthenticateUserCommandHandler` | Post-login provisioning: synchronize or create `User`, return actor profile + access decision. |

### Queries

| Query | Handler | Purpose |
|-------|---------|---------|
| `GetAuthenticatedActorProfileQuery` | `GetAuthenticatedActorProfileQueryHandler` | Return the current user's profile from `ICurrentUser`. |
| `CheckProtectedCapabilityAccessQuery` | `CheckProtectedCapabilityAccessQueryHandler` | Evaluate whether current user can access a capability. |

### Pipeline Behaviours (4)

| Behaviour | Purpose |
|-----------|---------|
| `AuthorizationBehaviour` | Enforces `[Authorize(Roles = "...")]` on request types via `ICurrentUser`. |
| `ValidationBehaviour` | Runs FluentValidation validators; throws `ValidationException` on failure. |
| `PerformanceBehaviour` | Logs warnings for requests exceeding 500ms. |
| `UnhandledExceptionBehaviour` | Logs and rethrows unhandled exceptions. |

## API Endpoints

### User Endpoints (`/api/users`)

| Method | Path | Description | Auth |
|--------|------|-------------|------|
| `POST` | `/api/users/authenticated` | Bootstrap/sync user after Keycloak login. Body: `{ "displayName": "..." }`. Requires trusted gateway headers. | Headers |
| `GET` | `/api/users/me` | Get current authenticated user profile. | Headers |

### Permission Endpoints (`/api/permissions`)

| Method | Path | Description | Auth |
|--------|------|-------------|------|
| `GET` | `/api/permissions/authenticated-platform-access` | Check if current user can access the platform. | Headers |

### Health Endpoints

| Method | Path | Description |
|--------|------|-------------|
| `GET` | `/health` | Database connectivity check. Returns `200 { "Status": "Healthy" }` or `503`. |
| `GET` | `/alive` | Liveness check. Always returns `200 { "Status": "Alive" }`. |

### Trusted Gateway Headers

The service reads three headers set by the api-gateway:

| Header | Source | Example |
|--------|--------|---------|
| `X-User-Id` | Keycloak `sub` claim | `"abc-123"` |
| `X-User-Role` | Keycloak realm role | `"Administrator"` |
| `X-User-Email` | Keycloak email claim | `"user@example.com"` |

Endpoints that require authentication check these headers via `ICurrentUser` (implemented by `Api.Services.CurrentUser` reading `IHttpContextAccessor`). Missing headers → `401 Unauthorized`. Deactivated user → `403 Forbidden`.

### Error Responses

All exceptions are mapped to RFC 7807 `ProblemDetails`:

| Exception | HTTP Status |
|-----------|-------------|
| `NotFoundException` | 404 |
| `ValidationException` | 400 |
| `UnauthorizedAccessException` | 401 |
| `ForbiddenAccessException` | 403 |
| `DeactivatedUserAccessDeniedException` | 403 |
| `UserRoleNotAuthorizedException` | 403 |
| Everything else | 500 |

## Infrastructure (EF Core + PostgreSQL)

- **Database**: PostgreSQL via Npgsql.
- **Connection string**: Configured as `umbral_backendDb` (environment variable or central config).
- **Tables**: `users`, `identity_provider_sessions`.
- **Migrations**: Applied automatically at startup via `dbContext.Database.MigrateAsync()`.
- **Interceptors**: `AuditableEntityInterceptor` (auto-sets Created/Modified timestamps) and `DispatchDomainEventsInterceptor` (publishes domain events via MediatR after save).
- **Design-time factory**: `ApplicationDbContextFactory` reads connection string from env var `IDENTITY_ACCESS_SERVICE_CONNECTION_STRING`.

## Testing

### Test Structure

```
tests/
  UnitTests/          — Pure logic, Moq for dependencies
  IntegrationTests/   — WebApplicationFactory + real PostgreSQL via Testcontainers
  EndToEndTests/      — Empty (not in current sprint)
```

### Coverage (coverlet.msbuild, merged from Unit + Integration)

| Module | Line | Branch | Method |
|--------|------|--------|--------|
| `umbral_backend.Domain` | 90.56% | 91.83% | 86.07% |
| `umbral_backend.Infrastructure` | 94.70% | 53.84% | 72.72% |
| `umbral_backend.Application` | 98.57% | 92.00% | 98.03% |
| `umbral_backend.Api` | 97.91% | 91.66% | 100% |
| **Total** | **94.95%** | **85.71%** | **88.33%** |

Coverage is collected per ADR-0005: `coverlet.msbuild` with `/p:CollectCoverage=true /p:CoverletOutputFormat=json` and `MergeWith` chaining. Threshold is 95% line coverage (total: 94.95%).

### Unit Tests (56 tests, ~712ms)

| Area | Tests | What |
|------|-------|------|
| `User` entity | Provision, synchronize, role assignment, deactivate (twice → exception), events, validation (blank fields → exceptions) | Domain |
| `IdentityProviderSession` entity | Start, end, idempotent end, validation (blank fields, invalid expiry) | Domain |
| `ValueObject` base | Equality, null handling | Domain |
| `BaseEntity` | Add/remove/clear domain events | Domain |
| `IdentityProvisioningPolicy` | Provision new, synchronize existing, identity mismatch → exception | Domain |
| `AccessPolicy` | Role/capability matrix (6 Theory rows), deactivated user → exception, wrong role → exception | Domain |
| `GatewayRoleParser` | Parse/TryParse supported roles, unsupported role → exception | Application |
| `AuthorizationBehaviour` | Anonymous → 401, wrong role → 403, correct role → pass, open request (no `[Authorize]`) | Application |
| `PerformanceBehaviour` | Fast request → no log, slow request (550ms) → warning logged | Application |
| `ValidationException` | Default ctor, errors grouping | Application |
| `AuthenticateUserCommandHandler` | Provision new, synchronize existing, deactivated → exception | Application |
| `GetAuthenticatedActorProfileQueryHandler` | Return profile, missing identity → 401, user not found | Application |
| `CheckProtectedCapabilityAccessQueryHandler` | Authorized → allowed, unauthorized → exception, deactivated → exception | Application |
| `AuthenticateUserCommandValidator` | Valid command, invalid command (4 field errors) | Application |
| `CheckProtectedCapabilityAccessQueryValidator` | Known capability, out-of-range enum → error | Application |
| `ProblemDetailsExceptionHandler` | Known exceptions → correct status codes (Theory), validation → 400 + combined detail, unknown → 500 | Api |
| `ApplicationDbContextFactory` | Connection string from env var | Infrastructure |

### Integration Tests (7 tests, ~749ms)

| Area | Tests | What |
|------|-------|------|
| API Endpoints | Bootstrap + read-back cycle (provision user via POST, GET /me, verify DB), no-headers → 401, deactivated user → 403, bootstrap without headers → 401, health → 200, alive → 200 | Api/infra |
| Persistence | Full authenticate + retrieve profile through real `UserRepository` + PostgreSQL | Infra |

## Running the Service

```bash
# Build
dotnet build src/Api/Api.csproj

# Run (requires PostgreSQL connection string)
IDENTITY_ACCESS_SERVICE_CONNECTION_STRING="Host=localhost;Database=umbral;Username=postgres;Password=..." \
  dotnet run --project src/Api/Api.csproj

# Tests - Unit
dotnet test tests/UnitTests/Application.UnitTests.csproj -c Release

# Tests - Integration (requires Docker for Testcontainers)
dotnet test tests/IntegrationTests/Infrastructure.IntegrationTests.csproj -c Release

# Tests - Full coverage (merge chain)
dotnet test tests/UnitTests/Application.UnitTests.csproj -c Release \
  /p:CollectCoverage=true /p:CoverletOutputFormat=json /p:CoverletOutput=coverage.unit.json
dotnet test tests/IntegrationTests/Infrastructure.IntegrationTests.csproj -c Release \
  /p:CollectCoverage=true /p:CoverletOutputFormat=json /p:CoverletOutput=coverage.json \
  /p:MergeWith="../UnitTests/coverage.unit.json" /p:Threshold=95 /p:ThresholdType=line /p:ThresholdStat=total
```

## Post-Login Flow (End-to-End)

```
1. Client authenticates with Keycloak → receives JWT
2. Client sends POST /api/users/authenticated with { "displayName": "..." }
   (Gateway adds X-User-Id, X-User-Role, X-User-Email from JWT)
3. identity-access-service:
   a. Parses headers via ICurrentUser
   b. Looks up User by ExternalIdentityId
   c. IdentityProvisioningPolicy.SynchronizeOrCreate()
   d. AccessPolicy.Evaluate(user, AuthenticatedPlatformAccess)
   e. Persists (AddAsync or UpdateAsync)
   f. Returns actor profile + access decision
4. Client uses GET /api/users/me for profile
5. Client uses GET /api/permissions/authenticated-platform-access for capability checks
```

## Key Decisions (ADRs)

- [ADR-0001](backend/docs/adr/0001-gateway-central-jwt-validation.md): Gateway-central JWT validation; per-service validation rejected.
- [ADR-0003](backend/docs/adr/0003-api-gateway-keycloak-design-summary.md): Keycloak realm design, trusted header contract, JoinToken is post-auth guard.
- [ADR-0004](backend/docs/adr/0004-required-domain-patterns.md): Proxy pattern for role/policy-based guards.
- [ADR-0005](backend/docs/adr/0005-coverlet-msbuild-for-aggregate-coverage.md): coverlet.msbuild with MergeWith chaining, 95% threshold.

## Boundary Rules

- `identity-access-service` does **not** validate JWTs — the gateway does that.
- `identity-access-service` does **not** own session admission — `SessionOperations` owns that.
- `identity-access-service` returns **Access Facts** (identity, role, token validity, coarse access-policy results); downstream services make final authorization decisions.

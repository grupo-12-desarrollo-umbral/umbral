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
| Manages realm roles via the admin UI | **Keycloak Role Sync** — when an admin changes a user's role via the dashboard, propagates the change to Keycloak's realm roles via the Admin API, keeping both in sync |
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
- **`IdentityProvisioningPolicy`** — Synchronizes or creates a `User` from Keycloak claims ("Post-Login Provisioning"). For new users, provisions with the Keycloak-provided role. For existing users, synchronizes only the profile (display name, email) — the application-side role is left intact so admin dashboard changes are not overwritten on re-login. Validates that existing users' `ExternalIdentityId` matches.
- **`AccessDecision`** — Value object: `Capability`, `IsAllowed`, `Reason`.

### Domain Events (6)

`UserProvisioned`, `UserRoleAssigned`, `UserAccessDeactivated`, `AccessDecisionRecorded`, `IdentityProviderSessionStarted`, `IdentityProviderSessionEnded`.

## Application Layer (CQRS + MediatR)

### Commands

| Command | Handler | Purpose |
|---------|---------|---------|
| `AuthenticateUserCommand` | `AuthenticateUserCommandHandler` | Post-login provisioning: synchronize or create `User`, return actor profile + access decision. |
| `AssignUserRoleCommand` | `AssignUserRoleCommandHandler` | Change a user's role. Updates the application DB and syncs the new realm role to Keycloak via `IKeycloakAdminService`. Only `Administrator` may call this. |
| `DeactivateUserCommand` | `DeactivateUserCommandHandler` | Soft-deactivate a user (`IsActive = false`), preserving history. Emits `UserAccessDeactivated`. Only `Administrator` may call this. |

### Queries

| Query | Handler | Purpose |
|-------|---------|---------|
| `GetAuthenticatedActorProfileQuery` | `GetAuthenticatedActorProfileQueryHandler` | Return the current user's profile from `ICurrentUser`. |
| `CheckProtectedCapabilityAccessQuery` | `CheckProtectedCapabilityAccessQueryHandler` | Evaluate whether current user can access a capability. |
| `GetUsersQuery` | `GetUsersQueryHandler` | Paginated user catalog. Returns all roles; `Administrator` or `Operator` may call it. |

### Pipeline Behaviours (4)

| Behaviour | Purpose |
|-----------|---------|
| `AuthorizationBehaviour` | Enforces `[Authorize(Roles = "...")]` on request types via `ICurrentUser`. |
| `ValidationBehaviour` | Runs FluentValidation validators; throws `ValidationException` on failure. |
| `PerformanceBehaviour` | Logs warnings for requests exceeding 500ms. |
| `UnhandledExceptionBehaviour` | Logs and rethrows unhandled exceptions. |

## API Endpoints

---

### `POST /api/users/authenticated`

Bootstrap or sync the application-side `User` record after a successful Keycloak login. New users are provisioned with the role from the Keycloak JWT. Existing users get their profile (display name, email) synced — the application-side role is preserved so dashboard changes are not overwritten.

Requires trusted gateway headers (`X-User-Id`, `X-User-Role`, `X-User-Email`). No additional auth.

**Request:**
```json
{
  "displayName": "string"
}
```

**Response `200`**
```json
{
  "userId": 0,
  "externalIdentityId": "string",
  "displayName": "string",
  "email": "string",
  "role": "Administrator | Operator | Participant",
  "accessDecision": {
    "capability": "AuthenticatedPlatformAccess",
    "isAllowed": true,
    "reason": "string"
  }
}
```

---

### `GET /api/users/me`

Returns the profile of the currently authenticated user based on the trusted gateway headers.

Requires trusted gateway headers.

**Response `200`**
```json
{
  "userId": 0,
  "externalIdentityId": "string",
  "displayName": "string",
  "email": "string",
  "role": "Administrator | Operator | Participant"
}
```

---

### `GET /api/users`

Paginated catalog of all users. Returns every role — authorization gates what the caller can do with the data, not what they can see.

**Auth:** `Administrator`, `Operator`

**Query parameters:** `page` (default 1), `pageSize` (default 20)

**Response `200`**
```json
{
  "items": [ { "userId": 0, "displayName": "string", "email": "string", "role": "...", "isActive": true, "createdAt": "...", "updatedAt": "..." } ],
  "totalCount": 0,
  "page": 1,
  "pageSize": 20,
  "totalPages": 0,
  "hasPreviousPage": false,
  "hasNextPage": false
}
```

---

### `PATCH /api/users/{id}/role`

Change a user's role. Persists the change to the application database, then syncs the new realm role to Keycloak via the Admin API. Idempotent: assigning the same role again succeeds without error.

**Auth:** `Administrator` only

**Request:**
```json
{
  "role": "Administrator"
}
```

**Response `204`** — no body

---

### `DELETE /api/users/{id}/access`

Soft-deactivate a user by their internal numeric ID. Sets `IsActive = false`, emits a `UserAccessDeactivated` domain event. Preserves history — the user record is not removed.

**Auth:** `Administrator` only

**Response `204`** — no body

---

### `GET /api/permissions/authenticated-platform-access`

Evaluates whether the currently authenticated user may access the platform. Returns the access decision from the `AccessPolicy`.

Requires trusted gateway headers.

**Response `200`**
```json
{
  "capability": "AuthenticatedPlatformAccess",
  "isAllowed": true,
  "reason": "string"
}
```

---

### Team Endpoints (`/api/teams`)

---

### `POST /api/teams`

Creates a new team with the given display name and unique team code. The team code must not already exist — a `TeamCodeAlreadyExistsException` is raised otherwise.

**Auth:** `Administrator` only

**Request:**
```json
{
  "displayName": "Alpha Squad",
  "teamCode": "ALPHA-01"
}
```

**Response `201`**
```json
{
  "teamId": "guid-code-here"
}
```

---

### `GET /api/teams`

Paginated list of all teams, including both active and deactivated ones.

**Auth:** `Administrator`, `Operator`

**Query parameters:** `page` (default 1), `pageSize` (default 20)

**Response `200`**
```json
{
  "items": [
    {
      "teamId": "guid-code-here",
      "displayName": "Alpha Squad",
      "teamCode": "ALPHA-01",
      "isActive": true,
      "createdAt": "2026-05-31T00:00:00Z",
      "updatedAt": "2026-05-31T00:00:00Z"
    }
  ],
  "totalCount": 1,
  "page": 1,
  "pageSize": 20,
  "totalPages": 1,
  "hasPreviousPage": false,
  "hasNextPage": false
}
```

---

### `GET /api/teams/{id}`

Returns a single team by its GUID identifier. Returns `404 NotFound` if the team does not exist.

**Auth:** `Administrator`, `Operator`

**Response `200`**
```json
{
  "teamId": "guid-code-here",
  "displayName": "Alpha Squad",
  "teamCode": "ALPHA-01",
  "isActive": true,
  "createdAt": "2026-05-31T00:00:00Z",
  "updatedAt": "2026-05-31T00:00:00Z"
}
```

---

### `PATCH /api/teams/{id}`

Updates the display name and/or team code of an existing team. The new team code must not already be in use by another team. Returns `204 No Content` on success.

**Auth:** `Administrator` only

**Request:**
```json
{
  "displayName": "Bravo Squad",
  "teamCode": "BRAVO-01"
}
```

**Response `204`** — no body

---

### `DELETE /api/teams/{id}/status`

Deactivates a team, setting its `IsActive` flag to `false`. Returns the updated team state. Raises `TeamAlreadyDeactivatedException` if the team is already deactivated.

**Auth:** `Administrator` only

**Response `200`**
```json
{
  "teamId": "guid-code-here",
  "displayName": "Alpha Squad",
  "teamCode": "ALPHA-01",
  "isActive": false,
  "createdAt": "2026-05-31T00:00:00Z",
  "updatedAt": "2026-05-31T00:00:00Z"
}
```

---

### `GET /api/sessions/{sessionCode}/teams`

Returns the teams associated with a live session, marking each one according to the participant's membership status. This is the participant-facing team lobby — it does not admit the participant to any team; it only lists what is available.

**Auth:** `Participant` only (via `ParticipantExperience` capability)

**Response `200`**
```json
{
  "liveSessionId": "guid",
  "sessionCode": "RSF231",
  "teams": [
    { "teamId": "guid", "displayName": "Blue Owls", "joinState": "mine" },
    { "teamId": "guid", "displayName": "Red Foxes", "joinState": "locked" }
  ]
}
```

**`joinState` values** — defined in `ParticipantSessionTeamLobbyService`:

| Value | Condition |
|-------|-----------|
| `mine` | The participant is a **member of this team** (pre-assigned by operator via HU-05). |
| `locked` | The participant is already a member of **another** team in this session. Enforces the "at most one active membership per participant per session" invariant — they cannot join a second team. |
| `joinable` | The participant has **no membership yet** in this session. All teams are available for self-assignment. |

**Logic summary**: if the participant already has a team → only theirs is `mine`, the rest are `locked`. If they have no team → all appear `joinable`; selecting one self-assigns their membership.

**Error responses:**

| Status | Reason |
|--------|--------|
| `401` | Missing or invalid trusted gateway headers. |
| `403` | Actor role is not `Participant`. |
| `404` | Session code not found. |

---

### Health Endpoints

---

### `GET /health`

Database connectivity check. Returns healthy when the database is reachable.

**Response `200`**
```json
{
  "status": "Healthy"
}
```

**Response `503`** — when the database is unreachable

---

### `GET /alive`

Liveness check. Always returns `200 OK` regardless of database state.

**Response `200`**
```json
{
  "status": "Alive"
}
```

---

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
| `TeamCodeAlreadyExistsException` | 409 |
| `TeamAlreadyDeactivatedException` | 409 |
| `TeamCodeRequiredException` | 400 |
| `TeamDisplayNameRequiredException` | 400 |
| `DeactivatedUserRoleAssignmentNotAllowedException` | 422 |
| Everything else | 500 |

### Keycloak Admin Role Sync

When an admin changes a user's role via `PATCH /api/users/{id}/role`, the handler persists the change to the application DB and then calls `KeycloakAdminService.SyncUserRoleAsync()`. This service:

1. Authenticates with the Keycloak master realm using the `admin-cli` client via `KEYCLOAK_ADMIN` / `KEYCLOAK_ADMIN_PASSWORD` (configured in docker-compose).
2. Fetches available realm roles from Keycloak.
3. Retrieves the user's current realm role mappings.
4. Removes existing roles (except the one being assigned) and adds the target role.

Failures are logged but do not roll back the DB update — the application DB is the source of truth. Configuration lives in the `Keycloak` section of `appsettings.json`:

```json
"Keycloak": {
  "AdminAuthority": "http://keycloak:8080",
  "Realm": "umbral",
  "AdminUsername": "admin",
  "AdminPassword": "admin"
}
```

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
  IntegrationTests/   — WebApplicationFactory + real PostgreSQL via Testcontainers (`postgres:16`)
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

### Unit Tests (71 tests, ~762ms)

| Area | Tests | What |
|------|-------|------|
| `User` entity | Provision, synchronize, role assignment, deactivate (twice → exception), events, validation (blank fields → exceptions) | Domain |
| `IdentityProviderSession` entity | Start, end, idempotent end, validation (blank fields, invalid expiry) | Domain |
| `ValueObject` base | Equality, null handling | Domain |
| `BaseEntity` | Add/remove/clear domain events | Domain |
| `IdentityProvisioningPolicy` | Provision new, synchronize existing (profile only, role left intact), identity mismatch → exception | Domain |
| `AccessPolicy` | Role/capability matrix (6 Theory rows), deactivated user → exception, wrong role → exception | Domain |
| `GatewayRoleParser` | Parse/TryParse supported roles, unsupported role → exception | Application |
| `AuthorizationBehaviour` | Anonymous → 401, wrong role → 403, correct role → pass, open request (no `[Authorize]`) | Application |
| `PerformanceBehaviour` | Fast request → no log, slow request (550ms) → warning logged | Application |
| `ValidationException` | Default ctor, errors grouping | Application |
| `AssignUserRoleCommandHandler` | Assign role, idempotent same-role, deactivated target → exception, non-admin caller → exception, unknown role → exception | Application |
| `AuthenticateUserCommandHandler` | Provision new, synchronize existing (profile only, role preserved), deactivated → exception | Application |
| `DeactivateUserCommandHandler` | Deactivate user, already deactivated → exception | Application |
| `GetUsersQueryHandler` | List users as admin, participant forbidden → exception | Application |
| `GetAuthenticatedActorProfileQueryHandler` | Return profile, missing identity → 401, user not found | Application |
| `CheckProtectedCapabilityAccessQueryHandler` | Authorized → allowed, unauthorized → exception, deactivated → exception | Application |
| `AuthenticateUserCommandValidator` | Valid command, invalid command (4 field errors) | Application |
| `DeactivateUserCommandValidator` | Valid command, invalid id → error | Application |
| `GetUsersQueryValidator` | Valid query, out-of-range page → error | Application |
| `CheckProtectedCapabilityAccessQueryValidator` | Known capability, out-of-range enum → error | Application |
| `ProblemDetailsExceptionHandler` | Known exceptions → correct status codes (Theory), validation → 400 + combined detail, unknown → 500 | Api |
| `ApplicationDbContextFactory` | Connection string from env var | Infrastructure |

### Integration Tests (11 tests, ~749ms)

| Area | Tests | What |
|------|-------|------|
| API Endpoints | Bootstrap + read-back cycle (provision user via POST, GET /me, verify DB), no-headers → 401, deactivated user → 403, bootstrap without headers → 401, list users → paginated results, deactivate user → 204, deactivate already-deactivated → 400, health → 200, alive → 200 | Api/infra |
| Persistence | Full authenticate + retrieve profile through real `UserRepository` + PostgreSQL | Infra |

## Seeding Users and Teams (Development)

Once the stack is running, seed test users and teams by running the script from the repo root:

```bash
./backend/scripts/seed-users.sh
```

The script provisions the dev identities in Keycloak (operators + eight participants), bootstraps each into `identity_access` through the gateway, then registers 4 teams (Delta/Echo/Bismarck/Los Panas) and assigns the eight participants 2-per-team via `POST /api/teams` and `POST /api/teams/{id}/participants`. It is idempotent — existing teams (409) are reused and existing memberships (409) are skipped. To target a different base URL or Keycloak instance, pass them as arguments:

```bash
./backend/scripts/seed-users.sh http://localhost:8000 http://localhost:8080
```

## Running the Service

```bash
# Build
dotnet build src/Api/Api.csproj

# Run (requires PostgreSQL connection string)
IDENTITY_ACCESS_SERVICE_CONNECTION_STRING="Host=localhost;Database=umbral;Username=postgres;Password=..." \
  dotnet run --project src/Api/Api.csproj

# Tests - Unit
dotnet test tests/UnitTests/Application.UnitTests.csproj -c Release

# Tests - Integration (requires Docker for Testcontainers; fixture disables Ryuk and waits up to 2 minutes for PostgreSQL startup)
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
      - New user: provisions with Keycloak-provided role
      - Existing user: syncs display name + email only, preserves existing role
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
- [ADR-0006](backend/docs/adr/0006-hu02-user-management-architecture.md): Authorization on operation, not data visibility — GET /api/users returns all roles; DELETE is resource-oriented on the access sub-resource.
- [ADR-0007](../frontend/docs/adr/0007-role-authority-app-database.md): Application database is the source of truth for user roles; login no longer overwrites from the Keycloak JWT; role changes sync outward to Keycloak via the Admin API.

## Boundary Rules

- `identity-access-service` does **not** validate JWTs — the gateway does that.
- `identity-access-service` does **not** own session admission — `SessionOperations` owns that.
- `identity-access-service` returns **Access Facts** (identity, role, token validity, coarse access-policy results); downstream services make final authorization decisions.

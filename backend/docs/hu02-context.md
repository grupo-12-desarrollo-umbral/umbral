# HU-02 Context — User Access Management

> Paste this section into any agent session that needs context for HU-02.
> Last updated: 2026-05-30 | Branch: `feature/hu-02-user-access-management`

## State

- DES-6 (HU-02): **In Progress**
- DES-5 (HU-01): **In Progress**
- DES-67 (PRD): **Backlog**, labels: `ready-for-agent`, `svc:identity-access-service`
- Branch: `feature/hu-02-user-access-management` (branched from `develop` before HU-01 was merged)

## What HU-01 has already landed (reuse candidates for HU-02)

All of this lives on `feature/hu-01-general-user-login`.

**Domain layer**
- `User` aggregate root — `ExternalIdentityId`, `DisplayName`, `Email`, `Role`, `IsActive`; `Deactivate()` method already defined (throws `UserAccessAlreadyDeactivatedException` on double-deactivation); domain events: `UserProvisioned`, `UserAccessDeactivated`, `UserRoleAssigned`, `AccessDecisionRecorded`
- `IdentityProviderSession` entity with `RevokedAt` / `ExpiresAt`
- `Role` enum: `Administrator`, `Operator`, `Participant`
- `ProtectedCapability` enum + access matrix
- `AccessPolicy` domain service — evaluates `User + Capability → AccessDecision`; `EnsureCanAccess()` throws `DeactivatedUserAccessDeniedException` for inactive users
- `IdentityProvisioningPolicy` — `SynchronizeOrCreate()` from Keycloak claims

**Application layer**
- `AuthenticateUserCommand` / `AuthenticateUserCommandHandler` (post-login provisioning; already checks deactivated state)
- `CheckProtectedCapabilityAccessQuery` + handler
- `GetAuthenticatedActorProfileQuery` + handler
- `AuthorizationBehaviour` pipeline guard (roles + `[Authorize]`)
- `ICurrentUser` / `GatewayRoleParser` (trusted-header identity)

**Infrastructure / API**
- EF Core `users` + `identity_provider_sessions` tables, Init migration, `UserRepository`
- Endpoints: `POST /api/users/authenticated`, `GET /api/users/me`, `GET /api/permissions/authenticated-platform-access`
- `ProblemDetailsExceptionHandler` mapping `DeactivatedUserAccessDeniedException → 403`

**Coverage:** ~95% across all layers; 56 unit tests + 7 integration tests.

## What HU-02 adds on top (per PRD DES-67)

| Concern | New work |
|---|---|
| `DeactivateUserAccess` use case | `DeactivateUserCommand` + handler — soft-deactivate (`IsActive = false`), preserve history, emit `UserAccessDeactivated` event |
| User access catalogue | `ListUsers` / `GetUsers` query handler — paginated read for admin/operator |
| Deactivated-user enforcement at login | `AuthenticateUserCommandHandler` must reject deactivated users; `AccessPolicy.EnsureCanAccess()` already throws — confirm it is wired in the handler |
| Deactivated-user enforcement at protected endpoints | `AuthorizationBehaviour` or guard path must propagate 403 for deactivated users hitting capabilities |
| API endpoints | `DELETE /api/users/{id}/access` (deactivate) and `GET /api/users` (list registered users, admin/operator-scoped) |
| Frontend | User list view; deactivate action; error on deactivated-user login; route/component guard for deactivated users |

## Touched surfaces

- `backend/` identity-access-service
- `frontend/` admin/user-access UI
- `backend/frontend` API contract boundary through the api-gateway for the two user-access endpoints and their request/response/error shapes

## Committed phases

| Commit | Phase | Description |
|---|---|---|
| `1a2c2c1` | X.1 | Domain layer |
| `5913c88` | X.2 | Application layer |
| `15bd154` | X.3 | Infrastructure layer |
| `be7eba8` | — | ADR + prompt doc update |

## Known quirks / gotchas

- In integration test lambda parameters, avoid `listedUser` as a variable name (conflicts with `ListUsers` method naming convention). Use `u` or `user` instead:

  ```csharp
  var activeRecord = await context.Users.SingleOrDefaultAsync(
      u => u.Id == persistedUser.Id && u.IsActive);
  ```

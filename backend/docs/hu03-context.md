# HU-03 Context — Role and Permission Assignment

> Paste this section into any agent session that needs context for HU-03.
> Last updated: 2026-05-30 | Branch: `feature/hu-03-role-permission-assignment`

## State

- DES-7 (HU-03): **In Progress**, labels: `Feature`, `ready-for-agent`, `svc:identity-access-service`
- DES-6 (HU-02): **Done**
- DES-67 (PRD): **Backlog**, labels: `ready-for-agent`, `svc:identity-access-service`
- Branch: `feature/hu-03-role-permission-assignment` (branch from `develop` after HU-02 merges, or from `feature/hu-02-user-access-management` if not yet merged)

## What HU-01 and HU-02 have already landed (reuse candidates for HU-03)

All of this is on `feature/hu-02-user-access-management` (or `develop` once HU-02 merges).

**Domain layer**
- `User` aggregate root — `ExternalIdentityId`, `DisplayName`, `Email`, `Role`, `IsActive`; `Deactivate()` (throws `UserAccessAlreadyDeactivatedException` on double-deactivation)
- `Role` enum: `Administrator`, `Operator`, `Participant`
- `ProtectedCapability` enum + permission matrix (`AuthenticatedPlatformAccess`, `AdministratorPanel`, `OperatorPanel`, `ParticipantExperience`)
- `AccessPolicy` domain service — `EnsureCanAccess()` throws `DeactivatedUserAccessDeniedException` for inactive users, `UserRoleNotAuthorizedException` for wrong roles; is the single authority for the permission matrix
- `IdentityProvisioningPolicy` — `SynchronizeOrCreate()` from Keycloak claims
- `IdentityProviderSession` entity with `RevokedAt` / `ExpiresAt`
- Domain events: `UserProvisioned`, `UserRoleAssigned`, `UserAccessDeactivated`, `AccessDecisionRecorded`, `IdentityProviderSessionStarted`, `IdentityProviderSessionEnded`
- **Note:** `UserRoleAssigned` event is already declared and unit-tested on `User`; `UserRoleRevoked` does not yet exist and will be added in phase Y.1.

**Application layer**
- `AuthenticateUserCommand` / handler (post-login provisioning, deactivated-user rejection)
- `DeactivateUserCommand` / handler (soft-deactivate, Administrator-only)
- `GetUsersQuery` / handler (paginated catalog, Administrator or Operator)
- `GetAuthenticatedActorProfileQuery` + handler
- `CheckProtectedCapabilityAccessQuery` + handler
- `AuthorizationBehaviour`, `ValidationBehaviour`, `PerformanceBehaviour`, `UnhandledExceptionBehaviour`
- `ICurrentUser` / `GatewayRoleParser` (trusted-header identity)

**Infrastructure / API**
- EF Core `users` + `identity_provider_sessions` tables; `UserRepository` with `GetByIdAsync` and `ListAsync(page, pageSize)`
- Endpoints: `POST /api/users/authenticated`, `GET /api/users/me`, `GET /api/users`, `DELETE /api/users/{id}/access`, `GET /api/permissions/authenticated-platform-access`
- `ProblemDetailsExceptionHandler` mapping: `NotFoundException → 404`, `ValidationException → 400`, `UnauthorizedAccessException → 401`, `ForbiddenAccessException → 403`, `DeactivatedUserAccessDeniedException → 403`, `UserRoleNotAuthorizedException → 403`

**Frontend**
- User list view (admin/operator)
- Deactivate action with confirmation
- Deactivated-user login error and route/component guard

**Coverage:** 94.95% total line (64 unit tests + 11 integration tests); threshold is 95% — phase Y.4 gate must push total to ≥ 95%.

## What HU-03 adds on top (per PRD DES-67)

| Concern | New work |
|---|---|
| `AssignUserRole` use case | `AssignUserRoleCommand` + handler — mutate `User.Role`, emit `UserRoleAssigned`; when displacing a prior role also emit `UserRoleRevoked` |
| Role invariants | Deactivated user must not receive a role assignment; same-role assignment is a no-op (idempotent, no event emitted) |
| `UserRoleRevoked` domain event | New event on `User` aggregate — emitted when a previous role is displaced by a new assignment |
| Permission matrix hardening | Confirm every `ProtectedCapability` is covered by `AccessPolicy` with no ad-hoc role checks in handlers or endpoints |
| API endpoint | `PATCH /api/users/{id}/role` — Administrator-only, body `{ "role": "<RoleValue>" }`, returns 204 |
| Frontend | Role selector in user list/detail (admin changes role); role-based feature visibility derived from `GET /api/permissions/authenticated-platform-access`; route and component guards block direct URL access for unauthorized roles |

## Touched surfaces

- `backend/` identity-access-service
- `frontend/` admin/user-role UI
- `backend/frontend` API contract boundary: `PATCH /api/users/{id}/role` request/response/error shape

## Committed phases

| Commit | Phase | Description |
|---|---|---|
| — | — | No commits yet |

## Known quirks / gotchas

- `UserRoleAssigned` is already declared as a domain event and tested on the `User` entity (HU-01). The phase Y.1 task is to add `AssignRole(Role newRole)` as a formal domain method that raises it, plus the new `UserRoleRevoked` event — not to introduce `UserRoleAssigned` from scratch.
- Coverage is currently at 94.95%, just below the 95% threshold. Phase Y.4 must close that gap; do not report coverage as passing until the merge of unit + integration tests clears 95%.
- The permission matrix in `AccessPolicy` already covers four capabilities. If HU-03 acceptance criteria require a new capability (e.g. a dedicated `RoleManagement` capability), add it to the matrix in phase Y.1 before writing any handler.

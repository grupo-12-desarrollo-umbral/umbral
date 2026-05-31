# Prompt Example — HU-03 Role and Permission Assignment (Feature Slice)

Concrete prompt sequence for driving HU-03 through a full feature slice on `feature/hu-03-role-permission-assignment`. Follows the pattern in [workflow_for_prompts.md](./workflow_for_prompts.md).

**Key difference from HU-02:** HU-02 introduced deactivation and user listing. HU-03 extends the same `User` aggregate root to support explicit role assignment and revocation, and hardens the permission matrix so that each `Role` resolves base permissions through `AccessPolicy` and `ProtectedCapability` rather than ad-hoc endpoint checks. Steps 1 and 2 from the HU-01 template (read backlog, create PRD) are replaced here by a pre-resolved orient. There is no PRD creation step.

When working from the monorepo root, make the target workload explicit in each prompt.
For backend steps, point to `@backend/.agents/backend-agent.md`. For frontend steps,
point to `@frontend/AGENTS.md`. Do not ask for backend and frontend implementation in
the same phase prompt; coordinate them as separate scoped steps tied together by the
verified API contract.

---

## Pre-resolved orient (as of 2026-05-30)

> Step 1 has already been run. Paste this section into any agent session that needs context
> before picking up a phase — no need to re-run the orient prompt.

### What HU-01 and HU-02 have already landed (reuse candidates for HU-03)

All of this lives on `feature/hu-02-user-access-management` (or `develop` once HU-02 merges).

**Domain layer**

- `User` aggregate root — `ExternalIdentityId`, `DisplayName`, `Email`, `Role`, `IsActive`; `Deactivate()` method (throws `UserAccessAlreadyDeactivatedException` on double-deactivation)
- `Role` enum: `Administrator`, `Operator`, `Participant`
- `ProtectedCapability` enum + permission matrix
- `AccessPolicy` domain service — `EnsureCanAccess()` throws `DeactivatedUserAccessDeniedException` for inactive users; role-to-capability resolution already defined
- `IdentityProvisioningPolicy` — `SynchronizeOrCreate()` from Keycloak claims
- Domain events: `UserProvisioned`, `UserAccessDeactivated`, `UserRoleAssigned`, `AccessDecisionRecorded`

**Application layer**

- `AuthenticateUserCommand` / handler (post-login provisioning, deactivated-user rejection)
- `DeactivateUserCommand` / handler (soft-deactivate, history preserved)
- `ListUsersQuery` / `GetUserQuery` handlers (paginated, admin/operator-scoped)
- `CheckProtectedCapabilityAccessQuery` + handler
- `GetAuthenticatedActorProfileQuery` + handler
- `AuthorizationBehaviour` pipeline guard (roles + `[Authorize]`)
- `ICurrentUser` / `GatewayRoleParser` (trusted-header identity)

**Infrastructure / API**

- EF Core `users` + `identity_provider_sessions` tables; `UserRepository` with `GetByIdAsync` and `ListAsync(page, pageSize)`
- Endpoints: `POST /api/users/authenticated`, `GET /api/users/me`, `GET /api/permissions/authenticated-platform-access`, `GET /api/users`, `DELETE /api/users/{id}/access`
- `ProblemDetailsExceptionHandler` mapping domain exceptions to HTTP status codes

**Frontend**

- User list view (admin/operator)
- Deactivate action with confirmation
- Deactivated-user login error and route/component guard

**Coverage:** ≥ 95% across all layers.

### What HU-03 adds on top (per PRD DES-67)

| Concern                       | New work                                                                                                                                                                                                                                       |
| ----------------------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `AssignUserRole` use case     | `AssignUserRoleCommand` + handler — change a user's `Role`, emit `UserRoleAssigned`; `RevokeUserRole` variant or overwrite strategy for reassignment, emit `UserRoleRevoked` when previous role is displaced                                   |
| Role invariants               | A deactivated user must not receive a role assignment; the role change must be idempotent when the new role equals the current one                                                                                                             |
| Permission matrix enforcement | `AccessPolicy` already holds the matrix; confirm every `ProtectedCapability` is covered and no handler contains ad-hoc role checks outside the policy                                                                                          |
| API endpoint                  | `PATCH /api/users/{id}/role` (admin-only) — validates that the target role is a known `Role` enum value                                                                                                                                        |
| Frontend                      | Role selector in the user list/detail view (admin can change role); role-based feature visibility (UI shows/hides sections based on authenticated role); route and component guards block unauthorized actions regardless of direct URL access |

### Branch state and prerequisite

`feature/hu-03-role-permission-assignment` should be branched from `develop` **after** HU-02 merges, or directly from `feature/hu-02-user-access-management` if HU-02 has not yet been merged to `develop`.

**Before starting HU-03 implementation:** confirm that the `UserRoleAssigned` domain event stub introduced in HU-01 is already present. If it is, extend rather than recreate. Verify that `AccessPolicy` covers the full `ProtectedCapability` matrix before adding new capabilities.

### Linear state (as of 2026-05-30)

- DES-7 (HU-03): **Backlog** — does not yet have `ready-for-agent` label; add it before the agent picks up the slice (see step 2 below)
- DES-6 (HU-02): **In Progress** (or Done if merged)
- DES-67 (PRD): **Backlog**, labels: `ready-for-agent`, `svc:identity-access-service`

> Linear live state may have changed. Use the Linear MCP to verify DES-7 status and labels if needed, but do not re-fetch PRD scope — read the local file at `@backend/docs/prd/DES-67-primera-implementacion-de-identity-access-service-hu-01-a-hu-08.md` instead.

> **Note:** DES-7 is the assumed Linear ID for HU-03 based on the DES-5/DES-6 pattern for HU-01/HU-02. Verify the actual ID via Linear MCP before running step 2.

---

## 1. Orient — read service state and PRD before planning

> **Skip this step if you have read the pre-resolved orient section above.** Run it only
> if the README or Linear state may have changed since 2026-05-30.

```
Read the following files and summarise what has been decided and implemented so far:
- @backend/services/identity-access-service/README.md — current implementation status
- @backend/docs/prd/DES-67-primera-implementacion-de-identity-access-service-hu-01-a-hu-08.md
  — the PRD scope for all HU-01 to HU-08 slices

Then use the Linear MCP to fetch only the current live state of:
- DES-7  (HU-03 — Asignación de roles y permisos) — status and labels
- DES-6  (HU-02 — Gestión de acceso de usuarios, the predecessor slice) — status

Output:
- what domain concepts HU-01 and HU-02 have already landed (User, Role enum,
  AccessPolicy, deactivation) from the README — these are reuse candidates for HU-03
- what HU-03 adds on top per the PRD: AssignUserRole, UserRoleAssigned/UserRoleRevoked
  events, permission matrix hardening
- current Linear status and labels for DES-7

Do not start planning or implementing yet.
```

---

## 2. Label DES-7 as ready-for-agent

> HU-03 (DES-7) is currently in Backlog without the `ready-for-agent` label.
> Add it before the agent picks up the slice.

```
Use the Linear MCP to add the label ready-for-agent to DES-7.
Confirm the label was applied and output the updated ticket state.
```

---

## 3. Confirm slice readiness

```
Use the Linear MCP to confirm DES-7 now carries both svc:identity-access-service
and ready-for-agent labels and output its current status and acceptance criteria.

The PRD scope is already in the local file at
@backend/docs/prd/DES-67-primera-implementacion-de-identity-access-service-hu-01-a-hu-08.md —
do not re-fetch the PRD from Linear; read the local file if you need implementation decisions.

Output the confirmed HU id, title, acceptance criteria, and labels before planning the slice.
```

In the remaining examples below, `HU-03` and `DES-7` are the resolved values for this slice. `DES-67` is the shared PRD reference for identity-access-service; its content lives in the local file above.

---

## 4. Start the slice

```
Prepare the role and permission assignment slice on branch feature/hu-03-role-permission-assignment.
Use the HU id and DES id resolved from Linear in the previous step.
This slice affects frontend and identity-access-service.

The pre-resolved orient at the top of this document lists what HU-01 and HU-02 have
already landed and what HU-03 adds. Do not re-read the README or PRD for scoping.

Move the resolved HU ticket to In Progress and output the exact scope, branch name, and touched surfaces.
```

---

## 5. Backend phase X.1 — Domain layer

```
Use @backend/.agents/backend-agent.md.
Implement backend phase X.1 for HU-03 in identity-access-service.
Use the service PRD (DES-67) and canonical docs.

Before writing anything, read @backend/services/identity-access-service/README.md to
identify which domain concepts (User, Role enum, AccessPolicy, ProtectedCapability,
UserRoleAssigned event) were already introduced in HU-01 and HU-02. Extend rather than recreate.

Scope:
- role assignment operation on User aggregate: AssignRole(Role newRole) — updates Role,
  emits UserRoleAssigned; if a previous role is displaced, also emits UserRoleRevoked
- invariant: a deactivated User must not receive a role assignment (throw a domain exception)
- invariant: assigning the same role that is already set must be a no-op (idempotent)
- confirm that the full ProtectedCapability matrix in AccessPolicy covers every capability
  needed by HU-03 acceptance criteria; extend the matrix if any capability is missing
- no new value objects or aggregates are expected; extend the existing User aggregate root

Gate:
- Domain build passes
- No existing HU-01 or HU-02 domain concepts broken
- New domain invariants are expressed as unit tests on the User aggregate

Do not touch other backend layers or frontend.
```

Commit:

```
feat(identity-access): phase X.1 — domain layer (HU-03)

Ref: HU-03
Ref: DES-7
Ref: DES-67
```

Then run: `/debrief`

---

## 6. Backend phase X.2 — Application layer

```
Use @backend/.agents/backend-agent.md.
Implement backend phase X.2 for HU-03 in identity-access-service.
Use the service PRD (DES-67) and canonical docs.

Scope:
- AssignUserRoleCommand + handler: load User by id, call User.AssignRole(newRole),
  persist, dispatch domain events; only Administrators may invoke this command
  (enforce via AuthorizationBehaviour or explicit actor-role check in the handler)
- validator: target role must be a known Role enum value; target user must exist and be active
- handler unit tests for: successful role assignment, idempotent assignment (same role),
  rejected assignment on deactivated user, rejected assignment by non-admin caller,
  unknown role value rejected

Gate:
- clean build passes
- handler unit tests pass for all five paths above

Do not touch Infrastructure, Api, or frontend.
```

Commit:

```
feat(identity-access): phase X.2 — application layer (HU-03)

Ref: HU-03
Ref: DES-7
Ref: DES-67
```

Then run: `/debrief`

---

## 7. Backend phase X.3 — Infrastructure layer

```
Use @backend/.agents/backend-agent.md.
Implement backend phase X.3 for HU-03 in identity-access-service.
Use the service PRD (DES-67) and canonical docs.

Scope:
- repository support for role assignment: UserRepository.GetByIdAsync already loads
  the User aggregate — confirm it is sufficient; no new migration is expected because
  Role is already a column on the users table
- check the current ApplicationDbContextModelSnapshot for the Role column and any
  relevant indexes; if they already exist, explicitly note that no new migration
  is required
- integration test: assign a role to an active user, confirm the persisted Role matches,
  confirm UserRoleAssigned event was raised
- integration test: attempt role assignment on a deactivated user, confirm domain exception
  is thrown and the role in the database is unchanged
- integration test: idempotent assignment — assign the current role again, confirm no
  UserRoleAssigned event is emitted and the record is unchanged

Database isolation: each integration test must clean shared state before its scenario
(ExecuteDeleteAsync on IdentityProviderSessions and Users) to avoid cross-test pollution.

Test infrastructure: the test class's BuildContext factory must accept an optional
IMediator? parameter and wire DispatchDomainEventsInterceptor into DbContextOptions
so domain events are dispatched during SaveChangesAsync. Use a CapturingMediator
stub (collects published notifications via IMediator.Publish) for tests that assert
events, and a NoOpMediator for tests that don't. Without this wiring, domain events
will never appear in the captured mediator and assertions will silently pass or fail
incorrectly.

Before writing tests, verify that the test file's using statements cover
Domain.Entities, Domain.Enums, Domain.Events, Domain.Exceptions, MediatR,
and Infrastructure.Persistence.Interceptors — these are often missing when the file
only imports Application-layer namespaces. Domain events and exceptions are always
asserted in integration tests, and the DispatchDomainEventsInterceptor must be
wired into the DbContext.

Gate:
- dotnet build passes on the solution
- migration confirmed no-op against current snapshot (or migration created if needed)
- repository integration tests pass for all three paths above

Do not touch Api or frontend.
```

Commit:

```
feat(identity-access): phase X.3 — infrastructure layer (HU-03)

Ref: HU-03
Ref: DES-7
Ref: DES-67
```

Then run: `/debrief`

---

## 8. Backend phase X.4 — API layer

```
Use @backend/.agents/backend-agent.md.
Implement backend phase X.4 for HU-03 in identity-access-service.
Use the service PRD (DES-67) and canonical docs.

Scope:
- PATCH /api/users/{id}/role endpoint (Administrator-only): accepts { "role": "<RoleValue>" },
  dispatches AssignUserRoleCommand, returns 204 on success
- proof that a non-admin caller receives 403
- proof that an unknown role value receives 400
- proof that targeting a deactivated user receives 422 or equivalent domain-error response
- confirm GET /api/users response includes the current Role for each user
  (likely already present from HU-02; extend the DTO only if missing)
- confirm GET /api/permissions/authenticated-platform-access reflects the correct
  capability set after a role change (re-use existing endpoint)

Gate:
- endpoint tests pass for role assignment, non-admin rejection, unknown role, deactivated target
- no regression on HU-02 endpoints (GET /api/users, DELETE /api/users/{id}/access)
- service coverage reaches 95%

Do not touch frontend.
```

Commit:

```
feat(identity-access): phase X.4 — api layer (HU-03)

Ref: HU-03
Ref: DES-7
Ref: DES-67
```

Then run: `/debrief`

---

## 8.5 — Rebuild backend container images

The backend Docker images were built before the new endpoint existed. Rebuild
and restart so the frontend can test against the live service:

```bash
cd backend && docker compose build identity-access-service
docker compose up -d identity-access-service
```

Also rebuild the gateway if any proxy configuration changed:

```bash
docker compose build api-gateway
docker compose up -d api-gateway
```

Verify the new endpoint responds:

```bash
curl -s -X PATCH http://localhost:5002/api/users/<user-id>/role \
  -H "Content-Type: application/json" \
  -H "X-User-Id: admin-1" \
  -H "X-User-Role: Administrator" \
  -H "X-User-Email: admin@umbral.local" \
  -d '{"role":"Operator"}'
```

**Gate:** the new endpoint (`PATCH /api/users/{id}/role`) returns the expected status codes when called with trusted headers; existing endpoints from HU-01 and HU-02 remain functional.

---

## 9. Frontend slice

```
Use @frontend/AGENTS.md.
Implement the frontend part of HU-03.
Use the verified backend contract.

Scope:
- role selector in the user list or user detail view: an admin can change a user's role
  via the PATCH /api/users/{id}/role endpoint; show current role and allow selection
  from the valid Role enum values (Administrator, Operator, Participant)
- role-based feature visibility: the UI must show or hide sections and actions
  based on the authenticated user's role (e.g. admin-only management panels,
  operator-only controls, participant-only views); derive visibility from the
  role returned by GET /api/users/me or the capability set from
  GET /api/permissions/authenticated-platform-access
- route and component guards: unauthorized users must be blocked even when
  accessing a URL directly; a clear error or redirect must occur — never silent
  partial rendering of protected content
- keep one shared app, not separate admin/operator apps
- do not regress HU-01 or HU-02 flows (login, deactivation, user list)

Gate:
- admin can view and change a user's role from the UI
- UI sections are shown or hidden correctly for each role (Administrator, Operator, Participant)
- direct URL access to a protected route by an unauthorized role results in a redirect or error
- active user flows from HU-01 and HU-02 are not regressed
```

Commit:

```
feat(frontend): role and permission assignment — HU-03

Ref: HU-03
Ref: DES-7
Ref: DES-67
```

Then run: `/debrief`

---

## 10. Close out

```
Verify HU-03 end to end for DES-7 on feature/hu-03-role-permission-assignment.

Acceptance criteria:
- administrator can assign or change an authorized user's role
- each role has associated base permissions according to the defined matrix
- the system displays features based on the authenticated role
- the system blocks unauthorized actions even when there is direct access to routes or endpoints

Then:
- open or update the draft PR to develop
- include DES-7 and DES-67 in the PR description
- move HU-03 to Done only if all acceptance criteria pass
```

PR command:

```
gh pr create --draft --base develop --title "feat: role and permission assignment — HU-03"
```

---

## Rationale for changes relative to HU-02

**Permission matrix hardening is scoped to domain, not endpoints.** HU-03 explicitly requires that unauthorized actions are blocked even with direct route/endpoint access. This means `AccessPolicy` must be the single authority — the domain phase (Y.1) must audit the full `ProtectedCapability` matrix before any handler is written.

**Role assignment is an operation on the existing User aggregate.** HU-01 defined `User` with a `Role` field. HU-03 formalizes mutating that field as a domain operation (`AssignRole`) with invariants (deactivated users cannot be reassigned, same-role assignment is a no-op). No new aggregate or value object is expected.

**UserRoleRevoked event.** When a role is replaced the previous role is displaced. Emitting `UserRoleRevoked` alongside `UserRoleAssigned` preserves a complete audit trail consistent with the "history preserved" principle established in HU-02.

**Frontend visibility is derived from capabilities, not raw role strings.** The frontend should prefer `GET /api/permissions/authenticated-platform-access` over hardcoding role names in visibility logic, so that future matrix changes in the backend propagate without frontend changes.

**Three DES refs per commit.** HU-03 commits reference DES-7 (HU ticket), DES-67 (PRD), matching the pattern used in HU-01 and HU-02.

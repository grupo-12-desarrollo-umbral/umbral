# Prompt Example — HU-02 User Access Management (Feature Slice)

Concrete prompt sequence for driving HU-02 through a full feature slice on `feature/hu-02-user-access-management`. Follows the pattern in [workflow_for_prompts.md](./workflow_for_prompts.md).

**Key difference from HU-01:** the service PRD (DES-67) already exists and the orient step has been pre-resolved (see section below). Steps 1 and 2 from the HU-01 template (read backlog, create PRD) are replaced here by a pre-resolved orient that any agent session can read directly. There is no PRD creation step.

When working from the monorepo root, make the target workload explicit in each prompt.
For backend steps, point to `@backend/.agents/backend-agent.md`. For frontend steps,
point to `@frontend/AGENTS.md`. Do not ask for backend and frontend implementation in
the same phase prompt; coordinate them as separate scoped steps tied together by the
verified API contract.

---

## Pre-resolved orient (as of 2026-05-30)

> Step 1 has already been run. Paste this section into any agent session that needs context
> before picking up a phase — no need to re-run the orient prompt.

### What HU-01 has already landed (reuse candidates for HU-02)

All of this lives on `feature/hu-01-general-user-login` and is ready to be merged or branched from.

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

### What HU-02 adds on top (per PRD DES-67)

| Concern | New work |
|---|---|
| `DeactivateUserAccess` use case | `DeactivateUserCommand` + handler — soft-deactivate (`IsActive = false`), preserve history, emit `UserAccessDeactivated` event |
| User access catalogue | `ListUsers` / `GetUsers` query handler — paginated read for admin/operator |
| Deactivated-user enforcement at login | `AuthenticateUserCommandHandler` must reject deactivated users; `AccessPolicy.EnsureCanAccess()` already throws — confirm it is wired in the handler |
| Deactivated-user enforcement at protected endpoints | `AuthorizationBehaviour` or guard path must propagate 403 for deactivated users hitting capabilities |
| API endpoints | `DELETE /api/users/{id}/access` (deactivate) and `GET /api/users` (list registered users, admin/operator-scoped) |
| Frontend | User list view; deactivate action; error on deactivated-user login; route/component guard for deactivated users |

### Branch state and prerequisite

`feature/hu-02-user-access-management` was branched from `develop` **before** HU-01 was merged. `develop` at that point only has the scaffold (no C# source files). The full HU-01 implementation lives on `feature/hu-01-general-user-login`.

**Before starting HU-02 implementation:** either merge HU-01 into `develop` and rebase `feature/hu-02-user-access-management` onto the updated `develop`, or rebase `feature/hu-02-user-access-management` directly onto `feature/hu-01-general-user-login`.

### Linear state (as of 2026-05-30)

- DES-6 (HU-02): **Backlog** — does not yet have `ready-for-agent` label; add it before the agent picks up the slice (see step 2 below)
- DES-5 (HU-01): **In Progress**
- DES-67 (PRD): **Backlog**, labels: `ready-for-agent`, `svc:identity-access-service`

> Linear live state may have changed. Use the Linear MCP to verify DES-6 status and labels if needed, but do not re-fetch PRD scope — read the local file at `@backend/docs/prd/DES-67-primera-implementacion-de-identity-access-service-hu-01-a-hu-08.md` instead.

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
- DES-6  (HU-02 — Gestión de acceso de usuarios) — status and labels
- DES-5  (HU-01 — Inicio de sesión general, the predecessor slice) — status

Output:
- what domain concepts HU-01 has already landed (User entity, active/deactivated state,
  AccessPolicy) from the README — these are reuse candidates for HU-02
- what HU-02 adds on top per the PRD: DeactivateUserAccess, user access catalogue,
  deactivated-user enforcement at login and protected endpoints
- current Linear status and labels for DES-6

Do not start planning or implementing yet.
```

---

## 2. Label DES-6 as ready-for-agent

> HU-02 (DES-6) is currently in Backlog without the `ready-for-agent` label.
> Add it before the agent picks up the slice.

```
Use the Linear MCP to add the label ready-for-agent to DES-6.
Confirm the label was applied and output the updated ticket state.
```

---

## 3. Confirm slice readiness

```
Use the Linear MCP to confirm DES-6 now carries both svc:identity-access-service
and ready-for-agent labels and output its current status and acceptance criteria.

The PRD scope is already in the local file at
@backend/docs/prd/DES-67-primera-implementacion-de-identity-access-service-hu-01-a-hu-08.md —
do not re-fetch the PRD from Linear; read the local file if you need implementation decisions.

Output the confirmed HU id, title, acceptance criteria, and labels before planning the slice.
```

In the remaining examples below, `HU-02` and `DES-6` are the resolved values for this slice. `DES-67` is the shared PRD reference for identity-access-service; its content lives in the local file above.

---

## 4. Start the slice

```
Prepare the user access management slice on branch feature/hu-02-user-access-management.
Use the HU id and DES id resolved from Linear in the previous step.
This slice affects frontend and identity-access-service.

The pre-resolved orient at the top of this document lists what HU-01 has already
landed and what HU-02 adds. Do not re-read the README or PRD for scoping.

Move the resolved HU ticket to In Progress and output the exact scope, branch name, and touched surfaces.
```

---

## 5. Backend phase X.1 — Domain layer

```
Use @backend/.agents/backend-agent.md.
Implement backend phase X.1 for HU-02 in identity-access-service.
Use the service PRD (DES-67) and canonical docs.

Before writing anything, read @backend/services/identity-access-service/README.md to
identify which domain concepts (User, deactivated state, access policy) were already
introduced in HU-01. Extend rather than recreate.

Scope:
- deactivation domain operation on User (soft-deactivate, preserving history)
- invariant: a deactivated user must not pass access checks
- user listing/query capability in the domain (read model or specification)
- domain events or value objects needed to express deactivation intent

Gate:
- Domain build passes
- No existing HU-01 domain concepts broken

Do not touch other backend layers or frontend.
```

Commit:

```
feat(identity-access): phase X.1 — domain layer (HU-02)

Ref: HU-02
Ref: DES-6
Ref: DES-67
```

Then run: `/debrief`

---

## 6. Backend phase X.2 — Application layer

```
Use @backend/.agents/backend-agent.md.
Implement backend phase X.2 for HU-02 in identity-access-service.
Use the service PRD (DES-67) and canonical docs.

Scope:
- DeactivateUser command handler (soft deactivation, history preserved)
- ListUsers / GetUsers query handler (for admin/operator use)
- enforce deactivated state in AuthenticateUser use case (if not already enforced by HU-01)
- handler and validator tests for: successful deactivation, listing users, deactivated
  user rejected at login, deactivated user rejected at protected operation

Gate:
- clean build passes
- handler unit tests pass for all four paths above

Do not touch Infrastructure, Api, or frontend.
```

Commit:

```
feat(identity-access): phase X.2 — application layer (HU-02)

Ref: HU-02
Ref: DES-6
Ref: DES-67
```

Then run: `/debrief`

---

## 7. Backend phase X.3 — Infrastructure layer

```
Use @backend/.agents/backend-agent.md.
Implement backend phase X.3 for HU-02 in identity-access-service.
Use the service PRD (DES-67) and canonical docs.

Scope:
- repository implementation for deactivation: `GetByIdAsync` — loads the user
  by primary key including `IdentityProviderSessions` so history is preserved
- repository implementation for user listing: `ListAsync(int page, int pageSize)`
  returning `PagedResult<User>` — stable sort by DisplayName then Id,
  AsNoTracking for read performance
- check the current `ApplicationDbContextModelSnapshot` for `IsActive` and any
  needed indexes; if they already exist, explicitly note that no new migration
  is required
- integration test: deactivate a user, confirm they cannot be retrieved as active,
  confirm history is preserved
- integration test: verify paginated listing returns correct total count,
  page offset, and ordering

Before writing tests, verify that the test file's `using` statements cover
`Domain.Entities` and `Domain.Enums` — these are often missing when the file
only imports Application-layer namespaces.

Database isolation: each integration test must clean shared state before
its scenario (e.g. `ExecuteDeleteAsync` on `IdentityProviderSessions` and
`Users`) to avoid cross-test pollution.

Gate:
- `dotnet build` passes on the solution
- migration succeeds (or confirmed no-op against current snapshot)
- repository integration tests pass
- a deactivated user's record still exists (history check)

Do not touch Api or frontend.
```

Commit:

```
feat(identity-access): phase X.3 — infrastructure layer (HU-02)

Ref: HU-02
Ref: DES-6
Ref: DES-67
```

Then run: `/debrief`

---

## 8. Backend phase X.4 — API layer

```
Use @backend/.agents/backend-agent.md.
Implement backend phase X.4 for HU-02 in identity-access-service.
Use the service PRD (DES-67) and canonical docs.

Scope:
- endpoint to list registered users (admin/operator-scoped)
- endpoint to deactivate a user by id
- proof that a deactivated user is rejected at login (re-use or extend existing auth endpoint)
- proof that a deactivated user is rejected at any protected operation

Gate:
- endpoint tests pass for listing and deactivation
- deactivated user is rejected at login (401 or equivalent)
- deactivated user is rejected at protected operation
- service coverage reaches 95%

Do not touch frontend.
```

Commit:

```
feat(identity-access): phase X.4 — api layer (HU-02)

Ref: HU-02
Ref: DES-6
Ref: DES-67
```

Then run: `/debrief`

---

## 9. Frontend slice

```
Use @frontend/AGENTS.md.
Implement the frontend part of HU-02.
Use the verified backend contract.

Scope:
- user list view (admin/operator can consult registered users)
- deactivate user action (admin/operator can deactivate; history is not deleted)
- deactivated users cannot log in (handled by backend; frontend shows appropriate error)
- deactivated users cannot access protected areas (frontend enforces on route/component level)
- keep one shared app, not separate admin/operator apps

Gate:
- admin/operator can see registered users
- admin/operator can deactivate a user
- deactivated user is blocked at login with a clear error
- deactivated user cannot reach protected functionality
- active user flows from HU-01 are not regressed
```

Commit:

```
feat(frontend): user access management — HU-02

Ref: HU-02
Ref: DES-6
Ref: DES-67
```

Then run: `/debrief`

---

## 10. Close out

```
Verify HU-02 end to end for DES-6 on feature/hu-02-user-access-management.

Acceptance criteria:
- admin/operator can consult registered users
- admin/operator can deactivate user access without losing history
- deactivated users cannot log in
- deactivated users cannot use protected functionality

Then:
- open or update the draft PR to develop
- include DES-6 and DES-67 in the PR description
- move HU-02 to Done only if all acceptance criteria pass
```

PR command:

```
gh pr create --draft --base develop --title "feat: user access management — HU-02"
```

---

## Rationale for changes relative to HU-01

**Steps 1–2 replaced by orient + label steps.** The PRD (DES-67) already exists. What the agent needs instead is the current implementation state of identity-access-service (the README) and to confirm DES-6 is ready-for-agent before picking it up.

**README read in orient and domain steps.** HU-01 already defines User with active/deactivated state and the access policy concept. HU-02 extends those concepts rather than recreating them. Reading the README before each scoped step prevents the agent from duplicating or conflating already-landed work.

**Deactivation is a soft delete.** The acceptance criterion "without losing history" means the user record must persist. The domain invariant (deactivated user blocked) and the infrastructure constraint (record preserved) must be stated explicitly in the phase gates or the agent may implement a hard delete.

**Three DES refs per commit.** HU-02 commits reference DES-6 (HU ticket), DES-67 (PRD), matching the pattern used in HU-01. This links each commit to both the user story and the service design document.

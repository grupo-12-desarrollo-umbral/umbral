# Prompt Example — HU-01 General User Login (Feature Slice)

Concrete prompt sequence for driving HU-01 through a full feature slice on `feature/general-user-login`. Follows the pattern in [workflow_for_prompts.md](./workflow_for_prompts.md).

When working from the monorepo root, make the target workload explicit in each prompt.
For backend steps, point to `@backend/.agents/backend-agent.md`. For frontend steps,
point to `@frontend/AGENTS.md`. Do not ask for backend and frontend implementation in
the same phase prompt; coordinate them as separate scoped steps tied together by the
verified API contract.

---

## 1. Read the service backlog first

```
Use the Linear MCP to fetch the identity-access-service backlog items labeled
svc:identity-access-service.

Resolve:
- the HU backlog relevant to identity-access-service
- the current Linear state before PRD creation

Output the candidate HU ids, titles, acceptance criteria, and labels that will
inform the service PRD.
```

---

## 2. PRD for the service

```
$to-prd for identity-access-service covering HU-01 to HU-08.
Focus the first delivery slice on HU-01 general user login.
```

---

## 3. Resolve the slice from Linear after the PRD exists

```
Use the Linear MCP to fetch the identity-access-service backlog items labeled
svc:identity-access-service and ready-for-agent.

Resolve:
- the exact HU ticket for general user login
- the PRD reference created for identity-access-service
- the current Linear state before work starts

Output the resolved HU id, DES id, title, acceptance criteria, and labels before planning the slice.
Do not hardcode the Linear ids; fetch them from the backlog.
```

This fetch is slice-scoped: resolve the HU ticket(s) needed for the current vertical slice, not the entire service backlog.

**If the HU ticket is not in the results**, stop. Apply the `ready-for-agent` label to the HU ticket in Linear before running step 4. The label must be on the HU ticket itself — not only on the PRD. Only the PRD carrying `ready-for-agent` is not sufficient to proceed.

In the remaining examples below, `HU-01` and `DES-5` are illustrative resolved values from that fetch step, not prerequisites that should be assumed without querying Linear first.

---

## 4. Start the slice

```
Prepare the general user login slice on branch feature/general-user-login.
Use the HU id and DES id resolved from Linear in the previous step.
This slice affects frontend and identity-access-service.
Move the resolved HU ticket to In Progress and output the exact scope, branch name, and touched surfaces.
```

---

## 5. Backend phase X.1 — Domain layer

```
HU ticket resolved from step 3: <paste id — e.g. DES-5>
DES reference resolved from step 3: <paste id — e.g. DES-67>

Use @backend/.agents/backend-agent.md.
Implement backend phase X.1 for HU-01 in identity-access-service.
Use the service PRD and canonical docs.

Scope:
- domain model for authenticated user and role
- active/deactivated user state
- access policy for protected capabilities

Gate:
- Domain build passes
- at least one unit test per public domain type (each aggregate/entity, each value object, each enum behavior) — no domain type may be left unexercised

Do not touch other backend layers or frontend.
```

Commit:

```
feat(identity-access): phase X.1 — domain layer

Ref: HU-01
Ref: DES-5
```

Then run: `/debrief`

---

## 6. Backend phase X.2 — Application layer

```
If this is a new session, use the Linear MCP to resolve the active slice before starting:
- fetch issues labeled svc:identity-access-service with state In Progress
- confirm the HU ticket id and the DES PRD reference
- output both ids before proceeding

Use @backend/.agents/backend-agent.md.
Implement backend phase X.2 for HU-01 in identity-access-service.
Use the service PRD and canonical docs.

Scope:
- AuthenticateUser application use case
- current authenticated user query
- protected access check by role
- handler and validator tests for valid and rejected paths

Gate:
- clean build passes
- every handler has unit tests covering all paths (valid path + every rejection/error branch)
- every FluentValidation validator has tests for valid input and each invalid input — "at least one" is not enough
- all handler tests use Moq for outbound ports — no real infrastructure anywhere in this suite

Do not touch Infrastructure, Api, or frontend.
```

Commit:

```
feat(identity-access): phase X.2 — application layer

Ref: HU-01
Ref: DES-5
```

Then run: `/debrief`

---

## 7. Backend phase X.3 — Infrastructure layer

```
If this is a new session, use the Linear MCP to resolve the active slice before starting:
- fetch issues labeled svc:identity-access-service with state In Progress
- confirm the HU ticket id and the DES PRD reference
- output both ids before proceeding

Use @backend/.agents/backend-agent.md.
Implement backend phase X.3 for HU-01 in identity-access-service.
Use the service PRD and canonical docs.

Scope:
- persistence and provisioning infrastructure for application-side user state
- repository implementations
- migration for the slice if needed
- integration test for provisioning/retrieving the authenticated user

Gate:
- migration succeeds
- repository integration test passes against a real PostgreSQL instance via Testcontainers — not EF Core in-memory

Do not touch Api or frontend.
```

Commit:

```
feat(identity-access): phase X.3 — infrastructure layer

Ref: HU-01
Ref: DES-5
```

Then run: `/debrief`

---

## 8. Backend phase X.4 — API layer

```
If this is a new session, use the Linear MCP to resolve the active slice before starting:
- fetch issues labeled svc:identity-access-service with state In Progress
- confirm the HU ticket id and the DES PRD reference
- output both ids before proceeding

Use @backend/.agents/backend-agent.md.
Implement backend phase X.4 for HU-01 in identity-access-service.
Use the service PRD and canonical docs.
Follow @backend/.claude/skills/aspnet-backend-testing/ for test type and layer placement.

Scope:
- endpoint to bootstrap/sync the authenticated user after Keycloak login
- endpoint to get current authenticated user and role
- protected endpoint or policy proof that rejects unauthenticated or deactivated users

Gate:
- endpoint integration tests run through WebApplicationFactory — not handler unit tests
- unauthorized and deactivated paths are rejected with the correct status codes
- service coverage reaches 95%

Do not touch frontend.
```

Commit:

```
feat(identity-access): phase X.4 — api layer

Ref: HU-01
Ref: DES-5
```

Then run: `/debrief`

---

NO FOR THIS SPRINT - 30th May 2026.

<!--
## 9. Backend phase X.5 — E2E layer

```
If this is a new session, use the Linear MCP to resolve the active slice before starting:
- fetch issues labeled svc:identity-access-service with state In Progress
- confirm the HU ticket id and the DES PRD reference
- output both ids before proceeding

Use @backend/.agents/backend-agent.md.
Implement backend phase X.5 for HU-01 in identity-access-service.
Use the service PRD and canonical docs.
Follow @backend/.claude/skills/aspnet-backend-testing/ for test type and layer placement.

Scope:
- black-box API system tests using HttpClient against TestServer with a real PostgreSQL database via Testcontainers
- cover the principal usage flows end to end:
  - valid user signs in and receives their role
  - unauthenticated request is rejected
  - deactivated user is rejected after login

Gate:
- all principal flow E2E tests pass against a real database
- no test doubles replace infrastructure dependencies in this suite

Do not touch frontend.
```

Commit:

```
feat(identity-access): phase X.5 — e2e layer

Ref: HU-01
Ref: DES-5
```

Then run: `/debrief`
-->

---

## 11. Frontend slice

```
Use @frontend/AGENTS.md.
Implement the frontend part of HU-01 in.
Use the verified backend contract.

Scope:
- login flow
- authenticated user bootstrap
- role-aware UI visibility
- block unauthenticated users from protected areas
- block deactivated users
- keep one shared app, not separate admin/operator apps

Gate:
- user can sign in
- role is identified
- unauthenticated access is blocked
- deactivated access is blocked
- visible functionality matches the authenticated role
```

Commit:

```
feat(frontend): general user login flow — HU-01

Ref: HU-01
Ref: DES-5
```

Then run: `/debrief`

---

## 12. Close out

```
Verify HU-01 end to end for DES-5 on feature/general-user-login.

Acceptance criteria:
- registered user can sign in with valid credentials
- system identifies the authenticated role
- system blocks unauthenticated or deactivated users
- authenticated user can access only functionality allowed by role

Then:
- open or update the draft PR to develop
- include DES-5 in the PR description
- move HU-01 to Done only if all acceptance criteria pass
```

PR command:

```
gh pr create --draft --base develop --title "feat: general user login — HU-01"
```

---

## Rationale for scope + gate in phase prompts

The Linear acceptance criteria tell you the user-visible outcome. They do not specify the implementation boundary for each prompt or the backend phase gate.

For the overall slice prompt, Linear acceptance criteria can mostly define the goal. For each backend phase prompt, explicit scope and gate prevent the agent from spilling into other layers or claiming completion too early.

Minimal version:

> Implement backend phase X.2 for HU-01 in identity-access-service.
> Use the service PRD and canonical docs.
> Stay within the Application layer only.
> Gate: clean build and handler unit tests pass.

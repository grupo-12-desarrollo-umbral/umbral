# Prompt Example — HU-19 Session Operator Assignment (Feature Slice)

> Updated on 2026-06-16 for the mission-runtime restructure. Any references to
> `CreateTriviaSessionFacade` are historical precedents only. Current session
> creation is mission-only and persists `MissionRuntimeSnapshot`.

Concrete prompt sequence for driving HU-19 through a full feature slice on
`feature/hu-19-session-operator-assignment`. Follows the pattern in
[workflow_for_prompts.md](./workflow_for_prompts.md). Context:
[hu19-context.md](./hu19-context.md).

**Key difference from HU-16:** HU-16 created the `LiveSession` from a published
trivia quiz. HU-19 does not create runtime state; it assigns runtime ownership
for that already-existing `LiveSession`. The canonical boundary is strict:
`SessionOperations` owns `LiveSession.AssignedOperatorUserId` as session state,
while `Identity` only supplies actor facts and coarse policy checks. Do not
build a second source of truth for assignment in `identity-access-service`.

Drive each backend phase with `@backend/.agents/driver-agent.md`, selecting
phases in order: **X.1 -> X.2 -> X.3 -> X.4**. The driver delegates
implementation to `@backend/.agents/backend-agent.md`; do not invoke it
directly. For the frontend slice, use Step 9 directly with `@frontend/AGENTS.md`.
Do not mix backend and frontend work in the same phase.

---

## Required design patterns

- `Facade`
  - Why: operator assignment is orchestration across session lookup, target-user
    validation, persistence, and audit/event publication.
  - Phase owner: **X.2 Application**
  - Gate obligation: a single application orchestration entry point
    (`AssignOperatorToSession`) coordinates the subsystems. The endpoint stays
    thin; these steps are not scattered across transport code or hidden in
    ad-hoc helper calls.

- `Proxy`
  - Why: operator assignment must also establish guarded access to protected
    session-administration actions.
  - Phase owner: **X.2 Application + X.4 API**
  - Gate obligation: access is enforced through a `Proxy`-style guard
    (`AuthorizationBehaviour`, a dedicated authorization proxy, and/or endpoint
    policy) with **no ad-hoc role/ownership `if` checks** in handlers or
    endpoints. The slice must leave a reusable assignment-aware authorization
    seam for downstream session actions.

Transport note (NOT a pattern, NOT a gate): HU-19 has **no** SignalR / RabbitMQ
obligation. It is a synchronous REST slice.

---

## Pre-resolved orient (as of 2026-06-03)

> Step 1 has already been run. Paste this section into any agent session that
> needs context before picking up a phase; no need to re-run the orient prompt
> unless local docs or Linear state changed.

### What has already landed and must be reused

**session-operations-service**
- `LiveSession` is already the aggregate root for live runtime state.
- `LiveSession.AssignedOperatorUserId` already exists and is already mapped in EF
  Core to `live_sessions.assigned_operator_user_id`.
- HU-07B established the "Identity returns facts, SessionOperations decides"
  runtime boundary and already ships a structural authorization proxy precedent:
  `ReconnectAuthenticatedParticipantAuthorizationProxy`.
- HU-16 established the `Facade` precedent in
  `CreateTriviaSessionFacade`, plus the existing `/api/sessions` endpoint group.

**identity-access-service**
- Identity owns `User`, `Role`, and coarse access facts, not runtime session
  ownership.
- HU-01 through HU-07A already established the `Proxy` pattern for protected
  operations and the trusted-header identity flow.
- Session-ops already has a cross-service client precedent into Identity via
  `ParticipantMembershipAccessClient`.

### What HU-19 adds

| Concern | New work |
| --- | --- |
| Domain | Formal session-owned assignment behavior on `LiveSession`, plus audit/event language for assignment changes. |
| Application | `AssignOperatorToSession` use case as the mandated `Facade`, plus an assignment-aware `Proxy` seam for protected session administration. |
| Infrastructure | Repository round-trip for the existing assignment column, plus a narrow Identity-facing actor-facts client if the existing surface is insufficient. |
| API | Admin endpoint to assign/change the responsible operator for a session and return the current assignment state. |
| Frontend | Admin session-management UI to assign/change the responsible operator and render the current assignment. |

### Branch state and prerequisite

`feature/hu-19-session-operator-assignment` branches from **`develop`**.
Same-service predecessors `DES-11`, `DES-12`, and `DES-23` are already `Done`;
there is no same-service predecessor currently `In Progress`, so no feature-branch
chaining is required.

### Linear state

- HU ticket: `DES-26` — **Todo**, labels `Feature`, `ready-for-agent`,
  `svc:session-operations-service`
- PRD ref: `DES-70`, local file
  `@backend/docs/prd/DES-70-primera-implementacion-de-session-operations-service-hu-15-a-hu-36.md`

---

## 1. Orient — read current service state

> Skip this step if you have read the pre-resolved orient above and the local
> docs are unchanged.

```text
Read the following and summarise what is already decided:
- @backend/docs/hu19-context.md
- @backend/docs/prd/DES-70-primera-implementacion-de-session-operations-service-hu-15-a-hu-36.md
- @backend/services/session-operations-service/CONTEXT.md
- @backend/services/identity-access-service/CONTEXT.md
- @backend/services/session-operations-service/src/Domain/Entities/LiveSession.cs
- @backend/services/session-operations-service/src/Infrastructure/Persistence/Configurations/LiveSessionConfiguration.cs
- @backend/docs/ddd_solution_model.md
- @backend/docs/bd_umbral_entity_spec.md

Then use the Linear MCP to fetch only the current live state of:
- DES-26 (HU-19 — Asignacion de operador a sesion) — status and labels
- DES-23 (HU-16 — predecessor) — status
- DES-70 (session-operations PRD) — status and labels

Output:
- what HU-07B and HU-16 already landed that HU-19 must reuse
- confirmation that AssignedOperatorUserId already exists on LiveSession and is
  already mapped in persistence
- the resolved HU id, PRD id, status, and labels

Do not start planning or implementing yet.
```

---

## 2. Confirm `ready-for-agent` label

> `DES-26` already carries `ready-for-agent` as of 2026-06-03. This step is a
> verification step; if the label has been removed, re-apply it.

```text
Use the Linear MCP to confirm that DES-26 still has the label ready-for-agent.
If it has been removed, add it back. Output the updated ticket state.
```

---

## 3. Confirm slice readiness

```text
Use the Linear MCP to confirm DES-26 carries both svc:session-operations-service
and ready-for-agent, and output its current status and acceptance criteria.

The PRD scope is already in the local file at
@backend/docs/prd/DES-70-primera-implementacion-de-session-operations-service-hu-15-a-hu-36.md
— do not re-fetch PRD scope from Linear; read the local file if you need the
service-level implementation decisions.

Output the confirmed HU id, title, acceptance criteria, labels, and PRD ref
before planning the slice.
```

In the remaining steps below, `HU-19`, `DES-26`, and `DES-70` are the resolved
references for this slice.

---

## 4. Start the slice

```text
Prepare the session operator assignment slice on branch
feature/hu-19-session-operator-assignment, based on develop.
Use the resolved HU id (DES-26) and PRD id (DES-70).
This slice affects session-operations-service primarily, with identity-access-service
only as a supporting actor-facts / policy dependency and frontend as an admin UI consumer.

Before implementation, confirm the baseline:
- LiveSession already exists and already owns AssignedOperatorUserId
- the assignment column is already persisted
- Identity is not the source of truth for assignment
- HU-20 consumes this slice later for assigned-session reads; do not expand into HU-20

Move DES-26 to In Progress and output the exact scope, branch name, and touched
surfaces.
```

---

## 5. Backend phase X.1 — Domain layer

> Run `@backend/.agents/driver-agent.md` and select **X.1** from the phase menu.

```text
Use @backend/.agents/backend-agent.md.
Implement backend phase X.1 for HU-19 in session-operations-service.

Before writing anything, inspect the existing domain and extend rather than recreate:
- LiveSession
- existing session domain events
- any session-history / audit event conventions already present

Scope:
- formalize operator assignment as domain behavior on LiveSession instead of a
  bare persisted property: assign or change the responsible operator while
  keeping the source of truth inside the session aggregate
- capture audit-relevant assignment change facts in session-owned domain language
  (for example a domain event and/or session event carrying previous/new operator ids)
- preserve the canonical boundary: the domain stores who is assigned; it does not
  ask Identity to own or persist assignment
- if the current canon docs do not resolve whether reassignment is allowed only
  pre-start or also during later states, keep the rule explicit in code/tests and
  record any unresolved ambiguity rather than inventing a hidden restriction

Gate:
- Domain build passes
- AssignedOperatorUserId ownership remains on LiveSession
- new domain behavior is unit-tested for assignment and reassignment audit facts
- no Identity-owned duplicate source of truth is introduced

Do not touch other backend layers or frontend.
```

Commit:

```text
feat(session-operations): phase X.1 — domain layer (HU-19)

Ref: HU-19
Ref: DES-26
Ref: DES-70
```

---

## 6. Backend phase X.2 — Application layer

> Run `@backend/.agents/driver-agent.md` and select **X.2** from the phase menu.

```text
Use @backend/.agents/backend-agent.md.
Implement backend phase X.2 for HU-19 in session-operations-service.

Before writing anything, inspect the existing Application baseline and mirror its
conventions:
- CreateTriviaSessionFacade (Facade precedent)
- ReconnectAuthenticatedParticipantAuthorizationProxy (Proxy precedent)
- AuthorizationBehaviour / ICurrentUser / Authorize usage

Scope:
- add AssignOperatorToSession command + handler/facade as the mandated Facade:
  one orchestration entry point loads the session, validates the target actor
  through an Identity-facing port, applies the assignment on LiveSession,
  persists, and emits audit/session events
- add the narrowest application port needed to validate target actor facts
  without moving assignment ownership into Identity; the port should answer the
  question "is this target user eligible to be assigned as an operator/admin actor?"
- realize the mandated Proxy explicitly: introduce an assignment-aware
  authorization seam for protected session administration, with no ad-hoc role or
  ownership checks inside handlers
- the command itself is Administrator-only; downstream session-administration
  actions should be able to reuse the same proxy seam later
- validator / handler tests for: successful first assignment; successful change of
  assigned operator; unknown session rejected; ineligible target actor rejected;
  unauthorized caller rejected

Gate:
- clean build passes
- handler + validator unit tests pass for all listed paths
- Facade gate: session lookup, target validation, assignment mutation, and audit
  publication are coordinated in one application entry point
- Proxy gate: access is enforced through a structural guard with no ad-hoc role /
  ownership if checks in handlers

Do not touch Infrastructure, Api, or frontend.
```

Commit:

```text
feat(session-operations): phase X.2 — application layer (HU-19)

Ref: HU-19
Ref: DES-26
Ref: DES-70
```

---

## 7. Backend phase X.3 — Infrastructure layer

> Run `@backend/.agents/driver-agent.md` and select **X.3** from the phase menu.

```text
Use @backend/.agents/backend-agent.md.
Implement backend phase X.3 for HU-19 in session-operations-service.

Scope:
- verify whether the existing AssignedOperatorUserId EF mapping and snapshot are
  already sufficient; if they are, explicitly note that no new migration is required
- extend repository / persistence tests so session operator assignment round-trips
  through the existing live_sessions.assigned_operator_user_id column
- if X.2 introduced an Identity-facing target-actor port, implement it using the
  narrowest existing Identity surface that is sufficient; if no existing surface
  is sufficient, add the smallest internal contract necessary and document it as
  supporting actor-facts data, not assignment ownership
- integration tests must prove: assigned operator persists; reassignment updates
  the persisted value; ineligible target actor facts are surfaced correctly to the
  application layer

Gate:
- dotnet build passes on the solution
- migration is confirmed no-op against the current snapshot, or a justified small
  migration exists if the model genuinely required one
- repository / client integration tests pass for assignment and reassignment
- no infrastructure code creates a second assignment source of truth in Identity

Do not touch Api or frontend.
```

Commit:

```text
feat(session-operations): phase X.3 — infrastructure layer (HU-19)

Ref: HU-19
Ref: DES-26
Ref: DES-70
```

---

## 8. Backend phase X.4 — API layer

> Run `@backend/.agents/driver-agent.md` and select **X.4** from the phase menu.

```text
Use @backend/.agents/backend-agent.md.
Implement backend phase X.4 for HU-19 in session-operations-service.

Scope:
- add an Administrator-only assignment endpoint to the existing SessionsEndpoints
  group, for example:
  PATCH /api/sessions/{liveSessionId}/operator-assignment
  body: { "operatorUserId": <int> }
- keep the endpoint thin: bind the request, send AssignOperatorToSession, return
  the updated assignment state (at minimum the liveSessionId and assignedOperatorUserId)
- expose the session's assigned operator as session state through the command
  result / response contract for this slice; do not wait for HU-20 read models
- realize the mandated Proxy at the API edge as well: use endpoint authorization
  policy / structural guard, not inline role / ownership checks
- endpoint tests for: successful assignment; successful reassignment; unauthorized
  caller receives 401/403; ineligible target actor receives 4xx; unknown session
  receives 404

Gate:
- endpoint integration tests pass for success, reassignment, unauthorized, invalid target,
  and unknown session paths
- Proxy gate: API-layer access is enforced through policy / guard wiring with no
  ad-hoc role or ownership if checks in the endpoint
- service coverage reaches the enforced ADR-0005 threshold

Do not touch frontend.
```

Commit:

```text
feat(session-operations): phase X.4 — api layer (HU-19)

Ref: HU-19
Ref: DES-26
Ref: DES-70
```

---

## 8.5. Docker rebuild + smoke

```text
From backend/, rebuild and start the stack for manual verification:

1. docker compose build session-operations-service && docker compose up -d session-operations-service
2. If X.3 required a supporting identity contract, also rebuild identity-access-service:
   docker compose build identity-access-service && docker compose up -d identity-access-service
3. docker compose build api-gateway && docker compose up -d api-gateway
4. Ensure a LiveSession already exists (HU-16 create path or fixture data).
5. Assign an operator:
   curl -i -X PATCH http://localhost:<gateway-port>/api/sessions/<liveSessionId>/operator-assignment \
     -H "Content-Type: application/json" \
     -H "X-User-Id: <adminId>" -H "X-User-Role: Administrator" -H "X-User-Email: admin@umbral.test" \
     -d '{"operatorUserId": <operatorUserId>}'
   Expect success with the updated assignment state.
6. Repeat with a different operator user id -> expect reassignment success with the
   new assigned operator reflected.
7. Repeat as a non-admin caller -> expect 401/403.
8. Repeat with a non-operator/non-admin target user id -> expect 4xx rejection.
```

**Gate:** the assignment path is reachable in the running stack; assignment and
reassignment succeed for valid admin/operator actors, and unauthorized or
ineligible paths are rejected.

---

## 9. Frontend slice

```text
Generate a multi phase plan in a markdown file, like the one in
@frontend/plans/hu-03-frontend-role-permission-assignment.md, save it in
@frontend/plans/ for the following:
Use @frontend/AGENTS.md.

Implement the admin session-operator assignment flow for HU-19 using the verified
session-operations contract and any verified Identity-backed operator catalog /
lookup surface.

Scope:
- present the current responsible operator on the relevant admin session-management
  surface
- let an Administrator assign or change the responsible operator for a session
  through the verified backend contract
- only offer valid operator/admin candidates from the verified actor-facts source;
  do not invent a frontend-only rule for who is assignable
- surface backend rejection cleanly for unauthorized callers or invalid target actors
- keep this slice focused on assignment/change; do not expand into the full
  "my assigned sessions" operator read model, which is HU-20

Gate:
- the UI shows the current assignment state returned by the backend
- an Administrator can assign or change the responsible operator successfully
- invalid-target and unauthorized failures are rendered cleanly
- the UI does not create a client-side source of truth separate from the session state
```

Commit:

```text
feat(frontend): session operator assignment — HU-19

Ref: HU-19
Ref: DES-26
Ref: DES-70
```

---

## 10. Close-out

```text
Before opening the PR:
- confirm all four backend phase commits exist
- confirm the assignment smoke path was exercised (assign, reassign, unauthorized, invalid target)
- confirm the frontend flow shows current assignment state and uses the verified backend contract
- map each acceptance criterion to where it is enforced:
  AC#1 admin can assign or change operator -> assignment command + API + admin UI
  AC#2 a session can be associated to a responsible operator -> LiveSession.AssignedOperatorUserId persisted and returned as session state
  AC#3 operator may administer only assigned / policy-visible sessions -> assignment-aware Proxy / guard seam for protected session administration
  AC#4 assignment is recorded for audit -> session-owned domain event / session history publication

Then open the PR:
gh pr create --draft --base develop \
  --title "feat: session operator assignment — HU-19" \
  --body "Closes DES-26
Ref: DES-70

Touched: backend/services/session-operations-service/, backend/services/identity-access-service/ (supporting actor-facts contract only, if needed), frontend/"
```

## Rationale

**HU-19 is about ownership, not duplication.** The canonical docs already place
`assignedOperatorUserId` on `LiveSession`, and the entity spec plus both service
contexts draw a bright line: `SessionOperations` owns runtime session state,
while `Identity` owns actor facts and coarse policy checks. The most important
failure mode in this slice is therefore architectural, not mechanical: adding a
second assignment owner in Identity. The prompt keeps the session aggregate as
the only assignment source of truth and treats any Identity surface as supporting
validation input only.

**The mandated patterns force two structural seams.** `Facade` belongs in X.2
because assignment spans session lookup, target-actor validation, mutation,
persistence, and audit publication. `Proxy` also belongs in X.2/X.4 because the
acceptance criteria are not just "store an operator id"; they imply guarded
session administration. Even if the later start/pause/resume actions land in
HU-21A and beyond, HU-19 must still introduce the assignment-aware guard as a
reusable structural seam now, rather than deferring it into future ad-hoc checks.

**One ambiguity is intentionally called out rather than guessed.** The PRD and
acceptance criteria do not state whether reassignment is legal only before the
session starts or also during later states. If the canon docs do not resolve
that, keep the decision explicit in code/tests and record it in the implementation
notes instead of silently baking in an arbitrary lifecycle restriction.

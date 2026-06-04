# Prompt Example - HU-18 Asociacion de equipos a sesiones (Feature Slice)

Concrete prompt sequence for driving HU-18 through a full feature slice on
`feature/hu-18-asociacion-de-equipos-a-sesiones`. Follows the pattern in
[workflow_for_prompts.md](./workflow_for_prompts.md).

**Key difference from HU-16:** HU-16 established creation of a `LiveSession`
from exactly one trivia source and a frozen source snapshot. HU-18 extends that
same session backbone with pre-start team association, team-list visibility, and
the minimum readiness rule that a session cannot start empty. This slice is
about setup orchestration, not about recreating source selection or full
lifecycle transitions.

When working from the monorepo root, make the target workload explicit in each
prompt. For backend steps, point to `@backend/.agents/backend-agent.md`. For
frontend steps, point to `@frontend/AGENTS.md`. Do not ask for backend and
frontend implementation in the same phase prompt; coordinate them as separate
scoped steps tied together by the verified API contract.

---

## Required design patterns

- `Facade`
  - Why: team assignment coordinates runtime session state changes and persistence.
  - Phase owner: X.2 Application
  - Gate obligation: team association is coordinated through one explicit
    orchestration entry point, not by scattering cross-context validation,
    session lookup, persistence, and readiness checks across multiple handlers or
    endpoints.

---

## Pre-resolved orient (as of 2026-06-03)

> Step 1 has already been run. Paste this section into any agent session that
> needs context before picking up a phase; no need to re-run the orient prompt
> unless local docs or Linear state changed.

### What has already landed and must be reused

**Same-service baseline**
- `SessionOperations` owns `LiveSession`, runtime `Team`, `SessionParticipant`,
  `JoinContext`, and the final admission decision for live sessions.
- HU-16 (`DES-23`) is **Done** and establishes the session-creation baseline
  from a single trivia source with a frozen source copy.
- HU-07B (`DES-12`) is **Done** and confirms reconnect, late join, capacity,
  assignment, and runtime restoration remain session-owned rules.

**Cross-context baseline**
- Identity already owns registered/active team reference data and
  participant-to-team membership facts.
- `SessionOperations` may consume team identity/reference facts, but it must not
  reuse the Identity `Team` aggregate as if both contexts shared ownership.

**Current same-service branch state**
- DES-26 (HU-19) is **In Progress** on the same service, so the branch base for
  HU-18 resolves to `feature/hu-19-asignacion-de-operador-a-sesion` while that
  predecessor remains unmerged.

### What HU-18 adds on top (per PRD DES-70 and DES-25)

| Concern | New work |
|---|---|
| Session setup | Allow an operator to associate registered, active teams to a scheduled session before start. |
| Query surface | Allow querying which teams are already associated with a given session. |
| Readiness rule | Prevent a session from starting without at least one associated team. |
| Persistence | Persist the team association as session-owned runtime setup state. |
| Frontend | Add or plan the operator-facing setup flow for assigning teams and reviewing the assigned list. |

### Branch state and prerequisite

`feature/hu-18-asociacion-de-equipos-a-sesiones` should branch from
`feature/hu-19-asignacion-de-operador-a-sesion` while DES-26 is still
**In Progress**. If DES-26 merges first, rebase the slice plan to `develop`.

### Linear state (as of 2026-06-03)

- DES-25 (HU-18): **Todo**, labels `ready-for-agent`,
  `svc:session-operations-service`, `Feature`
- DES-23 (HU-16): **Done**
- DES-26 (HU-19): **In Progress**
- DES-70 (PRD): **Backlog**, labels `ready-for-agent`,
  `svc:session-operations-service`

> Linear live state may have changed. Use the Linear MCP to verify DES-25 and
> DES-70 status/labels if needed, but do not re-fetch PRD scope from Linear; the
> local file `@backend/docs/prd/DES-70-primera-implementacion-de-session-operations-service-hu-15-a-hu-36.md`
> is authoritative.

---

## 1. Orient - read service state and PRD before planning

> **Skip this step if you have read the pre-resolved orient section above.** Run
> it only if the README or Linear state may have changed since 2026-06-03.

```text
Read the following files and summarise what has been decided and implemented so far:
- @backend/services/session-operations-service/README.md - current implementation status
- @backend/services/session-operations-service/CONTEXT.md - service ownership and required patterns
- @backend/docs/prd/DES-70-primera-implementacion-de-session-operations-service-hu-15-a-hu-36.md
- @backend/docs/hu07a-context.md
- @backend/docs/hu07b-context.md

Then use the Linear MCP to fetch only the current live state of:
- DES-25 (HU-18 - Asociacion de equipos a sesiones) - status and labels
- DES-23 (HU-16 - predecessor slice) - status
- DES-26 (HU-19 - same-service in-progress predecessor) - status

Output:
- what session-operations concepts and cross-context team boundaries already exist
- what HU-18 adds on top per the PRD: team association, team list, and no-empty-start rule
- current Linear status and labels for DES-25 and DES-70

Do not start planning or implementing yet.
```

---

## 2. Label DES-25 as ready-for-agent

> DES-25 already carries `ready-for-agent` as of 2026-06-03. Reconfirm it before
> the slice starts.

```text
Use the Linear MCP to confirm DES-25 carries the label ready-for-agent.
If it is missing, add it.
Confirm the updated ticket state.
```

---

## 3. Confirm slice readiness

```text
Use the Linear MCP to confirm DES-25 carries both
svc:session-operations-service and ready-for-agent labels and output its current
status and acceptance criteria.

The PRD scope is already in the local file at
@backend/docs/prd/DES-70-primera-implementacion-de-session-operations-service-hu-15-a-hu-36.md -
do not re-fetch the PRD from Linear; read the local file if you need
implementation decisions.

Output the confirmed HU id, title, acceptance criteria, labels, and PRD ref
before planning the slice.
```

In the remaining examples below, `HU-18`, `DES-25`, and `DES-70` are the
resolved values for this slice.

---

## 4. Start the slice

```text
Prepare the session team-association slice on branch
feature/hu-18-asociacion-de-equipos-a-sesiones.
Use the HU id and DES id resolved from Linear in the previous step.
This slice affects frontend and session-operations-service.

Before implementation, confirm the current baseline is:
- session creation from exactly one trivia source already exists (HU-16)
- SessionOperations owns runtime Team and LiveSession state
- Identity owns registered/active team reference data, not session runtime ownership

Move the resolved HU ticket to In Progress and output the exact scope, branch
name, base branch, and touched surfaces.
```

---

## 5. Backend phase X.1 - Domain layer

```text
Use @backend/.agents/backend-agent.md.
Implement backend phase X.1 for HU-18 in session-operations-service.
Use the service PRD (DES-70) and canonical docs.

Before writing anything, inspect the current session-operations domain baseline
and confirm which concepts already exist around LiveSession, Team, JoinContext,
SessionState, and session setup. Extend rather than recreate.

Scope:
- add or extend the session-owned model that associates teams to a scheduled LiveSession
- preserve cross-context ownership: correlate to the Identity team reference id,
  but do not model the Identity Team aggregate inside this service
- prevent duplicate association of the same team to the same session
- establish the no-empty-start rule as a session-owned invariant or readiness seam
  that later lifecycle work can reuse
- do not reopen source-selection or snapshot logic from HU-16

Gate:
- Domain build passes
- no existing session creation or runtime ownership invariants are broken
- new invariants are expressed as unit tests for team association and no-empty-start readiness

Do not touch other backend layers or frontend.
```

Commit:

```text
feat(session-operations): phase X.1 - domain layer (HU-18)

Ref: HU-18
Ref: DES-25
Ref: DES-70
```

Then run: `/debrief`

---

## 6. Backend phase X.2 - Application layer

```text
Use @backend/.agents/backend-agent.md.
Implement backend phase X.2 for HU-18 in session-operations-service.
Use the service PRD (DES-70) and canonical docs.

Scope:
- add the application use case(s) for associating teams to a session and listing
  the teams already associated with that session
- Facade obligation (mandated): coordinate session lookup, team validation
  against Identity-owned reference data, duplicate protection, persistence, and
  readiness consequences through one explicit orchestration entry point
- enforce operator-facing access through the existing authorization approach;
  do not scatter orchestration logic across multiple handlers or endpoints
- handler unit tests for: successful team association, rejected duplicate
  association, rejected inactive or missing team, rejected wrong session state if
  applicable, and successful list/query path

Gate:
- clean build passes
- handler and validator unit tests pass for all principal paths and rejection branches
- Facade gate: coordination is concentrated in one orchestration entry point, not
  split across ad-hoc handler/endpoint logic

Do not touch Infrastructure, Api, or frontend.
```

Commit:

```text
feat(session-operations): phase X.2 - application layer (HU-18)

Ref: HU-18
Ref: DES-25
Ref: DES-70
```

Then run: `/debrief`

---

## 7. Backend phase X.3 - Infrastructure layer

```text
Use @backend/.agents/backend-agent.md.
Implement backend phase X.3 for HU-18 in session-operations-service.
Use the service PRD (DES-70) and canonical docs.

Scope:
- persist the session-owned team association state needed by HU-18
- verify whether the current persistence model from HU-16 already contains the
  required shape; if not, add repository support, EF configuration, and a migration
- keep Identity team ids as foreign correlation values only; no cross-service FK
- integration tests must prove that associated teams are persisted and can be
  queried back by session, and that duplicate association is rejected or remains unchanged

Gate:
- dotnet build passes on the solution
- migration is confirmed no-op against the current snapshot or created if required
- repository/infrastructure integration tests pass for persistence and query paths

Do not touch Api or frontend.
```

Commit:

```text
feat(session-operations): phase X.3 - infrastructure layer (HU-18)

Ref: HU-18
Ref: DES-25
Ref: DES-70
```

Then run: `/debrief`

---

## 8. Backend phase X.4 - API layer

```text
Use @backend/.agents/backend-agent.md.
Implement backend phase X.4 for HU-18 in session-operations-service.
Use the service PRD (DES-70) and canonical docs.

Scope:
- expose the operator-facing API surface needed to associate teams to a session
  and query the teams already associated with that session
- keep transport thin: no business logic in endpoints
- prove that only the allowed actor can add teams and list associated teams
- prove that duplicate or invalid team association is rejected with the correct response
- prove that the query surface returns the persisted associated-team list

Gate:
- endpoint integration tests pass for association, duplicate rejection, invalid team, and list/query
- no regression on HU-16 session-creation behavior
- service coverage reaches the enforced threshold

Do not touch frontend.
```

Commit:

```text
feat(session-operations): phase X.4 - api layer (HU-18)

Ref: HU-18
Ref: DES-25
Ref: DES-70
```

Then run: `/debrief`

---

## 8.5. Docker rebuild + smoke

```text
From the repo root, rebuild and start the backend stack for manual verification:

1. docker compose build session-operations-service
2. docker compose up -d session-operations-service
3. Run a smoke test against the session team-association endpoint(s)
4. Confirm the happy path persists and returns the associated teams
5. Confirm duplicate or invalid team association is rejected
```

**Gate:** the association/query path is reachable in the running stack and the
observed behavior matches the implemented setup rules.

---

## 9. Frontend slice

```text
Generate a multi phase plan in a markdown file, like the one in `@frontend/plans/hu-03-frontend-role-permission-assignment.md`, save it in `@frontend/plans/` for the following:
Use @frontend/AGENTS.md.

Implement the operator-facing session setup flow for HU-18 against the verified
session-operations contract.

Scope:
- surface the list of registered/active teams available for assignment
- allow the operator to associate one or more teams to a scheduled session
- display the teams already associated with the session
- reflect backend validation and duplicate/inactive-team errors clearly
- preserve the verified API contract; do not invent direct frontend dependencies
  on backend source code

Gate:
- frontend uses only the verified backend contract
- operator can assign teams and then see the persisted associated-team list
- invalid or duplicate assignments show deterministic feedback

Commit message:

feat(frontend): asociacion de equipos a sesiones - HU-18

Ref: HU-18
Ref: DES-25
Ref: DES-70
```

---

## 10. Close-out

```text
Verify the full slice against the HU-18 acceptance criteria:
- the operator can add registered and active teams to a scheduled session
- the system can report which teams are associated with each session
- a session cannot start without at least one associated team
- the assignment is persisted before the session starts

Then prepare the draft PR:

gh pr create --draft --base develop --head feature/hu-18-asociacion-de-equipos-a-sesiones --title "feat: HU-18 asociacion de equipos a sesiones" --body "Implements HU-18 (DES-25) using PRD DES-70."
```

---

## Rationale

`HU-18` is mapped to `Facade` because the hard part is not a single domain
mutation by itself; it is the orchestration boundary between session-owned
runtime state and Identity-owned team reference data. The slice needs one
explicit coordination entry point to validate the referenced team, extend the
existing `LiveSession` setup backbone, persist the association, and carry the
no-empty-start rule forward into later lifecycle work.

This differs from the next lifecycle-oriented slices. `HU-18` should not invent
the full `State` transition system early just to satisfy the acceptance
criterion that a session cannot start empty. That rule should be established now
as a reusable session-owned invariant or readiness guard so `HU-21A` can plug
into it instead of re-deriving it later.

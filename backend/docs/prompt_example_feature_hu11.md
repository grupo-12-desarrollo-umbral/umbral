# Prompt Example — HU-11 Creación y edición de quizzes de trivia (Feature Slice)

Concrete prompt sequence for driving HU-11 through a full feature slice on `feature/hu-11-trivia-quiz-management`. Follows the pattern in [workflow_for_prompts.md](./workflow_for_prompts.md).

**Key difference from HU-09:** HU-09 established the first `mission-design-service` aggregate root around mission authoring. HU-11 introduces the second aggregate root, `TriviaQuiz`, and must preserve quiz basics plus associated-question shape without collapsing later slices (`HU-14A`, `HU-14B`, `HU-12`, `HU-13`) into the first trivia authoring baseline.

When working from the monorepo root, make the target workload explicit in each prompt.
For backend steps, point to `@backend/.agents/backend-agent.md`. For frontend steps,
point to `@frontend/AGENTS.md`. Do not ask for backend and frontend implementation in
the same phase prompt; coordinate them as separate scoped steps tied together by the
verified API contract.

---

## Required design patterns

- `Template Method`
  - Why: quiz creation/edit validation keeps one invariant workflow.
  - Phase owner: X.1 Domain and X.2 Application
  - Gate obligation: the stable trivia authoring validation sequence must be explicit in phase scope and in the gate for both phases; create and update must extend one shared workflow instead of duplicating or scattering rule checks.

---

## Pre-resolved orient (as of 2026-06-01)

> Step 1 has already been run. Paste this section into any agent session that needs context
> before picking up a phase — no need to re-run the orient prompt.

### What predecessors have already landed (reuse candidates for HU-11)

HU-09 (`DES-14`) is the only same-service predecessor currently tracked in Linear as **Done**. There is no dedicated `backend/docs/hu09-context.md`, and the service README is sparse, so the predecessor record here is intentionally conservative.

**Domain layer**

- `Mission` is already the established aggregate root for the first authoring slice in this service
- `MissionActivation` is already treated as source readiness, not runtime lifecycle
- `Difficulty` and `MaximumTime` already exist as bounded-context value objects on the mission side

**Application layer**

- mission create, update, deactivate, catalog, and detail flows are already the service baseline
- `AuthorizationBehaviour` and `ValidationBehaviour` already exist in the service pipeline

**Infrastructure / API**

- EF Core persistence baseline, repositories, and `/api/missions` endpoints already exist
- `mission-design-service` is already wired into local Docker and the API gateway

**Frontend**

- mission-management UI is the documented predecessor frontend slice

**Coverage:** no predecessor context file records a stable aggregate percentage for `mission-design-service`; verify real service coverage during phase X.4.

### What HU-11 adds on top (per PRD DES-62 and DES-17)

| Concern | New work |
|---|---|
| `TriviaQuiz` aggregate root | Create the trivia authoring aggregate in draft state |
| Quiz editability | Update a quiz only while its current state allows authoring edits |
| Unified quiz detail | Keep quiz metadata and associated questions together in backend detail/read models |
| Pre-publication consultation | Persist changes so they are queryable before publication and archival slices exist |
| Backend contract | Create the trivia quiz API contract for create, update, list/detail |
| Frontend flow | Administrator quiz create/edit/detail UI against the new contract |

### Branch state and prerequisite

`feature/hu-11-trivia-quiz-management` should be branched from `develop`. No same-service predecessor is currently **In Progress**, so there is no feature-branch dependency to inherit first.

**Before starting implementation:** inspect the current `mission-design-service` source and confirm that trivia-side types do not yet exist. Extend the existing service baseline rather than creating parallel architecture or runtime-owned abstractions.

### Linear state (as of 2026-06-01)

- DES-17 (HU-11): **Backlog**, labels: `Feature`, `ready-for-agent`, `svc:mission-design-service`
- DES-14 (HU-09): **Done**
- DES-62 (PRD): **Backlog**, labels: `ready-for-agent`

> Linear live state may have changed. Use the Linear MCP to verify DES-17 status and labels if needed, but do not re-fetch PRD scope — read the local file at `@backend/docs/prd/DES-62-mission-design-service-baseline.md` instead.

> Note: the local PRD file and DES-17 relation are authoritative for this slice even though the Linear PRD ticket does not currently carry `svc:mission-design-service`.

---

## 1. Orient — read service state and PRD before planning

> **Skip this step if you have read the pre-resolved orient section above.** Run it only
> if the service README/source or Linear state may have changed since 2026-06-01.

```text
Read the following files and summarise what has been decided and implemented so far:
- @backend/services/mission-design-service/README.md — current service status
- @backend/services/mission-design-service/CONTEXT.md — bounded-context language and pattern expectations
- @backend/docs/prd/DES-62-mission-design-service-baseline.md — the authoritative PRD for HU-09 to HU-14
- @backend/docs/hu11-context.md — the pre-resolved HU-11 context for this slice

Then inspect the existing mission-design source only enough to confirm the current baseline:
- @backend/services/mission-design-service/src/Domain/
- @backend/services/mission-design-service/src/Application/
- @backend/services/mission-design-service/src/Api/Endpoints/MissionsEndpoints.cs

Then use the Linear MCP to fetch only the current live state of:
- DES-17 (HU-11 — Creación y edición de quizzes de trivia) — status and labels
- DES-14 (HU-09 — predecessor slice) — status

Output:
- what same-service baseline already exists from HU-09
- what HU-11 adds on top per the PRD: TriviaQuiz authoring baseline, permitted edits by state, unified detail before publication
- whether trivia-side types already exist in source or must be introduced cleanly
- current Linear status and labels for DES-17

Do not start planning or implementing yet.
```

---

## 2. Label DES-17 as ready-for-agent

> DES-17 carries `ready-for-agent` as of 2026-06-01. Use this step to confirm the label remains present before execution.

```text
Use the Linear MCP to confirm DES-17 still carries the label ready-for-agent.
If it is missing, add it.
Output the updated DES-17 ticket state and labels.
```

---

## 3. Confirm slice readiness

```text
Use the Linear MCP to confirm DES-17 carries both svc:mission-design-service
and ready-for-agent labels and output its current status and acceptance criteria.

The PRD scope is already in the local file at
@backend/docs/prd/DES-62-mission-design-service-baseline.md —
do not re-fetch the PRD from Linear; read the local file if you need implementation decisions.

Output the confirmed HU id, title, acceptance criteria, and labels before planning the slice.
```

In the remaining examples below, `HU-11` and `DES-17` are the resolved values for this slice. `DES-62` is the shared PRD reference for `mission-design-service`; its content lives in the local file above.

---

## 4. Start the slice

```text
Prepare the trivia-quiz-management slice on branch feature/hu-11-trivia-quiz-management.
Use the HU id and DES id resolved from Linear in the previous step.
This slice affects backend mission-design-service and frontend.

The pre-resolved orient at the top of this document lists what HU-09 already landed
and what HU-11 adds. Do not re-read the PRD for scoping.

Before implementation, confirm the current service source has no trivia-side aggregate,
repository, endpoint, or read-model baseline yet. Extend the existing service architecture;
do not create parallel abstractions or runtime-owned concepts.

Move DES-17 to In Progress and output the exact scope, branch name, and touched surfaces.
```

---

## 5. Backend phase X.1 — Domain layer

```text
Use @backend/.agents/backend-agent.md.
Implement backend phase X.1 for HU-11 in mission-design-service.
Use the service PRD (DES-62) and canonical docs.

Before writing anything, inspect the current Mission-side domain baseline and the
bounded-context language in @backend/services/mission-design-service/CONTEXT.md.
Introduce the trivia-side domain cleanly inside the existing service structure.

Scope:
- add the trivia authoring aggregate root: TriviaQuiz
- add the minimum trivia-side child/entity/value-object/state concepts needed for HU-11
  to exist coherently, but do not implement full HU-14A/HU-14B question-management rules yet
- model the minimum quiz lifecycle needed by the PRD for HU-11 editability
  (draft/editable vs not editable), without prematurely implementing publish/archive flows
- implement the mandated Template Method obligation in the domain:
  one stable quiz authoring validation workflow with overridable steps for create vs update
  and state-sensitive editability checks
- preserve the aggregate/detail shape so quiz basics and associated questions stay together
  in the domain model, even if detailed question CRUD lands later
- add domain events needed for HU-11 authoring baseline, such as TriviaQuizCreated
  and TriviaQuizDetailsUpdated, if they are not already present
- if numeric trivia ranges needed by HU-11 are still undefined in the PRD, record the ambiguity
  in the debrief rather than inventing HU-14B validation scope here

Gate:
- Domain build passes
- New domain invariants are expressed as unit tests on TriviaQuiz
- Gate: the domain expresses a Template Method style quiz-authoring validation workflow,
  with one stable sequence and overridable steps, rather than duplicated create/update logic
- No publication/archive behavior from HU-12 or question-rule enforcement from HU-14B is
  prematurely implemented here

Do not touch other backend layers or frontend.
```

Commit:

```text
feat(mission-design): phase X.1 — domain layer (HU-11)

Ref: HU-11
Ref: DES-17
Ref: DES-62
```

Then run: `/debrief`

---

## 6. Backend phase X.2 — Application layer

```text
Use @backend/.agents/backend-agent.md.
Implement backend phase X.2 for HU-11 in mission-design-service.
Use the service PRD (DES-62) and canonical docs.

Before writing anything, inspect the existing Application/Missions baseline and mirror
its conventions for commands, queries, validators, DTOs, handlers, and permissions.

Scope:
- add trivia-side repository/read-model interfaces if they are missing:
  ITriviaQuizRepository and ITriviaQuizReadModelRepository
- commands + handlers + validators:
  CreateTriviaQuiz, UpdateTriviaQuiz
- queries + handlers:
  GetTriviaCatalog and GetTriviaDetail, if a catalog/detail read is needed to satisfy
  the HU-11 consultation acceptance criteria
- DTOs/projections that keep quiz metadata and associated-question shape together
- administrator-only authorization for trivia quiz mutations
- implement the mandated Template Method obligation in the application layer by routing
  create/update through one stable validation workflow instead of forking duplicated rule sets
- not-found handling and state-based edit rejection for update paths

Gate:
- clean build passes
- handler and validator unit tests cover valid path plus rejection/error branches
- Gate: create/update validation is enforced through a Template Method style workflow
  with shared sequencing and operation-specific steps, not two unrelated validation paths
- application layer does not leak infrastructure concerns

Do not touch Infrastructure, Api, or frontend.
```

Commit:

```text
feat(mission-design): phase X.2 — application layer (HU-11)

Ref: HU-11
Ref: DES-17
Ref: DES-62
```

Then run: `/debrief`

---

## 7. Backend phase X.3 — Infrastructure layer

```text
Use @backend/.agents/backend-agent.md.
Implement backend phase X.3 for HU-11 in mission-design-service.
Use the service PRD (DES-62) and canonical docs.

Before writing anything, inspect the current persistence baseline:
- ApplicationDbContext
- existing mission configuration and repositories
- Init migration and current snapshot

Scope:
- persist the TriviaQuiz aggregate cleanly in EF Core
- add trivia-side entity configuration, repository implementation, and read-model repository
- add or confirm storage for the minimum associated-question shape required by HU-11 detail reads
- create a migration if the snapshot does not already cover the new trivia-side tables
- integration tests for create quiz, update editable quiz, reject update when state disallows edits,
  and coherent detail read of quiz basics plus associated-question shape

Database isolation: each integration test must clean shared state before its scenario
to avoid cross-test pollution.

Gate:
- dotnet build passes on the service solution
- migration/snapshot is consistent with the new trivia-side persistence model
- repository integration tests pass for create, update, edit-blocked, and detail-read paths

Do not touch Api or frontend.
```

Commit:

```text
feat(mission-design): phase X.3 — infrastructure layer (HU-11)

Ref: HU-11
Ref: DES-17
Ref: DES-62
```

Then run: `/debrief`

---

## 8. Backend phase X.4 — API layer

```text
Use @backend/.agents/backend-agent.md.
Implement backend phase X.4 for HU-11 in mission-design-service.
Use the service PRD (DES-62) and canonical docs.

Scope:
- add trivia quiz endpoints under a dedicated trivia route group
- POST create quiz endpoint for administrators
- PUT or PATCH edit quiz endpoint for administrators, aligned to the existing service API style
- GET trivia catalog/detail endpoints needed so draft changes are queryable before publication
- endpoint proof that non-admin callers receive 403 on mutations
- endpoint proof that state-disallowed edits are rejected with the mapped domain/application error
- confirm the detail response keeps quiz basics and associated-question shape together

Gate:
- endpoint tests pass for create, update, non-admin rejection, and edit-blocked paths
- no regression on the existing mission endpoints
- service coverage reaches the required threshold for this service

Do not touch frontend.
```

Commit:

```text
feat(mission-design): phase X.4 — api layer (HU-11)

Ref: HU-11
Ref: DES-17
Ref: DES-62
```

Then run: `/debrief`

---

## 8.5. Docker rebuild and smoke

```text
Rebuild and restart the local backend stack for mission-design verification:

1. Run `docker compose -f backend/docker-compose.yml build mission-design-service api-gateway`
2. Run `docker compose -f backend/docker-compose.yml up -d mission-design-service api-gateway`
3. Wait for services to become healthy/started
4. Smoke test:
   - `curl http://localhost:5001/health`
   - if the trivia endpoints are routed through the gateway in this slice, also smoke the relevant
     gateway route after authentication setup is in place

Output:
- build result
- container status
- curl response summary
```

---

## 9. Frontend slice

```text
Use @frontend/AGENTS.md.
Implement the frontend slice for HU-11 on top of the verified trivia quiz backend contract.

Scope:
- administrator UI to create a trivia quiz
- administrator UI to edit a trivia quiz while its state permits edits
- quiz list/detail flow so saved draft changes are queryable before publication
- preserve and render the associated-question section in the detail/edit experience without
  inventing the full HU-14A question-management workflow early
- align route guards and action visibility with the backend authorization contract

Gate:
- frontend uses the verified backend trivia contract, not guessed payloads
- unauthorized users cannot access mutation UI
- draft changes are visible in the intended list/detail/edit flows
- no regression on existing mission-management frontend behavior
```

Commit:

```text
feat(frontend): trivia quiz management — HU-11

Ref: HU-11
Ref: DES-17
Ref: DES-62
```

Then run: `/debrief`

---

## 10. Close-out

```text
Before closing HU-11, verify every acceptance criterion against the implemented backend and frontend:
- administrator can create a trivia quiz
- system allows editing while the quiz state permits it
- quiz preserves base information and associated questions together
- changes are queryable before publication

Then prepare the PR:

gh pr create \
  --title "feat: HU-11 trivia quiz management" \
  --body-file backend/docs/hu11-context.md

Output:
- acceptance-criteria checklist with pass/fail evidence
- final touched surfaces
- any follow-on work intentionally deferred to HU-14A, HU-14B, HU-12, or HU-13
```

---

## Rationale

HU-11 carries `Template Method` while HU-09 carried no mandated design pattern because the trivia side introduces a validation workflow problem that mission-baseline CRUD did not. Here the risk is not just persisting a new aggregate, but doing so without duplicating nearly identical create/update rule sequences or scattering state/editability checks across handlers and endpoints. The mandated pattern keeps the quiz-authoring flow stable while allowing state-sensitive and operation-specific steps to vary.

There is one real ambiguity in the PRD: HU-11 says the quiz must preserve its associated questions together with its basic information, but the explicit question-management slice is scheduled later in HU-14A/HU-14B. The safe reading for HU-11 is to establish the `TriviaQuiz` aggregate, its detail shape, and the pre-publication edit boundary now, while deferring full question CRUD and strict question-rule enforcement to the later dedicated slices.

# Prompt Example — HU-14A Gestión de preguntas y opciones de trivia (Feature Slice)

Concrete prompt sequence for driving HU-14A through a full feature slice on `feature/hu-14a-trivia-question-and-options-management`. Follows the pattern in [workflow_for_prompts.md](./workflow_for_prompts.md).

**Key difference from HU-11:** HU-11 established the `TriviaQuiz` aggregate root and draft edit boundary, but deliberately stopped short of real question-management behavior. HU-14A extends that existing trivia baseline so administrators can author `TriviaQuestion` and `TriviaOption` content inside a quiz, while still avoiding the stricter invalid-rule matrix reserved for HU-14B.

When working from the monorepo root, make the target workload explicit in each prompt.
For backend steps, point to `@backend/.agents/backend-agent.md`. For frontend steps,
point to `@frontend/AGENTS.md`. Do not ask for backend and frontend implementation in
the same phase prompt; coordinate them as separate scoped steps tied together by the
verified API contract.

---

## Required design patterns

- `Template Method`
  - Why: question authoring uses a shared validation flow with question-specific steps.
  - Phase owner: X.1 Domain and X.2 Application
  - Gate obligation: the stable trivia-question authoring validation sequence must be explicit in phase scope and in the gate for both phases; add/update question flows must extend one shared workflow instead of duplicating or scattering rule checks.

---

## Pre-resolved orient (as of 2026-06-01)

> Step 1 has already been run. Paste this section into any agent session that needs context
> before picking up a phase — no need to re-run the orient prompt.

### What predecessors have already landed (reuse candidates for HU-14A)

HU-09 (`DES-14`) and HU-11 (`DES-17`) are the same-service predecessors currently tracked in Linear as **Done**. HU-11 is the immediate predecessor for trivia work and is the main reuse target for this slice.

**Domain layer**

- `Mission` remains the original aggregate root baseline for authoring on the mission side
- `TriviaQuiz` already exists as the trivia-side aggregate root baseline from HU-11
- the quiz lifecycle/editability boundary already exists at the aggregate level
- quiz basics and associated-question shape are already expected to live together in the trivia domain model
- HU-11 already introduced the mandated `Template Method` style validation workflow for quiz authoring

**Application layer**

- mission-side create/update/deactivate/catalog/detail flows remain the service baseline
- `AuthorizationBehaviour` and `ValidationBehaviour` already exist in the service pipeline
- trivia-side create/update/query use cases and repository/read-model abstractions were introduced in HU-11

**Infrastructure / API**

- EF Core persistence baseline, repositories, and `/api/missions` endpoints already exist
- trivia-side persistence and draft consultation endpoints were established in HU-11
- `mission-design-service` is already wired into local Docker and the API gateway

**Frontend**

- mission-management UI exists from the earlier service baseline
- trivia-quiz create/edit/detail UI exists from HU-11, including an associated-question section shape that now needs to become real question-management behavior

**Coverage:** no stable aggregate percentage is captured in predecessor docs for `mission-design-service`; verify real service coverage during phase X.4.

### What HU-14A adds on top (per PRD DES-62 and DES-20)

| Concern | New work |
| --- | --- |
| `TriviaQuestion` authoring | Add and edit questions under an existing `TriviaQuiz` |
| `TriviaOption` authoring | Support 2 to 4 options per question inside the same authoring flow |
| Correct-answer selection | Record exactly one correct option per question |
| Optional explanation | Persist explanation text when present without making it mandatory |
| Score and timer capture | Persist score and timer for each question as part of authoring |
| Backend contract | Extend trivia endpoints/DTOs for question and option management |
| Frontend flow | Extend the existing trivia quiz authoring UI to manage questions and options |

### Branch state and prerequisite

`feature/hu-14a-trivia-question-and-options-management` should be branched from `develop`. No same-service predecessor is currently **In Progress**, so there is no feature-branch dependency to inherit first.

**Before starting implementation:** inspect the current `mission-design-service` source and confirm that `TriviaQuiz` and its quiz-level read/write baseline already exist from HU-11. Extend that baseline for question authoring rather than creating a parallel trivia architecture or bypassing the existing authoring/editability flow.

### Linear state (as of 2026-06-01)

- DES-20 (HU-14A): **Backlog**, labels: `Feature`, `ready-for-agent`, `svc:mission-design-service`
- DES-17 (HU-11): **Done**
- DES-14 (HU-09): **Done**
- DES-62 (PRD): **Backlog**, labels: `ready-for-agent`

> Linear live state may have changed. Use the Linear MCP to verify DES-20 status and labels if needed, but do not re-fetch PRD scope — read the local file at `@backend/docs/prd/DES-62-mission-design-service-baseline.md` instead.

> Note: the local PRD file and DES-20 relation are authoritative for this slice even though the Linear PRD ticket does not currently carry `svc:mission-design-service`.

---

## 1. Orient — read service state and PRD before planning

> **Skip this step if you have read the pre-resolved orient section above.** Run it only
> if the service README/source or Linear state may have changed since 2026-06-01.

```text
Read the following files and summarise what has been decided and implemented so far:
- @backend/services/mission-design-service/README.md — current service status
- @backend/services/mission-design-service/CONTEXT.md — bounded-context language and pattern expectations
- @backend/docs/prd/DES-62-mission-design-service-baseline.md — the authoritative PRD for HU-09 to HU-14
- @backend/docs/hu11-context.md — the immediate-predecessor context for trivia baseline
- @backend/docs/hu14a-context.md — the pre-resolved HU-14A context for this slice

Then inspect the existing trivia-side source only enough to confirm the current baseline:
- @backend/services/mission-design-service/src/Domain/
- @backend/services/mission-design-service/src/Application/
- @backend/services/mission-design-service/src/Api/Endpoints/

Then use the Linear MCP to fetch only the current live state of:
- DES-20 (HU-14A — Gestión de preguntas y opciones de trivia) — status and labels
- DES-17 (HU-11 — predecessor trivia baseline) — status

Output:
- what same-service trivia baseline already exists from HU-11
- what HU-14A adds on top per the PRD: question authoring, option authoring, correct-answer capture, optional explanation, score and timer capture
- whether trivia question/option management already exists in source or must be introduced cleanly
- current Linear status and labels for DES-20

Do not start planning or implementing yet.
```

---

## 2. Label DES-20 as ready-for-agent

> DES-20 carries `ready-for-agent` as of 2026-06-01. Use this step to confirm the label remains present before execution.

```text
Use the Linear MCP to confirm DES-20 still carries the label ready-for-agent.
If it is missing, add it.
Output the updated DES-20 ticket state and labels.
```

---

## 3. Confirm slice readiness

```text
Use the Linear MCP to confirm DES-20 carries both svc:mission-design-service
and ready-for-agent labels and output its current status and acceptance criteria.

The PRD scope is already in the local file at
@backend/docs/prd/DES-62-mission-design-service-baseline.md —
do not re-fetch the PRD from Linear; read the local file if you need implementation decisions.

Output the confirmed HU id, title, acceptance criteria, and labels before planning the slice.
```

In the remaining examples below, `HU-14A` and `DES-20` are the resolved values for this slice. `DES-62` is the shared PRD reference for `mission-design-service`; its content lives in the local file above.

---

## 4. Start the slice

```text
Prepare the trivia-question-and-options-management slice on branch
feature/hu-14a-trivia-question-and-options-management.
Use the HU id and DES id resolved from Linear in the previous step.
This slice affects backend mission-design-service and frontend.

The pre-resolved orient at the top of this document lists what HU-11 already landed
and what HU-14A adds. Do not re-read the PRD for scoping.

Before implementation, confirm the current service source already has the trivia quiz
aggregate and draft authoring baseline from HU-11. Extend that baseline; do not create
parallel trivia abstractions or skip the existing quiz-level editability flow.

Move DES-20 to In Progress and output the exact scope, branch name, and touched surfaces.
```

---

## 5. Backend phase X.1 — Domain layer

```text
Use @backend/.agents/backend-agent.md.
Implement backend phase X.1 for HU-14A in mission-design-service.
Use the service PRD (DES-62) and canonical docs.

Before writing anything, inspect the current trivia-side domain baseline and the
bounded-context language in @backend/services/mission-design-service/CONTEXT.md.
Extend the existing TriviaQuiz aggregate cleanly inside the current service structure.

Scope:
- add the trivia-side child/entity concepts needed for HU-14A question authoring:
  TriviaQuestion and TriviaOption, owned by the existing TriviaQuiz aggregate
- add domain behavior to create and update question content inside a quiz while the
  quiz state still permits authoring edits
- carry question text, score, timer, optional explanation, option collection, and
  exactly one correct option as part of the domain model
- enforce the acceptance-level authoring invariants already explicit for HU-14A:
  2 to 4 options per question and exactly one correct option
- implement the mandated Template Method obligation in the domain:
  one stable trivia-question authoring validation workflow with overridable steps for
  add vs update operations and question-specific rule checks
- if numeric score/timer ranges are still unresolved in source/PRD, record that ambiguity
  in the debrief rather than inventing the full HU-14B validation matrix here
- add domain events needed for question authoring, such as TriviaQuestionAdded and
  TriviaQuestionUpdated, if they are not already present from earlier work

Gate:
- Domain build passes
- New domain invariants are expressed as unit tests on TriviaQuiz and its owned
  question/option behavior
- Gate: the domain expresses a Template Method style question-authoring validation workflow,
  with one stable sequence and overridable steps, rather than duplicated add/update logic
- No publication/archive behavior from HU-12 or full invalid-rule enforcement from HU-14B
  is prematurely implemented here

Do not touch other backend layers or frontend.
```

Commit:

```text
feat(mission-design): phase X.1 — domain layer (HU-14A)

Ref: HU-14A
Ref: DES-20
Ref: DES-62
```

Then run: `/debrief`

---

## 6. Backend phase X.2 — Application layer

```text
Use @backend/.agents/backend-agent.md.
Implement backend phase X.2 for HU-14A in mission-design-service.
Use the service PRD (DES-62) and canonical docs.

Before writing anything, inspect the existing Application trivia baseline and mirror
its conventions for commands, queries, validators, DTOs, handlers, and permissions.

Scope:
- add trivia question authoring commands + handlers + validators:
  AddTriviaQuestion and UpdateTriviaQuestion
- if the current trivia editing baseline already has a clear removal slot, add
  RemoveTriviaQuestion in this slice as part of question management; otherwise keep
  the scope to add/update and note the deferral explicitly in the debrief
- extend trivia detail/query DTOs or projections so question content, options,
  correct-answer marker, score, timer, and optional explanation round-trip coherently
- administrator-only authorization for trivia question mutations
- not-found handling for target quiz/question lookups and edit-blocked rejection when
  the enclosing quiz state no longer permits authoring changes
- implement the mandated Template Method obligation in the application layer by routing
  add/update question validation through one stable workflow instead of forking
  duplicated rule sequences per handler

Gate:
- clean build passes
- handler and validator unit tests cover valid path plus rejection/error branches
- Gate: add/update question validation is enforced through a Template Method style
  workflow with shared sequencing and operation-specific steps, not unrelated handlers
  each owning their own rule order
- application layer does not leak infrastructure concerns

Do not touch Infrastructure, Api, or frontend.
```

Commit:

```text
feat(mission-design): phase X.2 — application layer (HU-14A)

Ref: HU-14A
Ref: DES-20
Ref: DES-62
```

Then run: `/debrief`

---

## 7. Backend phase X.3 — Infrastructure layer

```text
Use @backend/.agents/backend-agent.md.
Implement backend phase X.3 for HU-14A in mission-design-service.
Use the service PRD (DES-62) and canonical docs.

Before writing anything, inspect the current trivia persistence baseline:
- ApplicationDbContext
- existing trivia configuration and repositories from HU-11
- current migration snapshot

Scope:
- persist TriviaQuestion and TriviaOption cleanly as part of the existing TriviaQuiz
  persistence model
- add or extend trivia-side entity configuration, repository implementation, and
  read-model projection support needed for question/option authoring
- create a migration if the snapshot does not already cover the new question/option tables
  or owned-entity shape
- integration tests for add question, update question, and coherent detail read of quiz
  basics plus authored questions/options
- integration proof that 2-to-4 option storage and exactly-one-correct-answer state
  persist as expected

Database isolation: each integration test must clean shared state before its scenario
to avoid cross-test pollution.

Gate:
- dotnet build passes on the service solution
- migration/snapshot is consistent with the new trivia question/option persistence model
- repository integration tests pass for add, update, and detail-read paths

Do not touch Api or frontend.
```

Commit:

```text
feat(mission-design): phase X.3 — infrastructure layer (HU-14A)

Ref: HU-14A
Ref: DES-20
Ref: DES-62
```

Then run: `/debrief`

---

## 8. Backend phase X.4 — API layer

```text
Use @backend/.agents/backend-agent.md.
Implement backend phase X.4 for HU-14A in mission-design-service.
Use the service PRD (DES-62) and canonical docs.

Scope:
- add trivia question-management endpoints under the existing trivia route group,
  aligned to the service's current API style
- endpoint(s) to add and update a question associated with a quiz
- if removal was implemented in X.2/X.3, expose the matching remove endpoint here
- request/response shapes must carry question text, options, correct-answer marker,
  score, timer, and optional explanation coherently
- endpoint proof that non-admin callers receive 403 on mutations
- confirm trivia detail reads return the updated question/option shape after authoring changes

Gate:
- endpoint tests pass for add question, update question, and non-admin rejection
- no regression on the existing trivia quiz endpoints from HU-11 or mission endpoints from HU-09
- service coverage reaches the required threshold for this service

Do not touch frontend.
```

Commit:

```text
feat(mission-design): phase X.4 — api layer (HU-14A)

Ref: HU-14A
Ref: DES-20
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
   - if the question-management endpoints are routed through the gateway in this slice,
     also smoke the relevant gateway route after authentication setup is in place

Output:
- build result
- container status
- curl response summary
```

---

## 9. Frontend slice

```text
Generate a multi phase plan in a markdown file, like the one in
@frontend/plans/hu-03-frontend-role-permission-assignment.md, save it in
@frontend/plans/ for the following:

Use @frontend/AGENTS.md.
Implement the frontend slice for HU-14A on top of the verified trivia-question backend contract.

Scope:
- extend the existing trivia quiz authoring UI so an administrator can add and edit questions
- support 2 to 4 options per question, exactly one correct option, score, timer,
  and optional explanation in the question authoring form
- render persisted question changes back through the existing trivia detail/edit flow
- align route guards and action visibility with the backend authorization contract

Gate:
- frontend uses the verified backend trivia-question contract, not guessed payloads
- unauthorized users cannot access mutation UI
- saved question/option changes are visible in the intended detail/edit flows
- no regression on existing mission-management or HU-11 trivia-quiz frontend behavior
```

Commit:

```text
feat(frontend): trivia question and options management — HU-14A

Ref: HU-14A
Ref: DES-20
Ref: DES-62
```

Then run: `/debrief`

---

## 10. Close-out

```text
Before closing HU-14A, verify every acceptance criterion against the implemented backend and frontend:
- administrator can create questions associated with a quiz
- each question records between 2 and 4 answer options
- each question defines exactly one correct option
- explanation is optional and stored with the question

Then prepare the PR:

gh pr create \
  --title "feat: HU-14A trivia question and options management" \
  --body-file backend/docs/hu14a-context.md

Output:
- acceptance-criteria checklist with pass/fail evidence
- final touched surfaces
- any follow-on work intentionally deferred to HU-14B, HU-12, or HU-13
```

---

## Rationale

HU-14A carries `Template Method` again because trivia question authoring has the same structural risk HU-11 had at the quiz level: the team can easily end up duplicating near-identical validation order across add/update handlers or scattering authoring checks into endpoint/controller conditionals. The mandated pattern keeps one stable question-authoring workflow while allowing operation-specific steps and question-rule checks to vary in the right places.

The main ambiguity is where to draw the line between HU-14A and HU-14B. DES-20 clearly requires question/option authoring, exactly one correct option, optional explanation, and carrying score/timer as authored fields. But DES-62 reserves explicit invalid-question rule enforcement as the next dedicated slice. The safe reading is: HU-14A must introduce coherent question management and the acceptance-level invariants already explicit in the HU, while any deeper numeric range matrix or exhaustive invalid-rule catalog that is still unresolved should be recorded and hardened in HU-14B rather than guessed here.

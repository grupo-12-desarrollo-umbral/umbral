# Prompt Example — HU-13 Duplicación y retiro de quizzes usados (Feature Slice)

Concrete prompt sequence for driving HU-13 through a full feature slice on `feature/hu-13-trivia-quiz-duplication-and-retirement`. Follows the pattern in [workflow_for_prompts.md](./workflow_for_prompts.md).

**Key difference from HU-12:** HU-12 established controlled publish/archive lifecycle transitions on `TriviaQuiz`. HU-13 builds on that baseline to preserve historical identity once a quiz has been used, so reuse happens through duplication into a new authoring copy and withdrawal-from-use happens without destructive deletion of the original record.

When working from the monorepo root, make the target workload explicit in each prompt.
For backend steps, point to `@backend/.agents/backend-agent.md`. For frontend steps,
point to `@frontend/AGENTS.md`. Do not ask for backend and frontend implementation in
the same phase prompt; coordinate them as separate scoped steps tied together by the
verified API contract.

---

## Required design patterns

- `Template Method`
  - Why: duplication and retirement need a stable validation sequence over an existing trivia quiz with operation-specific steps.
  - Phase owner: X.1 Domain and X.2 Application
  - Gate obligation: the stable trivia duplication/retirement validation sequence must be explicit in phase scope and in the gate for both phases; duplicate and retire flows must extend one shared workflow instead of duplicating or scattering used-quiz checks.

---

## Pre-resolved orient (as of 2026-06-03)

> Step 1 has already been run. Paste this section into any agent session that needs context
> before picking up a phase — no need to re-run the orient prompt.

### What predecessors have already landed (reuse candidates for HU-13)

HU-09 (`DES-14`), HU-11 (`DES-17`), HU-14A (`DES-20`), HU-14B (`DES-21`), and HU-12 (`DES-18`) are the same-service predecessors currently tracked in Linear as **Done**. HU-11, HU-14A, and HU-12 are the main documented reuse baseline for this slice.

**Domain layer**

- `Mission` remains the original aggregate root baseline for authoring on the mission side
- `TriviaQuiz` already exists as the trivia-side aggregate root baseline from HU-11
- quiz basics and associated-question shape already live together in the trivia domain model
- question and option authoring already exist from HU-14A
- DES-21 (HU-14B) is already **Done**, so a quiz-validation baseline for option count, correct-answer, score, and timer already landed, but there is no dedicated HU-14B context file; verify exact landed names in source before extending them
- HU-12 already established draft/published/archived lifecycle transitions and source-readiness behavior for trivia quizzes
- HU-11 and HU-12 already introduced the mandated `Template Method` style validation workflows on the trivia side

**Application layer**

- mission-side create/update/deactivate/catalog/detail flows remain the service baseline
- `AuthorizationBehaviour` and `ValidationBehaviour` already exist in the service pipeline
- trivia-side create/update/query use cases and repository/read-model abstractions were introduced in HU-11
- trivia question/option authoring was added in HU-14A
- trivia lifecycle publish/archive commands and projections were added in HU-12

**Infrastructure / API**

- EF Core persistence baseline, repositories, and `/api/missions` endpoints already exist
- trivia-side persistence and draft consultation endpoints were established in HU-11
- trivia-side question/option persistence and API surface were extended in HU-14A
- HU-12 extended persistence and API surfaces for lifecycle transitions and coherent state projection
- `mission-design-service` is already wired into local Docker and the API gateway

**Frontend**

- mission-management UI exists from the earlier service baseline
- trivia-quiz create/edit/detail UI exists from HU-11
- question and option authoring UI exists from HU-14A
- publish/archive UI and lifecycle state feedback exist from HU-12 and should be extended rather than replaced

**Coverage:** no stable aggregate percentage is captured in predecessor docs for `mission-design-service`; verify real service coverage during phase X.4.

### What HU-13 adds on top (per PRD DES-62 and DES-19)

| Concern | New work |
| --- | --- |
| Duplication flow | Duplicate an existing `TriviaQuiz` into a new reusable authoring copy |
| Historical traceability | Preserve the original quiz identity and lineage after duplication rather than overwriting or deleting the source quiz |
| Used-quiz protection | Reject destructive removal of a quiz that has already been used in a session |
| Withdrawal from future use | Route used quizzes toward archive or equivalent retirement semantics without corrupting prior session history |
| Backend contract | Extend the trivia API contract with duplication plus used-quiz retirement/delete-rejection semantics needed by verified consumers |
| Frontend flow | Extend trivia administration UI with duplicate action, retirement feedback, and historical/source-copy clarity |

### Branch state and prerequisite

`feature/hu-13-trivia-quiz-duplication-and-retirement` branches from `develop`.
No same-service predecessor is currently **In Progress**, so there is no feature-branch base dependency to inherit for this slice.

`DES-19` already carries both required labels: `svc:mission-design-service` and `ready-for-agent`.

The service PRD content lives in the local file:
`@backend/docs/prd/DES-62-mission-design-service-baseline.md`

Do not re-fetch PRD scope from Linear. Use the local PRD plus the predecessor context above.

---

## 1. Orient (optional refresh only)

```text
Run this step only if the README, predecessor slices, or Linear state may have changed
since 2026-06-03.

Re-orient HU-13 for mission-design-service.
Use @backend/docs/hu13-context.md, @backend/docs/prd/DES-62-mission-design-service-baseline.md,
and the current DES-19 ticket state.

Output:
- current DES-19 status and labels
- confirmed predecessor branch base
- any drift versus the pre-resolved orient in this prompt
```

---

## 2. Label DES-19

```text
Using Linear MCP, confirm DES-19 still has the label ready-for-agent.
If it is missing, add it and output the resulting label set.
Do not change any other ticket state in this step.
```

---

## 3. Confirm readiness

```text
Confirm DES-19 is ready for implementation.

Checklist:
- DES-19 has `svc:mission-design-service`
- DES-19 has `ready-for-agent`
- DES-62 local PRD file exists at @backend/docs/prd/DES-62-mission-design-service-baseline.md

Output the confirmed HU id, title, acceptance criteria, labels, and resolved PRD reference
before planning the slice.
```

In the remaining examples below, `HU-13` and `DES-19` are the resolved values for this slice.
`DES-62` is the shared PRD reference for `mission-design-service`; its content lives in the
local file above.

---

## 4. Start the slice

```text
Prepare the trivia-quiz-duplication-and-retirement slice on branch
feature/hu-13-trivia-quiz-duplication-and-retirement.
Use the HU id and DES id resolved from Linear in the previous step.
This slice affects backend mission-design-service and frontend.

The pre-resolved orient at the top of this document lists what predecessors already
landed and what HU-13 adds. Do not re-read the PRD for scoping.

Before implementation, confirm the current service source already contains:
- TriviaQuiz aggregate + question/option authoring baseline
- trivia lifecycle publish/archive baseline
- no dedicated duplication/lineage behavior yet unless explicitly discovered

Move DES-19 to In Progress and output the exact scope, branch name, and touched surfaces.
```

---

## 5. Backend phase X.1 — Domain layer

```text
Use @backend/.agents/backend-agent.md.
Implement backend phase X.1 for HU-13 in mission-design-service.
Use the service PRD (DES-62) and canonical docs.

Before writing anything, inspect the current trivia-side domain baseline and the
bounded-context language in @backend/services/mission-design-service/CONTEXT.md.
Extend the existing TriviaQuiz aggregate cleanly inside the current service structure.

Scope:
- add or extend the quiz behavior needed for HU-13: duplication into a new quiz copy
  and non-destructive retirement rules for used quizzes
- preserve historical identity of the original quiz after duplication; the duplicate
  must be a separate authoring artifact, not an in-place mutation of the source quiz
- reject destructive removal of a quiz that has already been used in a session;
  if the source model already exposes archive semantics from HU-12, reuse that boundary
  instead of inventing a parallel lifecycle
- implement the mandated Template Method obligation in the domain:
  one stable trivia duplication/retirement validation workflow with overridable
  duplicate-vs-retire steps and used-quiz/state-sensitive checks
- add domain events or lineage concepts needed for duplication/retirement history,
  if they are not already present in the service model
- if the exact rules for which quiz states may be duplicated remain implicit, record
  the ambiguity in the debrief rather than inventing new state transitions

Gate:
- Domain build passes
- New domain invariants are expressed as unit tests on TriviaQuiz
- Gate: the domain expresses a Template Method style duplication/retirement validation
  workflow, with one stable sequence and overridable operation-specific steps, rather
  than duplicated logic or ad-hoc used-quiz conditionals
- No runtime SessionOperations orchestration or unrelated trivia lifecycle redesign is
  prematurely implemented here

Do not touch other backend layers or frontend.
```

Commit:

```text
feat(mission-design): phase X.1 — domain layer (HU-13)

Ref: HU-13
Ref: DES-19
Ref: DES-62
```

Then run: `/debrief`

---

## 6. Backend phase X.2 — Application layer

```text
Use @backend/.agents/backend-agent.md.
Implement backend phase X.2 for HU-13 in mission-design-service.
Use the service PRD (DES-62) and canonical docs.

Before writing anything, inspect the existing Application trivia baseline and mirror
its conventions for commands, queries, validators, DTOs, handlers, and permissions.

Scope:
- add trivia duplication/retirement commands + handlers + validators for the verified
  operations required by HU-13
- administrator-only authorization for duplication and retirement mutations
- not-found handling for target quiz lookup and state/history-based rejection when a quiz
  cannot be duplicated or destructively removed
- route used-quiz withdrawal through the verified archive/deactivation semantics that
  already exist in the service baseline, rather than introducing a second retirement model
- extend trivia read DTOs or projections as needed so lineage or historical copy/source
  cues required by the verified consumers are visible
- implement the mandated Template Method obligation in the application layer by routing
  duplication/retirement validation through one stable workflow instead of duplicating
  precondition sequences per handler
- handler/unit coverage for: successful duplication, rejection of destructive removal
  for a used quiz, successful retirement path for a used quiz, and not-found/state
  rejection branches

Gate:
- clean build passes
- handler and validator unit tests cover valid paths plus rejection/error branches
- Gate: duplication/retirement validation is enforced through a Template Method style
  workflow with shared sequencing and operation-specific steps, not unrelated handlers
  each owning their own used-quiz rule order
- application layer does not leak infrastructure concerns

Do not touch Infrastructure, Api, or frontend.
```

Commit:

```text
feat(mission-design): phase X.2 — application layer (HU-13)

Ref: HU-13
Ref: DES-19
Ref: DES-62
```

Then run: `/debrief`

---

## 7. Backend phase X.3 — Infrastructure layer

```text
Use @backend/.agents/backend-agent.md.
Implement backend phase X.3 for HU-13 in mission-design-service.
Use the service PRD (DES-62) and canonical docs.

Before writing anything, inspect the current trivia persistence baseline:
- ApplicationDbContext
- existing trivia configuration and repositories from HU-11/HU-14A/HU-12
- current migration snapshot

Scope:
- persist the duplication/lineage and retirement behavior needed for HU-13
- add or extend trivia-side entity configuration, repository implementation, and
  read-model projection support needed for duplicate copies and historical-source tracing
- confirm whether the current snapshot already covers the new lineage or usage-protection
  shape; if not, create the required migration
- integration tests for duplicating a quiz with its reusable structure, rejecting
  destructive removal of a used quiz, and coherent readback of lineage/state
- integration proof that retiring a used quiz preserves the original record and does not
  break historical references already relied on by the service

Database isolation: each integration test must clean shared state before its scenario
to avoid cross-test pollution.

Gate:
- dotnet build passes on the service solution
- migration/snapshot is consistent with the duplication/retirement persistence model
- repository integration tests pass for duplicate, delete-rejected, retirement, and
  lineage/state read paths

Do not touch Api or frontend.
```

Commit:

```text
feat(mission-design): phase X.3 — infrastructure layer (HU-13)

Ref: HU-13
Ref: DES-19
Ref: DES-62
```

Then run: `/debrief`

---

## 8. Backend phase X.4 — API layer

```text
Use @backend/.agents/backend-agent.md.
Implement backend phase X.4 for HU-13 in mission-design-service.
Use the service PRD (DES-62) and canonical docs.

Scope:
- add the verified trivia duplication endpoint under the existing trivia route group
- add or extend the verified used-quiz retirement/delete-rejection API behavior under
  the existing trivia route group; do not invent a destructive delete contract unless
  it already exists and this slice is explicitly extending it
- prove that a used quiz cannot be removed destructively
- prove that duplication returns or exposes the new quiz copy coherently and preserves
  historical identity of the original quiz
- prove that any lineage/source-copy cues required by the verified UI are exposed
- confirm no regression on existing trivia authoring and lifecycle endpoints from
  HU-11, HU-14A, and HU-12

Gate:
- endpoint tests pass for duplication success, used-quiz destructive-removal rejection,
  retirement success on the verified path, and lineage/state projection
- no regression on existing trivia authoring/lifecycle endpoints
- service coverage reaches the enforced repository threshold

Do not touch frontend.
```

Commit:

```text
feat(mission-design): phase X.4 — api layer (HU-13)

Ref: HU-13
Ref: DES-19
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
   - if the duplication/retirement endpoints are routed through the gateway in this slice,
     also smoke the relevant gateway route after authentication setup is in place

Output:
- build result
- container status
- curl response summary
```

---

## 9. Frontend slice

```text
Generate a multi phase plan in a markdown file, like the one in `@frontend/plans/hu-03-frontend-role-permission-assignment.md`, save it in `@frontend/plans/` for the following:
Use @frontend/AGENTS.md.
Implement the frontend slice for HU-13 on top of the verified trivia duplication/retirement backend contract.

Scope:
- extend the existing trivia quiz administration UI with duplicate action and clear
  historical-source vs copied-quiz cues
- surface the verified retirement or delete-rejection behavior for used quizzes
- ensure UI copy and actions distinguish between withdrawing a quiz from future use and
  destroying historical data
- align route guards and action visibility with the backend authorization contract

Gate:
- frontend uses the verified backend duplication/retirement contract, not guessed payloads
- unauthorized users cannot access mutation UI
- duplicate and retirement flows preserve clear original-vs-copy feedback in the intended views
- no regression on existing mission-management or HU-11/HU-14A/HU-12 trivia frontend behavior
```

Commit:

```text
feat(frontend): duplicacion y retiro de quizzes usados — HU-13

Ref: HU-13
Ref: DES-19
Ref: DES-62
```

Then run: `/debrief`

---

## 10. Close-out

```text
Before closing HU-13, confirm:
- duplication creates a separate reusable quiz copy
- used quizzes cannot be removed destructively
- retirement preserves historical traceability of the original quiz
- backend and frontend both use the same verified API contract

Then prepare the PR:

gh pr create --base develop --head feature/hu-13-trivia-quiz-duplication-and-retirement --title "feat: HU-13 duplicacion y retiro de quizzes usados" --body-file backend/docs/pull_request_template.md
```

---

## Rationale

HU-13 keeps the same mandated `Template Method` family as HU-11 and HU-12, but the
reason is different: HU-11 stabilized authoring validation, HU-12 stabilized lifecycle
readiness validation, and HU-13 must stabilize reuse/retirement validation over an
already-authored and potentially already-used quiz. The distinguishing obligation here
is preserving historical identity while branching a new copy and rejecting destructive
removal of the original used artifact.

One scope ambiguity remains explicit by design: DES-19 states that an administrator can
duplicate an existing quiz and that used quizzes cannot be deleted, but it does not fully
specify whether duplication is allowed from every quiz state or only from certain states
such as published or archived. That ambiguity should be resolved from the current landed
source conventions during implementation or recorded in the debrief, not guessed in the
generator output.

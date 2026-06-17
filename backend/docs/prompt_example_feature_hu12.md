# Prompt Example — HU-12 Publicación y archivado de quizzes de trivia (Feature Slice)

> Updated on 2026-06-16 for the mission-runtime restructure. Keep the quiz
> publication lifecycle, but reinterpret "source-ready" as selectable by a
> mission trivia `Substage`. A published `TriviaQuiz` is not a direct
> `SessionSource` and cannot create a `LiveSession`.

Concrete prompt sequence for driving HU-12 through a full feature slice on `feature/hu-12-trivia-quiz-publication-and-archive`. Follows the pattern in [workflow_for_prompts.md](./workflow_for_prompts.md).

**Key difference from HU-14A/HU-14B:** HU-11, HU-14A, and HU-14B established the trivia authoring baseline and question validity rules. HU-12 moves the existing `TriviaQuiz` model into controlled lifecycle transitions so only valid, published quizzes can be selected by mission trivia substages, while draft and archived quizzes remain unavailable for new selections.

When working from the monorepo root, make the target workload explicit in each prompt.
For backend steps, point to `@backend/.agents/backend-agent.md`. For frontend steps,
point to `@frontend/AGENTS.md`. Do not ask for backend and frontend implementation in
the same phase prompt; coordinate them as separate scoped steps tied together by the
verified API contract.

---

## Required design patterns

- `Template Method`
  - Why: publication/archival requires a stable readiness validation pipeline.
  - Phase owner: X.1 Domain and X.2 Application
  - Gate obligation: the stable trivia lifecycle validation sequence must be explicit in phase scope and in the gate for both phases; publish/archive flows must extend one shared workflow instead of duplicating or scattering readiness checks.

---

## Pre-resolved orient (as of 2026-06-01)

> Step 1 has already been run. Paste this section into any agent session that needs context
> before picking up a phase — no need to re-run the orient prompt.

### What predecessors have already landed (reuse candidates for HU-12)

HU-09 (`DES-14`), HU-11 (`DES-17`), HU-14A (`DES-20`), and HU-14B (`DES-21`) are the same-service predecessors currently tracked in Linear as **Done**. HU-11 and HU-14A are the main documented reuse baseline for this slice.

**Domain layer**

- `Mission` remains the original aggregate root baseline for authoring on the mission side
- `TriviaQuiz` already exists as the trivia-side aggregate root baseline from HU-11
- the quiz authoring/editability boundary already exists at the aggregate level
- quiz basics and associated-question shape already live together in the trivia domain model
- HU-11 already introduced the mandated `Template Method` style validation workflow for quiz authoring
- HU-14A extended the trivia domain with question and option authoring
- HU-14A explicitly deferred the deeper invalid-rule matrix to HU-14B; DES-21 is now **Done**, but because there is no dedicated HU-14B context file, verify the exact landed validation baseline in source before extending it

**Application layer**

- mission-side create/update/deactivate/catalog/detail flows remain the service baseline
- `AuthorizationBehaviour` and `ValidationBehaviour` already exist in the service pipeline
- trivia-side create/update/query use cases and repository/read-model abstractions were introduced in HU-11
- trivia question/option authoring was added in HU-14A

**Infrastructure / API**

- EF Core persistence baseline, repositories, and `/api/missions` endpoints already exist
- trivia-side persistence and draft consultation endpoints were established in HU-11
- trivia-side question/option persistence and API surface were extended in HU-14A
- `mission-design-service` is already wired into local Docker and the API gateway

**Frontend**

- mission-management UI exists from the earlier service baseline
- trivia-quiz create/edit/detail UI exists from HU-11
- question and option authoring UI exists from HU-14A and must now surface lifecycle state changes coherently

**Coverage:** no stable aggregate percentage is captured in predecessor docs for `mission-design-service`; verify real service coverage during phase X.4.

### What HU-12 adds on top (per PRD DES-62 and DES-18)

| Concern | New work |
| --- | --- |
| Lifecycle transitions | Publish and archive the existing `TriviaQuiz` aggregate |
| Publishability enforcement | Allow publication only when the quiz satisfies the established validity rules and current-state preconditions |
| Source readiness | Ensure only published quizzes are usable for trivia session creation; draft and archived quizzes are excluded |
| Historical retention | Preserve quiz history while withdrawing archived quizzes from future session selection |
| State projection | Reflect draft/published/archived state in backend reads used by admin and operational views |
| Backend contract | Extend trivia endpoints/DTOs for publish/archive actions and state/readiness projection |
| Frontend flow | Extend the existing trivia administration UI with publish/archive actions and visible lifecycle state feedback |

### Branch state and prerequisite

`feature/hu-12-trivia-quiz-publication-and-archive` should be branched from `develop`. No same-service predecessor is currently **In Progress**, so there is no feature-branch dependency to inherit first.

**Before starting implementation:** inspect the current `mission-design-service` source and confirm that the trivia quiz baseline, question authoring flow, and question-validity rules from HU-11/HU-14A/HU-14B already exist. Extend that baseline for lifecycle transitions rather than creating a parallel publication model or mixing runtime session behavior into this service.

### Linear state (as of 2026-06-01)

- DES-18 (HU-12): **Backlog**, labels: `Feature`, `ready-for-agent`, `svc:mission-design-service`
- DES-21 (HU-14B): **Done**
- DES-20 (HU-14A): **Done**
- DES-17 (HU-11): **Done**
- DES-14 (HU-09): **Done**
- DES-62 (PRD): **Backlog**, labels: `ready-for-agent`

> Linear live state may have changed. Use the Linear MCP to verify DES-18 status and labels if needed, but do not re-fetch PRD scope — read the local file at `@backend/docs/prd/DES-62-mission-design-service-baseline.md` instead.

> Note: the local PRD file and DES-18 relation are authoritative for this slice even though the Linear PRD ticket does not currently carry `svc:mission-design-service`.

---

## 1. Orient — read service state and PRD before planning

> **Skip this step if you have read the pre-resolved orient section above.** Run it only
> if the service README/source or Linear state may have changed since 2026-06-01.

```text
Read the following files and summarise what has been decided and implemented so far:
- @backend/services/mission-design-service/README.md — current service status
- @backend/services/mission-design-service/CONTEXT.md — bounded-context language and pattern expectations
- @backend/docs/prd/DES-62-mission-design-service-baseline.md — the authoritative PRD for HU-09 to HU-14
- @backend/docs/hu11-context.md — the trivia aggregate baseline
- @backend/docs/hu14a-context.md — the documented trivia-question baseline
- @backend/docs/hu12-context.md — the pre-resolved HU-12 context for this slice

Then inspect the existing trivia-side source only enough to confirm the current baseline:
- @backend/services/mission-design-service/src/Domain/
- @backend/services/mission-design-service/src/Application/
- @backend/services/mission-design-service/src/Api/Endpoints/

Then use the Linear MCP to fetch only the current live state of:
- DES-18 (HU-12 — Publicación y archivado de quizzes de trivia) — status and labels
- DES-21 (HU-14B — predecessor validation slice) — status
- DES-20 (HU-14A — predecessor question-authoring slice) — status

Output:
- what same-service trivia baseline already exists from HU-11, HU-14A, and the landed HU-14B work
- what HU-12 adds on top per the PRD: publish/archive lifecycle transitions, published-only source readiness, state projection
- whether lifecycle publish/archive behavior already exists in source or must be introduced cleanly
- current Linear status and labels for DES-18

Do not start planning or implementing yet.
```

---

## 2. Label DES-18 as ready-for-agent

> DES-18 carries `ready-for-agent` as of 2026-06-01. Use this step to confirm the label remains present before execution.

```text
Use the Linear MCP to confirm DES-18 still carries the label ready-for-agent.
If it is missing, add it.
Output the updated DES-18 ticket state and labels.
```

---

## 3. Confirm slice readiness

```text
Use the Linear MCP to confirm DES-18 carries both svc:mission-design-service
and ready-for-agent labels and output its current status and acceptance criteria.

The PRD scope is already in the local file at
@backend/docs/prd/DES-62-mission-design-service-baseline.md —
do not re-fetch the PRD from Linear; read the local file if you need implementation decisions.

Output the confirmed HU id, title, acceptance criteria, and labels before planning the slice.
```

In the remaining examples below, `HU-12` and `DES-18` are the resolved values for this slice. `DES-62` is the shared PRD reference for `mission-design-service`; its content lives in the local file above.

---

## 4. Start the slice

```text
Prepare the trivia-quiz-publication-and-archive slice on branch
feature/hu-12-trivia-quiz-publication-and-archive.
Use the HU id and DES id resolved from Linear in the previous step.
This slice affects backend mission-design-service and frontend.

The pre-resolved orient at the top of this document lists what HU-11, HU-14A,
and the landed HU-14B work already established and what HU-12 adds. Do not re-read
the PRD for scoping.

Before implementation, confirm the current service source already has the trivia quiz
aggregate, question authoring flow, and question-validity baseline from predecessor
slices. Extend that baseline; do not create parallel trivia abstractions or mix in
runtime-owned session orchestration.

Move DES-18 to In Progress and output the exact scope, branch name, and touched surfaces.
```

---

## 5. Backend phase X.1 — Domain layer

```text
Use @backend/.agents/backend-agent.md.
Implement backend phase X.1 for HU-12 in mission-design-service.
Use the service PRD (DES-62) and canonical docs.

Before writing anything, inspect the current trivia-side domain baseline and the
bounded-context language in @backend/services/mission-design-service/CONTEXT.md.
Extend the existing TriviaQuiz aggregate cleanly inside the current service structure.

Scope:
- add or extend the quiz lifecycle behavior needed for HU-12: publish and archive
  transitions on the existing TriviaQuiz aggregate
- allow publication only when the quiz satisfies the established validity baseline
  from predecessor slices and the current quiz state permits publication
- ensure a draft or archived quiz is not source-ready for trivia session creation,
  while a published quiz is
- preserve historical identity when archiving; this is a withdrawal-from-use transition,
  not destructive removal
- implement the mandated Template Method obligation in the domain:
  one stable trivia lifecycle validation workflow with overridable publish-vs-archive
  steps and state-sensitive readiness checks
- add domain events needed for lifecycle transitions, such as TriviaQuizPublished
  and TriviaQuizArchived, if they are not already present
- if the exact source-readiness projection mechanism toward SessionOperations is
  still implicit, record the ambiguity in the debrief rather than inventing runtime scope

Gate:
- Domain build passes
- New lifecycle invariants are expressed as unit tests on TriviaQuiz
- Gate: the domain expresses a Template Method style lifecycle validation workflow,
  with one stable sequence and overridable publish/archive steps, rather than duplicated
  transition logic or ad-hoc readiness conditionals
- No duplication/retirement behavior from HU-13 or runtime session orchestration is
  prematurely implemented here

Do not touch other backend layers or frontend.
```

Commit:

```text
feat(mission-design): phase X.1 — domain layer (HU-12)

Ref: HU-12
Ref: DES-18
Ref: DES-62
```

Then run: `/debrief`

---

## 6. Backend phase X.2 — Application layer

```text
Use @backend/.agents/backend-agent.md.
Implement backend phase X.2 for HU-12 in mission-design-service.
Use the service PRD (DES-62) and canonical docs.

Before writing anything, inspect the existing Application trivia baseline and mirror
its conventions for commands, queries, validators, DTOs, handlers, and permissions.

Scope:
- add trivia lifecycle commands + handlers + validators:
  PublishTriviaQuiz and ArchiveTriviaQuiz
- administrator-only authorization for publish/archive mutations
- not-found handling for target quiz lookup and state-based rejection when a quiz
  cannot be published or archived from its current state
- extend trivia read DTOs or projections as needed so current lifecycle state is
  visible to the consumers required by HU-12
- implement the mandated Template Method obligation in the application layer by
  routing publish/archive validation through one stable workflow instead of forking
  duplicated precondition sequences per handler
- handler/unit coverage for: successful publish, rejected publish on invalid quiz,
  successful archive, rejected archive when state disallows it

Gate:
- clean build passes
- handler and validator unit tests cover valid path plus rejection/error branches
- Gate: publish/archive validation is enforced through a Template Method style workflow
  with shared sequencing and operation-specific steps, not unrelated handlers each
  owning their own readiness rule order
- application layer does not leak infrastructure concerns

Do not touch Infrastructure, Api, or frontend.
```

Commit:

```text
feat(mission-design): phase X.2 — application layer (HU-12)

Ref: HU-12
Ref: DES-18
Ref: DES-62
```

Then run: `/debrief`

---

## 7. Backend phase X.3 — Infrastructure layer

```text
Use @backend/.agents/backend-agent.md.
Implement backend phase X.3 for HU-12 in mission-design-service.
Use the service PRD (DES-62) and canonical docs.

Before writing anything, inspect the current trivia persistence baseline:
- ApplicationDbContext
- existing trivia configuration and repositories from HU-11/HU-14A
- current migration snapshot

Scope:
- persist the trivia quiz lifecycle state transitions needed for publish/archive
- add or extend trivia-side entity configuration, repository implementation, and
  read-model projection support needed for state/readiness persistence
- confirm whether the current snapshot already covers the lifecycle state shape; if not,
  create the required migration
- integration tests for publishing a valid quiz, rejecting publication when the quiz
  is not ready, archiving a quiz, and coherent readback of lifecycle state
- integration proof that archived and draft quizzes remain excluded from the persisted
  source-readiness view used by verified consumers

Database isolation: each integration test must clean shared state before its scenario
to avoid cross-test pollution.

Gate:
- dotnet build passes on the service solution
- migration/snapshot is consistent with the lifecycle persistence model
- repository integration tests pass for publish, publish-rejected, archive, and state-read paths

Do not touch Api or frontend.
```

Commit:

```text
feat(mission-design): phase X.3 — infrastructure layer (HU-12)

Ref: HU-12
Ref: DES-18
Ref: DES-62
```

Then run: `/debrief`

---

## 8. Backend phase X.4 — API layer

```text
Use @backend/.agents/backend-agent.md.
Implement backend phase X.4 for HU-12 in mission-design-service.
Use the service PRD (DES-62) and canonical docs.

Scope:
- add trivia lifecycle endpoints under the existing trivia route group, aligned to the
  current service API style:
  - publish endpoint for administrators
  - archive endpoint for administrators
- prove that a quiz failing readiness rules cannot be published
- prove that lifecycle state is exposed correctly in the quiz reads used by
  administration and operation-facing consumers
- confirm that only published quizzes remain eligible on the verified source-readiness
  contract used for trivia session creation, while draft and archived quizzes do not
- confirm no regression on existing trivia authoring endpoints from HU-11/HU-14A

Gate:
- endpoint tests pass for publish success, publish rejection, archive success, and
  lifecycle state projection
- no regression on existing trivia authoring endpoints
- service coverage reaches the enforced repository threshold

Do not touch frontend.
```

Commit:

```text
feat(mission-design): phase X.4 — api layer (HU-12)

Ref: HU-12
Ref: DES-18
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
   - if the publish/archive endpoints are routed through the gateway in this slice,
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
Implement the frontend slice for HU-12 on top of the verified trivia lifecycle backend contract.

Scope:
- extend the existing trivia quiz administration UI with publish and archive actions
- render visible lifecycle state feedback for draft, published, and archived quizzes
- ensure UI only presents session-eligibility cues that match the verified backend
  readiness contract
- align route guards and action visibility with the backend authorization contract

Gate:
- frontend uses the verified backend trivia lifecycle contract, not guessed payloads
- unauthorized users cannot access mutation UI
- lifecycle state changes are visible in the intended admin/operation views
- no regression on existing mission-management or HU-11/HU-14A trivia frontend behavior
```

Commit:

```text
feat(frontend): trivia quiz publication and archive — HU-12

Ref: HU-12
Ref: DES-18
Ref: DES-62
```

Then run: `/debrief`

---

## 10. Close-out

```text
Before closing HU-12, verify every acceptance criterion against the implemented backend and frontend:
- the system only allows publication of quizzes that satisfy the defined validity rules
- only published quizzes can be used to create trivia sessions
- draft or archived quizzes cannot be used to create new sessions
- the quiz lifecycle state is reflected correctly in administration and operation views

Then prepare the PR:

gh pr create \
  --title "feat: HU-12 trivia quiz publication and archive" \
  --body-file backend/docs/hu12-context.md

Output:
- acceptance-criteria checklist with pass/fail evidence
- final touched surfaces
- any follow-on work intentionally deferred to HU-13 or other later slices
```

---

## Rationale

HU-12 carries `Template Method` again because the main failure mode here is not data modeling but validation drift: one publish path can end up checking readiness in one order, an archive path in another, and API/application handlers can start re-encoding state checks independently. The mandated pattern keeps one stable lifecycle-validation workflow while allowing publish-specific and archive-specific steps to vary in the right places.

The main ambiguity is how the published-only source-readiness fact is surfaced to downstream consumers. `DES-62` is explicit that only published quizzes should feed session creation and that `SessionOperations` consumes readiness facts rather than mutating `MissionDesign` content, but it does not prescribe a single endpoint/projection shape for that fact. The safe reading is: verify the current service contract patterns first, extend the existing trivia reads or source-readiness surface as needed, and do not invent cross-context runtime behavior inside HU-12.

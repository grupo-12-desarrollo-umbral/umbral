# Prompt Example — HU-16 Trivia Session Creation (Feature Slice)

Concrete prompt sequence for driving HU-16 through a full feature slice on `feature/hu-16-trivia-session-creation`. Follows the pattern in [workflow_for_prompts.md](./workflow_for_prompts.md). Context: [hu16-context.md](./hu16-context.md).

**Key difference from HU-07A/07B:** those slices established the `LiveSession` aggregate and the *participant* runtime (admission, reconnect, presence). HU-16 is the first **operator session-creation** slice: it does not touch participants — it selects a **published** trivia quiz, generates a **fixed copy** of it, and opens a `LiveSession` (`Scheduled`) bound to that single quiz source. Because the quiz content lives in `mission-design-service`, HU-16 is dual-service: session-ops orchestrates and owns the session + snapshot; mission-design is read as the source of truth for the published quiz.

Drive each backend phase with `@backend/.agents/driver-agent.md`, selecting phases in order: **X.1 -> X.2 -> X.3 -> X.4**. The driver delegates implementation to `@backend/.agents/backend-agent.md`; do not invoke it directly. For the frontend slice, use Step 9 directly with `@frontend/AGENTS.md`. Do not mix backend and frontend work in the same phase.

---

## ⚠️ Generator preconditions not yet met (resolve before driving)

This prompt was generated as a **draft** while two generator gates were open. Close them before pre-flight:

1. **`DES-23` is in `Backlog` and is missing `ready-for-agent`.** Apply the label and bring readiness through the normal gate (Steps 2–3 below). The driver pre-flight will otherwise hard-stop.
2. **No `session-operations-service` PRD exists.** `<DES-PRD-SESSION-OPS>` is a placeholder throughout this file (same situation as HU-07B). Resolve/author the owning PRD and substitute its DES id in the commit `Ref:` lines, or confirm the team decision to drive session-ops slices from the canonical model docs. Scope here is derived from the **DES-23 acceptance criteria**, the **patterns matrix**, and the **landed code**.

---

## Required design patterns

- `Facade`
  - Why: session creation from a quiz orchestrates source checks, fixed copy, and side effects.
  - Phase owner: **X.2 Application**
  - Gate obligation: a single application orchestration entry point (`CreateTriviaSession` handler / facade) coordinates the subsystems — published-quiz lookup, publication-state assertion, fixed-copy construction, `LiveSession.Create`, persistence, event publication. The endpoint stays thin and delegates to the one facade; these steps are **not** scattered across the endpoint, nor pushed into the domain entity.

**No `Proxy` mandate.** The create endpoint is operator-authorized, but that inherits the existing gateway + `AuthorizationBehaviour`/endpoint-policy guard landed in HU-07B (ADR-0001/0002, applies-where) — **not** a new pattern gate. Reuse the policy plumbing; add an operator policy if one is missing.

Transport note (NOT a pattern, NOT a gate): HU-16 has **no** SignalR/RabbitMQ obligation. It is a synchronous REST creation slice. Live transport begins at HU-21A/22/33A.

---

## Pre-resolved orient (draft skeleton)

> Dated 2026-06-03. Replace placeholder ids after PRD/Linear resolution.

### What has already landed and must be reused

**session-operations-service (HU-07A/07B)**
- `LiveSession.Create(sessionMode, source, sessionCode, titleSnapshot, maximumTimeMinutes, scheduledAt)` — already validates that the source type matches the mode (`Trivia` ⇒ `TriviaQuiz`), requires code/title, sets `State = Scheduled`, raises `LiveSessionCreatedEvent`.
- `SessionSource` value object `(SessionSourceType, Guid SourceEntityId)` — single-source association (AC #3).
- Enums `SessionMode`, `SessionSourceType`, `SessionState`; VOs `MaximumTime`, `TeamCode`.
- Cross-service HTTP client template: `ParticipantMembershipAccessClient` + `IParticipantMembershipAccessClient` + options + DI.
- Application plumbing (MediatR, `AuthorizationBehaviour`, `ValidationBehaviour`, `ICurrentUser`, exceptions), `SessionsEndpoints` (`/api/sessions`), `AuthorizationPolicies`, EF `ApplicationDbContext` + `LiveSessionConfiguration` + `ILiveSessionRepository`.

**mission-design-service (HU-11/12/13/14A/14B)**
- `TriviaQuiz` aggregate keyed by **`int`**, with `Status` (`Draft=0/Published=1/Archived=2`), `IsSourceReady => Published`, `Questions` → `TriviaOption`s.
- `GET /api/trivias/{id:int}` returns full detail incl. `Status` + questions/options — the read contract for the fixed copy and the publication gate. `GET /api/trivias` is the catalog.

### What HU-16 adds

| Concern | New work |
| --- | --- |
| Domain | A fixed, immutable quiz copy owned by `LiveSession` (snapshot of questions/options/correct answer/explanation), attached at creation. HU-07B stored only `TitleSnapshot` + a reference — not the content copy. |
| Application | `CreateTriviaSession` command + handler as the mandated **Facade**; application port `IPublishedTriviaQuizSource`; publication-state assertion (Published only). |
| Infrastructure | HTTP client implementing `IPublishedTriviaQuizSource` against mission-design `GET /api/trivias/{id}`; EF config + migration for snapshot tables; repository persistence. |
| API | Operator-authorized thin `POST /api/sessions` (trivia) delegating to the facade. |

### Branch state and prerequisite

`feature/hu-16-trivia-session-creation` branches from **`develop`** (HU-07B merged via PR #17; predecessors HU-11/12/14A/14B Done).

### Linear state

- HU ticket: `DES-23` — **Backlog**, labels `svc:session-operations-service`, `svc:mission-design-service`, `Feature`; **`ready-for-agent` missing** (resolve in Step 2).
- PRD ref: `<DES-PRD-SESSION-OPS>` — unresolved; mission-design supporting PRD `DES-62`.

---

## 1. Orient — read current service state

> Skip if you have read the pre-resolved orient above and the local docs are unchanged.

```text
Read the following and summarise what is already decided:
- @backend/services/session-operations-service/README.md
- @backend/services/session-operations-service/CONTEXT.md
- @backend/services/session-operations-service/src/Domain/Entities/LiveSession.cs
- @backend/services/session-operations-service/src/Domain/ValueObjects/SessionSource.cs
- @backend/services/mission-design-service/src/Domain/Entities/TriviaQuiz.cs
- @backend/services/mission-design-service/src/Api/Endpoints/TriviasEndpoints.cs
- @backend/docs/ddd_solution_model.md
- @backend/docs/hu16-context.md

Then use the Linear MCP to fetch the current live state of DES-23 (status + labels)
and the owning session-operations PRD ticket (if one now exists).

Output:
- what LiveSession.Create / SessionSource already give HU-16 for free
- what the fixed quiz copy must add over the existing TitleSnapshot
- the resolved HU id, PRD id (or confirmed absence), status, and labels

Do not start planning or implementing yet.
```

---

## 2. Label the HU ticket as ready-for-agent

```text
Use the Linear MCP to add the label ready-for-agent on DES-23 (it currently only
carries svc:session-operations-service, svc:mission-design-service, Feature).
Confirm the label was applied and output the updated ticket state.
```

---

## 3. Confirm slice readiness

```text
Use the Linear MCP to confirm DES-23 now carries svc:session-operations-service
and ready-for-agent, and output its current status and acceptance criteria.

Resolve the owning session-operations PRD ticket and local file in
@backend/docs/prd/. If none exists, record that explicitly and proceed from the
DES-23 acceptance criteria + patterns matrix + landed code (per hu16-context.md).

Output the confirmed HU id, title, acceptance criteria, labels, and PRD ref
before planning the slice.
```

In the remaining steps, `<DES-PRD-SESSION-OPS>` is a placeholder until resolved.

---

## 4. Start the slice

```text
Prepare the trivia session creation slice on branch
feature/hu-16-trivia-session-creation, based on develop.
Use the HU id (DES-23) and PRD id resolved in the previous step.
This slice affects session-operations-service, mission-design-service (read-only
consumer contract), and the frontend operator UI.

Before implementation, confirm the baseline:
- LiveSession + LiveSession.Create already exist (HU-07B); reuse them
- mission-design exposes GET /api/trivias/{id} with Status + full content
- the fixed copy (AC #2) is NEW domain work, not the existing TitleSnapshot

Move DES-23 to In Progress and output the exact scope, branch name, and touched
surfaces.
```

---

## 5. Backend phase X.1 — Domain layer

> Run `@backend/.agents/driver-agent.md` and select **X.1** from the phase menu.

```text
Use @backend/.agents/backend-agent.md.
Implement backend phase X.1 for HU-16 in session-operations-service.

Before writing anything, inspect the existing domain (LiveSession, SessionSource,
SessionMode, SessionState, MaximumTime) and extend rather than recreate.

Scope:
- model the fixed, immutable quiz copy owned by LiveSession: a snapshot of the
  quiz title, questions, options, correct option(s), and explanation, captured at
  creation (e.g. TriviaSnapshot/SessionQuiz aggregate-internal entity + child
  value objects). Once created it must be immutable.
- extend/overload LiveSession creation (or add a CreateTrivia factory) to attach
  the snapshot atomically with the session, keeping SessionSource as the single
  source-quiz association (AC #3) and preserving ValidateSourceForMode.
- reconcile the source-quiz id type: mission-design quiz id is int, SessionSource.
  SourceEntityId is Guid — decide and encode the mapping explicitly; do not coerce.
- a session cannot be created without a non-empty snapshot (at least one question).

Gate:
- Domain build passes
- existing LiveSession creation/lifecycle invariants are not broken
- new invariants are unit-tested: snapshot immutability, snapshot-required-on-create,
  source/mode match, empty-snapshot rejection

Do not touch other backend layers or frontend.
```

Commit:

```text
feat(session-operations): phase X.1 — domain layer (HU-16)

Ref: HU-16
Ref: DES-23
Ref: <DES-PRD-SESSION-OPS>
```

---

## 6. Backend phase X.2 — Application layer

> Run `@backend/.agents/driver-agent.md` and select **X.2** from the phase menu.

```text
Use @backend/.agents/backend-agent.md.
Implement backend phase X.2 for HU-16 in session-operations-service.

Before writing anything, inspect the existing Application baseline (the
ReconnectAuthenticatedParticipant command/handler/validator/DTOs) and mirror its
conventions.

Scope:
- add the CreateTriviaSession command + handler as the mandated Facade: a single
  orchestration entry point that (1) looks up the published quiz via a new port,
  (2) asserts the quiz is Published — rejecting Draft/Archived (AC #1, #4),
  (3) builds the fixed copy from the quiz content (AC #2), (4) calls LiveSession
  creation bound to the single source quiz (AC #3), (5) persists, (6) lets
  LiveSessionCreatedEvent publish. No orchestration leaks into the endpoint or the
  domain entity.
- add an application port IPublishedTriviaQuizSource that returns the quiz status +
  full content by source id (Infrastructure implements it in X.3).
- add a command validator for required inputs (source quiz id, title, max time,
  scheduled-at).
- Facade obligation (mandated): the create flow is one cohesive orchestrator; the
  publication-state gate and snapshot construction live here, not in the endpoint.
- handler unit tests: published quiz -> session created with snapshot; draft quiz
  -> rejected; archived quiz -> rejected; missing/unknown quiz -> NotFound; invalid
  inputs -> validation error.

Gate:
- clean build passes
- handler + validator unit tests pass for all paths and rejection branches
- Facade gate: orchestration is a single entry point; source check, snapshot, and
  side effects are coordinated there and not duplicated in the endpoint or domain

Do not touch Infrastructure, Api, or frontend.
```

Commit:

```text
feat(session-operations): phase X.2 — application layer (HU-16)

Ref: HU-16
Ref: DES-23
Ref: <DES-PRD-SESSION-OPS>
```

---

## 7. Backend phase X.3 — Infrastructure layer

> Run `@backend/.agents/driver-agent.md` and select **X.3** from the phase menu.

```text
Use @backend/.agents/backend-agent.md.
Implement backend phase X.3 for HU-16 in session-operations-service.

Scope:
- implement IPublishedTriviaQuizSource as a typed HTTP client to mission-design
  GET /api/trivias/{id}, returning Status + full questions/options/explanation.
  Mirror ParticipantMembershipAccessClient (client + options + DI registration).
  Map a non-Published or missing quiz to the result the handler expects (so the
  publication gate stays in the Application facade, not the client).
- add EF configuration + a migration for the fixed-copy snapshot tables owned by
  LiveSession; review the generated migration for leaked audit columns
  (created_by/updated_by) and Ignore them per the base-entity convention.
- extend the LiveSession repository so a session persists together with its
  snapshot in one transaction.
- integration tests must prove: a published quiz round-trips into a persisted
  session + snapshot; the client correctly surfaces draft/archived/missing.

Gate:
- migration succeeds (or confirmed no-op against snapshot)
- repository/client integration tests pass against real PostgreSQL via Testcontainers
- mission-design client integration is covered (success + non-published + not-found)

Do not touch Api or frontend.
```

Commit:

```text
feat(session-operations): phase X.3 — infrastructure layer (HU-16)

Ref: HU-16
Ref: DES-23
Ref: <DES-PRD-SESSION-OPS>
```

---

## 8. Backend phase X.4 — API layer

> Run `@backend/.agents/driver-agent.md` and select **X.4** from the phase menu.

```text
Use @backend/.agents/backend-agent.md.
Implement backend phase X.4 for HU-16 in session-operations-service.
Follow @backend/.claude/skills/aspnet-backend-testing/ for test type and layer placement.

Scope:
- add POST /api/sessions (trivia creation) to the existing SessionsEndpoints group,
  operator-authorized via the existing AuthorizationPolicies (add an Operator policy
  if absent). Keep it thin: bind the request, send the CreateTriviaSession command,
  return 201 Created with the new live session id + summary.
- no business logic in the endpoint; the facade owns orchestration.
- integration tests through the service host: a published quiz -> 201 with a created
  session bound to the source quiz and a populated fixed copy; a draft/archived quiz
  -> rejected with the correct status (e.g. 409/422); an unauthorized caller -> 401/403.

Gate:
- endpoint integration tests run through the service host, not only handler unit tests
- published-success and draft/archived-rejection and unauthorized paths are correct
- service coverage reaches the enforced threshold (>= 93%, ADR-0005)

Do not touch frontend.
```

Commit:

```text
feat(session-operations): phase X.4 — api layer (HU-16)

Ref: HU-16
Ref: DES-23
Ref: <DES-PRD-SESSION-OPS>
```

---

## 8.5. Docker rebuild + smoke

```text
From backend/, rebuild and start the stack for manual verification:

1. docker compose build session-operations-service && docker compose up -d session-operations-service
2. docker compose build api-gateway && docker compose up -d api-gateway
3. Ensure a Published trivia quiz exists in mission-design (publish one if needed).
4. Create a session from that published quiz:
   curl -i -X POST http://localhost:<gateway-port>/api/sessions \
     -H "Content-Type: application/json" \
     -H "X-User-Id: <operatorId>" -H "X-User-Role: Operator" -H "X-User-Email: op@umbral.test" \
     -d '{"sourceTriviaQuizId": <publishedQuizId>, "title": "Smoke Trivia", "maximumTimeMinutes": 10, "scheduledAt": "2026-06-04T15:00:00Z"}'
   Expect 201 Created; the response references a session bound to the source quiz
   with a populated fixed copy.
5. Attempt the same with a Draft or Archived quiz id -> expect rejection (4xx),
   not a created session.
```

**Gate:** the create path is reachable in the running stack; a published quiz yields a created session with a fixed copy, and a draft/archived quiz is rejected.

---

## 9. Frontend slice

```text
Generate a multi phase plan in a markdown file, like the one in
@frontend/plans/hu-03-frontend-role-permission-assignment.md, save it in
@frontend/plans/ for the following:
Use @frontend/AGENTS.md.

Implement the operator "create trivia session" flow for HU-16 using the verified
session-operations contract (POST /api/sessions) and the mission-design catalog.

Scope:
- present only PUBLISHED quizzes for selection (AC #1): load the mission-design
  trivia catalog (GET /api/trivias) and filter/show Published only; do not offer
  Draft or Archived quizzes.
- let the operator pick one published quiz, enter session title, maximum time, and
  scheduled-at, and submit creation to POST /api/sessions.
- on success, show the created session (id/title/state Scheduled) and that it is
  bound to the single source quiz with a fixed copy.
- surface the backend rejection cleanly if the chosen quiz is no longer Published
  by creation time (server-side gate is authoritative, AC #4).

Gate:
- the UI only ever submits a Published quiz, but treats the server publication gate
  as authoritative (handles 4xx rejection without a broken screen)
- a created session is rendered with its source-quiz binding
- no Draft/Archived quiz is selectable
```

Commit:

```text
feat(frontend): trivia session creation — HU-16

Ref: HU-16
Ref: DES-23
Ref: <DES-PRD-SESSION-OPS>
```

---

## 10. Close-out

```text
Before opening the PR:
- confirm all four backend phase commits exist
- confirm the create smoke path was exercised (published -> 201, draft/archived -> 4xx)
- confirm the frontend flow only offers Published quizzes and uses POST /api/sessions
- map each acceptance criterion to where it is enforced:
  AC#1 only-published-selectable -> catalog filter + server gate
  AC#2 fixed copy -> domain snapshot built in the facade
  AC#3 single source quiz -> SessionSource binding
  AC#4 reject draft/archived -> publication-state assertion in the facade (X.2)

Then open the PR:
gh pr create --draft --base develop \
  --title "feat: trivia session creation — HU-16" \
  --body "Closes DES-23
Ref: <DES-PRD-SESSION-OPS>

Touched: backend/services/session-operations-service/, backend/services/mission-design-service/ (read contract), frontend/"
```

## Rationale

**HU-16 is the seam between mission-design and session-operations.** HU-07A/07B
built the participant runtime on top of `LiveSession`; HU-16 builds the *operator*
entry that brings a `LiveSession` into existence from a published quiz. The one
piece that looks done but isn't is the "copia fija" (AC #2): HU-07B persisted only
a `TitleSnapshot` and a `SessionSource` reference, so the full content copy —
questions, options, correct answers, explanations — is genuinely new domain work
and is what the downstream spine (HU-33A/34A/35/37A) consumes. The mandated
`Facade` is the natural home for the create orchestration because the flow spans
two subsystems (mission-design read + session-ops write) plus a publication gate
and a side-effecting event; collapsing that into the endpoint or the entity is the
failure mode the pattern exists to prevent.

**Two open gates (top of file)** — `DES-23` not yet `ready-for-agent`, and no
session-ops PRD — were noted rather than papered over. If the team confirms
session-ops slices are driven from the canonical model docs (as HU-07B effectively
was), drop the `<DES-PRD-SESSION-OPS>` placeholder accordingly; otherwise resolve
the PRD before the driver runs.

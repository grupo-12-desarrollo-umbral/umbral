# Prompt Example - HU-15 Creacion de sesion de mision desde mision activa (Feature Slice)

Concrete prompt sequence for driving DES-22 (HU-15) through a full feature slice on `feature/hu-15-session-creation-from-mission`. Follows the pattern in [workflow_for_prompts.md](./workflow_for_prompts.md).

**Key difference for the DES-22 rebuild:** the old session-creation baseline (HU-07A/07B) created a `LiveSession` **from a published trivia quiz** — `SessionSource` carried a `TriviaQuiz` member, the session froze a `TriviaSessionSnapshot`, and a session-level `SessionMode` distinguished trivia vs treasure-hunt. After the mission-runtime rewrite this is stale: `Mission` is the **only** `SessionSource`, play mode is per-`Substage`, and the session must freeze an immutable **`MissionRuntimeSnapshot`** of the full mission runtime plan at creation. This slice rebuilds session creation around an active, runtime-ready `Mission`. The `LiveSession` aggregate itself and its State machine (HU-07A/07B, HU-21A) are **not** rebuilt — only the mission-based creation path + snapshot are added, and the quiz-as-source model is torn out.

When working from the monorepo root, make the target workload explicit in each prompt.
For backend steps, point to `@backend/.agents/backend-agent.md`. For frontend steps,
point to `@frontend/AGENTS.md`. Do not ask for backend and frontend implementation in
the same phase prompt; coordinate them as separate scoped steps tied together by the
verified API contract.

---

## Stop 1 acceptance guard

Reject this generated prompt before implementation if it does not explicitly scope all of the following:

- `Mission` is the only `SessionSource`; `TriviaQuiz` is **not** a `SessionSource`
- session creation freezes an immutable `MissionRuntimeSnapshot` of the full mission runtime plan
- `LiveSession.Create(...)` is mission-based and sets initial state `Scheduled`
- no session-level `SessionMode` in the creation path (play mode is per-`Substage` inside the snapshot)
- session creation orchestration lives behind a single Application-layer `Facade`
- the `LiveSession` aggregate + State machine are **not** rebuilt (HU-07A/07B/HU-21A own them)
- mission-design exposes a new `GET /api/missions/{id}/runtime-plan` resolving full trivia content (Phase 0 prerequisite, lands before X.3)
- `POST /api/sessions` stays `Administrator`-guarded, `201 Created`, request reshaped to mission-only

---

## Required design patterns

- `Facade`
  - Why: session creation is a multi-subsystem orchestration (readiness gate, mission-runtime fetch, snapshot build, aggregate construction, persistence) that must live behind one Application-layer entry point, not an ad-hoc handler.
  - Phase owner: X.2 Application.
  - Gate obligation: a single Application-layer `Facade` orchestrating `readiness gate (IMissionReadinessSource) → fetch mission runtime content (IMissionRuntimeSource) → build MissionRuntimeSnapshot → LiveSession.Create → persist (ILiveSessionRepository)`; the command handler stays a pass-through to the facade — no scattered orchestration.

> Resolution note: HU-15 is **not** a row in `backend/docs/trivia_sprint_required_patterns_matrix.md`. The matrix maps **HU-16** ("Session creation from a quiz") to `Facade` (`:70`), and the realignment **supersedes HU-16** (DES-75 turns trivia selection into a `Substage`), moving session creation to HU-15/HU-17 — so the `Facade` mandate transfers HU-16 → HU-15. This is canon-backed: `backend/docs/adr/0004-required-domain-patterns.md` line 3 ("`Facade` for session orchestration and event publication in `SessionOperations`") + PRD DES-70 lines 195-197. The existing `CreateTriviaSessionFacade` is the predecessor this rebuild continues.
>
> Applies-where note (no new gate): `POST /api/sessions` is a protected mutation, but HU-15 is not matrix-tagged for `Proxy` and not in the applies-where set (HU-04/05/36B) — it inherits the standard `Administrator` `AuthorizationBehaviour`/gateway guard (ADR-0001/0002). The `LiveSession` lifecycle `State` pattern is HU-21A's scope (DES-76); HU-15 only sets the initial `Scheduled` state.

---

## Pre-resolved orient (as of 2026-06-21)

> Step 1 has already been run. Paste this section into any agent session that needs context
> before picking up a phase - no need to re-run the orient prompt.

### What predecessors have already landed

DES-22 (HU-15) is **Todo**, a `needs-rebuild` foundation row in the realignment map (phase #2 "Session creation"). Its dependencies are Done: **DES-14 (HU-09)** and **DES-15 (HU-10A)** rebuilt the mission-design model — `Mission` wrapper over a `MissionNode` Composite (`Stage`/`Substage`/`Clue`), `SubstagePlayMode`, `Target`-based treasure hunt, optional `Clue`, `TriviaQuizSelection`, and `MissionActivationPolicy` readiness. That full runtime plan (incl. resolved trivia content) is what HU-15 freezes into its snapshot.

**HU-07A/07B** (session-operations-service) landed the `LiveSession` aggregate root, EF persistence, the session State machine, and participant/team runtime. HU-15 builds on that aggregate — but the existing **source/snapshot** parts (`SessionSource` with a `TriviaQuiz` member, `TriviaSessionSnapshot`, `CreateTrivia` factory, the published-quiz integration) were built under the old quiz-as-source model and are exactly what this slice tears out.

Superseded and excluded from the predecessor set: HU-16 (DES-23→DES-75), HU-21A (DES-28→DES-76), HU-22 (DES-30→DES-77), HU-33A (DES-44→DES-78). HU-18/19/20 (team/operator association + reads) build on creation, not the reverse.

There are no same-service In Progress predecessors, so the branch base is `develop`.

**Domain layer**

- `LiveSession` is the aggregate root (HU-07A/07B); HU-15 adds a mission-based `Create` path, not a new aggregate.
- `Mission` is the **only** `SessionSource`; `TriviaQuiz` is not a `SessionSource`.
- `LiveSession` freezes an immutable `MissionRuntimeSnapshot` (net-new — grep = 0) of the full runtime plan at creation: ordered stages/substages, treasure targets + clues, resolved trivia questions/options/correct answers/timers/scores.
- Initial state at creation = `Scheduled`. There is no session-level `SessionMode`.

**Application layer**

- Creation orchestration must be a mission-based `Facade` (continuation of `CreateTriviaSessionFacade`).
- `IMissionReadinessSource` (active + runtime-ready gate) is reusable; what is missing is a read port for the mission **content** to snapshot.

**Infrastructure / API**

- EF persistence, `LiveSessionRepository`, and `POST /api/sessions` (`Administrator`, 201) exist from HU-07A/07B.
- Persistence shape changes: drop the trivia-snapshot tables + `source_trivia_quiz_id`, add the `MissionRuntimeSnapshot` owned graph.

**Frontend**

- Session-creation UI selects a source; this slice reshapes it to select an active, runtime-ready `Mission` and surface eligibility/readiness rejections.

**Coverage:** session-operations-service carries the HU-07A/07B baseline; verify the real service percentage against the ADR-0005 repo gate at phase X.4.

### What HU-15 rebuild adds on top (per DES-22, DES-70, and the realignment map)

| Concern | New work |
|---|---|
| Mission-only source | Reshape `SessionSource` to one `Mission`; drop `TriviaQuiz` member + `SessionSourceType.TriviaQuiz`. |
| Runtime snapshot | Net-new immutable `MissionRuntimeSnapshot` owned by `LiveSession`, frozen at creation. |
| Mission factory | `LiveSession.Create(...)` mission-based; owns snapshot; sets `Scheduled`; keeps `LiveSessionCreatedEvent`. |
| Creation `Facade` | Mission-based orchestration: readiness gate → runtime fetch → snapshot build → `Create` → persist. |
| Runtime read port | New `IMissionRuntimeSource` + HTTP adapter resolving the full runtime plan incl. trivia content. |
| Persistence | EF mapping + migration for the snapshot owned graph; drop trivia-snapshot tables + `source_trivia_quiz_id`. |
| API contract | Keep `POST /api/sessions` (`Administrator`, 201); mission-only request; map eligibility exception to 409/422. |
| Frontend flow | Mission-selection session-creation form with eligibility/readiness feedback. |

### Branch state and prerequisite

`feature/hu-15-session-creation-from-mission` should be branched from `develop`. No same-service predecessor is currently **In Progress**, so there is no feature-branch dependency to inherit first.

**Before starting implementation:** inspect the current session-creation source (`SessionSource`, `LiveSession.CreateTrivia`, `TriviaSessionSnapshot`, `CreateTriviaSession*`, `PublishedTriviaQuizSource`) and identify the quiz-as-source paths to delete or reconcile. Treat DES-22 as a rebuild: tear out the quiz-as-source model; do not layer a mission path beside it.

### Linear state (as of 2026-06-21)

- DES-22 (HU-15): **Todo**, labels: `canon-realign`, `needs-rebuild`, `ready-for-agent`, `svc:session-operations-service`, `svc:mission-design-service`, `Feature`
- DES-70 (PRD): authoritative local file; do not re-fetch PRD scope from Linear
- Dependencies: DES-14 (HU-09) **Done**, DES-15 (HU-10A) **Done**
- Same-service In Progress issues: none
- Superseded: DES-23 (→DES-75), DES-28 (→DES-76), DES-30 (→DES-77), DES-44 (→DES-78) — excluded from predecessor set

> Linear live state may have changed. Use the Linear MCP to verify DES-22 status and labels if needed, but do not re-fetch PRD scope - read the local file at `@backend/docs/prd/DES-70-primera-implementacion-de-session-operations-service-hu-15-a-hu-36.md` and overlay `@backend/docs/canon-realignment-after-mission-runtime-rewrite.md`.

---

## 1. Orient - read service state, PRD, and realignment overlay

> **Skip this step if you have read the pre-resolved orient section above.** Run it only
> if the service source, README, or Linear state may have changed since 2026-06-21.

```text
Read the following files and summarise what has been decided and implemented so far:
- @backend/services/session-operations-service/README.md - current service status
- @backend/services/session-operations-service/CONTEXT.md - bounded-context language and pattern expectations
- @backend/docs/prd/DES-70-primera-implementacion-de-session-operations-service-hu-15-a-hu-36.md - PRD for HU-15 to HU-36
- @backend/docs/canon-realignment-after-mission-runtime-rewrite.md - realignment map; it supersedes stale DES-22 body AC
- @backend/docs/hu15-context.md - the pre-resolved HU-15 rebuild context

Then inspect the existing session-creation source only enough to identify stale or reusable baseline code:
- @backend/services/session-operations-service/src/Domain/Entities/LiveSession.cs
- @backend/services/session-operations-service/src/Domain/ValueObjects/SessionSource.cs
- @backend/services/session-operations-service/src/Application/Sessions/Commands/CreateTriviaSession/
- @backend/services/session-operations-service/src/Infrastructure/Integrations/MissionDesign/
- @backend/services/session-operations-service/src/Api/Endpoints/SessionsEndpoints.cs

Then use the Linear MCP to fetch only the current live state of:
- DES-22 (HU-15 - Creacion de sesion de mision) - status and labels
- DES-14 (HU-09) and DES-15 (HU-10A) - confirm Done

Output:
- which current session-creation concepts are stale and must be rebuilt (quiz-as-source, SessionMode, TriviaSessionSnapshot)
- the canonical rebuilt scope: Mission-only SessionSource, immutable MissionRuntimeSnapshot, mission-based LiveSession.Create, creation Facade
- confirmation that the LiveSession aggregate + State machine are NOT rebuilt
- confirmation that mission-design needs a new GET /api/missions/{id}/runtime-plan endpoint (Step 4.5 / Phase 0) — existing /api/missions/{id} returns only a TriviaQuizId reference, not resolved trivia content
- current Linear status and labels for DES-22

Do not start planning or implementing yet.
```

---

## 2. Label DES-22 as ready-for-agent

> DES-22 already carries `ready-for-agent` as of 2026-06-21. Use this step to confirm the label remains present before execution.

```text
Use the Linear MCP to confirm DES-22 still carries the label ready-for-agent.
If it is missing, add it.
Output the updated DES-22 ticket state and labels, including canon-realign and needs-rebuild.
```

---

## 3. Confirm slice readiness

```text
Use the Linear MCP to confirm DES-22 carries both svc:session-operations-service
and ready-for-agent labels and output its current status and acceptance criteria.

The PRD scope is already in the local file at
@backend/docs/prd/DES-70-primera-implementacion-de-session-operations-service-hu-15-a-hu-36.md.
The realignment overlay is in
@backend/docs/canon-realignment-after-mission-runtime-rewrite.md.
Do not re-fetch PRD scope from Linear; read local files if you need implementation decisions.

Before planning, explicitly confirm the Stop 1 acceptance guard:
- Mission is the only SessionSource; TriviaQuiz is not a SessionSource
- session creation freezes an immutable MissionRuntimeSnapshot of the full mission runtime plan
- LiveSession.Create(...) is mission-based and sets initial state Scheduled
- no session-level SessionMode in the creation path
- session creation orchestration lives behind a single Application-layer Facade
- the LiveSession aggregate + State machine are NOT rebuilt (HU-07A/07B/HU-21A own them)
- POST /api/sessions stays Administrator-guarded, 201 Created, request reshaped to mission-only

Output the confirmed HU id, title, acceptance criteria, labels, and the guard confirmation before planning the slice.
```

In the remaining examples below, `HU-15` and `DES-22` are the resolved values for this slice. `DES-70` is the shared PRD reference for `session-operations-service`; its content lives in the local file above and is overlaid by the canon realignment document.

---

## 4. Start the slice

```text
Prepare the session-creation-from-mission rebuild slice on branch feature/hu-15-session-creation-from-mission.
Use the HU id and DES id resolved from Linear in the previous step.
This slice affects backend session-operations-service (owner) and frontend; mission-design-service is a read seam only.

The pre-resolved orient at the top of this document lists what existing code has landed
(HU-07A/07B session aggregate, HU-09/10A mission model) and what the DES-22 rebuild adds.
Do not re-read the PRD for scoping unless you need to resolve a precise implementation detail.

Before implementation, inspect whether the current session-creation source contradicts the
realigned canon. Treat DES-22 as a rebuild: delete, replace, or reconcile the quiz-as-source
paths (SessionSource TriviaQuiz member, TriviaSessionSnapshot, CreateTrivia factory,
PublishedTriviaQuizSource) instead of layering a mission path beside them. Do NOT rebuild the
LiveSession aggregate or its State machine — those are HU-07A/07B/HU-21A scope.

Move DES-22 to In Progress, and output the exact scope, branch name, base branch, and touched surfaces.
```

---

## 4.5. Backend prerequisite (mission-design) - runtime-plan read endpoint

> Cross-service prerequisite, covered by the `svc:mission-design-service` label on DES-22.
> It must land **before** phase X.3 (session-operations X.3 consumes it). It does not touch
> session-operations and is independent of X.1/X.2 — land it first.

```text
Use @backend/.agents/backend-agent.md.
Implement the mission-design runtime-plan read endpoint for HU-15 in mission-design-service,
per the **Phase 0 derivation block in @backend/docs/hu15-context.md** (your spec — do not
re-read the canon or re-inspect the tree; open a cited canon section only to fill a gap the
block leaves open).

Gate:
- new GET /api/missions/{id}/runtime-plan returns the resolved runtime plan: mission title + maximumTime, ordered stages -> substages (play mode, winner score), TH targets + optional clue, and per Trivia substage the selected published quiz's RESOLVED questions (prompt, options + isCorrect, scoreValue, timeLimitSeconds) in strict mission order
- query/endpoint integration test covers a ready mission with both TreasureHunt and Trivia substages
- resolves TriviaQuizSelection.TriviaQuizId -> TriviaQuiz questions (the join /api/missions/{id} omits); no N+1 left for the consumer
- read-only: no mutation of mission authoring content
- mission-design coverage reaches the repo gate target (ADR-0005)

Do not touch session-operations-service or frontend.
```

Commit:

```text
feat(mission-design): mission runtime-plan read endpoint (HU-15)

Ref: HU-15
Ref: DES-22
Ref: DES-70
```

---

## 5. Backend phase X.1 - Domain layer

```text
Use @backend/.agents/backend-agent.md.
Implement backend phase X.1 for HU-15 in session-operations-service, per the
**X.1 derivation block in @backend/docs/hu15-context.md** (your spec — including its
keep/delete/decide classification — do not re-read the canon or re-inspect the tree;
open a cited canon section only to fill a gap the block leaves open).

Gate:
- Domain build passes; unit test per new domain type (MissionRuntimeSnapshot + stage/substage/target snapshot VOs, mission-based LiveSession.Create)
- MissionRuntimeSnapshot is immutable; ordering + TH/trivia substage invariants enforced
- LiveSession.Create sets initial state Scheduled and rejects a non-Mission source
- Mission is the only SessionSource; TriviaQuiz is not a SessionSource; no session-level SessionMode in the creation path
- quiz-as-source domain code (TriviaSessionSnapshot, SessionSourceTriviaQuizIdRequiredException) deleted or reconciled per keep/delete/decide
- the LiveSession aggregate and its State machine are not rebuilt

Do not touch other backend layers or frontend.
```

Commit:

```text
feat(session-operations): phase X.1 - domain layer (HU-15)

Ref: HU-15
Ref: DES-22
Ref: DES-70
```

---

## 6. Backend phase X.2 - Application layer

```text
Use @backend/.agents/backend-agent.md.
Implement backend phase X.2 for HU-15 in session-operations-service, per the
**X.2 derivation block in @backend/docs/hu15-context.md** (your spec — including its
keep/delete/decide classification — do not re-read the canon or re-inspect the tree;
open a cited canon section only to fill a gap the block leaves open).

Gate:
- clean build passes; facade unit tests cover the happy path plus reject inactive / not-ready mission, and validator tests pass
- session creation orchestration lives behind a single Application-layer Facade (readiness gate -> IMissionRuntimeSource fetch -> build MissionRuntimeSnapshot -> LiveSession.Create -> persist); the command handler is a pass-through, no scattered orchestration
- CreateSessionCommand/result DTO carry no SourceTriviaQuizId or quiz fields; no session-level SessionMode
- quiz-as-source application code (IPublishedTriviaQuizSource, PublishedTriviaQuizDto, CreateTriviaSession*) deleted or reconciled per keep/delete/decide
- application layer does not leak infrastructure concerns

Gate (pattern): session creation orchestrated through a single `Facade` entry point — no ad-hoc handler orchestration.

Do not touch Infrastructure, Api, or frontend.
```

Commit:

```text
feat(session-operations): phase X.2 - application layer (HU-15)

Ref: HU-15
Ref: DES-22
Ref: DES-70
```

---

## 7. Backend phase X.3 - Infrastructure layer

```text
Use @backend/.agents/backend-agent.md.
Implement backend phase X.3 for HU-15 in session-operations-service, per the
**X.3 derivation block in @backend/docs/hu15-context.md** (your spec — including its
keep/delete/decide classification — do not re-read the canon or re-inspect the tree;
grep the model snapshot rather than full-reading it, as the block instructs).

Prerequisite: the mission-design GET /api/missions/{id}/runtime-plan endpoint (Step 4.5)
must already be landed — IMissionRuntimeSource consumes it. If it is not yet present, land
Step 4.5 first.

Gate:
- build passes
- new IMissionRuntimeSource HTTP adapter calls GET /api/missions/{id}/runtime-plan and returns the full runtime plan incl. resolved trivia content (integration test)
- EF mapping + migration represent the MissionRuntimeSnapshot owned graph; the trivia-snapshot tables and source_trivia_quiz_id are dropped
- repository integration test round-trips the full MissionRuntimeSnapshot
- no SessionMode in schema/read models; no persistence model treats TriviaQuiz as SessionSource

Do not touch Api or frontend.
```

Commit:

```text
feat(session-operations): phase X.3 - infrastructure layer (HU-15)

Ref: HU-15
Ref: DES-22
Ref: DES-70
```

---

## 8. Backend phase X.4 - API layer

```text
Use @backend/.agents/backend-agent.md.
Implement backend phase X.4 for HU-15 in session-operations-service, per the
**X.4 derivation block in @backend/docs/hu15-context.md** (your spec — do not
re-read the canon or re-inspect the tree; open a cited canon section only to
fill a gap the block leaves open).

Gate:
- endpoint integration tests pass: a ready mission -> 201 Created; an inactive / not-ready mission -> rejected (409/422)
- POST /api/sessions keeps Administrator auth and 201 Created; request is mission-only CreateSessionRequest (MissionId, Title, MaximumTimeMinutes, ScheduledAt) — no SourceTriviaQuizId
- MissionNotEligibleForSessionCreationException maps to 409/422
- no API request/response contains SessionMode or treats TriviaQuiz as SessionSource
- service coverage reaches the repo gate target (ADR-0005)

Gate (pattern): endpoint inherits the standard Administrator AuthorizationBehaviour — no ad-hoc role if-checks, no new Proxy gate.

Do not touch frontend.
```

Commit:

```text
feat(session-operations): phase X.4 - api layer (HU-15)

Ref: HU-15
Ref: DES-22
Ref: DES-70
```

---

## 8.5. Docker rebuild and smoke

```text
From the monorepo root, rebuild and restart the backend stack after the API phase:

docker compose build mission-design-service session-operations-service api-gateway
docker compose up -d mission-design-service session-operations-service api-gateway

Run curl smoke checks through the gateway for:
- GET /api/missions/{id}/runtime-plan for a ready mission -> expect 200 with resolved trivia content (Phase 0)
- POST /api/sessions with an active, runtime-ready mission -> expect 201 Created
- POST /api/sessions with an inactive / not-ready mission -> expect 409/422
- confirm the created session carries a frozen MissionRuntimeSnapshot and initial state Scheduled

Output:
- container status
- smoke command results
- the API contract shape that the frontend phase must consume (mission-only CreateSessionRequest + created-session response)
```

---

## 9. Frontend slice

```text
Generate a multi phase plan in a markdown file — following the **frontend plan concreteness rule** (below), modelled on the exemplar closest to this slice's shape (`@frontend/plans/hu-03-frontend-role-permission-assignment.md` for a small 1–few-endpoint surface; `@frontend/plans/hu-10a-frontend-mission-hierarchy-authoring.md` for a large/multi-endpoint or partially-blocked surface) — save it in `@frontend/plans/` for the following:
Use @frontend/AGENTS.md.
Implement the frontend slice for HU-15 session creation from an active mission.

The backend exposes a single reshaped endpoint: POST /api/sessions taking a mission-only
CreateSessionRequest (MissionId, Title, MaximumTimeMinutes, ScheduledAt), Administrator-only,
201 Created, and 409/422 when the mission is not eligible (inactive / not runtime-ready). This
is a small surface — choose the hu-03 exemplar.

Scope:
- update session data client/types to the mission-only CreateSessionRequest (drop SourceTriviaQuizId / quiz fields)
- session-creation form selects an active, runtime-ready Mission (not a trivia quiz) as the source
- surface backend eligibility/readiness rejection (mission inactive / not ready -> 409/422) as an explicit message
- show the created session in Scheduled state
- remove or rewrite stale UI copy/types that mention creating a session from a quiz, SessionMode, or a trivia source

Gate:
- frontend typecheck/build passes
- session-creation flow exercises create-from-mission against the reshaped API contract, incl. the not-eligible rejection path
- Gate: UI selects a Mission as the only source; no UI type/copy treats TriviaQuiz as a SessionSource
- Gate: no UI type/copy introduces SessionMode

Do not modify backend code in this step.
```

**Frontend plan concreteness rule (embed verbatim in the generated plan's altitude choice):**

1. **Proportion concreteness to certainty.** Write code-complete detail — exact DTO/request types, real component skeletons, exact client-fn + server-action bodies, a `data-testid` contract — only for the **fully-knowable near-term increments** (typically the foundation + first authoring increment). Keep later, large, or blocked increments at **contract + gate altitude**: a contract table, scope, and gate, with no invented bodies. Never write code for an increment blocked on an open question.
2. **Verify every code anchor against the real source before writing it.** Open the files the plan names — exported vs. private helpers, exact signatures, the const/env it reads, the line a refactor targets — and write only what the source actually supports. A confident-but-wrong anchor (e.g. "reuse `getIdentityHeaders`" when it is not exported) is worse than an altitude note. If a detail is not verifiable, state the assumption under Open Questions rather than inventing it.
3. **Required sections** (both exemplars carry these; a plan missing one is a defect): Context · Verified Backend Contract (endpoint/shape table) · Architecture Decisions · **Environment** (env vars / config consts reused) · **data-testid contract** · phased Scope + Gate per increment · **Acceptance-criteria → test mapping** · Open Questions / Dependencies · Out of Scope.
4. **Final forms only, sequential by default.** Write only the final version of each anchor — no "wrong → revised" trails — and keep increments sequential unless the slice genuinely parallelizes.

Commit:

```text
feat(frontend): session creation from mission - HU-15

Ref: HU-15
Ref: DES-22
Ref: DES-70
```

---

## 9b. Implement the frontend plan

> Run only after the Step 9 plan is written and reviewed. The plan is the source of
> truth and supersedes the Step 9 seed scope — including Step 9's single seed commit:
> commit per the plan's own per-phase Commit Sequence, not the one above.

```text
Use @frontend/AGENTS.md. Implement the Step 9 plan at @frontend/plans/hu-15-frontend-session-creation-from-mission.md,
phase by phase per the plan's own Scope / Gate / Commit Sequence.

For each phase: implement only that phase, run its Gate (build + typecheck, plus any e2e the
phase lands), then commit with the exact subject from the plan's Commit Sequence for that phase.

STOP at any increment the plan marks blocked on an Open Question (name it). Do not invent the
blocked behaviour; surface the question and wait.

Do not re-generate the plan. Do not modify backend code in this step.
```

---

## 10. Close-out

```text
Use the Linear MCP to re-check DES-22 acceptance criteria and labels.
Verify the final implementation against the Stop 1 acceptance guard:
- Mission is the only SessionSource; TriviaQuiz is not a SessionSource
- session creation freezes an immutable MissionRuntimeSnapshot of the full mission runtime plan
- LiveSession.Create(...) is mission-based and sets initial state Scheduled
- no session-level SessionMode in the creation path
- session creation orchestration lives behind a single Application-layer Facade
- the LiveSession aggregate + State machine are not rebuilt
- POST /api/sessions stays Administrator-guarded, 201 Created, request reshaped to mission-only

Run final backend and frontend verification required by the repo instructions.
Summarise:
- commits created
- backend API contract changes (mission-only CreateSessionRequest, eligibility -> 409/422)
- frontend plan/file produced
- tests and gates run
- any unresolved ambiguity (esp. the mission-design runtime-plan endpoint dependency) for HU-16/HU-17 follow-up

Create the PR:

gh pr create \
  --base develop \
  --head feature/hu-15-session-creation-from-mission \
  --title "feat(session-operations): rebuild HU-15 session creation from mission" \
  --body "Rebuilds DES-22/HU-15 session creation around the realigned mission runtime model: Mission as the only SessionSource, an immutable MissionRuntimeSnapshot frozen at creation, a mission-based LiveSession.Create setting Scheduled state, and an Application-layer creation Facade. Tears out the quiz-as-source model (SessionSource TriviaQuiz member, TriviaSessionSnapshot, CreateTrivia, published-quiz integration). The LiveSession aggregate and State machine (HU-07A/07B/HU-21A) are unchanged."
```

---

## Rationale

The old session-creation baseline (HU-07A/07B) created a `LiveSession` from a published trivia quiz: `SessionSource` carried a `TriviaQuiz` member, the session froze a `TriviaSessionSnapshot`, and a session-level `SessionMode` distinguished trivia vs treasure-hunt sessions. The 2026-06-16 mission-runtime rewrite invalidated that model — `Mission` is now the only `SessionSource`, play mode is a per-`Substage` concern, and a session must freeze the full mission runtime plan as an immutable `MissionRuntimeSnapshot`. DES-22 is a `needs-rebuild` foundation row precisely because every downstream session HU (16, 17, 18–36) depends on this corrected source model.

The required pattern differs from the mission-design predecessors (HU-09/10A used `Composite` for the authoring hierarchy). HU-15's controlling obligation is `Facade`: session creation coordinates several subsystems (readiness gate, mission-runtime fetch, snapshot construction, aggregate creation, persistence) and must present a single Application-layer entry point — the direct continuation of the existing `CreateTriviaSessionFacade`. The `Facade` mandate is transferred from superseded HU-16, anchored in ADR-0004 line 3 and PRD DES-70 lines 195-197, not invented to fill a matrix gap.

**Resolved cross-service dependency:** HU-15's `MissionRuntimeSnapshot` must freeze resolved trivia question content (play-time scoring/timing read the snapshot, not the live quiz — `bd_umbral_entity_spec.md:343,450,617-638`), and no existing mission-design endpoint returns it (`/api/missions/{id}` carries only a `TriviaQuizId` reference; `/api/trivias/{id}` is the old per-quiz fetch). HU-15 therefore adds a new `GET /api/missions/{id}/runtime-plan` endpoint in mission-design (Step 4.5 / Phase 0), covered by the `svc:mission-design-service` label on DES-22, that resolves the full plan with inlined trivia content; session-operations X.3 consumes it in a single read. Phase 0 lands before X.3.

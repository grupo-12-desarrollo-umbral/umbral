# HU-15 — Driver brief
_Driver reads only this file. The full prompt + context are for the subagent
and human review; the driver never loads them._

> **Nature of this HU:** realignment-rebuild. X.1 Domain adds the net-new immutable
> `MissionRuntimeSnapshot` + mission-based `LiveSession.Create`; X.2/X.3/X.4 rebuild
> the creation orchestration, persistence, and endpoint around `Mission` as the only
> source — tearing out the quiz-as-source model. **Never let a subagent rebuild the
> `LiveSession` aggregate or its State machine** — HU-07A/07B own the aggregate, HU-21A
> owns the lifecycle `State` pattern; HU-15 only adds the mission `Create` path + snapshot
> and sets the initial `Scheduled` state.
>
> **Cross-service prerequisite (Phase 0, mission-design):** HU-15 adds a new
> `GET /api/missions/{id}/runtime-plan` endpoint in mission-design (covered by the
> `svc:mission-design-service` label on DES-22) that resolves the full plan with inlined
> trivia content — the snapshot must freeze it, and existing endpoints return only a
> `TriviaQuizId` reference. Phase 0 lands **before** X.3; session-operations X.3 consumes it.

## Slice
| HU | DES | PRD | Service | Branch | Base |
|----|-----|-----|---------|--------|------|
| HU-15 — Creacion de sesion de mision desde mision activa | DES-22 | DES-70 | session-operations-service | feature/hu-15-session-creation-from-mission | develop |

## Required pattern(s) → owning phase
- `Facade` (phase X.2 Application) — Why: session creation coordinates several subsystems (readiness gate, mission-runtime fetch, snapshot build, aggregate construction, persistence) and must present a single Application-layer entry point — obligation: one creation `Facade` orchestrating `IMissionReadinessSource` gate → `IMissionRuntimeSource` fetch → build `MissionRuntimeSnapshot` → `LiveSession.Create` → persist via `ILiveSessionRepository`; the handler stays a pass-through. (Mandate transferred from superseded HU-16; backed by ADR-0004:3 + PRD DES-70:195-197, not the trivia matrix.)
- No `Proxy` gate (endpoint inherits standard `Administrator` `AuthorizationBehaviour`) and no `State` gate (HU-21A scope) — note only.

## Per phase — gate + owned pattern
| Phase | Gate | Pattern |
|-------|------|---------|
| P0 mission-design (prereq, before X.3) | New `GET /api/missions/{id}/runtime-plan` returns the resolved plan: mission title + maximumTime, ordered stages → substages (play mode, winner score), TH targets + optional clue, and per Trivia substage the published quiz's **resolved** questions (prompt, options + isCorrect, scoreValue, timeLimitSeconds) in strict order; query/endpoint integration test (mission with TH + Trivia substages); read-only, no N+1 left for the consumer; ADR-0005 coverage | — |
| X.1 Domain | Build + unit test per new type (`MissionRuntimeSnapshot` + stage/substage/target snapshot VOs, mission-based `LiveSession.Create`); snapshot immutable with ordering + TH/trivia substage invariants; `Create` sets `Scheduled` and rejects non-Mission source; `Mission` only `SessionSource`, `TriviaQuiz` not a source, no session-level `SessionMode`; quiz-as-source domain code deleted/reconciled; aggregate + State machine not rebuilt | — |
| X.2 Application | Build + facade unit tests (happy + reject inactive/not-ready) + validator tests; creation behind a single `Facade` (gate→fetch→snapshot→`Create`→persist), handler pass-through; command/result DTO carry no quiz fields, no `SessionMode`; quiz-as-source application code deleted/reconciled | `Facade` |
| X.3 Infrastructure | Build; new `IMissionRuntimeSource` HTTP adapter **calls `GET /api/missions/{id}/runtime-plan`** (Phase 0 endpoint — must be landed first) integration test; EF mapping + migration for `MissionRuntimeSnapshot` owned graph, trivia-snapshot tables + `source_trivia_quiz_id` dropped; repo integration round-trips the snapshot; no `SessionMode`/`TriviaQuiz`-as-source in schema | — |
| X.4 Api | Endpoint integration tests (ready mission → 201; inactive/not-ready → 409/422); `POST /api/sessions` keeps `Administrator` + 201, request mission-only `CreateSessionRequest` (no `SourceTriviaQuizId`); `MissionNotEligibleForSessionCreationException` → 409/422; no `SessionMode`/`TriviaQuiz`-as-source in any payload; ADR-0005 coverage (service ≥ repo gate) | — (standard `AuthorizationBehaviour`, no new Proxy) |

Commit subjects — copy each phase's exact subject from the prompt's Steps 4.5–8 verbatim:
- P0 `feat(mission-design): mission runtime-plan read endpoint (HU-15)`
- X.1 `feat(session-operations): phase X.1 - domain layer (HU-15)`
- X.2 `feat(session-operations): phase X.2 - application layer (HU-15)`
- X.3 `feat(session-operations): phase X.3 - infrastructure layer (HU-15)`
- X.4 `feat(session-operations): phase X.4 - api layer (HU-15)`

Trailer (every phase): `Ref: HU-15` / `Ref: DES-22` / `Ref: DES-70`

## Acceptance criteria
- `Mission` is the only `SessionSource`; `TriviaQuiz` is not a `SessionSource`
- session creation freezes an immutable `MissionRuntimeSnapshot` of the full mission runtime plan
- `LiveSession.Create(...)` is mission-based and sets initial state `Scheduled`
- no session-level `SessionMode` in the creation path
- session creation orchestration lives behind a single Application-layer `Facade`
- the `LiveSession` aggregate + State machine are not rebuilt (HU-07A/07B/HU-21A)
- `POST /api/sessions` stays `Administrator`-guarded, 201 Created, request reshaped to mission-only
- frontend session-creation form selects an active, runtime-ready mission and surfaces eligibility rejection

## Endpoints + smoke (driver verifies at Stop 2)
After the X.4 docker rebuild (`docker compose build mission-design-service session-operations-service api-gateway && docker compose up -d mission-design-service session-operations-service api-gateway`), smoke through the gateway:
- `GET /api/missions/{id}/runtime-plan` (Phase 0) — for a ready mission → expect **200** with resolved trivia content (questions/options/correct answers/timers/scores)
- `POST /api/sessions` — mission-only `CreateSessionRequest(MissionId, Title, MaximumTimeMinutes, ScheduledAt)`, `Administrator` — with an active, runtime-ready mission → expect **201 Created**; response carries a frozen `MissionRuntimeSnapshot` and initial state `Scheduled`
- `POST /api/sessions` — with an inactive / not-runtime-ready mission → expect **409/422** (`MissionNotEligibleForSessionCreationException`)
- Frontend contract: mission-only `CreateSessionRequest`; created-session response with snapshot + `Scheduled` state

## Frontend slice
Human-driven — see Steps 9 (generate plan) + 9b (implement it) of
`prompt_example_feature_hu15.md`.

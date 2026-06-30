# HU-17 — Driver brief
_Driver reads only this file. The full prompt + context are for the subagent
and human review; the driver never loads them._

> **Nature of this HU:** realignment-rebuild, **verification-dominant**. HU-15 (DES-22, Done)
> already shipped the mission-only creation path — `SessionSource` (Mission-only),
> `MissionRuntimeSnapshot`, `CreateSessionFacade`, `SessionCreationPolicy`, and the mission-only
> `POST /api/sessions` contract. **HU-17 does NOT rebuild any of these.** Every phase locks the
> single-source invariant with tests and deletes the residual dead two-source debris HU-15 left
> (`SessionMode` enum, `SessionSourceDoesNotMatchModeException`, the orphaned `TriviaSessionSnapshot*`
> exception pair). No new aggregate, no new endpoint, **no new migration**. Never authorize a
> subagent to re-implement a HU-15 type or reshape the creation contract.

## Slice
| HU | DES | PRD | Service | Branch | Base |
|----|-----|-----|---------|--------|------|
| HU-17 — Creacion de sesion desde una unica fuente | DES-24 | DES-70 | session-operations-service | feature/hu-17-single-source-session-creation | develop |

## Required pattern(s) → owning phase
- **None mandated** (`required_patterns_matrix.md:100`: the single-source invariant is enforced inside HU-15's `Facade` + `SessionCreationPolicy`, not its own pattern). Do not let a subagent add a pattern.
- No `Proxy` gate — `POST /api/sessions` inherits the standard `Administrator` `AuthorizationBehaviour`/gateway guard (ADR-0001/0002); HU-17 is not in the applies-where set — note only.

## Per phase — gate + owned pattern
| Phase | Gate | Pattern |
|-------|------|---------|
| X.1 Domain | Domain build; unit test locks the single-source invariant (`SessionSource.Create` -> Mission only; `LiveSession.Create` rejects a non-mission source; `SessionSourceType` exposes only `Mission`); dead two-source debris deleted (`SessionMode`, `SessionSourceDoesNotMatchModeException`, `TriviaSessionSnapshotMustContainQuestionsException`, `TriviaSessionSnapshotRequiredException`); no `SessionMode` type remains; the live `TriviaQuestionSnapshot*` exceptions untouched | — |
| X.2 Application | App build; facade/handler test proves the snapshot is built **only** from the requested mission (no external source mixed) and the command carries no non-mission source field; single creation entry point (the mission `CreateSessionFacade`); no `CreateTriviaSession*`/`IPublishedTriviaQuizSource` path remains | — (invariant rides HU-15's `Facade`) |
| X.3 Infrastructure | Infra build; **no new migration** (assert model snapshot already excludes `source_trivia_quiz_id`/`SessionMode`/quiz-snapshot tables); repo integration test round-trips a session with a mission-only `SessionSource`; no foreign-source column/table persists | — |
| X.4 Api | Endpoint integration test (active+ready mission -> 201, single mission source, no quiz/second-source field, no alternate creation route; plus ineligible-mission 409/422) + ADR-0005 coverage | — (standard `AuthorizationBehaviour`, no new Proxy) |

Commit subjects — copy each phase's exact subject from the prompt's Steps 5–8 verbatim:
- X.1 `feat(session-operations): phase X.1 - domain layer (HU-17)`
- X.2 `feat(session-operations): phase X.2 - application layer (HU-17)`
- X.3 `feat(session-operations): phase X.3 - infrastructure layer (HU-17)`
- X.4 `feat(session-operations): phase X.4 - api layer (HU-17)`

Trailer (every phase): `Ref: HU-17` / `Ref: DES-24` / `Ref: DES-70`

## Acceptance criteria
- a `LiveSession` is created from exactly one `Mission`; `Mission` is the only `SessionSource`
- a `TriviaQuiz` cannot create a `LiveSession` directly (no quiz-as-source, no alternate creation route)
- trivia reaches runtime only via a trivia `Substage` copied into the `MissionRuntimeSnapshot` at creation (HU-15)
- no external source can be mixed into the mission snapshot
- no session-level `SessionMode` exists in the service after this slice
- HU-15's mission-only creation path is verified and locked, **not** rebuilt

## Endpoints + smoke (driver verifies at Stop 2)
_No new endpoints — the contract is unchanged from HU-15. Smoke the existing `POST /api/sessions`._
After the X.4 docker rebuild (`docker compose build session-operations-service api-gateway && docker compose up -d session-operations-service api-gateway`), smoke through the gateway:
- `POST /api/sessions` — mission-only `CreateSessionRequest(MissionId, Title, MaximumTimeMinutes, ScheduledAt)`, `Administrator` — with an active, runtime-ready mission → expect **201 Created** (single mission source)
- `POST /api/sessions` — with an inactive / not-runtime-ready mission → expect **409/422**
- Confirm there is no quiz-as-source creation route and the request body carries no quiz/second-source field (contract unchanged from HU-15)

## Frontend slice
Human-driven — see Steps 9 (generate plan) + 9b (implement it) of
`prompt_example_feature_hu17.md`. Verification/cleanup only — the backend contract is unchanged.

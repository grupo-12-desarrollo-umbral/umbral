# HU-16 - Driver brief
_Driver reads only this file. The full prompt + context are for the subagent
and human review; the driver never loads them._

> **Nature of this HU:** realignment-rebuild, **verification-dominant**. HU-15 (DES-22, Done)
> already ships the immutable `MissionRuntimeSnapshot` **including the full trivia-question copy**
> (`TriviaQuestionSnapshot`/`TriviaOptionSnapshot` VOs, `CreateSessionFacade.BuildMissionRuntimeSnapshot`),
> and HU-17 (DES-24, Done) already locked the single-source invariant and tore out the two-source debris.
> **HU-16 does NOT rebuild any of these and there is NOTHING to delete in code.** Every phase locks the
> **snapshot-content-fidelity** invariant with tests (the whole published quiz is frozen - all
> questions/options/correct/score/timer, no partial `TriviaQuestionSelection`) and X.4 retires the last
> stale quiz-as-source docs. No new aggregate, endpoint, or migration. Never authorize a subagent to
> re-implement a HU-15 type, reshape the creation contract, or delete/rename the live `TriviaQuestionSnapshot*` types.

## Slice
| HU | DES | PRD | Service | Branch | Base |
|----|-----|-----|---------|--------|------|
| HU-16 - Realineacion trivia-substage runtime snapshot | DES-75 | DES-70 | session-operations-service | feature/hu-16-runtime-snapshot-trivia-substage | develop |

## Required pattern(s) → owning phase
- **`Facade` (phase X.2)** - `required_patterns_matrix.md` HU-16 row: "Runtime snapshot orchestrates the immutable copy of the mission Composite tree ... into `MissionRuntimeSnapshot`." Canon note: "realized via HU-15/HU-17" - obligation: verify the immutable snapshot copy rides on the single `CreateSessionFacade`; **no new facade/pattern class**.
- No `Proxy` gate - `POST /api/sessions` inherits the standard `Administrator` `AuthorizationBehaviour`/gateway guard (ADR-0001/0002); HU-16 is not in the applies-where set - note only.

## Per phase - gate + owned pattern
| Phase | Gate | Pattern |
|-------|------|---------|
| X.1 Domain | Domain build; unit tests lock whole-quiz snapshot fidelity (all questions/options/correct-flag/score/timer copied in strict mission order; trivia substage >=1 question; snapshot immutable after `Create`); assert no `TriviaQuestionSelection` type / no partial-selection path; live `TriviaQuestionSnapshot*` types/exceptions untouched | — |
| X.2 Application | App build; facade test proves the immutable copy of the **full mission tree** (substages, targets, clues, questions - `required_patterns_matrix.md:99`) into `MissionRuntimeSnapshot` is orchestrated through the single `CreateSessionFacade` (no second creation/snapshot path); DES-75 fidelity depth on the trivia slice - the **full** published quiz (all questions/options/correct/score/timer, mission order) copied into `TriviaQuestionSnapshots`, no partial copy; command carries no quiz/second-source field; no `CreateTriviaSession*`/`IPublishedTriviaQuizSource` path remains | `Facade` (verify; rides HU-15's `CreateSessionFacade`) |
| X.3 Infrastructure | Infra build; **no new migration** (assert model clean - no `source_trivia_quiz_id`/quiz-snapshot tables); repo integration test round-trips a session from a trivia-bearing mission with all trivia questions/options/correct-flags intact | — |
| X.4 Api | Endpoint integration test (trivia-bearing mission -> 201; no standalone trivia-session/quiz-as-source route; + ineligible-mission 409/422); stale quiz-as-source doc block retired (`faq/workflow-and-sprint-planning.md:167-173`) + supersession documented; + ADR-0005 coverage | — (standard `AuthorizationBehaviour`, no new Proxy) |

Commit subjects - copy each phase's exact subject from the prompt's Steps 5-8 verbatim:
- X.1 `feat(session-operations): phase X.1 - domain layer (HU-16)`
- X.2 `feat(session-operations): phase X.2 - application layer (HU-16)`
- X.3 `feat(session-operations): phase X.3 - infrastructure layer (HU-16)`
- X.4 `feat(session-operations): phase X.4 - api layer (HU-16)`

Trailer (every phase): `Ref: HU-16` / `Ref: DES-75` / `Ref: DES-70`

## Acceptance criteria
- a trivia `Substage` assigns one **whole** published `TriviaQuiz` (`TriviaQuizSelection`); all its questions are copied into the immutable `MissionRuntimeSnapshot` at `LiveSession` creation - no partial/ordered-subset selection
- there is no `TriviaQuestionSelection` type and no partial-selection path anywhere in the model
- the snapshot copy carries every question's prompt/options/correct-answer/score/timer in strict mission order and is immutable after creation
- there is no standalone trivia-session / quiz-as-source creation route (a `TriviaQuiz` cannot create a `LiveSession`)
- HU-15's snapshot machinery and HU-17's single-source lock are verified and locked, **not** rebuilt
- DES-23's behaviour is superseded and documented as such (banners confirmed + stale faq block retired)

## Endpoints + smoke (driver verifies at Stop 2)
_No new endpoints - the contract is unchanged from HU-15. Smoke the existing `POST /api/sessions` with a trivia-bearing mission._
After the X.4 docker rebuild (`docker compose build session-operations-service api-gateway && docker compose up -d session-operations-service api-gateway`), smoke through the gateway:
- `POST /api/sessions` - mission-only `CreateSessionRequest(MissionId, Title, MaximumTimeMinutes, ScheduledAt)`, `Administrator` - with an active, runtime-ready mission that has a **trivia substage** → expect **201 Created**; confirm the created session's snapshot carries **all** of the source quiz's questions/options
- `POST /api/sessions` - with an inactive / not-runtime-ready mission → expect **409/422**
- Confirm there is no quiz-as-source / standalone trivia-session creation route (contract unchanged from HU-15)

## Frontend slice
Human-driven - see Steps 9 (generate plan) + 9b (implement it) of
`prompt_example_feature_hu16.md`. Verification/cleanup only - the backend contract is unchanged.

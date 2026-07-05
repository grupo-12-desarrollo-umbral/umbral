# HU-22 — Driver brief
_Driver reads only this file. The full prompt + context are for the subagent
and human review; the driver never loads them._

> **Nature of this HU:** realignment-rebuild — **genuine rebuild, not verification.** Unlike its sibling
> HU-21A (which locked already-aligned lifecycle code with tests), HU-22 changes behaviour: the displayed
> remaining time must be **derived from the active substage's `SubstagePlayMode`** (X.1–X.2), the pre-canon
> **whole-session `MaximumTime` countdown must be deleted** (X.1 domain + X.3 migration dropping the
> `_sessionTimer*` columns), and the pause-freeze / trivia-resume-same-question behaviour kept. Never authorize
> a subagent to touch question activation/advancement or the `TriviaRoundOrchestratorFacade` (HU-33A/DES-78,
> downstream), or to build treasure-hunt play (HU-29–32).
>
> **✅ The three former open decisions are RESOLVED and committed to scope** (see below) — X.1 may start once Stop 1 confirms the slice is grabbed.

## Slice
| HU | DES | PRD | Service | Branch | Base |
|----|-----|-----|---------|--------|------|
| HU-22 — Temporizador autoritativo por `SubstagePlayMode` | DES-77 | DES-70 | session-operations-service | feature/hu-22-authoritative-timer-substage-realign | develop |

## Resolved decisions (committed scope — X.1 builds to these)
- **OD-1 — RESOLVED: no treasure-hunt countdown.** Canon defines an authoritative clock only for trivia; treasure-hunt substages expose no remaining-time countdown. Elapsed `ResolutionTime` only if the frontend explicitly needs a figure (UI-contract call, confirm at Stop 2). A per-substage authored duration is out of scope (needs an ADR + new snapshot field).
- **OD-2 — RESOLVED: dispatch on `ActiveQuestionIndex`.** The timer selector dispatches on `ActiveQuestionIndex` present (trivia window) vs absent (no countdown). No multi-substage active-pointer, no advancement — those stay downstream (DES-78 / HU-29–32).
- **OD-3 — RESOLVED: redefine in place + drop columns.** `SessionTimerSnapshotDto.{RemainingSeconds,TotalSeconds}` are redefined in place as the active-substage window (0/absent when no active question), `ActiveQuestion` nesting kept; the `_sessionTimer*` columns are dropped via a new migration. `MaximumTime` VO/column survives as authoring metadata. **Frontend contract change — surface at Stop 2.**

## Required pattern(s) → owning phase
- **`State`** (phase X.1) — `required_patterns_matrix.md:41,110`: timer behaviour depends on session state (Active vs Paused) — obligation: advancing-vs-frozen owned by the per-`SessionState` type via `LiveSessionStateFactory`, **re-pointed at the substage-derived timer**, not ad-hoc conditionals.
- **Transport SignalR** (phase X.4) — verify the substage-derived `SessionTimerUpdated` broadcast to `live-session:{id}`. **No RabbitMQ.**
- No new `Proxy` gate — the two timer GETs inherit the standard operator / participant-membership `AuthorizationBehaviour`/gateway guard (ADR-0001/0002); HU-22 is not in the applies-where set — note only.

## Per phase — gate + owned pattern
| Phase | Gate | Pattern |
|-------|------|---------|
| X.1 Domain | Domain build; unit test locks: authoritative remaining = active `TriviaQuestionTimer` window when a trivia question is active, **no advancing countdown otherwise** (OD-1); `Paused` freezes and `Active` resumes the **same** question at the frozen remainder; the whole-session `MaximumTime` countdown (`_sessionTimer*`) is **gone**; no session-level `SessionMode` | `State` |
| X.2 Application | App build; test proves the timer DTO/queries (operator + participant) return the active-substage remaining, `ActiveQuestion` populated only when a question is active, no whole-session-countdown reference remains; authz preserved | — |
| X.3 Infrastructure | Infra build; a migration **DROPS** the `_sessionTimer*` columns (assert via model snapshot); `ListActiveTimersAsync` selects by advancing **question** timer; repo integration test round-trips an active-question timer; DES-78's question close/advance call left unchanged | — |
| X.4 Api | Endpoint tests (operator + participant timer GET → active-substage remaining; auth enforced) + hub test (substage-derived `SessionTimerUpdated` to `live-session:{id}`) + ADR-0005 coverage | — (standard `AuthorizationBehaviour`, no new Proxy) |

Commit subjects — copy each phase's exact subject from the prompt's Steps 5–8 verbatim:
- X.1 `feat(session-operations): phase X.1 - domain layer (HU-22)`
- X.2 `feat(session-operations): phase X.2 - application layer (HU-22)`
- X.3 `feat(session-operations): phase X.3 - infrastructure layer (HU-22)`
- X.4 `feat(session-operations): phase X.4 - api layer (HU-22)`

Trailer (every phase): `Ref: HU-22` / `Ref: DES-77` / `Ref: DES-70`

## Acceptance criteria
- the displayed remaining time is derived from the active substage and its `SubstagePlayMode` (trivia → active question window; treasure-hunt → no countdown per OD-1)
- no timer logic conditioned on a session-level `SessionMode`, and no whole-session `MaximumTime` authoritative countdown
- pause freezes the timer; paused trivia resumes on the same question
- **out of scope (surface, do not build):** question activation/advancement + trivia round orchestration (HU-33A/DES-78); treasure-hunt target resolution + substage advancement (HU-29–32); a per-treasure-hunt-substage authored duration (needs an ADR — OD-1)

## Endpoints + smoke (driver verifies at Stop 2)
_No new endpoints — the two timer GETs already exist; their remaining-time **meaning changes** (whole-session → active-substage). This is a contract change for the frontend._
After the X.4 docker rebuild (`docker compose build session-operations-service api-gateway && docker compose up -d session-operations-service api-gateway`), smoke through the gateway:
- `GET /api/sessions/{liveSessionId}/timer` — Operator → expect **200**; with an active trivia question, `RemainingSeconds` tracks the question window; with no active question, remaining is **0/absent** (no whole-session countdown); response shape `SessionTimerSnapshotDto { …, RemainingSeconds, TotalSeconds, ActiveQuestion? }`
- `GET /api/sessions/{liveSessionId}/participants/timer` — participant (team + token) → expect **200** with the same substage-derived remaining
- pause (`PATCH …/state` → `Paused`) then re-read the timer → remaining **frozen**; resume (→ `Active`) → the **same** question continues from the frozen remainder
- confirm a SignalR client on `live-session:{id}` receives `SessionTimerUpdated` carrying the substage-derived remaining

## Frontend slice
Human-driven — see Steps 9 (generate plan) + 9b (implement it) of
`prompt_example_feature_hu22.md`. **Contract change:** the timer remaining-time now tracks the active substage (active trivia question), not a whole-session countdown; treasure-hunt shows no countdown (pending OD-1).

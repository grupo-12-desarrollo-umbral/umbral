# HU-21A — Driver brief
_Driver reads only this file. The full prompt + context are for the subagent
and human review; the driver never loads them._

> **Nature of this HU:** realignment-rebuild, **verification-dominant**. The `State`-pattern
> state machine, the `Chain of Responsibility` transition validators, the `TransitionSessionStateFacade`,
> the `PATCH /api/sessions/{id}/state` endpoint, and the `SessionStateChanged` SignalR broadcast were
> already shipped in cycle 1 (DES-28) with the six canonical states; HU-15 (DES-22) creates the session
> in `Scheduled` and HU-17 (DES-24) removed the session-level `SessionMode`. **HU-21A does NOT rebuild any
> of this.** Every phase locks the canonical model with tests. No new aggregate, no new endpoint, **no new
> migration**. Never authorize a subagent to re-implement a State class, the validator chain, the facade,
> or the broadcast — or to build first-substage start (HU-33A/DES-78), the timer (HU-22/DES-77), or the
> RabbitMQ audit event (HU-21B).

## Slice
| HU | DES | PRD | Service | Branch | Base |
|----|-----|-----|---------|--------|------|
| HU-21A — Máquina de estados de sesión | DES-76 | DES-70 | session-operations-service | feature/hu-21a-session-state-machine-realign | develop |

## Required pattern(s) → owning phase
- **`State`** (phase X.1) — `required_patterns_matrix.md:41,109`: lifecycle transitions need an explicit state model — obligation: per-state type owns the allowed-transition set (`SessionStates/*` + `LiveSessionStateFactory`), verified, not enum conditionals.
- **`Chain of Responsibility`** (phase X.2) — `required_patterns_matrix.md:42,109`: ordered transition validators — obligation: `CurrentStateGate → OperatorAssignmentGate → ParticipantReadinessGate` composable + short-circuiting, verified, not one collapsed handler.
- **Transport SignalR** (phase X.4) — verify `SessionStateChanged` broadcast to `live-session:{id}`. **No RabbitMQ** (that is HU-21B).
- No new `Proxy` gate — `PATCH …/state` inherits the standard `Operator` `AuthorizationBehaviour`/gateway guard (ADR-0001/0002); HU-21A is not in the applies-where set — note only.

## Per phase — gate + owned pattern
| Phase | Gate | Pattern |
|-------|------|---------|
| X.1 Domain | Domain build; unit test locks the **full** canonical matrix (`Scheduled→{Preparing,Cancelled}`, `Preparing→{Active,Cancelled}`, `Active→{Paused,Finished,Cancelled}`, `Paused→{Active,Finished,Cancelled}`, `Finished`/`Cancelled` terminal, `Cancelled` from any non-terminal); `Create`⇒`Scheduled`; team association rejected outside `Scheduled`; `MoveTo` raises `SessionStateChangedEvent` only on an allowed edge; no session-level `SessionMode` type remains | `State` |
| X.2 Application | App build; test proves the chain runs `CurrentStateGate → OperatorAssignmentGate → ParticipantReadinessGate` in order + short-circuits on first failure; each rejection asserted; facade orchestrates chain → `MoveTo` → persist; command `Operator`-authorized | `Chain of Responsibility` |
| X.3 Infrastructure | Infra build; **no new migration** (assert model already carries `state`/`last_state_changed_at`/`state_reason`); repo integration test round-trips a transitioned session's state, timestamp, reason | — |
| X.4 Api | Endpoint test (valid transition → 200 with previous/next state; invalid target/edge → ProblemDetails; `Operator`-guarded) + hub test (transition broadcasts `SessionStateChanged` to `live-session:{id}`) + ADR-0005 coverage | — (standard `AuthorizationBehaviour`, no new Proxy) |

Commit subjects — copy each phase's exact subject from the prompt's Steps 5–8 verbatim:
- X.1 `feat(session-operations): phase X.1 - domain layer (HU-21A)`
- X.2 `feat(session-operations): phase X.2 - application layer (HU-21A)`
- X.3 `feat(session-operations): phase X.3 - infrastructure layer (HU-21A)`
- X.4 `feat(session-operations): phase X.4 - api layer (HU-21A)`

Trailer (every phase): `Ref: HU-21A` / `Ref: DES-76` / `Ref: DES-70`

## Acceptance criteria
- the session exposes exactly the six states `Scheduled`, `Preparing`, `Active`, `Paused`, `Finished`, `Cancelled`
- a `LiveSession` is created in `Scheduled` and team association is allowed only while `Scheduled`
- invalid transitions are rejected with a reason (the full canonical matrix is enforced)
- no session-level `SessionMode` logic exists
- valid transitions broadcast live via SignalR (`SessionStateChanged`)
- **out of scope (surface, do not build):** `Preparing → Active` starting the first substage is a downstream seam (HU-33A/DES-78, treasure-hunt HUs); the timer behaviour is HU-22/DES-77; the RabbitMQ audit event is HU-21B

## Endpoints + smoke (driver verifies at Stop 2)
_No new endpoints — the contract is unchanged from cycle 1. Smoke the existing `PATCH /api/sessions/{id}/state`._
After the X.4 docker rebuild (`docker compose build session-operations-service api-gateway && docker compose up -d session-operations-service api-gateway`), smoke through the gateway:
- `PATCH /api/sessions/{liveSessionId}/state` — body `TransitionSessionStateRequest(TargetState, Reason)`, `Operator` — valid next state (e.g. a `Scheduled` session → `Preparing`) → expect **200** with `{ liveSessionId, previousState, newState, lastStateChangedAt, timer }`
- `PATCH /api/sessions/{liveSessionId}/state` — invalid edge (e.g. `Scheduled → Finished`) → expect a **ProblemDetails** rejection (400/409)
- Confirm a SignalR client on the `live-session:{id}` group receives `SessionStateChanged` on a valid transition; no contract drift for the frontend

## Frontend slice
Human-driven — see Steps 9 (generate plan) + 9b (implement it) of
`prompt_example_feature_hu21a.md`. Verification/cleanup only — the backend contract is unchanged.

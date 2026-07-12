# HU-24A — Driver brief
_Driver reads only this file. The full prompt + context are for the subagent
and human review; the driver never loads them._

**Nature of this HU:** feature build, but **read-only over existing persistence** — HU-24A adds an
all-teams operator-panel projection + operator-guarded query + GET endpoint + an operator-group SignalR
push over existing `LiveSession`/`Team`/snapshot state and HU-21A's lifecycle. X.1 adds a `LiveSession`
read method + value objects; X.3 **verifies** the read path and adds **no migration**. Do not authorize a
subagent to build score ledger/ranking, QR target validation, clue release, evidence intake, HU-24B
ranking/events, or to re-guard existing mutation endpoints.

## Slice
| HU | DES | PRD | Service | Branch | Base |
|----|-----|-----|---------|--------|------|
| HU-24A — Panel del operador en tiempo real de estado y progreso | DES-32 | DES-70 | session-operations-service | feature/hu-24a-operator-live-session-panel | develop |

## Required pattern(s) → owning phase
- `Proxy` (phase X.2 + X.4) — the operator panel is a guarded projection; the read + live subscription must
  be gated to the session's assigned operator — obligation: access via `ISessionAdministrationAccessResolver` /
  `SessionAdministrationAuthorizationProxy` (Administrator all / Operator `AssignedOperatorUserId == actor.UserId`
  else `ForbiddenAccessException`, ADR-0009); no ad-hoc role/owner `if` in handler, controller, or hub.
- Transport: SignalR — panel pushed to the reused operator-only `live-session-operators:{id}` group; a new
  broadcaster **method**, not a new group. Initial snapshot from the GET endpoint.

## Per phase — gate + owned pattern
| Phase | Gate | Pattern |
|-------|------|---------|
| X.1 Domain | Domain build; unit tests — panel carries the session `State`; includes every team (ordered) with score default 0; treasure-hunt progress counts active targets, never clues; trivia teams carry active-question/timer context; no score ledger/ranking/winner | — |
| X.2 Application | App build; handler tests cover assigned-operator 200 + non-owner rejection; access through the ownership resolver Proxy (`ForbiddenAccessException` for non-owner, no ad-hoc `if`); `[Authorize(Roles="Operator")]`; DTO carries state + all-teams progress, no ledger/ranking | `Proxy` (resource-ownership resolver) |
| X.3 Infrastructure | Infra build; read path hydrates session `State` + all teams + active-substage target/question snapshots; repo/integration test round-trips; **no migration**, no new persisted state; no `winner_score` regression | — |
| X.4 Api | `GET /api/sessions/{id}/operator-panel` → 200 for assigned operator, 403 RFC 7807 for non-owner; SignalR panel push reaches `live-session-operators:{id}` and not participants; a state/substage transition pushes an updated panel; coverage gate (ADR-0005) | `Proxy` (endpoint policy + reused hub-join guard) |

Commit subjects — copy each phase's exact subject from the prompt's Steps 5–8 verbatim:
- X.1 `feat(session-operations): phase X.1 - domain layer (HU-24A)`
- X.2 `feat(session-operations): phase X.2 - application layer (HU-24A)`
- X.3 `feat(session-operations): phase X.3 - infrastructure layer (HU-24A)`
- X.4 `feat(session-operations): phase X.4 - api layer (HU-24A)`

Trailer (every phase): `Ref: HU-24A` / `Ref: DES-32` / `Ref: DES-70`

## Acceptance criteria
- the operator sees only sessions assigned/authorized to them per policy (non-owned session → 403)
- the panel reflects session-state changes without manual reload
- the operator monitors team progress in real time
- the system blocks access/subscription to unauthorized sessions
- the panel reflects state and progress in real time via SignalR/WebSockets, without manual reload

## Endpoints + smoke (driver verifies at Stop 2)
- `GET /api/sessions/{liveSessionId}/operator-panel` — operator-auth read; as the **assigned** operator
  expect **200** with the session `state` and ordered per-team entries `(teamId, teamCode, displayName, score,
  active-substage target progress resolvedTargets/totalActiveTargets, timer)`; response shape is the frontend contract
- same endpoint as a **non-owning** operator — expect **403** RFC 7807
- SignalR panel push — operator-only `live-session-operators:{id}` group (joined via `JoinLiveSessionAsOperatorAsync`);
  after a session-state transition or substage advance, the re-projected panel reaches the operator without reload;
  verify it does **not** reach participant connections

## Frontend slice
Human-driven — see Steps 9 (generate plan) + 9b (implement it) of
`prompt_example_feature_hu24a.md`. Operator **web** surface (`@frontend/AGENTS.md`); pick the plan exemplar
by shape (small 1–few-endpoint surface → `hu-03`). Primary outcome: operator live session panel (snapshot
fetch + operator-group SignalR push) showing session state + per-team progress; no ranking/events/evidence (HU-24B).

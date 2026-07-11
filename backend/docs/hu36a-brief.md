# HU-36A — Driver brief
_Driver reads only this file. The full prompt + context are for the subagent
and human review; the driver never loads them._

**Nature of this HU:** feature build, but **read-only over existing persistence** — HU-36A adds a
projection + operator-guarded query + GET endpoint over HU-34's answer data and HU-33A's active-question
runtime. X.1 adds a `LiveSession` read method + value objects; X.3 **verifies** the read path and adds
**no migration**. Do not authorize a subagent to rebuild the answer domain, add a SignalR broadcaster, or
create a migration — all transport (operator-only `TeamAnswered` group) already landed in HU-34.

## Slice
| HU | DES | PRD | Service | Branch | Base |
|----|-----|-----|---------|--------|------|
| HU-36A — Monitoreo restringido de respondido/no respondido en trivia | DES-49 | DES-70 | session-operations-service | feature/hu-36a-trivia-answered-monitor | develop |

## Required pattern(s) → owning phase
- `Proxy` (phase X.2 + X.4) — restricted monitoring is a guarded projection; the read must be gated to the
  session's assigned operator — obligation: access via `ISessionAdministrationAccessResolver` /
  `SessionAdministrationAuthorizationProxy` (Administrator all / Operator `AssignedOperatorUserId == actor.UserId`
  else `ForbiddenAccessException`, ADR-0009); no ad-hoc role/owner `if` in handler, controller, or hub.
- Transport: SignalR — **reused** from HU-34 (operator-only `live-session-operators:{id}` group + `TeamAnswered`
  event). HU-36A adds only the initial-snapshot query; no new broadcaster.

## Per phase — gate + owned pattern
| Phase | Gate | Pattern |
|-------|------|---------|
| X.1 Domain | Domain build; unit tests — answered iff an accepted `TriviaAnswerSubmission` for the active `(ActiveSubstageId, QuestionSequenceOrder)`; teams with none render not-answered; view/status types expose no option/correctness/score; projection rejects when no active trivia question | — |
| X.2 Application | App build; handler tests cover authorized-operator + non-owner rejection; access through the ownership resolver Proxy (`ForbiddenAccessException` for non-owner, no ad-hoc `if`); `[Authorize(Roles="Operator")]`; result DTO leaks no option/correctness/points | `Proxy` (resource-ownership resolver) |
| X.3 Infrastructure | Infra build; read path hydrates teams + accepted answers for the active question; repo/integration test proves answered/not-answered round-trips (some answered, some not); **no migration**, no new persisted state | — |
| X.4 Api | `GET /api/sessions/{id}/answered-monitor` → 200 for assigned operator, 403 RFC 7807 for non-owner; payload never carries option/correctness/points; operator-only monitor unreachable by participants; coverage gate (ADR-0005) | `Proxy` (endpoint policy + reused hub-join guard) |

Commit subjects — copy each phase's exact subject from the prompt's Steps 5–8 verbatim:
- X.1 `feat(session-operations): phase X.1 - domain layer (HU-36A)`
- X.2 `feat(session-operations): phase X.2 - application layer (HU-36A)`
- X.3 `feat(session-operations): phase X.3 - infrastructure layer (HU-36A)`
- X.4 `feat(session-operations): phase X.4 - api layer (HU-36A)`

Trailer (every phase): `Ref: HU-36A` / `Ref: DES-49` / `Ref: DES-70`

## Acceptance criteria
- during the active question, the operator sees only whether each team answered or not
- before close, the operator cannot see the option chosen by a team
- the monitoring view updates during the session without manual reload
- monitoring respects the operator's authorized sessions
- answered/not-answered updates are broadcast to the operator in real time via SignalR, without manual reload

## Endpoints + smoke (driver verifies at Stop 2)
- `GET /api/sessions/{liveSessionId}/answered-monitor` — operator-auth read; as the **assigned** operator
  expect **200** with per-team `answered`/`answeredAt` + active-question identity `(SubstageSnapshotId,
  QuestionSequenceOrder)` and **no** option/correctness/points; response shape is the frontend contract
- same endpoint as a **non-owning** operator — expect **403** RFC 7807
- SignalR `TeamAnswered` (reused) — operator-only `live-session-operators:{id}` group; after a team answers,
  the pulse reaches the operator and the snapshot reflects it; verify it does **not** reach participant connections

## Frontend slice
Human-driven — see Steps 9 (generate plan) + 9b (implement it) of
`prompt_example_feature_hu36a.md`. Small surface → use the `hu-03` plan exemplar shape.
Primary outcome: operator answered/not-answered board (snapshot fetch + live `TeamAnswered`), option-free pre-close.

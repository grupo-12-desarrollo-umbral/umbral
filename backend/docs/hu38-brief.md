# HU-38 — Driver brief
_Driver reads only this file. The full prompt + context are for the subagent
and human review; the driver never loads them._

**Nature of this HU:** feature build that adds the operator-facing **justified-penalty** layer on top of
the **already-landed** DES-99 (HU-37 + HU-39) `ScoreEntry` ledger: an `ApplyPenalty` command that records an
append-only `ScoreEntry` of `EntryType.Penalty` + its `Penalty` child, gated to the session's assigned
operator, with eligibility (`IPenaltyPolicy`) as a new `Strategy` **reusing the landed `IScorePolicy`** for
impact, and a `PenaltyApplied` event. **Not greenfield** — the ledger, enums (`Penalty` cases), `IScorePolicy`,
`ScoringMonitoringDbContext`, and the Api host all exist on `develop`; HU-38 **reuses/extends** them and must
**not** rebuild the ledger/ranking/DbContext/Api host. **Primary cross-service dependency:** the assigned-operator
`Proxy` reads a projection fed by consuming `session-operator-assigned` from session-operations — an integration
event that **does not exist yet** (only a domain event); until session-operations publishes it (GH #164), the
Proxy admits only Administrators. Flag this at Stop 2; do not fix session-operations from this service.

## Slice
| HU | DES | PRD | Service | Branch | Base |
|----|-----|-----|---------|--------|------|
| HU-38 — Aplicación de penalizaciones justificadas | DES-53 | DES-85 | scoring-monitoring-service | feature/hu-38-justified-penalties | develop |

## Required pattern(s) → owning phase
- `Strategy` (phase X.1 + X.2) — a penalty is a score-policy outcome — obligation: **new** `IPenaltyPolicy` (eligibility/justification) as interface + `sealed DefaultPenaltyPolicy` in `Domain/Services/`, selected at runtime; **reuse the landed `IScorePolicy`** for the deduction magnitude (do not recreate it); no eligibility/impact `if`/`switch` in the handler. Single-impl, no selector until a 2nd variant lands.
- `Proxy` (phase X.2 + X.4) — only the session's assigned operator may penalize its teams — obligation: `ScoringSessionAuthorizationProxy` over `IScoringSessionAccessResolver` (mirror `SessionAdministrationAuthorizationProxy`): Administrator unrestricted, Operator only when `AssignedOperatorUserId == actor.UserId` else `ForbiddenAccessException`; ownership read from the **local session-assignment projection** (not an HTTP client); no ad-hoc role/owner `if` in handler, controller, or DI; endpoint `[Authorize(Policy = AdministratorOrOperator)]`.
- Transport: RabbitMQ/MassTransit (gated by GH #164) — **consume** `session-operator-assigned` into the assignment projection; **publish** `PenaltyApplied` (+ inherited `ScoreEntryRegistered`) **after commit**. The apply flow must not depend on the broker (a broker outage never fails/rolls back the ledger write).

## Per phase — gate + owned pattern
| Phase | Gate | Pattern |
|-------|------|---------|
| X.1 Domain | Domain build; unit test per new/changed type — `ScoreEntry.Penalty` raises `ScoreEntryRegistered` + `PenaltyApplied` as an append-only `EntryType.Penalty` deduction (non-negative magnitude, no mutator); `Penalty` requires non-blank `PenaltyReason` + `appliedAt` + `appliedByUserId`; `DefaultPenaltyPolicy` eligibility (accept + reject); each new exception; penalty impacts score only via a `ScoreEntry` Penalty entry, no mutable total; `IScorePolicy`/enums reused not recreated | `Strategy` (new `IPenaltyPolicy` + `sealed` impl; `IScorePolicy` reused) |
| X.2 Application | App build; handler tests cover valid apply, **non-owning operator → `ForbiddenAccessException`**, Administrator unrestricted, ineligible-penalty rejection, missing/blank reason; validator tests (reason required, ids present); access through the Proxy resolver reading the local projection — no ad-hoc role/owner `if`; policies injected/selected at runtime; assignment consumer upserts idempotently; `PenaltyApplied` raised for post-commit publish | `Proxy` (assigned-session resolver) + `Strategy` (consumed) |
| X.3 Infrastructure | Infra build; `ef migrations add AddPenaltyAndSessionAssignmentProjection` succeeds (Penalty one-to-one under `ScoreEntry` + `session_operator_assignments` projection); repo/integration test round-trips an append-only `ScoreEntry` Penalty + its `Penalty` child; assignment consumer upserts the projection idempotently; `PenaltyApplied` (+ `ScoreEntryRegistered`) published after commit and a broker outage does not fail the apply path; no mutable total persisted | — |
| X.4 Api | `POST /api/sessions/{liveSessionId}/penalties` → success for assigned operator, **403 RFC 7807** for non-owner, **400** for blank reason; penalty reflected as a `ScoreEntry` Penalty deduction; endpoint `[Authorize(Policy = AdministratorOrOperator)]` + resolver (no ad-hoc role `if`); coverage gate (ADR-0005) | `Proxy` (endpoint policy + access resolver) |

Commit subjects — copy each phase's exact subject from the prompt's Steps 5–8 verbatim:
- X.1 `feat(scoring-monitoring): phase X.1 - domain layer (HU-38)`
- X.2 `feat(scoring-monitoring): phase X.2 - application layer (HU-38)`
- X.3 `feat(scoring-monitoring): phase X.3 - infrastructure layer (HU-38)`
- X.4 `feat(scoring-monitoring): phase X.4 - api layer (HU-38)`

Trailer (every phase): `Ref: HU-38` / `Ref: DES-53` / `Ref: DES-85`

## Acceptance criteria
- the operator can register a penalty against a team of an assigned session
- every penalty requires an explicit reason (`PenaltyReason`)
- the system records the moment of application (`appliedAt`) and the actor (`appliedByUserId`)
- the penalty impacts the team's score per the current rules, through an append-only `ScoreEntry` `EntryType.Penalty` deduction (no mutable total)
- on transactional success, the penalty publishes a domain event (`PenaltyApplied` / `ScoreEntryRegistered`) to RabbitMQ for secondary recalculation and audit, without the main flow depending on RabbitMQ

## Endpoints + smoke (driver verifies at Stop 2)
- `POST /api/sessions/{liveSessionId}/penalties` — operator-auth apply; body `{ teamId, reason }`. As an **Administrator** (or an assigned Operator once the projection is populated) with a non-blank reason → expect success and a `ScoreEntry` Penalty deduction recorded (response is the applied-penalty result, the frontend contract).
- same endpoint as a **non-owning** operator → expect **403** RFC 7807
- same endpoint with a **blank reason** → expect **400**
- **Cross-service flag:** verify whether session-operations publishes `session-operator-assigned`; if not, the assignment projection is empty and the Proxy admits only Administrators (GH #164).

## Frontend slice
Human-driven — see Steps 9 (generate plan) + 9b (implement it) of
`prompt_example_feature_hu38.md`. Operator **web** surface (`@frontend/AGENTS.md`); pick the plan exemplar
by shape (small 1-endpoint surface → `hu-03`). Primary outcome: an operator action to apply a justified
penalty (reason required) to a team in a supervised session and see the score reflect the deduction; no
ranking/audit/history (later HUs).

# HU-38 — Driver brief
_Driver reads only this file. The full prompt + context are for the subagent
and human review; the driver never loads them._

**Nature of this HU:** feature build that adds the operator-facing **justified-penalty** layer on top of
HU-37's `ScoreEntry` ledger: an `ApplyPenalty` command that records an append-only `ScoreEntry` deduction +
its `Penalty` child, gated to the session's assigned operator, with eligibility/impact as `Strategy` policies
and a `PenaltyApplied` event. **Prerequisite (resolved):** HU-37 lands first — the `ScoreEntry`/`ScoreValue`
ledger core is HU-37's (DES-51); HU-38 **consumes/extends** it and adds only the `Penalty` child, policies,
recording behavior, authorization, and endpoint. **Do not start X.1 until DES-51/HU-37 has merged**, and do
not authorize a subagent to rebuild the ledger/ranking/audit under HU-38.

## Slice
| HU | DES | PRD | Service | Branch | Base |
|----|-----|-----|---------|--------|------|
| HU-38 — Aplicación de penalizaciones justificadas | DES-53 | DES-85 | scoring-monitoring-service | feature/hu-38-justified-penalties | develop |

## Required pattern(s) → owning phase
- `Strategy` (phase X.1 + X.2) — a penalty is a score-policy outcome — obligation: `IPenaltyPolicy` (eligibility/justification) and `IScorePolicy` (deduction impact) as interface + `sealed` concrete impl in `Domain/Services/`, selected at runtime in the handler; no eligibility/impact `if`/`switch` in the handler. Single-impl, no selector until a 2nd variant lands.
- `Proxy` (phase X.2 + X.4) — only the session's assigned operator may penalize its teams — obligation: `ScoringSessionAuthorizationProxy` over an access-resolver interface (mirror `SessionAdministrationAuthorizationProxy`): Administrator unrestricted, Operator only when `AssignedOperatorUserId == actor.UserId` else `ForbiddenAccessException`; no ad-hoc role/owner `if` in handler, controller, or DI; endpoint `[Authorize(Policy=...)]`.
- Transport: RabbitMQ — publish `PenaltyApplied` (+ `ScoreEntryRecorded`) **after commit**; the apply flow must not depend on the broker (a broker outage never fails/rolls back the ledger write).

## Per phase — gate + owned pattern
| Phase | Gate | Pattern |
|-------|------|---------|
| X.1 Domain | Domain build; unit test per new/changed type — `ScoreEntry` penalty-recording behavior raises `PenaltyApplied`/`ScoreEntryRecorded` as an append-only deduction; `Penalty` requires non-blank `PenaltyReason` + `appliedByUserId`/`appliedAt`; both policies; each exception; penalty impacts score only via a `ScoreEntry` deduction, no mutable total (HU-37 covers base `ScoreEntry`/`ScoreValue`) | `Strategy` (`IPenaltyPolicy`/`IScorePolicy` interface + `sealed` impl in `Domain/Services/`) |
| X.2 Application | App build; handler tests cover valid apply, **non-owning operator → `ForbiddenAccessException`**, ineligible-penalty rejection, missing/blank reason; validator tests (reason required, ids present); access through the Proxy resolver — no ad-hoc role/owner `if`; policies injected/selected at runtime; `PenaltyApplied` raised for post-commit publish | `Proxy` (assigned-session resolver) + `Strategy` (consumed) |
| X.3 Infrastructure | Infra build; `ef migrations add` for the `Penalty` child succeeds under HU-37's `ScoreEntry` mapping; `ISessionAssignmentAccessClient` HTTP impl to session-operations; repo/integration test round-trips an append-only `ScoreEntry` + `Penalty`; `PenaltyApplied` published after commit and a broker outage does not fail the apply path; no mutable total persisted | — |
| X.4 Api | `POST /api/sessions/{liveSessionId}/penalties` → success for assigned operator, **403 RFC 7807** for non-owner, **400** for blank reason; penalty reflected as a `ScoreEntry` deduction; coverage gate (ADR-0005) | `Proxy` (endpoint policy + access resolver) |

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
- the penalty impacts the team's score per the current rules, through an append-only `ScoreEntry` deduction
- on transactional success, the penalty publishes a domain event (`PenaltyApplied`/`ScoreEntryRecorded`) to RabbitMQ for secondary recalculation and audit, without the main flow depending on RabbitMQ

## Endpoints + smoke (driver verifies at Stop 2)
- `POST /api/sessions/{liveSessionId}/penalties` — operator-auth apply; body `{ teamId, reason }`. As the **assigned** operator with a non-blank reason → expect success and a `ScoreEntry` deduction recorded (response is the applied-penalty result, the frontend contract).
- same endpoint as a **non-owning** operator → expect **403** RFC 7807
- same endpoint with a **blank reason** → expect **400**

## Frontend slice
Human-driven — see Steps 9 (generate plan) + 9b (implement it) of
`prompt_example_feature_hu38.md`. Operator **web** surface (`@frontend/AGENTS.md`); pick the plan exemplar
by shape (small 1-endpoint surface → `hu-03`). Primary outcome: an operator action to apply a justified
penalty (reason required) to a team in a supervised session and see the score reflect the deduction; no
ranking/audit/history (later HUs).

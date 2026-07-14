# HU-37 + HU-39 — Driver brief
_Driver reads only this file. The full prompt + context are for the subagent
and human review; the driver never loads them._

**Nature of this HU:** greenfield feature build — one merged workstream (DES-99)
landing HU-37's append-only `ScoreEntry` ledger **and** HU-39's derived per-session
`Ranking` together, because the ranking is derived directly from the ledger and both
share the score-event flow. Each phase is **ledger-first, then ranking-second**.
`scoring-monitoring-service` is empty scaffold — build all four layers fresh; do not
look for existing scoring code to extend. **Out of scope (do not authorize a subagent
to build):** `Penalty`/`ApplyPenalty`/`Proxy` (HU-38), audit/monitoring projections
(HU-40), the QR/treasure-hunt ledger path (blocked on an upstream `TargetResolved`
event that does not exist yet), any mutable session total, and any ranking computed
in session-operations.

## Slice
| HU | DES | PRD | Service | Branch | Base |
|----|-----|-----|---------|--------|------|
| HU-37 + HU-39 — Ledger de puntaje y ranking en tiempo real | DES-99 | DES-85 | scoring-monitoring-service | feature/hu-37-39-ledger-ranking | develop |

## Required pattern(s) → owning phase
- `Strategy` (phase X.1 + X.2) — score entries come from interchangeable scoring policies and ranking depends on tie-break policy — obligation: `IScorePolicy` and `IRankingPolicy` as **interface + single `sealed` impl in `Domain/Services/`**, injected/selected at runtime in `RecordScoreEntryHandler` / `RecalculateRankingHandler`; **no** scoring or tie-break `if`/`switch` in handlers, consumers, or the projection. Single impl, no selector until a 2nd variant lands.
- `Proxy` — **not mandated** here (it belongs to HU-38/HU-40). Ranking reads inherit the standard gateway + `AuthorizationBehaviour` guard; no new gate.
- Transport: **RabbitMQ/MassTransit** — consume `session-answer-registered` (idempotent), publish `scoring-score-entry-registered` **post-commit**; the record flow must not depend on the broker. **SignalR** — push the refreshed ranking (`/hubs/scoring`, `RankingChanged`), refresh driven by **consuming** the score event (async).

## Per phase — gate + owned pattern
| Phase | Gate | Pattern |
|-------|------|---------|
| X.1 Domain | Domain build; unit test per new type — `ScoreEntry.Grant` raises `ScoreEntryRegistered` and has **no mutator** (append-only), team total is a fold not a field; `ScoreValue` validity; `ResolutionTimeRankingPolicy` orders descending total + `ResolutionTime` tie-break + equal/non-comparable share rank; no mutable total; no runtime-authority concept | `Strategy` (`IScorePolicy`/`IRankingPolicy`, interface + `sealed` impl in `Domain/Services/`) |
| X.2 Application | App build; consumer/handler/validator tests — correct answer → **exactly one** Grant `ScoreEntry`; incorrect → none; **redelivered** `TriviaAnswerSubmissionId` → no second entry (idempotent); `RecalculateRanking` ordered snapshot honors tie-break; policies **injected** (no branching); `ScoreEntryRegistered` raised for post-commit publish; ranking refresh runs off the **consumed** `ScoreEntryRegistered`, not a sync call; consumed copy `[EntityName("session-answer-registered")]`, published `[EntityName("scoring-score-entry-registered")]` | `Strategy` (consumed) |
| X.3 Infrastructure | Infra build; `ef migrations add AddScoringLedgerAndRanking` succeeds (append-only ledger + one-per-session Ranking); repo integration test round-trips append-only `ScoreEntry` + proves **unique-index dedupe** on `(sourceEntityType, sourceEntityId)`; Ranking snapshot replace round-trips; MassTransit test — publish to `session-answer-registered` → `ScoreEntry` written + `ScoreEntryRegistered` published; **broker outage does not fail the record path**; guard the known integration-test hang (bounded bus) | — |
| X.4 Api | `GET /api/sessions/{liveSessionId}/ranking` → rows ordered **descending total, `ResolutionTime` tie-break, shared rank on ties**; SignalR test — `RankingRefreshed` pushes `RankingChanged` to the session group; ranking reads inherit standard gateway guard (no ad-hoc role `if`, no new Proxy); no mutable total in any payload; coverage gate (ADR-0005) | — (ranking order visible in contract) |

Commit subjects — copy each phase's exact subject from the prompt's Steps 5–8 verbatim:
- X.1 `feat(scoring-monitoring): phase X.1 - domain layer (HU-37+HU-39)`
- X.2 `feat(scoring-monitoring): phase X.2 - application layer (HU-37+HU-39)`
- X.3 `feat(scoring-monitoring): phase X.3 - infrastructure layer (HU-37+HU-39)`
- X.4 `feat(scoring-monitoring): phase X.4 - api layer (HU-37+HU-39)`

Trailer (every phase): `Ref: HU-37` / `Ref: HU-39` / `Ref: DES-99` / `Ref: DES-85`

## Acceptance criteria
**HU-37 (ledger):**
- when valid evidence (QR resolved or correct trivia answer) or a penalty impacts the score, the team's score updates
- each score change produces a `ScoreEntry` recording its origin (`sourceEntityType` distinguishing e.g. `TargetResolution` vs `TriviaAnswerSubmission`)
- the team total is obtained from existing entries (derived, not stored)
- each score entry publishes a domain event (`ScoreEntryRegistered`) to RabbitMQ for secondary recalc/projection, without the main flow depending on RabbitMQ

**HU-39 (ranking):**
- ranking shown ordered high → low total score
- ties resolved by the defined `ResolutionTime` criterion
- ranking refreshed after every score-affecting fact
- the ranking projection sources from the score ledger
- ranking available to participants and operation in real time, broadcast over SignalR when it changes
- the projection updates by consuming the RabbitMQ score events, async from the main flow

> **Scope note for the driver:** the concrete path built this slice is the **trivia**
> path (`AnswerRegisteredIntegrationEvent`). The QR/`TargetResolution` source is
> modeled as a first-class `ScoreSourceType` but **not yet fed** — its upstream
> `TargetResolved` event does not exist yet. Do not authorize wiring a fake producer;
> flag it as a cross-service item at Stop 2.

## Endpoints + smoke (driver verifies at Stop 2)
- `GET /api/sessions/{liveSessionId}/ranking` — ordered ranking snapshot for the session; expect **200** and rows ordered descending by total with `ResolutionTime` tie-break (equal/non-comparable teams share rank). Response = the ranking snapshot shape (the frontend contract).
- **Score-event smoke:** publish `AnswerRegisteredIntegrationEvent` (`IsCorrect=true`) to exchange `session-answer-registered` → expect one `ScoreEntry` grant recorded and `scoring-score-entry-registered` published; a **redelivery** of the same `TriviaAnswerSubmissionId` records **no** second entry.
- **SignalR smoke:** a client on `/hubs/scoring` (session group) receives a `RankingChanged` frame after a score event.

## Frontend slice
Human-driven — see Steps 9 (generate plan) + 9b (implement it) of
`prompt_example_feature_hu37-39.md`. Real-time ranking surface (`@frontend/AGENTS.md`,
participant + operator); pick the plan exemplar by shape (small 1-endpoint + 1-hub
surface → `hu-03`). Primary outcome: render the ordered session ranking from
`GET …/ranking` and update it live on `RankingChanged`, presenting shared rank on ties;
no client-side total or ranking computation.

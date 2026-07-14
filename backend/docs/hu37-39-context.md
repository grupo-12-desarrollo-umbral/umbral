# HU-37 + HU-39 Context — Ledger de puntaje y ranking en tiempo real

> Paste this section into any agent session that needs context for the merged
> HU-37 + HU-39 slice (DES-99). Last updated: 2026-07-14 |
> Branch: `feature/hu-37-39-ledger-ranking`

## State

- **DES-99 (HU-37 + HU-39):** **Todo**, labels: `backend-only`, `canon-realign`,
  `svc:scoring-monitoring-service`, `Feature`, `ready-for-agent`.
- **Resolved mode: feature flow (greenfield).** `scoring-monitoring-service` is
  greenfield — there is no existing scoring code to realign or rebuild. The
  `canon-realign` label reflects the **2026-07-14 merge reword** that absorbed
  DES-51 (HU-37) and DES-54 (HU-39) into this one ticket, *not* an existing-code
  realignment. There is **no `⚠️ Deuda de canon` / `⚠️ Nota de canon` comment**
  on DES-99 (comment list is empty), so the issue body / AC checklist **is**
  authoritative. Canon source is the standard feature-flow doc set.
- **Supersession:** DES-99 is the **successor** (created 2026-07-14). It absorbs
  DES-51 and DES-54, which are **cancelled**. DES-99 is not itself superseded and
  does not appear in any superseded column — proceed.
- **Merged identity (per human decision 2026-07-14):** artifacts use the combined
  basename `hu37-39`; every phase commit carries **both** `Ref: HU-37` and
  `Ref: HU-39`. HU-37 = the append-only `ScoreEntry` ledger; HU-39 = the derived
  session `Ranking`. They are one backend workstream because the ranking is
  derived directly from the ledger and both share the same score-event flow
  ("se implementan como un único workstream backend").
- **Predecessors (same service): none.** Greenfield. No same-service ticket is
  Done or In Progress.
- **PRD:** DES-85 → local file
  `backend/docs/prd/DES-85-primera-implementacion-de-scoring-monitoring-service-hu-37-a-hu-40.md`
  (authoritative; not re-fetched from Linear).
- **Branch:** `feature/hu-37-39-ledger-ranking`, base `develop` (no same-service
  predecessor In Progress).

## Required design patterns

- **`Strategy`** — mandated for **both** HU-37 and HU-39
  (`required_patterns_matrix.md` rows: `HU-37 | Strategy` — "score ledger entries
  come from interchangeable scoring policies (`ScorePolicy`)"; `HU-39 | Strategy`
  — "real-time ranking depends on score-policy outcomes and tie-breaking
  (`RankingPolicy`, `ResolutionTime`)"). Also mandated by the service `CONTEXT.md`
  "Required Patterns → Strategy" and PRD Implementation Decisions ("The required
  `Strategy` pattern is mandatory here for `ScorePolicy`, `RankingPolicy`, and
  mode-specific scoring variation … must not be spread across handlers through
  conditionals").
  - **Phase owners:** `IScorePolicy` in X.1 Domain, consumed in X.2 Application
    (`RecordScoreEntry`); `IRankingPolicy` in X.1 Domain, consumed in X.2
    Application (`RecalculateRanking`).
  - **Concrete obligation** (per `adr/0012-*` placement + the HU-38 sibling
    convention): each policy is an **interface + a single `sealed` concrete impl
    in `Domain/Services/`**, injected and selected at runtime by the handler — no
    scoring/tie-break `if`/`switch` in handlers, consumers, or the `Ranking`
    projection. Single impl, **no selector** until a second variant lands (kill
    ceremony per the overengineering checklist).
- **`Proxy`: not mandated here.** The matrix assigns `Proxy` to HU-38 and HU-40,
  **not** HU-37/HU-39. The ranking read surface is exposed to participants and
  operators and inherits the standard gateway + `AuthorizationBehaviour` guard
  (ADR-0001/0002) — **no new pattern gate**. Do not attach `Proxy` to this slice.
- **Transport (not a GoF pattern, but a mandated gate):**
  - HU-37 → **RabbitMQ via MassTransit** (`required_patterns_matrix.md` real-time
    map: `HU-37 | RabbitMQ`): consume `AnswerRegisteredIntegrationEvent`, publish
    `ScoreEntryRegistered`.
  - HU-39 → **SignalR** (`HU-39 | SignalR / WebSockets`): push the refreshed
    ranking; the ranking projection is refreshed by **consuming** the published
    `ScoreEntryRegistered` over RabbitMQ (async from the record flow).

## What predecessors have already landed

`scoring-monitoring-service` is **greenfield**: only the target-folder scaffold
exists (`README.md`, `CONTEXT.md`, `structure.md`, `src/{Domain,Application,
Infrastructure,Api}` gitkeeps, `Application/{Scores,Metrics,Alerts,Common}`,
`Directory.*.props`, empty test projects). There is **no** same-service domain,
application, infrastructure, API, or frontend code to build on, and **no** captured
coverage baseline.

The build-on surface is entirely **cross-service seams** this slice consumes:

- **HU-34 / DES-46 (session-operations) — BUILD-ON (consumed contract).** Publishes
  `AnswerRegisteredIntegrationEvent`
  (`session-operations-service/src/Application/Sessions/Common/AnswerRegisteredIntegrationEvent.cs`):
  `(LiveSessionId, TeamId, TriviaAnswerSubmissionId, TriviaSubstageSnapshotId,
  QuestionSequenceOrder, SelectedOptionSequenceOrder, IsCorrect, ScoreValue,
  SubmittedAt)`. This is the **concrete first ledger input** pinned by the PRD
  ("Canonical demo flow", 2026-07-12): correct trivia answer → `ScoreEntry` grant.
- **MassTransit migration #164/#165/#166 (session-operations) — BUILD-ON
  (conventions).** Establishes MassTransit-native publish/consume: vanilla
  RabbitMQ topology, per-type exchange named via `[EntityName(...)]`, broker
  host/creds from `RabbitMqOptions` (config), Generic-Host-managed bus. Exemplars:
  `Infrastructure/Messaging/MassTransitMessagingRegistration.cs`,
  `Application/Sessions/Common/QuestionClosedIntegrationEvent.cs`
  (`[EntityName("session-question-closed")]`),
  `Application/Sessions/EventHandlers/PublishQuestionClosedIntegrationEventHandler.cs`
  (post-commit `IPublishEndpoint.Publish` with a 5 s timeout, failures swallowed).
  **#166 removes the topic exchange `umbral.session-operations`**, so the consumer
  must bind the **per-type exchange `session-answer-registered`**, not the routing
  key `session.answer.registered`.
- **HU-33B / DES-45 (session-operations) — available seam (not the first path).**
  Publishes `QuestionClosedIntegrationEvent` (`[EntityName("session-question-closed")]`)
  and `SessionResultsFinalizedIntegrationEvent`. Relevant to the HU-39 AC "ranking
  refreshes after … trivia-question close", but the concrete first refresh path is
  the score-event flow, so treat question-close as a later ranking-refresh input,
  not X.1–X.4 scope for the initial slice.
- **HU-31 / DES-42 (treasure-hunt QR) — OPEN DEPENDENCY.** HU-31 landed
  `TargetResolution` server-side in session-operations, but **no `TargetResolved`
  integration event is published anywhere in the repo yet** (grep: none). The
  HU-37 AC names QR-resolved evidence as a ledger source, but the upstream fact is
  not yet on the wire. See "Known quirks" and the prompt's Rationale — scope the
  concrete consume path to `AnswerRegisteredIntegrationEvent` and model the QR
  source as a first-class but **not-yet-wired** `ScoreSourceType`.

**Coverage:** no baseline exists (greenfield); establish the ADR-0005 aggregate
gate at X.4.

## What this HU adds

| Concern | New work |
|---|---|
| Score ledger (HU-37) | `ScoreEntry` **append-only** aggregate root: every score change is an immutable ledger fact; the team total is **derived** by summing entries, never a mutable field. |
| Source traceability (HU-37) | Each entry records `sourceEntityType` (`TargetResolution` \| `TriviaAnswerSubmission` \| `Penalty`) + `sourceEntityId`, so every change is explainable. |
| Score policy (HU-37) | `IScorePolicy` (`Strategy`) computes the awarded `ScoreValue` for an accepted outcome — no scoring `if`/`switch` in the handler. |
| Consume runtime fact (HU-37) | MassTransit `IConsumer<AnswerRegisteredIntegrationEvent>` bound to `session-answer-registered` → records one `ScoreEntry` grant for a **correct** answer; **idempotent** on `sourceEntityId`. |
| Publish score fact (HU-37) | Post-commit publish of `ScoreEntryRegistered` (`[EntityName("scoring-score-entry-registered")]`) for secondary recalc/projection — the record flow **does not depend** on RabbitMQ. |
| Ranking derivation (HU-39) | `Ranking` derived read-model, **one per `LiveSession`**, projected from `ScoreEntry` facts via `IRankingPolicy` (`Strategy`). |
| Ranking order + tie-break (HU-39) | Descending total score; tie → lower comparable `ResolutionTime` ranks first; equal / non-comparable `ResolutionTime` share rank. |
| Ranking refresh (HU-39) | Refreshed after every score-affecting fact by **consuming** `ScoreEntryRegistered` over RabbitMQ (async from the record flow), raising `RankingRefreshed`. |
| Real-time push (HU-39) | `RankingRefreshed` → SignalR broadcast of the new snapshot to the session's participants + operators. |
| Read surface (HU-39) | `GET /api/sessions/{liveSessionId}/ranking` returns the ordered snapshot (participant + operator). |

**Explicitly deferred (out of this slice):**
- **`Penalty` child + `ApplyPenalty` + `PenaltyPolicy`/`Proxy`** → HU-38 (DES-53).
  `ScoreEntryType`/`ScoreSourceType` carry a `Penalty` case for completeness, but
  only the **Grant** path is constructed here; do not build the operator penalty
  command, the `Penalty` entity, or its authorization.
- **`AuditHistory`, monitoring/metrics projections, detailed score-history reads**
  → HU-40 (the pre-made `Application/{Metrics,Alerts}` folders are for that HU).
- **Treasure-hunt QR ledger path** → blocked on the upstream `TargetResolved`
  event (see open dependency above).

## Touched surfaces

- `backend/services/scoring-monitoring-service` (all four layers — first delivery).
- **API contract boundary (backend ↔ frontend):** `GET /api/sessions/{liveSessionId}/ranking`
  (ordered snapshot) + a SignalR hub (`/hubs/scoring`) pushing `RankingChanged`.
- **Cross-service integration boundary (consume):** binds `session-answer-registered`
  (from session-operations). **(publish):** `scoring-score-entry-registered` for
  downstream secondary recalc/projection.
- **Frontend:** human-driven ranking/leaderboard slice (Steps 9 / 9b of the prompt).
- Boundary rule: this service **consumes** runtime facts and **derives** views; it
  never owns session progression, admission, clue release, or evidence acceptance
  (PRD "Out of Scope").

## Committed phases

| Commit | Phase | Description |
|---|---|---|
| _(none yet)_ | | |

## Known quirks / gotchas

- **`AnswerRegisteredIntegrationEvent` producer lacks `[EntityName]`.** Unlike
  `QuestionClosedIntegrationEvent`, the producer record carries **no**
  `[EntityName]` and its XML doc still cites the legacy routing key
  `session.answer.registered`. DES-99 pins the target: bind the **per-type
  exchange `session-answer-registered`** (post-#166). The scoring side declares
  its **own** copy of the contract record decorated
  `[EntityName("session-answer-registered")]` with a **structurally identical**
  shape (separate deployable — no shared type). If the producer has not yet been
  given the matching `[EntityName]`, that is a cross-service contract item to flag
  at Stop 2, not to fix from this service. See
  `rabbitmq-clue-release-audit-scope-handoff-2026-07-11.md` and
  `masstransit-follow-up-fixes-handoff-2026-07-12.md`.
- **scoring-monitoring is the first production `IConsumer` in the repo.** Only a
  test (`session-operations …/Messaging/MassTransitQuestionClosedPublishTests.cs`)
  references `IConsumer<>` today — there is **no** in-repo consumer to mirror.
  Follow the `rabbitmq-events-dotnet` skill + the session-ops **publish** wiring
  for topology/options; the consumer itself is new ground.
- **MassTransit integration-test hang risk.** Per
  `gh-164-masstransit-integration-test-hang-postmortem-2026-07-12.md` and local
  memory, the session-ops messaging/integration suite has hung. Budget for this at
  X.3: prefer a bounded, harness-controlled bus (test harness / in-memory or a
  Testcontainers broker with a hard timeout) and do **not** let a publish/consume
  round-trip block the suite indefinitely.
- **Idempotency is a ledger invariant, not an afterthought.** The ledger is
  append-only *and* the consumer may redeliver, so dedupe on
  (`sourceEntityType`, `sourceEntityId`) — enforce it with a unique index (X.3)
  and a pre-insert check (X.2). A redelivered `AnswerRegisteredIntegrationEvent`
  must not create a second grant.
- **Total is derived, never stored.** `bd_umbral_entity_spec.md` §Team keeps
  `currentScore` only as an *optional cached view derived from `ScoreEntry`* —
  do not introduce a mutable authoritative total in this service.
- **New `Application/Rankings/` area.** The scaffold pre-created
  `Application/{Scores,Metrics,Alerts,Common}` but **not** `Rankings`. Create
  `Application/Rankings/` for HU-39; `Metrics`/`Alerts` stay empty for HU-40.
- **No `SessionMode` / no runtime authority.** Reject any model that computes
  ranking inside session-operations or that treats this service as the owner of
  session state (PRD "Out of Scope", `CONTEXT.md` Boundary Rules).

## Per-phase derivation — authoritative for implementation

> Primary source for the phase subagent (per `backend-agent.md` "Read first").
> Feature-flow canon: `bd_umbral_entity_spec.md` §ScoringMonitoring (ScoreEntry /
> Penalty / Ranking, lines ~700–795), `services/scoring-monitoring-service/CONTEXT.md`,
> PRD DES-85 (Implementation / Testing Decisions), and the MassTransit / SignalR
> exemplars in `session-operations-service`. Open a cited canon section only to
> fill a gap a block leaves open. Each phase is **ledger-first, then
> ranking-derived-from-ledger** — one merged workstream.

### Phase X.1 — Domain
**Derive** (`bd_umbral_entity_spec.md` §ScoreEntry/Ranking, `CONTEXT.md` Language,
PRD Implementation Decisions):
- `ScoreEntry` **aggregate root**, append-only ledger fact —
  `scoreEntryId, liveSessionId, teamId, entryType (ScoreEntryType), reasonCode,
  scoreValue (ScoreValue VO), recordedAt, sourceEntityType (ScoreSourceType),
  sourceEntityId, recordedByUserId`. Construction only via a **`Grant` factory**
  (`ScoreEntry.Grant(...)`) that raises `ScoreEntryRegistered`; **no mutators**
  (append-only — guard any state change with `ScoreEntryIsAppendOnlyException`).
  The team total is a pure fold over entries, never a field.
- `ScoreValue` **value object** — integer score quantity under domain rules
  (reject invalid magnitudes via `InvalidScoreValueException`; grants are
  non-negative). Vocabulary: `ScoreValue`, not "points"/"score number".
- `ResolutionTime` **value object** — tie-break criterion; **comparable or
  non-comparable** (equal / non-comparable ⇒ shared rank). Vocabulary:
  `ResolutionTime`, not "elapsed time".
- `ScoreEntryType` enum (`Grant`, `Penalty`, `Correction`) and `ScoreSourceType`
  enum (`TargetResolution`, `TriviaAnswerSubmission`, `Penalty`). Only `Grant` +
  `TriviaAnswerSubmission` are exercised this slice; the others are declared for
  the ledger's completeness (Penalty → HU-38; TargetResolution → blocked upstream).
- `Ranking` **derived read-model aggregate**, one per `LiveSession` —
  `rankingId, liveSessionId, generatedAt, calculationVersion` + ordered rows
  (`teamId, position, totalScore, resolutionTime`). Built (not mutated ad hoc)
  from `ScoreEntry` facts; a refresh replaces the snapshot and raises
  `RankingRefreshed`.
- `IScorePolicy` (**Strategy**) — `ScoreValue Award(accepted-outcome)`; one
  `sealed` impl (e.g. `SnapshotScorePolicy` — awards the event's snapshot
  `ScoreValue`) in `Domain/Services/`.
- `IRankingPolicy` (**Strategy**) — orders `(teamId, totalScore, ResolutionTime)`
  rows: descending `totalScore`; tie → lower comparable `ResolutionTime`; equal /
  non-comparable ⇒ shared `position`. One `sealed` impl
  (`ResolutionTimeRankingPolicy`) in `Domain/Services/`.
- Domain events: `ScoreEntryRegistered`, `RankingRefreshed`.

**Target files** (create — file to mirror):
- create `Domain/Common/BaseEntity.cs` — mirror `session-operations-service/src/Domain/Common/BaseEntity.cs` (domain-event collection)
- create `Domain/Entities/ScoreEntry.cs` — mirror an event-raising aggregate, e.g. `session-operations-service/src/Domain/Entities/LiveSession.cs` (factory + `AddDomainEvent`, append-only)
- create `Domain/Entities/Ranking.cs` — mirror `LiveSession.cs` (aggregate with owned ordered rows)
- create `Domain/ValueObjects/{ScoreValue,ResolutionTime}.cs` — mirror `session-operations-service/src/Domain/ValueObjects/*.cs`
- create `Domain/Enums/{ScoreEntryType,ScoreSourceType}.cs`
- create `Domain/Services/{IScorePolicy,SnapshotScorePolicy,IRankingPolicy,ResolutionTimeRankingPolicy}.cs` — Strategy interface + `sealed` impl (convention per `hu38-brief.md`)
- create `Domain/Events/{ScoreEntryRegistered,RankingRefreshed}.cs`
- create `Domain/Exceptions/{ScoreEntryIsAppendOnlyException,InvalidScoreValueException}.cs`

**Pattern this phase owns:** `Strategy` (`IScorePolicy` + `IRankingPolicy` — interface + `sealed` impl in `Domain/Services/`, no branching).
**Gate:** Domain build; unit test per new type — `ScoreEntry.Grant` raises `ScoreEntryRegistered` and exposes **no** mutator (append-only); `ScoreValue` validity rules; `ResolutionTimeRankingPolicy` orders **descending total, `ResolutionTime` tie-break, equal/non-comparable share rank**; team total is a fold over entries (no stored total); policies are interface + `sealed` impl in `Domain/Services/` with no scoring/tie-break `if`/`switch`.

### Phase X.2 — Application
**Derive** (PRD "consume completed runtime facts" / CQRS-separated reads;
MassTransit consume/publish conventions):
- **Consume contract:** `AnswerRegisteredIntegrationEvent` — a **local copy** of
  the session-ops record, decorated `[EntityName("session-answer-registered")]`,
  structurally identical, in `Application/Scores/Common/`.
- **Consumer:** `AnswerRegisteredConsumer : IConsumer<AnswerRegisteredIntegrationEvent>`
  (`Application/Scores/Consumers/`) → on `IsCorrect == true` sends
  `RecordScoreEntryCommand`; on `false` records nothing. **Idempotent**: skip if a
  `ScoreEntry` already exists for (`TriviaAnswerSubmission`, `TriviaAnswerSubmissionId`).
- **Command:** `RecordScoreEntryCommand` + handler + validator
  (`Application/Scores/Commands/RecordScoreEntry/`) — awards via injected
  `IScorePolicy`, persists an append-only `ScoreEntry` `Grant`, raises
  `ScoreEntryRegistered`. Validator: ids present, source type/id set.
- **Post-commit publish:** `PublishScoreEntryRegisteredIntegrationEventHandler :
  INotificationHandler<ScoreEntryRegistered>` (`Application/Scores/EventHandlers/`)
  → `IPublishEndpoint.Publish(new ScoreEntryRegisteredIntegrationEvent(...))`
  (`[EntityName("scoring-score-entry-registered")]`, `Application/Scores/Common/`),
  5 s timeout, failures logged + swallowed — mirror
  `PublishQuestionClosedIntegrationEventHandler`. The record flow must **not**
  depend on the broker.
- **Ranking projection consumer:** `ScoreEntryRegisteredConsumer :
  IConsumer<ScoreEntryRegisteredIntegrationEvent>` (`Application/Rankings/Consumers/`)
  → sends `RecalculateRankingCommand` — this is the **async** refresh path
  (HU-39 AC "consumiendo los eventos de puntaje … de forma asíncrona respecto al
  flujo principal"); the record handler never calls ranking synchronously.
- **Command:** `RecalculateRankingCommand` + handler
  (`Application/Rankings/Commands/RecalculateRanking/`) — reads the session's
  `ScoreEntry` folds, orders via injected `IRankingPolicy`, replaces the `Ranking`
  snapshot, raises `RankingRefreshed`.
- **Broadcast trigger:** `BroadcastRankingRefreshedHandler :
  INotificationHandler<RankingRefreshed>` (`Application/Rankings/EventHandlers/`)
  → `IRankingBroadcaster.RankingChanged(liveSessionId, snapshot)` (app interface;
  impl lands in Api at X.4).
- **Query:** `GetRankingSnapshotQuery` + handler + `RankingSnapshotDto`
  (`Application/Rankings/Queries/GetRankingSnapshot/`) — ordered snapshot for a
  `LiveSession`.
- **Interfaces:** `Application/Common/Interfaces/{IScoreEntryRepository,
  IRankingRepository,IRankingBroadcaster}.cs`.

**Target files** (create — file to mirror):
- create `Application/Scores/Common/AnswerRegisteredIntegrationEvent.cs` — mirror `session-operations-service/.../Common/AnswerRegisteredIntegrationEvent.cs` **+ add `[EntityName("session-answer-registered")]`**
- create `Application/Scores/Common/ScoreEntryRegisteredIntegrationEvent.cs` — mirror `session-operations-service/.../Common/QuestionClosedIntegrationEvent.cs` (`[EntityName]` shape)
- create `Application/Scores/Consumers/AnswerRegisteredConsumer.cs` — no in-repo consumer to mirror; follow `rabbitmq-events-dotnet` skill
- create `Application/Scores/Commands/RecordScoreEntry/{RecordScoreEntryCommand,RecordScoreEntryHandler,RecordScoreEntryValidator}.cs`
- create `Application/Scores/EventHandlers/PublishScoreEntryRegisteredIntegrationEventHandler.cs` — mirror `session-operations-service/.../EventHandlers/PublishQuestionClosedIntegrationEventHandler.cs`
- create `Application/Rankings/Consumers/ScoreEntryRegisteredConsumer.cs`
- create `Application/Rankings/Commands/RecalculateRanking/{RecalculateRankingCommand,RecalculateRankingHandler}.cs`
- create `Application/Rankings/EventHandlers/BroadcastRankingRefreshedHandler.cs`
- create `Application/Rankings/Queries/GetRankingSnapshot/{GetRankingSnapshotQuery,GetRankingSnapshotHandler,RankingSnapshotDto}.cs`
- create `Application/Common/Interfaces/{IScoreEntryRepository,IRankingRepository,IRankingBroadcaster}.cs`

**Pattern this phase owns:** `Strategy` consumed — `IScorePolicy` injected in `RecordScoreEntryHandler`, `IRankingPolicy` injected in `RecalculateRankingHandler`; **no** scoring/tie-break branching in handlers or consumers.
**Gate:** App build; consumer/handler tests — correct answer → exactly **one** `Grant` `ScoreEntry`; incorrect answer → none; **redelivered** submission id → no second entry (idempotent); `RecalculateRanking` produces an ordered snapshot honoring the `ResolutionTime` tie-break; policies injected (no branching); `ScoreEntryRegistered` raised for post-commit publish; ranking refresh runs off the **consumed** `ScoreEntryRegistered`, not a synchronous call from the record handler.

### Phase X.3 — Infrastructure
**Derive** (EF owned-type persistence + MassTransit wiring; greenfield — the
DbContext itself is new):
- **EF Core:** new `Infrastructure/Persistence/ScoringMonitoringDbContext.cs`;
  `ScoreEntryConfiguration` (append-only table; `ScoreValue` conversion; index on
  `(liveSessionId, teamId)`; **unique index on `(sourceEntityType, sourceEntityId)`**
  for idempotency); `RankingConfiguration` (`Ranking` + owned ordered rows; one row
  set per `liveSessionId`, replaced on refresh). Migration
  `AddScoringLedgerAndRanking`. Repos `ScoreEntryRepository`, `RankingRepository`.
- **MassTransit:** `Infrastructure/Messaging/MassTransitMessagingRegistration.cs`
  + `RabbitMqOptions.cs` — mirror the session-ops registration
  (`UsingRabbitMq` + `RabbitMqOptions` + `ConfigureEndpoints`); register
  `AnswerRegisteredConsumer` and `ScoreEntryRegisteredConsumer`. Per-type exchanges
  via `[EntityName]`; the answer consumer binds `session-answer-registered`.
- Grep the model snapshot rather than full-reading it once one exists.

**Target files** (create — file to mirror):
- create `Infrastructure/Persistence/ScoringMonitoringDbContext.cs` — mirror `session-operations-service/src/Infrastructure/Persistence/*DbContext.cs`
- create `Infrastructure/Persistence/Configurations/{ScoreEntryConfiguration,RankingConfiguration}.cs` — mirror an owned-type config in session-ops / mission-design
- create `Infrastructure/Persistence/Repositories/{ScoreEntryRepository,RankingRepository}.cs`
- create `Infrastructure/Persistence/Migrations/*_AddScoringLedgerAndRanking.cs` (via `dotnet ef migrations add`)
- create `Infrastructure/Messaging/{MassTransitMessagingRegistration,RabbitMqOptions}.cs` — mirror `session-operations-service/src/Infrastructure/Messaging/MassTransitMessagingRegistration.cs`

**Pattern this phase owns:** none (persistence + composition wiring).
**Gate:** Infra build; `dotnet ef migrations add AddScoringLedgerAndRanking` succeeds and represents the append-only ledger + one-per-session ranking; repository integration test round-trips an append-only `ScoreEntry` and proves the **unique-index dedupe** on `(sourceEntityType, sourceEntityId)`; `Ranking` snapshot replace round-trips; a MassTransit integration test publishes `AnswerRegisteredIntegrationEvent` to `session-answer-registered` → a `ScoreEntry` is written and `ScoreEntryRegistered` published; a **broker outage does not fail** the record path (publish swallowed). Guard against the known integration-test hang (bounded/harness bus).

### Phase X.4 — Api
**Derive** (ranking read endpoint + SignalR broadcast; ADR-0005 coverage):
- `GET /api/sessions/{liveSessionId}/ranking` → ordered `RankingSnapshotDto`
  (participant + operator; standard gateway auth — no new Proxy). Map domain
  exceptions in a `ProblemDetailsExceptionHandler`.
- SignalR: `Api/Hubs/ScoringHub.cs` mapped `/hubs/scoring` (group per
  `liveSessionId`); `Api/Hubs/SignalRRankingBroadcaster.cs : IRankingBroadcaster`
  using `IHubContext<ScoringHub>` → `RankingChanged` to the session group. Mirror
  `session-operations-service/src/Api/Hubs/{SessionsHub,SignalRTeamBoardBroadcaster}.cs`
  and `Program.cs`'s `MapHub<SessionsHub>("/hubs/sessions")`. Wire
  `AddMassTransitMessaging` + `AddSignalR` in `Program.cs`.

**Target files** (create — file to mirror):
- create `Api/Endpoints/RankingEndpoints.cs` — mirror a minimal-API endpoint group in session-ops
- create `Api/Hubs/ScoringHub.cs` — mirror `session-operations-service/src/Api/Hubs/SessionsHub.cs`
- create `Api/Hubs/SignalRRankingBroadcaster.cs` — mirror `session-operations-service/src/Api/Hubs/SignalRTeamBoardBroadcaster.cs`
- create `Api/Program.cs` — mirror `session-operations-service/src/Api/Program.cs` (MapHub + messaging + DI)
- create `Api/Services/ProblemDetailsExceptionHandler.cs` — mirror the session-ops / mission-design handler

**Pattern this phase owns:** none new (ranking order — the `Strategy` outcome — must be **visible in the snapshot contract**; ranking reads inherit the standard gateway guard, no `Proxy`).
**Gate:** endpoint test — `GET …/ranking` returns rows **ordered descending by total with the `ResolutionTime` tie-break** and equal/non-comparable teams sharing rank; SignalR test — a `RankingRefreshed` pushes `RankingChanged` to the session group; API request/response contains **no** mutable session total and does not compute ranking in session-operations; service coverage meets the ADR-0005 aggregate gate.

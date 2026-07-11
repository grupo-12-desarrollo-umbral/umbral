# HANDOFF — RabbitMQ audit-history scope: clue release on the bus, session-state off it

**Date:** 2026-07-11
**Focus for next session:** Execute the RabbitMQ consumer side (scoring-monitoring) and the clue-release publish, now that the audit-history event scope is decided and the affected Linear tickets are updated.

## What this session decided

The prior handoff (`HANDOFF.md`, RabbitMQ academic-requirements alignment) concluded the backlog was broadly aligned but the **consumer side is the critical gap**. This session validated that against the actual code and Linear tickets, then resolved the one real coherence gap it found and updated the tickets.

### Verification results (all confirmed)

- **Publisher code is correct and well-built**, not something to "fix". `session-operations-service` publishes three integration events (`AnswerRegistered`, `QuestionClosed`, `SessionResultsFinalized`) via a hand-rolled `RabbitMqIntegrationEventPublisher` (RabbitMQ.Client 7.1.2) behind `IIntegrationEventPublisher`. Publishing is correctly decoupled from the DB transaction (post-commit dispatch via `DispatchDomainEventsInterceptor` in `SavedChanges`, plus a non-blocking in-memory bounded channel hand-off). Best-effort by design (D-3): messages can be dropped on overflow/broker failure, no durable outbox. Not a bug — intentional.
- **No consumer exists anywhere.** `scoring-monitoring-service` is a pure empty scaffold (`.gitkeep` + build props only, 0 `.cs` files). The named consumer events (`ScoreEntry`, `PenaltyApplied`, `TargetResolved`, `EvidenceSubmissionRegistered`) live only in docs, not code.
- **Ticket ACs were more aligned than an earlier (stale) analysis claimed:** DES-42 (HU-31) already has explicit RabbitMQ publish ACs for `EvidenceSubmissionRegistered`/`TargetResolved` (updated 2026-07-10, carries `ScoreValue`). DES-51/DES-54 already have explicit consume ACs. DES-53/DES-56 already had correct RabbitMQ publish/consume ACs.

### The one real gap found

DES-56 (HU-40A, audit history) requires the history to show **four** categories — state changes, clue releases, evidence, penalties — and to build the history by **consuming RabbitMQ events**. But two of those categories had **no producer**: clue releases (DES-36/DES-37) and general session-state changes were never published. So DES-56 as written could not be satisfied by consuming RabbitMQ.

### User's scope decision

RabbitMQ carries **evidence + penalties + clue releases**, but **NOT** general session-state changes (pause/resume/cancel).
- `Finished` still reaches the broker via the existing `SessionResultsFinalizedIntegrationEvent` (a "game ended" business fact) — not lost.
- Session-state history in the audit view is sourced by **direct read** of session-operations, not event-sourced.
- Rationale: the 4 academic requirements are met by evidence→score→ranking; state-change auditing has no requirement payoff and would widen scope on unrelated Backlog tickets.

### "Team wins" question — resolved

No `TeamWon` event. A win is a **derived read** (top of `Ranking`, itself derived from the `ScoreEntry` ledger). Emitting a winner event would push scoring logic into the wrong bounded context. `SessionResultsFinalized` already covers the "game ended" trigger.

## MassTransit migration context (important for implementers)

GitHub issues **#164 / #165 / #166** migrate `session-operations` messaging from the hand-rolled `RabbitMQ.Client` publisher to **MassTransit over RabbitMQ**:

- **#164** (OPEN, `ready-for-agent`, unblocked): bootstrap `AddMassTransit(...).UsingRabbitMq(...)`, migrate `QuestionClosed`, publish via `IPublishEndpoint.Publish(...)` directly from the MediatR handler, exchange named via `[EntityName("session-question-closed")]`. Config from appsettings + env vars. Testcontainers.RabbitMq integration test.
- **#165** (blocked by #164): migrate `SessionResultsFinalized` (`session-results-finalized`) and `AnswerRegistered` (`session-answer-registered`).
- **#166** (blocked by #165): DELETE `RabbitMqIntegrationEventPublisher`, `IIntegrationEventPublisher`, `RabbitMqOptions`, drop `RabbitMQ.Client`. MassTransit becomes the single path. Refresh the `rabbitmq-events-dotnet` skill doc.

**Consequence:** all new publish/consume work must be MassTransit-native (`IPublishEndpoint.Publish(...)`, `[EntityName]`, `IConsumer<...>`), NOT the doomed `IIntegrationEventPublisher` seam. `ClueReleased` should be born on the bus — land #164 first so it never touches the hand-rolled publisher.

## Linear ticket edits applied this session

| Ticket | Change |
|---|---|
| [DES-92](https://linear.app/desarrollo-equipo-12/issue/DES-92) (**NEW** enabler) | Created: publish `ClueReleased` to RabbitMQ (MassTransit), covering both manual (DES-36) and conditional (DES-37) release via one shared contract. Blocked by #164, **blocks DES-56**. |
| [DES-36](https://linear.app/desarrollo-equipo-12/issue/DES-36) (HU-26 manual clue release) | Kept core ACs (ships independently); the `ClueReleased` publish AC was split out to DES-92. Added a short pointer note. |
| [DES-37](https://linear.app/desarrollo-equipo-12/issue/DES-37) (HU-27 conditional clue release) | Same as DES-36 — publish split to DES-92, pointer note added. |
| [DES-56](https://linear.app/desarrollo-equipo-12/issue/DES-56) (HU-40A audit history) | State changes now sourced by direct read (off RabbitMQ). Async-consume AC narrowed to clue releases + evidence + penalties via MassTransit `IConsumer<...>`. Scope note + #164 prerequisite. |

Exchange name convention chosen: `ClueReleased` → `[EntityName("session-clue-released")]` (kebab-case, matches #164/#165 spirit).

**Sequencing resolved:** the `ClueReleased` publish was split into enabler **DES-92** rather than gating DES-36/DES-37 on #164. Rationale: the core clue-release feature is messaging-independent and ships this sprint; its only consumer (DES-56) is Backlog, so there's no urgency to publish now; and doing it via the old seam would just create #166 cleanup. DES-92 is blocked by #164 (born MassTransit-native) and blocks DES-56 (lands when its consumer is built).

## Open items / recommended next actions

- **Build DES-51 first** (HU-37 ledger + consume `AnswerRegisteredIntegrationEvent` → write `ScoreEntry`). This is the linchpin that turns academic requirement #4 (demonstrate publish→consume→effect) from half-satisfied to real. Everything else (DES-54 ranking, DES-56 audit) is blocked behind the ledger.
- **Sequencing — RESOLVED.** The `ClueReleased` publish was split into enabler DES-92 (blocked by #164, blocks DES-56), so DES-36/DES-37 ship this sprint un-gated. Remaining decision for the team: prioritize #164 (still OPEN, `ready-for-agent`, unblocked) so DES-92 — and the whole MassTransit path — can proceed.
- **Optional clarity edits not yet applied:** DES-53 is dual-labeled scoring+session-operations but the penalty is applied in scoring-monitoring per DES-85 PRD — worth a one-line clarification. DES-85 talks in abstract "runtime facts"; pinning the canonical demo flow to the concrete `AnswerRegisteredIntegrationEvent` name would de-risk the first slice.

## Canonical sources

- Requirements: `backend/docs/requisitos-academicos-umbral-ucab.md`, `backend/docs/academic-requirements-canon.md`
- Scoring PRD: `backend/docs/prd/DES-85-*.md` (and Linear DES-85)
- Existing RabbitMQ contract doc: `backend/docs/hu33b-rabbitmq-contract.md`
- Prior handoff: `HANDOFF.md` (worktree root) — RabbitMQ academic-requirements alignment

## Suggested skills

- `rabbitmq-events-dotnet` — RabbitMQ/MassTransit design & implementation in .NET (note: skill doc is being refreshed for MassTransit conventions in #166).
- `cqrs-mediatr-aspnetcore` — event handlers, consumers, command/query boundaries.
- `ef-core-postgresql` — `ScoreEntry` ledger / audit persistence.
- `aspnet-backend-testing` — Testcontainers.RabbitMq publish/consume integration tests.
- `triage` / `to-issues` — if splitting or resequencing Linear issues.
- `handoff` — to compact the next session.

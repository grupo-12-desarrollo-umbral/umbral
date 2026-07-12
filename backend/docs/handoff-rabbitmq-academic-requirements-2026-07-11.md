# HANDOFF — RabbitMQ Academic Requirements Alignment

**Date:** 2026-07-11
**Focus for next session:** Continue validating and tightening RabbitMQ-related Linear backlog coverage against the UCAB academic requirements.

> Archived from the rolling `/HANDOFF.md` on 2026-07-12 (that file is gitignored and was being
> replaced by an unrelated HU-23 handoff). Preserved here so this RabbitMQ analysis is not lost.

## Summary

This session mapped the academic RabbitMQ requirements to current docs, code, and Linear issues. The main conclusion: the backlog is broadly aligned, but the **consumer side is still the critical gap**. Current code publishes some RabbitMQ events from `session-operations-service`, but `scoring-monitoring-service` is not implemented yet, so there is not yet a real publish → broker → consume → business effect workflow.

## Canonical Requirement Sources

- `backend/docs/requisitos-academicos-umbral-ucab.md`
- `backend/docs/academic-requirements-canon.md`
- `frontend/docs/requirements_traceability.md`
- `frontend/docs/umbral_user_stories.md`

Academic RabbitMQ requirements discussed:

- Publish domain events to RabbitMQ when evidence is registered or significant changes occur.
- Decouple asynchronous processes through RabbitMQ.
- Use RabbitMQ queues for secondary events such as audit, recalculation, and notifications.
- Demonstrate RabbitMQ publish and consume with at least one relevant business flow.

## Current Code State

RabbitMQ is currently implemented only as a publisher in `session-operations-service`.

Published events found in code:

- `AnswerRegisteredIntegrationEvent` — first valid trivia answer.
- `QuestionClosedIntegrationEvent` — question close.
- `SessionResultsFinalizedIntegrationEvent` — session reaches final results / finished state.

Relevant code anchors:

- `backend/services/session-operations-service/src/Application/Common/Interfaces/IIntegrationEventPublisher.cs`
- `backend/services/session-operations-service/src/Infrastructure/DependencyInjection.cs`
- `backend/services/session-operations-service/src/Application/Sessions/EventHandlers/PublishAnswerRegisteredIntegrationEventHandler.cs`
- `backend/services/session-operations-service/src/Application/Sessions/EventHandlers/PublishQuestionClosedIntegrationEventHandler.cs`
- `backend/services/session-operations-service/src/Application/Sessions/EventHandlers/PublishSessionResultsFinalizedIntegrationEventHandler.cs`
- RabbitMQ package: `backend/services/session-operations-service/src/Directory.Packages.props` (`RabbitMQ.Client` 7.1.2)
- RabbitMQ container: `backend/docker-compose.yml`

Important finding: there is **no consumer implementation yet**. `scoring-monitoring-service` is still the missing downstream service that should consume runtime facts and create scoring/audit/ranking projections.

## Linear Mapping

Primary RabbitMQ alignment issue:

- `DES-60` — **ENABLER - Publicación de eventos de dominio a RabbitMQ**
  - Directly covers evidence publishing, significant session changes, non-replacement of persistence, secondary consumers, and at least one end-to-end workflow.
  - Blocked by HU-31, HU-37A/HU-37, HU-40A.

Scoring/consumer side:

- `DES-85` — PRD for first `scoring-monitoring-service` implementation (HU-37 to HU-40).
  - Defines that `ScoringMonitoring` consumes completed runtime facts from `SessionOperations`.
  - Owns `ScoreEntry`, `Penalty`, `Ranking`, `AuditHistory`, monitoring projections.
- `DES-51` — HU-37, score ledger from validations, answers, and penalties.
  - Should consume accepted answer / target resolved / penalty-related facts and create `ScoreEntry` records.
  - Also says each `ScoreEntry` publishes `ScoreEntryRegistered` to RabbitMQ for secondary recalculation/projection support.
- `DES-53` — HU-38, justified penalties.
  - Explicitly requires RabbitMQ publish after transactional success, e.g. `PenaltyApplied` or `ScoreEntryRegistered`, for secondary recalculation and audit.
- `DES-56` — HU-40A, session event history.
  - Audit/history consumer candidate for runtime and scoring facts.
- `DES-54` — HU-39, real-time ranking.
  - Ranking derives from the score ledger after score-affecting facts.

Already-done publisher-side trivia/session tickets:

- `DES-45` — HU-33B, question close and final results. Done; current code publishes `QuestionClosedIntegrationEvent` and `SessionResultsFinalizedIntegrationEvent`.
- HU-34 / answer submission code exists and publishes `AnswerRegisteredIntegrationEvent` in current code.

Canceled MassTransit tickets seen during search:

- `DES-88`, `DES-89`, `DES-90` are canceled. Current implementation is still the raw `RabbitMQ.Client` publisher behind `IIntegrationEventPublisher`, not MassTransit.

## Flow Conclusions

Minimum academic demo flow should be:

- Trivia answer accepted in `session-operations-service`.
- Publish `AnswerRegisteredIntegrationEvent` to RabbitMQ.
- `scoring-monitoring-service` consumes it.
- Create `ScoreEntry` ledger record.
- Ranking/audit projections update from that ledger.

Current status of that demo:

- Publish side: exists.
- Broker: exists in Compose.
- Consume side: missing.
- Business effect: missing until DES-85/DES-51 implementation lands.

Treasure hunt flow should be:

- HU-31 validates QR / target evidence.
- Publish `EvidenceSubmissionRegistered` and/or `TargetResolved` after transactional success.
- `scoring-monitoring-service` consumes `TargetResolved` to create score entries and audit/projection updates.

Current status of treasure hunt RabbitMQ:

- HU-31 / `DES-42` is Backlog.
- It should probably get an explicit RabbitMQ AC if the team wants strong academic traceability.

Penalty flow should be:

- Operator applies penalty (`DES-53`).
- Persist penalty and score deduction transactionally in `scoring-monitoring-service`.
- Publish `PenaltyApplied` or `ScoreEntryRegistered` after success.
- Secondary audit/projection/recalc consumers process it.

Current status of penalties:

- DES-53 has explicit RabbitMQ AC.
- Not built yet.

Clue release flow:

- `DES-36` (manual clue release) and `DES-37` (conditional clue release) do **not** currently mention RabbitMQ.
- They require session history recording, team scoping, and duplicate prevention.
- Architecturally, a `ClueReleasedIntegrationEvent` could make sense for audit/history because clue release is a significant session event and crosses into `ScoringMonitoring` audit views.
- It is not required for the minimum academic RabbitMQ demo if answer/target → score ledger is completed.
- Potential backlog gap: DES-60 says “significant session changes”; if the team interprets clue release as significant, DES-36/DES-37 should gain an explicit event-publish AC or DES-60 should clarify which session changes count.

Session state audit:

- `DES-29` (HU-21) covers session-state audit.
- Search did not confirm an explicit RabbitMQ publish AC in DES-29.
- Not needed for the minimum demo, but could be another “significant session change” candidate depending on interpretation.

## Recommended Next Actions

- Decide the canonical minimum RabbitMQ demo: likely `AnswerRegisteredIntegrationEvent` → `ScoreEntry` via `scoring-monitoring-service`.
- Update `DES-51` if needed to explicitly say it consumes `AnswerRegisteredIntegrationEvent` from RabbitMQ and writes a `ScoreEntry`.
- Update `DES-42` if treasure hunt must also demonstrate RabbitMQ: add explicit publish of `EvidenceSubmissionRegistered` / `TargetResolved` after transactional success.
- Decide whether `ClueReleased` is a required RabbitMQ “significant session change” or only an in-service audit/history record.
- If yes, add an AC to `DES-36` / `DES-37` or add a follow-up issue for `ClueReleasedIntegrationEvent` and audit-history consumption.
- Keep RabbitMQ off the critical gameplay path: persistence and live UX should not depend on broker availability.

## Suggested Skills

- `rabbitmq-events-dotnet` — for any RabbitMQ design/implementation in .NET.
- `cqrs-mediatr-aspnetcore` — for event handlers, commands, queries, and application boundary design.
- `ef-core-postgresql` — for score ledger / audit persistence work.
- `aspnet-backend-testing` — for integration tests around publish/consume flows.
- `triage` — if updating or splitting Linear issues.
- `handoff` — if compacting the next session again.

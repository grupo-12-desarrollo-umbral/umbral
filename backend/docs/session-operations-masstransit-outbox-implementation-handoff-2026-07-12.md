# Handoff: SessionOperations MassTransit transactional outbox — implementation

Date: 2026-07-12. Scope: SessionOperations service only. All work is in the working tree,
**uncommitted**.

## What this session did

Implemented the MassTransit EF Core transactional outbox designed in
[`docs/session-operations-masstransit-outbox-design.md`](./session-operations-masstransit-outbox-design.md),
then drove it green against a purpose-built broker-unavailable regression suite. This removes the
~5s gameplay stall (and silent event loss) that occurred whenever RabbitMQ was unreachable, by
turning integration-event publishing into a local DB insert that commits atomically with the
business write and is drained to the broker asynchronously.

Preceding context (already captured elsewhere, referenced not duplicated):

- Root-cause diagnosis of the 5s stall:
  [`session-operations-masstransit-five-second-stall-handoff-2026-07-12.md`](./session-operations-masstransit-five-second-stall-handoff-2026-07-12.md).
- The chosen design, API surface, and rejected alternatives:
  [`session-operations-masstransit-outbox-design.md`](./session-operations-masstransit-outbox-design.md).

## What we accomplished (and why)

The outbox change itself (per the design doc):

- Added `MassTransit.EntityFrameworkCore` 8.4.1; mapped the outbox entities in `ApplicationDbContext`
  via `AddTransactionalOutboxEntities()`; generated migration
  `20260713020055_AddMassTransitTransactionalOutbox` (creates `InboxState` / `OutboxMessage` /
  `OutboxState`). Applied automatically by the existing `MigrateAsync` in `Api/Program.cs` and the
  test fixture.
- Wired `AddEntityFrameworkOutbox<ApplicationDbContext>(UsePostgres / UseBusOutbox / QueryDelay=1s /
  DuplicateDetectionWindow=30m)` in `MassTransitMessagingRegistration`.
- Split `DispatchDomainEventsInterceptor` into two phases: **pre-commit (`SavingChanges`)** runs the
  transactional-outbox publishers (so the `IPublishEndpoint.Publish` insert rides the same
  transaction as the business write) via a new `IOutboxDomainEventDispatcher` /
  `OutboxDomainEventDispatcher`; **post-commit (`SavedChanges`)** keeps the SignalR/orchestration
  MediatR notification fan-out, where re-loading state and re-entrant saves are safe. This is a
  deliberate improvement over the design's "move all dispatch pre-save" (which would have broadcast
  SignalR before the commit).
- Removed the 5s `PublishTimeout` / `CancelAfter` guards from the three publish handlers; a publish
  is now a local insert, so a failure is a DB fault that is logged **and rethrown** to roll the
  transaction back (no more silent loss). Updated their XML docs.
- `TriviaRoundOrchestratorFacade.CloseAndAdvanceAsync` unchanged — its stall disappears for free once
  the publish is a local insert.

## Why the tests were failing (three defects the implementation had, all now fixed)

The subagent's first pass compiled ("build passes") but had never had a green **run**. Diagnosing
took hang-dump analysis (`dotnet-dump`); the "hangs" were not the latency logic. In order of
discovery:

1. **Circular DI → `StackOverflowException` (production-affecting, the important one).** The
   bus-outbox `IPublishEndpoint` depends on `ApplicationDbContext`; `DispatchDomainEventsInterceptor`
   is attached to that context and eagerly depended (via `IOutboxDomainEventDispatcher` → publish
   handlers → `IPublishEndpoint`) on the publish endpoint → the DbContext options factory re-entered
   itself forever while building the context. The same `options.AddInterceptors(sp.GetServices<
   ISaveChangesInterceptor>())` pattern lives in production `Persistence/DependencyInjection.cs`, so
   **the real app would have crashed on its first save**, not just the tests. Fix: the interceptor
   resolves the outbox dispatcher **lazily** at `SaveChanges` time (context already exists → no
   re-entrancy), injecting `IServiceProvider` instead of the dispatcher; both DI sites now add the
   app's own interceptors by concrete type.

2. **Regression harness never started the bus.** MassTransit's `Publish` blocks on a bus-ready gate
   until the bus is started; the harness built a RabbitMQ bus but only called `BuildServiceProvider`.
   "Bus not started" is not "broker unavailable." Fix: start an **in-memory** bus (transport-agnostic
   outbox insert still writes to Postgres) and deliberately do not run the delivery service, so rows
   stay pending — which is the assertion we want.

3. **Outbox rows leaked across tests.** The shared-DB resets cleared `LiveSessions` but not the
   outbox tables. `BrokerUnavailableGameplayStallTests` created undelivered rows (in-memory bus,
   `loopback://` source addresses); a later real-app e2e test
   (`RoundClosePublicationEndToEndTests`) booted a real delivery service that stalled on those stale
   rows and drained an empty queue. Fix: clear `OutboxMessage` / `OutboxState` / `InboxState` in both
   the regression seed reset and `SessionOperationsApiWebApplicationFactory.ResetDatabaseAsync`.

## Verification (all green)

- Domain unit tests: **297 passed**.
- Application unit tests: **240 passed**.
- Integration tests (Testcontainers): **274 passed** — includes `BrokerUnavailableGameplayStallTests`
  (5 tests: 3 `WhenBrokerUnavailable` + 2 `WhenBrokerAvailable` controls) and the `RoundClose…`
  end-to-end outbox delivery test against a real RabbitMQ. Note what those 5 actually model: a bus that
  is **ready** with the delivery service simply not draining — i.e. the transport is off the write path
  once the bus is up — not a genuinely down/unreachable RabbitMQ. They assert prompt return plus a
  durable pending outbox row.
- Full service build via `make build SVC=session-operations-service` (structure-guard + layer-guard):
  OK.

Commands: use the sandbox-hardened Makefile from `backend/AGENTS.md` for builds; integration tests
and `dotnet ef` need Docker, so run those with the sandbox disabled (never invoke `dotnet` build
directly). The broker-unavailable regression suite is
`services/session-operations-service/tests/IntegrationTests/Messaging/BrokerUnavailableGameplayStallTests.cs`.

## Changed / new files

Product code (modified): `src/Application/DependencyInjection.cs`,
`src/Application/Sessions/EventHandlers/PublishAnswerRegistered|QuestionClosed|SessionResultsFinalized IntegrationEventHandler.cs`,
`src/Directory.Packages.props`, `src/Infrastructure/Infrastructure.csproj`,
`src/Infrastructure/Messaging/MassTransitMessagingRegistration.cs`,
`src/Infrastructure/Persistence/ApplicationDbContext.cs`,
`src/Infrastructure/Persistence/DependencyInjection.cs`,
`src/Infrastructure/Persistence/Interceptors/DispatchDomainEventsInterceptor.cs`,
`src/Infrastructure/Migrations/ApplicationDbContextModelSnapshot.cs`.

Product code (new): `src/Application/Common/Interfaces/IOutboxDomainEventDispatcher.cs`,
`src/Application/Sessions/EventHandlers/OutboxDomainEventDispatcher.cs`,
`src/Infrastructure/Migrations/20260713020055_AddMassTransitTransactionalOutbox{,.Designer}.cs`.

Tests (modified): the three publish-handler unit tests,
`tests/IntegrationTests/Api/SessionOperationsApiWebApplicationFactory.cs`,
`tests/IntegrationTests/Persistence/PersistenceTestContextFactory.cs` and `TestMediators.cs`,
`tests/IntegrationTests/Infrastructure.IntegrationTests.csproj`.

Tests (new): `tests/IntegrationTests/Messaging/BrokerUnavailableGameplayStallTests.cs`,
`tests/IntegrationTests/Messaging/OutboxDeliveryOnRecoveryTests.cs` (real-RabbitMQ
accumulate-during-outage → drain-on-recovery),
`tests/IntegrationTests/Messaging/ColdStartBrokerUnreachableWriteTests.cs` (characterization of the
cold-start / pre-ready broker-unreachable window — observed prompt return, Finding B not a defect).

## Open items for the next session

- **Commit as one logical change.** The migration + new outbox files are untracked; they must land
  together with the modified files. Per repo convention, local-squash the branch to one commit before
  a PR.
- **Consumer idempotency (downstream, out of scope here).** Delivery is at-least-once; the
  scoring-monitoring `AnswerRegisteredConsumer` should dedupe on the natural key
  (`TriviaAnswerSubmissionId`) — see §5 of the design doc.
- **Skill doc drift.** The `rabbitmq-events-dotnet` skill still prescribes the removed "5s
  cancellation timeout, log and swallow" pattern; update it to note the transactional-outbox
  exception (docs follow-up, not code).
- **Manual re-verify.** Re-run `frontend/docs/hu-171-manual-test.md` with the broker up to confirm
  the mobile spinner and operator/mobile timer sync symptoms are gone.

## Suggested skills

- `rabbitmq-events-dotnet` — for any follow-up on outbox config, consumer idempotency, or updating
  the skill's own guidance.
- `ef-core-postgresql` — if the migration or outbox table mapping needs adjustment.
- `aspnet-backend-testing` — if extending the regression coverage (e.g. a delivery-on-recovery
  assertion driving the hosted `BusOutboxDeliveryService`).
- `verify` — to drive the end-to-end gameplay flow and observe the stall is gone before committing.

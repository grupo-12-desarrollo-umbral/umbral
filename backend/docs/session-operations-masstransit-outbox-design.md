# SessionOperations — MassTransit transactional outbox design

Design document only. No code changes, no migration generated. Fixes the ~5s gameplay stall when
RabbitMQ is unreachable by moving integration-event publishing to a MassTransit EF Core **bus
outbox**, so the publish becomes a fast DB insert inside the same transaction as the business write
and the broker is reached asynchronously by a delivery service.

---

## 1. Problem

Gameplay-critical writes on SessionOperations stall for exactly 5 seconds and then silently drop the
event whenever RabbitMQ is down. Mechanism, traced through the actual code:

1. `DispatchDomainEventsInterceptor.SavedChangesAsync` (`src/Infrastructure/Persistence/Interceptors/DispatchDomainEventsInterceptor.cs`)
   awaits `_mediator.Publish(domainEvent)` **synchronously, after the EF save**, once per domain event.
2. Each integration-event handler
   (`PublishAnswerRegisteredIntegrationEventHandler`, `PublishQuestionClosedIntegrationEventHandler`,
   `PublishSessionResultsFinalizedIntegrationEventHandler` under
   `src/Application/Sessions/EventHandlers/`) awaits `IPublishEndpoint.Publish(...)` bounded by a
   `CancellationTokenSource.CreateLinkedTokenSource(...)` + `CancelAfter(TimeSpan.FromSeconds(5))`, and
   swallows the resulting exception.
3. With the broker unreachable, MassTransit's `Publish` blocks on its connect-wait until the 5s token
   fires. The save has already committed, so the write is durable — but the 5s is burned on the
   caller's thread and the integration event is **lost** (swallowed, nothing retained).
4. In `TriviaRoundOrchestratorFacade.CloseAndAdvanceAsync`
   (`src/Application/Sessions/Common/TriviaRoundOrchestratorFacade.cs`), the persist that raises
   `QuestionClosedEvent` runs **before** the SignalR broadcast and **before** `ActivateQuestionAsync`,
   so the whole close→advance transition inherits the 5s stall.

The red regression is `tests/IntegrationTests/Messaging/BrokerUnavailableGameplayStallTests.cs`: two
`WhenBrokerUnavailable` latency tests fail at ~5s; a third documents that no durable pending record
exists (asserts `context.Model.FindEntityType("...Outbox.OutboxMessage")` is null today).

---

## 2. Chosen approach — MassTransit EF Core bus outbox

Adopt MassTransit's built-in **transactional bus outbox** backed by `ApplicationDbContext`. With the
bus outbox configured, the scoped `IPublishEndpoint.Publish(...)` no longer talks to RabbitMQ on the
hot path — it inserts an `OutboxMessage` row into the tracked `ApplicationDbContext` and that row is
committed by the same `SaveChanges` that writes the `LiveSession` mutation. A background
`BusOutboxDeliveryService` polls the outbox tables and publishes to RabbitMQ asynchronously, with
retry, when the broker is available.

Result:
- The publish is a local INSERT — microseconds, never blocked on the broker → the 5s stall is gone.
- The event is durably retained (an `OutboxMessage`/`OutboxState` row) and delivered on recovery →
  no silent loss.
- Publish + business write commit atomically (same transaction) → no "committed but never published"
  window.

### Why the bus outbox and not the alternatives
- **Just shrink the 5s timeout** — still loses the event (swallowed), still blocks the hot path for
  the reduced budget, still no atomic delivery guarantee. Rejected: does not fix loss or atomicity.
- **Hand-rolled outbox table + relay** — re-implements exactly what MassTransit ships, and the repo
  skill (`rabbitmq-events-dotnet`) mandates MassTransit as the single messaging path and forbids
  bespoke publisher/retry seams. Rejected: reinvention + convention violation.
- **In-memory outbox only** — MassTransit's in-memory outbox defers publishes until after the handler
  completes but is not durable across a crash and is consume-side oriented. Rejected: no durability.

---

## 3. Verified API surface (MassTransit 8.4.1)

Checked against the local NuGet cache at `~/.nuget/packages/masstransit/8.4.1/lib/net9.0/MassTransit.xml`:

- Present in the **main `MassTransit` 8.4.1 package**: `IBusRegistrationConfigurator.AddConfigureEndpointsCallback(...)`,
  `IBusOutboxConfigurator.DisableDeliveryService`, and the full in-memory outbox surface
  (`AddInMemoryInboxOutbox`, `UseInMemoryOutbox`, …).
- **Not present** in the main package: `AddEntityFrameworkOutbox`, `UsePostgres`, `UseBusOutbox`,
  `AddTransactionalOutboxEntities`. These live in the **`MassTransit.EntityFrameworkCore`** package,
  which is **not currently referenced by this service and is not in the local NuGet cache**. It must
  be added at version **8.4.1** (matching the pinned MassTransit/RabbitMQ trio).

  Because that package could not be introspected locally, the EF-outbox member names below
  (`AddEntityFrameworkOutbox<TDbContext>`, `o.UsePostgres()`, `o.UseBusOutbox()`,
  `o.QueryDelay`, `o.DuplicateDetectionWindow`, the `modelBuilder.AddTransactionalOutboxEntities()` /
  `AddOutboxMessageEntity()` / `AddOutboxStateEntity()` / `AddInboxStateEntity()` extensions) are
  stated from the stable MassTransit 8.x public API and **must be confirmed against the restored
  8.4.1 assembly** during implementation. The overall wiring shape is not in doubt; only exact
  method names should be re-verified.

Pinned versions confirmed in `services/session-operations-service/src/Directory.Packages.props`:
MassTransit / MassTransit.Abstractions / MassTransit.RabbitMQ = 8.4.1; EF Core + Npgsql provider =
10.0.0; target framework net10.0.

---

## 4. Exact changes, file by file

### 4.1 `src/Directory.Packages.props` — add the EF outbox package version
Add `MassTransit.EntityFrameworkCore` pinned to `8.4.1` alongside the existing MassTransit entries.

### 4.2 `src/Infrastructure/Infrastructure.csproj` — reference it
Add `<PackageReference Include="MassTransit.EntityFrameworkCore" />` in the same `ItemGroup` as the
existing `MassTransit` / `MassTransit.RabbitMQ` references. (Infrastructure owns the transport and the
DbContext, so this reference belongs here, not in Application.)

### 4.3 `src/Infrastructure/Persistence/ApplicationDbContext.cs` — add the outbox entities
In `OnModelCreating`, after `ApplyConfigurationsFromAssembly`, register the outbox/inbox entity types
so EF Core maps the three tables:

```
protected override void OnModelCreating(ModelBuilder builder)
{
    base.OnModelCreating(builder);
    builder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());
    builder.AddTransactionalOutboxEntities(); // InboxState, OutboxMessage, OutboxState
}
```

`AddTransactionalOutboxEntities()` maps all three MassTransit tables. This service only **publishes**
(it registers no `IConsumer`), so the `InboxState` table will stay empty; it is harmless to map it and
keeps the model aligned with the standard shape. If a leaner model is preferred, map only
`AddOutboxMessageEntity()` + `AddOutboxStateEntity()` and skip the inbox — but confirm the bus outbox
does not require `InboxState` before dropping it.

### 4.4 New EF Core migration — `AddMassTransitTransactionalOutbox`
Generate with `dotnet ef migrations add AddMassTransitTransactionalOutbox` against **the same
DbContext the app runs** (`ApplicationDbContext`, migrations assembly = Infrastructure). It lands in
`src/Infrastructure/Migrations/` next to the existing `2026…` migrations and updates
`ApplicationDbContextModelSnapshot.cs`. It must create `InboxState`, `OutboxMessage`, and
`OutboxState` (with MassTransit's indexes/unique constraints). No migration is generated as part of
this design — the implementer runs the command.

Migrations are applied by `Api/Program.cs` (`await dbContext.Database.MigrateAsync()`) and, in tests,
by `PostgreSqlFixture.InitializeAsync` (also `MigrateAsync`), so no pipeline change is needed — the new
migration flows through both automatically.

### 4.5 `src/Infrastructure/Messaging/MassTransitMessagingRegistration.cs` — wire the outbox
Add the EF outbox registration inside `AddMassTransit(bus => …)`, before `UsingRabbitMq`:

```
builder.Services.AddMassTransit(bus =>
{
    bus.AddEntityFrameworkOutbox<ApplicationDbContext>(o =>
    {
        o.UsePostgres();                              // Npgsql-specific lock semantics
        o.UseBusOutbox();                             // scoped IPublishEndpoint -> DB insert
        o.QueryDelay = TimeSpan.FromSeconds(1);       // delivery-service poll interval
        o.DuplicateDetectionWindow = TimeSpan.FromMinutes(30); // MessageId dedup window
    });

    bus.UsingRabbitMq((context, cfg) =>
    {
        var options = context.GetRequiredService<IOptions<MassTransitRabbitMqOptions>>().Value;
        cfg.Host(options.HostName, (ushort)options.Port, options.VirtualHost, host =>
        {
            host.Username(options.UserName);
            host.Password(options.Password);
        });
        cfg.ConfigureEndpoints(context);
    });
});
```

`AddEntityFrameworkOutbox<ApplicationDbContext>` registers the scoped outbox-backed
`IPublishEndpoint`/`ISendEndpointProvider` and the hosted `BusOutboxDeliveryService`. The RabbitMQ
host wiring is unchanged. `DependencyInjection.cs` needs no change — it already calls
`builder.AddMassTransitMessaging()` after `AddPersistenceServices()`.

Ordering note: `AddDbContext<ApplicationDbContext>` (in `Persistence/DependencyInjection.cs`) and the
outbox must resolve the **same scoped** `ApplicationDbContext` instance so the outbox INSERT rides the
business transaction. The current registration is `AddScoped` DbContext — correct. Do **not** switch
to `AddDbContextFactory`/pooling for the request path, or the outbox and the write would use different
context instances and lose atomicity.

### 4.6 `DispatchDomainEventsInterceptor.cs` — dispatch BEFORE the save (the critical change)
This is the repo-specific crux. The bus outbox works by having `Publish` add an `OutboxMessage` row to
the tracked `ApplicationDbContext`; that row is persisted by the **same `SaveChanges`** as the business
write. Today the interceptor dispatches in `SavedChanges`/`SavedChangesAsync` — i.e. **after** the
save has already run. If left there, the outbox INSERT would land in the change tracker with no
subsequent `SaveChanges` to flush it → the message would never be written and never delivered.

Move dispatch to the pre-save hook:
- Override `SavingChanges` / `SavingChangesAsync` instead of `SavedChanges` / `SavedChangesAsync`.
- Keep the same body: collect `BaseEntity` entries with domain events, clear them, `await
  _mediator.Publish(domainEvent)` for each. Now each publish enlists an `OutboxMessage` insert into
  the pending change set, and EF writes business rows + outbox rows in one command batch / one
  transaction.

This preserves existing semantics safely because the three handlers now do **only** a local DB insert
(no external I/O), so dispatching pre-commit no longer risks blocking or partial external effects. Any
other `INotificationHandler` for these domain events must be audited before the move (see §7); in this
service the SignalR broadcasts are invoked directly by the facade, not via MediatR, so they are
unaffected.

Atomicity guarantee after this change: `LiveSession` mutation + domain-event dispatch + `OutboxMessage`
insert all commit in the single `SaveChanges` transaction. Either all persist or none do.

### 4.7 The three publish handlers — remove the 5s timeout guards
With the bus outbox, `IPublishEndpoint.Publish` is a synchronous local insert; there is nothing to
time out. Remove from all three handlers:
- the `private static readonly TimeSpan PublishTimeout = TimeSpan.FromSeconds(5);` field,
- the `CancellationTokenSource.CreateLinkedTokenSource(...)` + `CancelAfter(...)` wrapper,
- pass the caller's `cancellationToken` straight to `Publish`.

**Recommendation: remove the guards entirely** (do not keep a smaller guard). Rationale: the guard
existed only because `Publish` could block on the broker connect-wait; under the outbox it cannot, so
a residual timeout is dead code that could only mask a genuine DB fault by swallowing it. The
`try/catch` that logs and swallows should also be reconsidered — under the outbox a `Publish` failure
means the DbContext insert failed, which should fail the transaction, not be swallowed. Preferred:
let the exception propagate (the whole `SaveChanges` rolls back atomically). At minimum, keep logging
but re-throw. Update the XML-doc comments on the handlers, which currently describe the 5s
fail-fast behaviour that no longer exists.

The `rabbitmq-events-dotnet` skill currently prescribes the "caller-linked five-second cancellation
timeout, log and swallow" pattern (its steps 3 and the "Project conventions" bullet). This design
supersedes that guidance for gameplay-critical publishes; the skill should be updated to note the
transactional-outbox exception, but that is a docs follow-up, not part of this code change.

### 4.8 `TriviaRoundOrchestratorFacade.CloseAndAdvanceAsync` — no code change required
Once §4.6 lands, the persist inside `CloseAndAdvanceAsync` no longer waits on the broker (the publish
is a local insert), so the broadcast and `ActivateQuestionAsync` that follow it run immediately. The
existing ordering (persist → broadcast → activate next) is already correct and stays as-is; only its
latency characteristic changes. No edit needed here.

---

## 5. Atomicity & delivery semantics

- **Atomic capture**: business write + `OutboxMessage` insert share one `ApplicationDbContext`
  `SaveChanges` and therefore one transaction. A broker outage cannot produce a committed write with a
  lost event, and cannot produce a phantom event for a rolled-back write.
- **Async delivery**: `BusOutboxDeliveryService` polls `OutboxState` every `QueryDelay` (1s here),
  claims pending rows with a Postgres row lock (`UsePostgres()`), publishes to RabbitMQ, and marks them
  delivered. While the broker is down it simply retries on the next tick; rows accumulate durably and
  drain on recovery — in order per `OutboxState` (per-context sequence), preserving publish order.
- **At-least-once**: delivery can retry after a crash between broker-ack and row-update, so consumers
  can see duplicates. `DuplicateDetectionWindow` (30m) dedups by `MessageId` on the delivery side.
- **Consumer idempotency**: the downstream consumer
  `scoring-monitoring-service/.../Consumers/AnswerRegisteredConsumer.cs` maps each event to a
  `RecordAnswerReceiptCommand`. It must tolerate redelivery — dedupe on the natural key
  (`TriviaAnswerSubmissionId`, unique per accepted answer) in `RecordAnswerReceipt`, or adopt the
  consume-side EF inbox on scoring-monitoring. This is a downstream concern, called out but out of
  scope for this change; per the skill, we do not claim exactly-once.

Config knobs available if tuning is needed: `QueryDelay` (poll cadence vs. DB load), `QueryMessageLimit`
/ `MessageDeliveryLimit` on `UseBusOutbox(...)` (batch size), `DuplicateDetectionWindow` (dedup memory).

---

## 6. Test impact — `BrokerUnavailableGameplayStallTests.cs`

The test builds `ApplicationDbContext` directly via `PersistenceTestContextFactory` (not through DI),
injecting a hand-rolled `IntegrationEventPublishingMediator` whose handlers receive an
`IPublishEndpoint` mock. Two consequences:

1. **Latency assertions (`elapsed < PromptBudget`) become satisfiable and the ~5s wall-clock concern
   disappears.** After the fix, the production write path never awaits the broker — the publish is a
   local insert. The two `WhenBrokerUnavailable` tests turn green. However, they cannot pass *with the
   current `UnreachableBroker()` mock injected as the handler endpoint*: that mock blocks infinitely and
   the fix removes the `CancelAfter` that used to bound it, so an infinite-blocking mock would now hang
   forever, not return in <2s. The harness must be updated so the handlers publish through the
   **outbox-backed endpoint** (a fast local insert) rather than a raw broker mock — i.e. the "broker
   down" condition moves off the write path entirely and onto the delivery service. Practically this
   means constructing the context with the EF outbox services wired (or asserting against the
   outbox-backed `IPublishEndpoint` the DI container produces). This is a real harness change, not a
   no-op; the parent's "pass unchanged" holds for the *assertions*, not for the injected seam.

2. **The "no pending record" test upgrades to a real durability assertion.** Today
   `AcceptedAnswerSave_WhenBrokerUnavailable_AttemptsPublishButRetainsNoPendingRecord` asserts
   `context.Model.FindEntityType("...Outbox.OutboxMessage")` is **null**. After §4.3 that entity type
   exists, so the assertion must flip: with the broker down, save the accepted answer through the
   outbox-backed context, then assert an `OutboxMessage` row is present and pending (not yet
   delivered) for the `AnswerRegisteredIntegrationEvent`. To assert delivery-on-recovery, drive the
   `BusOutboxDeliveryService` (or its query) against a reachable broker and assert the row is marked
   delivered / the consumer observes the event. Note the MassTransit `OutboxMessage` type lives in the
   MassTransit namespace, not `umbral_backend.Infrastructure.Persistence.Outbox` — update the lookup
   string accordingly (or query the `DbSet` MassTransit registers).

The two `WhenBrokerAvailable` control tests keep their intent (prompt return + publish observed), but
"publish observed" now means "outbox row written" rather than "broker mock invoked"; adjust their
verification to the outbox row (or the delivery-service delivering it).

Testcontainers implication: `PostgreSqlFixture` already runs `MigrateAsync`, so the new outbox tables
are created automatically in the test database — no fixture change beyond that. The harness's
`PersistenceTestContextFactory` will need to opt into the outbox services for the tests that assert
outbox behaviour.

---

## 7. Pitfalls specific to this repo

- **Interceptor timing (most important).** Dispatch must move from `SavedChanges*` (post) to
  `SavingChanges*` (pre) or the outbox INSERT is never flushed. See §4.6. This is the single change
  most likely to be missed.
- **Other domain-event handlers.** Moving dispatch pre-commit changes when *every* `INotificationHandler`
  for these domain events runs. Audit the Application assembly for handlers beyond the three publish
  handlers before the move. In this service the SignalR broadcasts are called directly by
  `TriviaRoundOrchestratorFacade` (not via MediatR), so they are unaffected — but confirm no other
  notification handler assumes post-commit execution.
- **DbContext lifetime / scoping.** The outbox needs the same scoped `ApplicationDbContext` as the
  write. Keep `AddScoped` DbContext; do not move the request path to a factory/pool. There is exactly
  **one** DbContext in this service (`ApplicationDbContext`), so no multi-context ambiguity — but the
  outbox is bound to it by the `AddEntityFrameworkOutbox<ApplicationDbContext>` type parameter.
- **`ExecuteDeleteAsync` bypasses the outbox.** The test seed uses
  `LiveSessions.ExecuteDeleteAsync()`; bulk `ExecuteUpdate/Delete` do not go through `SaveChanges` and
  therefore never touch the outbox. That is fine for seeding but is a general gotcha: any production
  path that publishes must go through tracked `SaveChanges`, not bulk operations.
- **Connection/transaction.** `UsePostgres()` selects Npgsql-appropriate row-locking for the delivery
  service. No connection multiplexing is configured (none in the repo); the delivery service opens its
  own scope/connection from the Npgsql pool — ensure the pool max is sized for it plus request traffic
  (default is ample).
- **Migrations pipeline.** Both `Api/Program.cs` and `PostgreSqlFixture` call `MigrateAsync`, so the
  new migration applies everywhere without extra wiring. Startup auto-migrate is a pre-existing pattern
  here; the outbox migration inherits it.
- **Delivery service in tests/CI.** The `BusOutboxDeliveryService` is a hosted service; it only runs
  when the Generic Host runs. The unit-style integration tests construct the DbContext directly and do
  not start the host, so they must drive delivery explicitly if they want to assert recovery.

---

## 8. Rollout / migration steps

1. Add `MassTransit.EntityFrameworkCore` 8.4.1 to `Directory.Packages.props` + `Infrastructure.csproj`;
   restore and **re-verify the EF-outbox member names** against the restored assembly (§3).
2. Add `builder.AddTransactionalOutboxEntities()` to `ApplicationDbContext.OnModelCreating`.
3. `dotnet ef migrations add AddMassTransitTransactionalOutbox` (ApplicationDbContext); review the
   generated `InboxState`/`OutboxMessage`/`OutboxState` tables and indexes.
4. Wire `AddEntityFrameworkOutbox<ApplicationDbContext>(UsePostgres/UseBusOutbox/QueryDelay/
   DuplicateDetectionWindow)` in `MassTransitMessagingRegistration`.
5. Move `DispatchDomainEventsInterceptor` dispatch to `SavingChanges`/`SavingChangesAsync`.
6. Strip the 5s timeout guards from the three publish handlers; reconsider swallow-vs-propagate;
   update their XML-doc comments.
7. Update `BrokerUnavailableGameplayStallTests` per §6 (outbox-backed endpoint; flip the "no pending
   record" assertion to a real pending-then-delivered assertion).
8. Deploy: schema migration is backward compatible (additive tables only), so it can ship ahead of or
   with the code. On rollback, the outbox tables are inert if the code no longer uses them.

---

## 9. Risks & alternatives rejected

- **Biggest risk — interceptor ordering silently breaks delivery.** If dispatch is left in
  `SavedChanges*`, the app compiles, writes commit, *no exception is thrown*, yet **no message is ever
  enqueued or delivered** — a silent regression worse than the current 5s stall. Mitigation: the
  pre-save move (§4.6) plus the upgraded durability test in §6 that asserts a pending `OutboxMessage`
  row actually exists after a save.
- **Ordering behaviour change.** Handlers now run pre-commit. Low risk here (only local inserts), but
  must be validated against any non-publish notification handler (§7).
- **Delivery latency.** Events are delivered on the delivery service's cadence (`QueryDelay`, 1s), not
  synchronously. Acceptable for downstream scoring; note it is now eventually-consistent by ~1s under
  normal operation.
- **Alternatives rejected** (detail in §2): shrinking the timeout (still lossy, still blocks); a
  hand-rolled outbox (reinvents MassTransit, violates the messaging skill); in-memory outbox only
  (not durable).

### Broker-unavailable coverage — two distinct scenarios

The broker-unavailable suite (`BrokerUnavailableGameplayStallTests`, 3 `WhenBrokerUnavailable` + 2
`WhenBrokerAvailable` controls) covers the **post-ready steady state**: the bus has reached ready
(broker up at boot) and the delivery service simply is not draining — modelling a broker that becomes
lost *mid-gameplay*, with the transport off the write path once ready. It does not exercise a broker
that is unreachable before the bus ever connects.

The **cold-start / pre-ready broker-unreachable window** is a distinct scenario: RabbitMQ is
unreachable from cold start, so MassTransit's one-shot bus-ready latch never opens, and — because the
API serves requests before the bus connects (`WaitUntilStarted = false`) — a gameplay write can land
while the bus is still connecting. The suspected risk (Finding B) was that the outbox `Publish`, which
runs inside the interceptor's pre-commit `SavingChanges` holding the DB transaction, would block on the
bus-ready gate indefinitely (the old 5s guard having been removed).

`tests/IntegrationTests/Messaging/ColdStartBrokerUnreachableWriteTests.cs` characterizes this window
against the real seam, with the bus pointed at a dead localhost port (started but never ready) and no
production safeguard added. **Observed outcome: the accepted-answer save returns promptly (~0.5s, well
under a 2s budget), the answer commits, and a pending outbox row is retained.** The bus-outbox
`IPublishEndpoint.Publish` is a local `OutboxMessage` insert that does not await the bus-ready latch, so
the pre-ready window does not stall the write — Finding B is **not** a real defect. The test guards
against a future regression that would reintroduce that gate on the write path.

### Explicitly unverified in this document
- Exact `MassTransit.EntityFrameworkCore` 8.4.1 method names (§3) — package absent from local cache;
  confirm on restore.
- Whether the bus outbox strictly requires the `InboxState` entity when the service has no consumers
  (§4.3) — confirm before trimming to outbox-only entities.

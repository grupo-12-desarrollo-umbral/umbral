# HU-21 Context — Auditoría de cambios de estado de sesión

> Paste this section into any agent session that needs context for HU-21 (DES-29).
> Last updated: 2026-07-13 | Branch: `feature/hu-21-session-state-change-audit`
>
> **Transport regenerated for MassTransit (2026-07-13).** The MassTransit migration chain
> (GH #164→#166) + the EF Core transactional bus outbox have landed on `develop`
> (`session-operations-masstransit-outbox-implementation-handoff-2026-07-12.md`). Integration events are
> now MassTransit records published through `IPublishEndpoint.Publish`, dispatched **pre-commit** by
> `OutboxDomainEventDispatcher` so the publish is a local `OutboxMessage` insert that commits atomically
> with the business write and drains to RabbitMQ asynchronously. `IIntegrationEventPublisher`,
> `RabbitMqIntegrationEventPublisher`, and `RabbitMqOptions` were **deleted** by #166 — do **not** mirror
> them, do **not** add a routing-key switch. The domain + persistence half (`SessionEvent` entity,
> `live_session_events` table) is transport-independent.

## State

- DES-29 (HU-21): **Todo**, labels: `svc:session-operations-service`, `Feature`, `ready-for-agent`. Both required labels present.
- **Resolved mode: feature flow.** DES-29 carries no `canon-realign` / `needs-rebuild` label. It was renamed `HU-21B → HU-21` on 2026-07-09 (the A/B split went orphan when `HU-21A`/DES-28 was cancelled and rebuilt as DES-76); it is the single surviving `HU-21`. Its query AC (*"el historial de cambios puede consultarse posteriormente"*) was **stripped** on 2026-07-09 because that read surface is owned by DES-56 (HU-40A); this HU keeps only the **write** half: persist the fact + publish `SessionStateChanged` (`ab-ticket-merge-execution-checklist-2026-07-09.md:18`, `workflow_refactor.md:278-281`).
- **Supersession handling applied:** DES-29 is a **live descendant** in the realignment map, **not** superseded — it appears in the *blocked/descendant* column of `canon-realignment-after-mission-runtime-rewrite.md:196` (`DES-28 | DES-76 | DES-29, …`), whose blocker was merely re-pointed off cancelled **DES-28** onto rebuild **DES-76**. The predecessor **DES-28 (old state machine) is dropped and substituted by DES-76 (HU-21A)** — never anchor on DES-28's cancelled code.
- Predecessor DES ids (build-on, Done): **DES-76 (HU-21A — session state machine)**; underneath it **DES-22 (HU-15)**, **DES-24 (HU-17)**, **DES-25 (HU-18)**, **DES-26 (HU-19)**. Also **build-on: the MassTransit EF-outbox migration** (GH #164→#166 + outbox, Done on `develop`) — the transport backbone this HU publishes through. Landed-untouched: DES-32 (HU-24A), DES-30→DES-77 (HU-22 timer, downstream), the trivia publish HUs (HU-33B/HU-34).
- PRD DES id: **DES-70** → `backend/docs/prd/DES-70-primera-implementacion-de-session-operations-service-hu-15-a-hu-36.md` (authoritative; never re-fetch PRD scope from Linear). US14 (`:101-102`) = "every state change recorded with actor, time, and context, so later audit is possible"; US12/US13 (`:96-100`) the lifecycle/reject-with-reason it audits; `:211-212` "SessionOperations publishes facts but retains ownership of … state."
- Blocks: **DES-56 (HU-40A — historial de eventos de sesión)** — the durable queryable history consumer; this HU is its producer. Blocked by: DES-76 (HU-21A) — **Done**.
- Branch: `feature/hu-21-session-state-change-audit`, base **`develop`** (all build-on predecessors Done/merged; no same-service predecessor is In Progress).

## Required design patterns

| Pattern | Owning phase | Why (from patterns matrix) | Concrete obligation |
|---|---|---|---|
| `State` (mandated) | X.1 Domain | `required_patterns_matrix.md:41,109`: "Lifecycle (`Scheduled`→…→`Finished`/`Cancelled`) needs an explicit state model." | Already realized + locked by HU-21A (`Domain/Services/SessionStates/*` behind `LiveSessionStateFactory`; `SessionStateTransitionPolicy`). HU-21 **verifies + inherits** — it does not touch the transition graph. It only enriches the transition to *record* the audited fact (see below). |
| `Chain of Responsibility` (mandated) | X.2 Application | `required_patterns_matrix.md:42,109`: "…plus ordered transition validators (`SessionStateTransitionPolicy`)." | Already realized + locked by HU-21A (`Application/Sessions/StateTransitions/` — `CurrentStateGate → OperatorAssignmentGate → ParticipantReadinessGate`). HU-21 **verifies + inherits**; it does not add or reorder a gate. |
| `Facade` (mandated — ADR-0004 + matrix "Facade publishes the event") | X.2 Application | `adr/0004-required-domain-patterns.md`: "session orchestration **+ outbound event publication** in `SessionOperations`"; `required_patterns_matrix.md:109`: "**Facade publishes the event**." | **This HU's new obligation.** The transition's event-publication boundary publishes `SessionStateChanged` for audit after transactional success. Per ADR-0012 the Facade is **realized by the MediatR handler + the integration publish handler — no standalone `*Facade.cs`** (`adr/0012-...:62`). Realized as `PublishSessionStateChangedIntegrationEventHandler` (a plain handler, like the three existing publish handlers), routed from `OutboxDomainEventDispatcher` **pre-commit** so its `IPublishEndpoint.Publish` outbox insert rides the transition's transaction. |

Transport note: **MassTransit / RabbitMQ via the EF Core transactional bus outbox** (`required_patterns_matrix.md:58,109`) is this HU's new transport — `SessionStateChanged` published for **async audit**. Under the bus outbox, `IPublishEndpoint.Publish` is a **local `OutboxMessage` insert** that commits atomically with the transition write (`DispatchDomainEventsInterceptor.SavingChanges` → `OutboxDomainEventDispatcher`, pre-commit) and is drained to RabbitMQ asynchronously by `BusOutboxDeliveryService`. This is exactly AC #4 "tras el éxito transaccional … sin que el flujo principal dependa de RabbitMQ": the broker is **off the write path** (not because the exception is swallowed — a publish/outbox-insert failure is a DB fault and **propagates**, rolling the transaction back). **SignalR** (the live `SessionStateChanged` broadcast, `SessionStateChangedNotificationHandler`, post-commit) is HU-21A's and is **untouched** here. Mirror the shipped MassTransit publish handlers (`PublishAnswerRegisteredIntegrationEventHandler` / `PublishSessionResultsFinalizedIntegrationEventHandler`) — **do not** reintroduce `IIntegrationEventPublisher`/raw `RabbitMQ.Client`/a routing-key switch (all deleted by #166).

Applies-where note (no new gate): `PATCH /api/sessions/{liveSessionId}/state` is a protected mutation, but HU-21 is **not** matrix-tagged for `Proxy` and not in the applies-where set (HU-04/05/36B). It inherits the standard `Operator` `AuthorizationBehaviour`/resource-ownership resolver (ADR-0001/0002/0009) — note only, **no** new `Proxy` gate.

## What predecessors have already landed

**Build-on (read for derivation):**

- **DES-76 (HU-21A) — Done.** The canonical six-state machine, `State`-pattern classes, `SessionStateTransitionPolicy`, the CoR transition chain, the `TransitionSessionStateCommand`/handler orchestration, the `PATCH …/state` endpoint, and the **SignalR** `SessionStateChanged` broadcast are all shipped and locked. Critically for this HU: `LiveSession.MoveTo(nextState, occurredAt, transitionPolicy, reason)` (`LiveSession.cs:281`) already sets `LastStateChangedAt` + `StateReason` and raises `SessionStateChangedEvent` (`:294`); the transition handler already resolves the acting operator through the auth resolver. See `hu21a-context.md`.
- **DES-26 (HU-19) — Done.** `LiveSession.AssignedOperatorUserId` (`int?`, `:97`) — the numeric operator id that is the "responsible user" of a transition.
- **DES-22 (HU-15) / DES-24 (HU-17) / DES-25 (HU-18) — Done.** `LiveSession` aggregate creation in `Scheduled`, single-source invariant, team association — the aggregate this HU appends a child entity to.

**MassTransit EF-outbox backbone already shipped in this service (mirror it — do not rebuild):** integration events are MassTransit `record`s decorated `[EntityName("…")]` in `Application/Sessions/Common/` (`AnswerRegisteredIntegrationEvent` → `session-answer-registered`, `SessionResultsFinalizedIntegrationEvent` → `session-results-finalized`, `QuestionClosedIntegrationEvent`). Each has a plain publish handler in `Application/Sessions/EventHandlers/` (`PublishAnswerRegisteredIntegrationEventHandler`, `PublishSessionResultsFinalizedIntegrationEventHandler`, `PublishQuestionClosedIntegrationEventHandler`) — **not** `INotificationHandler`s: they expose `Handle(TDomainEvent, ct)`, inject `IPublishEndpoint` + `ILogger`, and are invoked by `OutboxDomainEventDispatcher.DispatchAsync` (a `switch` on the domain-event type). Dispatch runs **pre-commit** from `DispatchDomainEventsInterceptor.SavingChanges/SavingChangesAsync` via `IOutboxDomainEventDispatcher`, so the `Publish` (a local `OutboxMessage` insert under `AddEntityFrameworkOutbox<ApplicationDbContext>`) is flushed by the same `SaveChanges` transaction. `BusOutboxDeliveryService` drains to RabbitMQ asynchronously. The outbox tables (`OutboxMessage`/`OutboxState`/`InboxState`) already exist via migration `20260713020055_AddMassTransitTransactionalOutbox`.

**Landed, untouched by this HU:** DES-32 (HU-24A operator panel), DES-77 (HU-22 timer — the `Enter` timer hooks inside the state classes), the trivia publish HUs. Do not modify them.

**Coverage:** session-operations-service carries the HU-07/15/17/18/19/21A/24A baseline; verify the real service percentage against the ADR-0005 repo gate at phase X.4.

## What this HU adds

| Concern | New work |
|---|---|
| Per-change audit record (domain) | A new **append-only `SessionEvent`** child entity of `LiveSession` (canon: `bd_umbral_entity_spec.md:453-480`, "SessionEvent is append-only", "major session state changes should create a SessionEvent") — captures `OccurredAt`, `ActorType` (`Operator`/`System`), `ActorId` (nullable), `EventType`, `PayloadSummary` (`previous→next` + reason), `CorrelationId`. This is the durable "cada cambio queda registrado" (AC #1) with the responsible user (AC #2) and reason (AC #3). |
| Enriched domain event | `SessionStateChangedEvent` gains `ResponsibleUserId` (`int?`) + `Reason` (`string?`) + `ActorType` so the published payload and the appended record carry actor + reason. **Add them as defaulted ctor params** so the locked HU-33B consumers/tests (`PublishSessionResultsFinalizedIntegrationEventHandler`, `SessionStateChangedNotificationHandler`, their tests) that construct the event keep compiling. |
| Responsible-user threading (application) | The transition handler surfaces the acting operator's numeric `UserId` (already fetched by the auth resolver `SessionAdministrationAuthorizationProxy` via `IAuthenticatedActorProfileAccessClient.GetCurrentAsync`) and threads it through `SessionTransitionContext` → `MoveTo` → the event + the `SessionEvent`. `null` on the Administrator/system path. |
| MassTransit audit publish (application) | New `SessionStateChangedIntegrationEvent` (`[EntityName("session-state-changed")]`) + `PublishSessionStateChangedIntegrationEventHandler` mirroring the existing publish handlers, **routed from `OutboxDomainEventDispatcher`**. The `SessionStateChangedEvent` switch arm currently maps only to `PublishSessionResultsFinalizedIntegrationEventHandler` (Finished-gated) — it must **also** invoke the new audit handler (sequential await on the same DbContext, not `Task.WhenAll`). Published on **every** valid transition; committed atomically via the outbox, drained async (AC #4). |
| Persistence (infra) | New `live_session_events` table — an `OwnsMany` child of `LiveSession` configured **inside** `LiveSessionConfiguration.cs` (there is no per-entity config file in this service), plus a migration. `LiveSession.State`/`LastStateChangedAt`/`StateReason` columns already exist (no change). |
| API | **No new endpoint** — `PATCH /api/sessions/{liveSessionId}/state` already exists (HU-21A). X.4 verifies end-to-end: a transition now persists a `SessionEvent` and enqueues `SessionStateChanged` into the outbox (drained to the broker). |
| Frontend | None. This is a write/audit + async-publish slice with no contract change. (The history read surface is DES-56/HU-40A.) |

## Touched surfaces

- `backend/services/session-operations-service` (owner — new `SessionEvent` domain entity + append-on-transition, enriched event, publish handler + integration event, `OutboxDomainEventDispatcher` fan-out arm, `LiveSessionConfiguration` + migration, tests)
- Frontend: **none** (no contract change)
- API contract boundary: **unchanged** — `PATCH /api/sessions/{liveSessionId}/state` shape is unaffected; the new fact travels via the async `SessionStateChanged` MassTransit event (`[EntityName("session-state-changed")]`), consumed downstream by DES-56/HU-40A
- Integration boundary: this HU is the **producer** of `SessionStateChanged`; DES-56 (HU-40A, scoring-monitoring) is the consumer that builds the queryable `AuditHistory` — **do not build that read surface here**

## Committed phases

| Commit | Phase | Description |
|---|---|---|
| | | |

## Known quirks / gotchas

- **Do NOT build a queryable audit-history endpoint/projection.** DES-29's query AC was deliberately stripped; the durable, queryable session-event history (`AuditHistory`) is owned by **DES-56 (HU-40A) / scoring-monitoring (DES-85)**, fed by consuming this HU's published event. HU-21 is the **producer**: persist the `SessionEvent` fact + publish `SessionStateChanged`. Reject any plan that adds a `GET …/history`/`AuditHistory` read model in session-operations.
- **Do NOT touch the transition graph, the `State` classes, the CoR gates, or the SignalR broadcast.** All are HU-21A's and canon-locked. This HU only *records* and *publishes* the fact of a transition; it adds no edge, gate, or broadcast path.
- **`MoveTo` has a second, system-driven caller.** `LiveSession.CompleteActiveSubstageAndAdvance` (`LiveSession.cs:755`) calls `MoveTo(SessionState.Finished, …)` (`:778`) with no operator and no reason. The new actor parameter must default to / accept a **`System` actor with a null `ActorId`** — the appended `SessionEvent` records `ActorType = System`. Do not force an operator id onto system-driven transitions.
- **MassTransit is off the write path via the outbox (AC #4).** `IPublishEndpoint.Publish` under `AddEntityFrameworkOutbox` is a local `OutboxMessage` insert, not a broker round-trip; it commits with the transition's `SaveChanges` and is delivered later by `BusOutboxDeliveryService`. A broker outage therefore **cannot** fail the transition — the row is retained and drains on recovery. Do **not** re-add a 5s timeout, a swallow-and-forget `catch`, or a bespoke outbox. Mirror the three shipped publish handlers: log + **rethrow** (a publish failure = the outbox INSERT failed = a DB fault that must roll the transaction back).
- **Publish handlers are plain classes, dispatched pre-commit — NOT MediatR `INotificationHandler`s.** The `SessionStateChangedEvent` → SignalR bridge (`SessionStateChangedNotificationHandler`) IS an `INotificationHandler` and runs **post-commit** (`SavedChanges`). The new audit publisher must follow the *publish-handler* pattern (invoked by `OutboxDomainEventDispatcher`, pre-commit), not the notification-handler pattern — or the outbox insert won't ride the transaction.
- **The dispatcher's `SessionStateChangedEvent` arm already routes to the results-finalized publisher.** `OutboxDomainEventDispatcher.DispatchAsync` maps `SessionStateChangedEvent => _sessionResultsFinalized.Handle(...)`. Adding the audit publisher means that arm must invoke **both** handlers (await sequentially — the shared scoped `ApplicationDbContext` is not safe for concurrent EF operations). Inject the new handler into `OutboxDomainEventDispatcher`'s constructor. The results-finalized publisher is Finished-gated; the new audit publisher fires on every transition.
- **Enriching `SessionStateChangedEvent` must stay additive.** `PublishSessionResultsFinalizedIntegrationEventHandler` (HU-33B) and its test, plus `SessionStateChangedNotificationHandler`, construct `new SessionStateChangedEvent(id, prev, next, changedAt)`. Add `ResponsibleUserId`/`Reason`/`ActorType` as **defaulted** ctor params (or update every call site) so those locked consumers keep compiling.
- **Responsible user = numeric `UserId`, not the Keycloak id.** `ICurrentUser.Id` is the external identity string; the value that matches `AssignedOperatorUserId` (`int`) comes from `IAuthenticatedActorProfileAccessClient.GetCurrentAsync(ct).UserId`. The auth resolver (`SessionAdministrationAuthorizationProxy.GetAuthorizedSessionInternalAsync:62`) already fetches it on the Operator path — surface it (return it alongside the session, or re-fetch in the handler) rather than re-deriving.
- **`[EntityName]` names the exchange — no routing key.** MassTransit resolves topology by message type; the contract's `[EntityName("session-state-changed")]` names the exchange (mirror `session-answer-registered`/`session-results-finalized`). There is no `ResolveRoutingKey` switch anymore (`RabbitMqIntegrationEventPublisher` was deleted).
- **Persistence is `OwnsMany` inside `LiveSessionConfiguration.cs`.** This service maps every aggregate child (`Teams`, `Participants`, `TriviaAnswerSubmissions`, …) via `builder.OwnsMany(session => session.X, …).ToTable("live_session_*")` in the single `LiveSessionConfiguration.cs` — there is **no** `SessionEventConfiguration.cs`. Mirror the `TriviaAnswerSubmissions` block (`:603+`). EF auto-includes owned collections, so `GetByIdAsync` needs no `.Include` change.
- **Namespace is `umbral_backend.*`**; vertical-slice layout per ADR-0011 (`Commands/TransitionSessionState/`, `EventHandlers/`, `Sessions/Common/`). No `Handlers/`/`DTOs/`/`Facades/` buckets.
- **Test projects:** domain → `tests/UnitTests/`; application → `tests/Application.UnitTests/` (mirror `Sessions/EventHandlers/PublishSessionResultsFinalizedIntegrationEventHandlerTests.cs` — it uses `Mock<IPublishEndpoint>` and asserts publish-once + **propagates-on-failure**); integration → `tests/IntegrationTests/` (`Api/`, `Persistence/`, `Messaging/`; publish exemplars `Messaging/MassTransitRemainingIntegrationEventPublishTests.cs`, `Messaging/OutboxDeliveryOnRecoveryTests.cs`, `Api/RoundClosePublicationEndToEndTests.cs`; endpoint `Api/TransitionSessionStateEndpointTests.cs`).

## Per-phase derivation — authoritative for implementation

> Primary source for the phase subagent (per `backend-agent.md` "Read first"). Mode = **feature flow**;
> canon precedence per `backend-agent.md` (`ddd_solution_model.md` → `CONTEXT.md` → `structure.md` →
> `bd_umbral_entity_spec.md`). Read a cited canon section only to fill a gap a block leaves open.
> Canon anchors: `bd_umbral_entity_spec.md` §SessionEvent (`:453-480` append-only child of `LiveSession`);
> `CONTEXT.md` §SessionEvent + §Facade (orchestration triggers outbound event publication);
> `ddd_solution_model.md` outbound events (`SessionStateChanged`; Facade; RabbitMQ workflow);
> PRD DES-70 US12-14 (`:96-102`); patterns `required_patterns_matrix.md:41,42,58,109`;
> transport backbone `session-operations-masstransit-outbox-design.md` +
> `…-outbox-implementation-handoff-2026-07-12.md`.

### Phase X.1 — Domain
**Derive** (`bd_umbral_entity_spec.md:453-480`; `CONTEXT.md` §SessionEvent; PRD DES-70:101-102):
- New **`SessionEvent`** append-only child entity of `LiveSession`: fields `Guid Id`, `Guid LiveSessionId`, `DateTimeOffset OccurredAt`, `SessionEventActorType ActorType` (`Operator`|`System`), `int? ActorId`, `string EventType` (e.g. `"SessionStateChanged"`), `string PayloadSummary` (`"{previous}→{current}"` + reason when present), `Guid CorrelationId`. Constructed only through a factory method (`SessionEvent.ForStateChange(...)`) — append-only, no mutators.
- `LiveSession` gains a private `List<SessionEvent>` exposed as `IReadOnlyCollection<SessionEvent> SessionEvents`, and `MoveTo` **appends one `SessionEvent`** per valid transition capturing date + actor + reason.
- `SessionStateChangedEvent` (`Domain/Events/SessionStateChangedEvent.cs`) gains `int? ResponsibleUserId` + `string? Reason` + `SessionEventActorType ActorType` **as defaulted ctor params** (so HU-33B's `PublishSessionResultsFinalizedIntegrationEventHandler`, `SessionStateChangedNotificationHandler`, and their tests keep compiling). `MoveTo` populates them (currently it raises the event with only `previous/current/changedAt` — `:294`).
- `MoveTo` signature accepts the responsible actor (e.g. `int? responsibleUserId = null`, defaulting to a `System`/null actor); its **system-driven caller** `CompleteActiveSubstageAndAdvance` (`:755` → `MoveTo` at `:778`) leaves it defaulted → `System` actor / null `ActorId`.

**Target files** (create | edit — file to mirror):
- create `src/Domain/Entities/SessionEvent.cs` — mirror a simple child entity (`src/Domain/Entities/TriviaAnswerSubmission.cs` — a `BaseEntity`-derived aggregate child with a factory method)
- create `src/Domain/Enums/SessionEventActorType.cs` — mirror `src/Domain/Enums/SessionState.cs`
- edit `src/Domain/Events/SessionStateChangedEvent.cs` — add defaulted `ResponsibleUserId`, `Reason`, `ActorType`
- edit `src/Domain/Entities/LiveSession.cs` — `MoveTo` (`:281-294`) accepts the actor, appends a `SessionEvent`, threads actor + reason into `SessionStateChangedEvent`; add the `SessionEvents` collection; `CompleteActiveSubstageAndAdvance` (`:778`) records a `System` actor
- edit `tests/UnitTests/Domain/Entities/LiveSessionTests.cs` — mirror existing `MoveTo` tests

**Pattern this phase owns:** `State` (mandated) — verified/inherited from HU-21A; not modified.
**Gate:** Domain build passes; unit tests prove (a) each valid `MoveTo` appends exactly one append-only `SessionEvent` capturing `OccurredAt` + `ActorType`/`ActorId` (responsible user) + `PayloadSummary` (previous→next + reason); (b) a system-driven transition (`CompleteActiveSubstageAndAdvance` → `Finished`) records `ActorType = System`, `ActorId = null`, no reason; (c) `SessionStateChangedEvent` now carries `ResponsibleUserId` + `Reason`; (d) a rejected edge appends **no** `SessionEvent` and raises no event. **`State` pattern verified — transitions still decided by per-state types; the graph is unchanged.**

### Phase X.2 — Application
**Derive** (`required_patterns_matrix.md:42,109`; `CONTEXT.md` §Facade; MassTransit outbox handoff; mirror `PublishSessionResultsFinalizedIntegrationEventHandler` + `AnswerRegisteredIntegrationEvent` + `OutboxDomainEventDispatcher`):
- The transition handler (`TransitionSessionStateCommandHandler`, the realized Facade — no standalone `*Facade.cs`) surfaces the acting operator's numeric `UserId` (from `IAuthenticatedActorProfileAccessClient.GetCurrentAsync`, already fetched by `SessionAdministrationAuthorizationProxy` on the Operator path; either return it from the resolver or fetch it in the handler — `null` for the Administrator/system path) and threads it through `SessionTransitionContext` → `MoveTo`.
- New `SessionStateChangedIntegrationEvent` record (`Application/Sessions/Common/`), decorated `[EntityName("session-state-changed")]`: `(Guid LiveSessionId, SessionState PreviousState, SessionState CurrentState, DateTimeOffset ChangedAt, int? ResponsibleUserId, string? Reason)` — mirror `AnswerRegisteredIntegrationEvent`/`SessionResultsFinalizedIntegrationEvent`.
- New `PublishSessionStateChangedIntegrationEventHandler` (`Application/Sessions/EventHandlers/`) — a plain `sealed class` (NOT `INotificationHandler`) with ctor `(IPublishEndpoint, ILogger<…>)` and `Handle(SessionStateChangedEvent notification, CancellationToken ct)`: map domain event → integration event, `await _publishEndpoint.Publish(evt, ct)` inside `try/catch` that logs + **rethrows** — mirror `PublishSessionResultsFinalizedIntegrationEventHandler` exactly, but publish on **every** transition (no Finished gate).
- Route it: inject the new handler into `OutboxDomainEventDispatcher`, and change the `SessionStateChangedEvent` arm of `DispatchAsync` to await **both** `_sessionResultsFinalized.Handle(...)` **and** `_sessionStateChanged.Handle(...)` (sequentially — shared scoped DbContext). Dispatch already runs pre-commit via `DispatchDomainEventsInterceptor` (no interceptor change).

**Target files** (create | edit — file to mirror):
- edit `src/Application/Sessions/Commands/TransitionSessionState/TransitionSessionStateCommandHandler.cs` (`:31-59`) — obtain + thread responsible `UserId`
- edit `src/Application/Sessions/StateTransitions/SessionTransitionContext.cs` — add `ResponsibleUserId`
- edit `src/Application/Sessions/Common/Authorization/SessionAdministrationAuthorizationProxy.cs` — surface the resolved actor `UserId` (only if choosing the resolver-returns-actor approach; otherwise leave untouched and fetch in the handler)
- create `src/Application/Sessions/Common/SessionStateChangedIntegrationEvent.cs` — mirror `AnswerRegisteredIntegrationEvent.cs` (incl. `[EntityName("session-state-changed")]`)
- create `src/Application/Sessions/EventHandlers/PublishSessionStateChangedIntegrationEventHandler.cs` — mirror `PublishSessionResultsFinalizedIntegrationEventHandler.cs` (no Finished gate)
- edit `src/Application/Sessions/EventHandlers/OutboxDomainEventDispatcher.cs` — inject the new handler; `SessionStateChangedEvent` arm invokes both publishers
- create `tests/Application.UnitTests/Sessions/EventHandlers/PublishSessionStateChangedIntegrationEventHandlerTests.cs` — mirror `PublishSessionResultsFinalizedIntegrationEventHandlerTests.cs` (publishes exactly one `SessionStateChangedIntegrationEvent`; a `Publish` failure **propagates** — `act.Should().ThrowAsync()`) using `Mock<IPublishEndpoint>`
- edit `tests/Application.UnitTests/Sessions/Commands/TransitionSessionState/TransitionSessionStateCommandHandlerTests.cs` — assert the responsible `UserId` reaches `MoveTo`

**Pattern this phase owns:** `Chain of Responsibility` (mandated — verified/inherited, not modified) + **`Facade`** (mandated — the outbound `SessionStateChanged` publication boundary, realized by the publish handler routed pre-commit from `OutboxDomainEventDispatcher`, no standalone `*Facade.cs`).
**Gate:** Application build passes; the existing CoR chain is still exercised (order + first-failure short-circuit) — verified, not changed; the handler threads the responsible operator `UserId` into `MoveTo`; `PublishSessionStateChangedIntegrationEventHandler` maps `SessionStateChangedEvent` → `SessionStateChangedIntegrationEvent` and publishes via `IPublishEndpoint`, and a publish/outbox-insert failure **propagates** (`act.Should().ThrowAsync()`, rolling the transaction back); `OutboxDomainEventDispatcher` routes `SessionStateChangedEvent` to **both** the results-finalized and the new audit publisher; `TransitionSessionStateCommand` remains `[Authorize(Roles = "Operator")]`. **`Chain of Responsibility` verified — ordered composable validators; `Facade`/event-publication boundary present as a pre-commit outbox publish handler (no standalone `*Facade.cs`).**

### Phase X.3 — Infrastructure
**Derive** (`Infrastructure/Persistence/Configurations/LiveSessionConfiguration.cs` OwnsMany children `:603+`; `bd_umbral_entity_spec.md:453-480`):
- Persist the append-only `SessionEvent` as a `live_session_events` table via `builder.OwnsMany(session => session.SessionEvents, eventBuilder => { eventBuilder.ToTable("live_session_events"); … })` **inside `LiveSessionConfiguration.cs`** (mirror the `TriviaAnswerSubmissions` OwnsMany block): FK `live_session_id`, `occurred_at`, `actor_type` (enum→string conversion), `actor_id` (nullable int), `event_type`, `payload_summary`, `correlation_id`, `HasKey(e => e.Id)`. Append-only (no update path exercised). `LiveSession.State`/`LastStateChangedAt`/`StateReason` columns already exist (`LiveSessionConfiguration.cs`) — **no change to `live_sessions`**.
- **No transport wiring.** MassTransit topology is resolved by `[EntityName("session-state-changed")]` on the contract (X.2); the EF bus outbox + `MassTransitMessagingRegistration` are already configured. There is no `RabbitMqIntegrationEventPublisher`/routing-key switch to touch (deleted by #166).

**Target files** (create | edit — file to mirror):
- edit `src/Infrastructure/Persistence/Configurations/LiveSessionConfiguration.cs` — add the `SessionEvents` `OwnsMany` block (mirror `TriviaAnswerSubmissions` `:603+`)
- new migration under `src/Infrastructure/Migrations/` (`AddSessionEvents`) — `dotnet ef migrations add` (needs Docker; run with sandbox disabled per `session-operations-masstransit-outbox-implementation-handoff`); **grep** `ApplicationDbContextModelSnapshot.cs` for the new table rather than full-reading it
- create/extend `tests/IntegrationTests/Persistence/` — a repository/persistence test that a transitioned session round-trips its appended `SessionEvent` (mirror an existing `Persistence` test)

**Pattern this phase owns:** none (MassTransit/RabbitMQ **transport** via the existing EF bus outbox — already configured).
**Gate:** Infrastructure build passes; `ef migrations add` produces a migration that adds `live_session_events` (append-only child of `live_sessions`) with `occurred_at`/`actor_type`/`actor_id`/`payload_summary`/`correlation_id`, **no change to `live_sessions`** and **no change to the outbox tables**; a persistence integration test round-trips the `SessionEvent` appended by a transition (date + actor + reason).

### Phase X.4 — Api
**Derive** (`Api/Controllers/SessionsController.cs` transition endpoint — HU-21A; `tests/IntegrationTests/Messaging/`; ADR-0005 coverage):
- **No new endpoint.** `PATCH /api/sessions/{liveSessionId}/state` (Operator-guarded) is unchanged. X.4 proves the audit + publish end-to-end: a valid transition returns `200`, persists a `SessionEvent`, and enqueues `SessionStateChangedIntegrationEvent` into the outbox → delivered to RabbitMQ by `BusOutboxDeliveryService`; and a broker outage does **not** fail the transition (bus outbox off the write path, AC #4).
- The endpoint inherits the standard `Operator` guard (ADR-0001/0002/0009); HU-21 is not in the applies-where `Proxy` set → no new gate.

**Target files** (create | edit — file to mirror):
- edit `tests/IntegrationTests/Api/TransitionSessionStateEndpointTests.cs` — a valid transition → `200` + a `SessionEvent` row persisted with actor/reason
- create/extend an end-to-end publish test under `tests/IntegrationTests/Messaging/` — mirror `Messaging/OutboxDeliveryOnRecoveryTests.cs` / `Messaging/MassTransitRemainingIntegrationEventPublishTests.cs` / `Api/RoundClosePublicationEndToEndTests.cs`: a transition enqueues/delivers `SessionStateChanged` with previous/next + responsible user; assert the transition still commits (and retains a pending outbox row) when the broker is unavailable
- keep (verify, do not reshape) `src/Api/Controllers/SessionsController.cs`

**Pattern this phase owns:** none new — `State`/`Chain of Responsibility` are realized/verified in X.1/X.2, the `Facade` publication boundary in X.2. Endpoint inherits the standard guard.
**Gate:** end-to-end integration test — a valid transition → `200`, a `SessionEvent` persisted (date + responsible user + reason), and `SessionStateChanged` enqueued to the outbox and delivered to the broker; the transition commits (and retains a durable pending outbox row) even when the broker is down (bus outbox off the critical path); **ADR-0005 coverage** (service ≥ repo gate).

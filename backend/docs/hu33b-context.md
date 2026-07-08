# HU-33B Context — Round-close & final-results async publication (RabbitMQ)

> Paste this section into any agent session that needs context for HU-33B (DES-45).
> Last updated: 2026-07-08 | Branch: `feature/hu-33b-trivia-round-close-results-publication`
> Builds directly on the **Done** HU-33A (DES-78) runtime. HU-33A already closes each trivia
> question, advances substages, reaches `Finished` via `SessionCompletion`, and broadcasts
> `QuestionActivated`/`QuestionClosed`/`SubstageAdvanced`/`SessionStateChanged` over SignalR.
> HU-33B adds the **one thing HU-33A deliberately deferred**: publishing the round-close and
> final-results **facts to RabbitMQ** after transactional success, for asynchronous
> history/consolidation, without the runtime flow depending on RabbitMQ. It computes **no**
> score/ranking (canon: emitted, not computed — that is `ScoringMonitoring`, HU-37A/37B/39B).
>
> Boundary note: `SessionOperations` is the **runtime authority** — it owns *when* a question
> closes and *when* the session finishes, and it **emits** those facts. Scoring/history are
> derived downstream from the emitted facts; session-ops never computes puntaje/ranking here
> (`CONTEXT.md:195-197`, ADR-0005, PRD DES-70 out-of-scope `:271`).

## State

- DES-45 (HU-33B): **Todo**, labels: `ready-for-agent`, `svc:session-operations-service`, `Feature`. Blocked-by (Linear) DES-44 (HU-33A) + DES-51 (HU-37A) — see supersession + cross-service notes below.
- **Resolved mode: feature flow** — DES-45 carries **neither** `canon-realign` **nor** `needs-rebuild`, so it is a standard feature build, not a realignment rebuild. **But** the mission-runtime realignment touched this service, so `canon-realignment-after-mission-runtime-rewrite.md` is a required input and the **pre-canon AC is read through canon** (D-1 below): the ticket body's "calcula puntaje y ranking" / "la sesión pasa a resultados finales" predates the rewrite that made trivia a substage and moved scoring to `ScoringMonitoring`.
- **Superseded handling applied:** DES-45's Linear `blockedBy` still names **DES-44 (HU-33A cycle-1, Canceled)** — it is in the realignment map's superseded column (`:69,:146`) and was **rebuilt as DES-78**. The real predecessor is **DES-78 (HU-33A realign, Done)**; DES-44 is **not** a predecessor and is never anchored on. This is the map's substitution rule (`generator-agent.md` step 3).
- **Cross-service blocker DES-51 (HU-37A) is NOT a predecessor to read.** It lives in `scoring-monitoring-service` (a different service/aggregate) and is **Todo** (not built). It is the eventual **consumer** of the events HU-33B publishes, not a seam HU-33B builds on. Unlike HU-33A's D-1 (which deferred RabbitMQ *because* no consumer existed), **HU-33B's AC #6 explicitly mandates the publish** ("publicar … como eventos de dominio a RabbitMQ para historial/consolidación asíncrona") — the **producer** side is landed here and is demonstrable independently of HU-37A. This is the sprint's designated RabbitMQ producer HU (`trivia_sprint_required_patterns_matrix.md:30,33,75`).
- Predecessor DES ids (build-on, Done/merged): **DES-78 (HU-33A — the direct seam: `TriviaRoundOrchestratorFacade`, `QuestionClosedEvent`, `SubstageAdvancedEvent`, `SessionCompletion → Finished`, the SignalR broadcasts)**, **DES-76 (HU-21A — `SessionStateChangedEvent` + `TransitionSessionStateFacade`, the `→Finished` fact)**, **DES-77 (HU-22 — the timer worker that drives `CloseAndAdvanceAsync`, the close trigger point)**. Landed-untouched: DES-22 (HU-15) / DES-75 (HU-16) snapshot foundation, DES-25/26/27 (HU-18/19/20), DES-11/12 (HU-07A/07B).
- PRD DES id: **DES-70** → `backend/docs/prd/DES-70-primera-implementacion-de-session-operations-service-hu-15-a-hu-36.md` (authoritative; never re-fetch PRD scope from Linear). Overlaid by `backend/docs/canon-realignment-after-mission-runtime-rewrite.md` + `backend/adr/0005-substage-advancement-pointer-and-timer-driven-orchestration.md`.
- Governing inputs: **ADR-0005** (winner **emitted, not computed**; "no dedicated event until a consumer needs a signal these two cannot supply" — reuse the existing facts). **PRD DES-70 `:271`** puts "Score calculation, ranking ownership, penalties, and derived projections" **out of scope**. The RabbitMQ **broker already exists** (`backend/docker-compose.yml:47` `rabbitmq:3-management`, ports `5672`/`15672`) — HU-33B wires the **first .NET publisher** in the backend to it (no service publishes today; the AMQP producer is bootstrapped here). Canonical publisher guidance: the `rabbitmq-events-dotnet` skill (`backend/.claude/skills/rabbitmq-events-dotnet/`).
- Branch: `feature/hu-33b-trivia-round-close-results-publication`, base **`develop`** (HU-33A/DES-78 is Done/merged; no same-service predecessor In Progress).

## Required design patterns

HU-33 is the only backlog sprint area mandating **three** patterns (`trivia_sprint_required_patterns_matrix.md:75`, transport `SignalR + RabbitMQ`). HU-33A **realized** all three over the runtime; **HU-33B reuses those seams** and adds the RabbitMQ transport — it does not re-invent State/Strategy/Facade.

| Pattern | Owning phase | Why (from patterns matrix) | Concrete obligation in HU-33B |
|---|---|---|---|
| `Facade` (mandated) | X.2 Application | `trivia_sprint_required_patterns_matrix.md:75`; `CONTEXT.md:209-211`: the Facade "executes session operations and **triggers outbound event publication**". | The close/finish path already funnels through `TriviaRoundOrchestratorFacade` (question close) and the `SessionCompletion → Finished` transition (HU-33A/21A). HU-33B keeps that the **single** entry point: the facade raises the domain facts; **MediatR publish handlers** bridge those facts to RabbitMQ. No publish call scattered across the timer worker, endpoint, or repository. |
| `State` (mandated) | X.1 Domain | `trivia_sprint_required_patterns_matrix.md:75`; `CONTEXT.md:213-215`. | The publishable facts are raised **only on legitimate lifecycle transitions**: `QuestionClosedEvent` on a real `CloseActiveQuestion`, `SessionStateChangedEvent(→Finished)` only via `SessionCompletion` (never operator-forced — `CONTEXT.md:63-65,185-187`). Reused from HU-33A/21A; no new state type. |
| `Strategy` (mandated) | X.1 Domain (reused seam) | `trivia_sprint_required_patterns_matrix.md:75` — "score/ranking variation". | **Reused, not newly realized here.** The score/ranking `Strategy` the matrix's "Why" cites lives **downstream in `ScoringMonitoring`** (HU-37A/37B/39B `:86-88`), per Runtime-Authority + PRD out-of-scope `:271`. In session-ops HU-33B, the **close-vs-advance** decision that determines *which* fact is published is driven by the existing `IQuestionActivationStrategy` seam (HU-33A), not ad-hoc index math. Per ADR-0012 (genuine-vs-ceremony), **do not manufacture a score Strategy in session-ops** — that would be ceremony that also violates the boundary. |

Transport note: HU-33B carries **SignalR + RabbitMQ** (`trivia_sprint_required_patterns_matrix.md:29,30,75`). **SignalR is already satisfied** by HU-33A/21A (`QuestionClosed`, `SessionStateChanged(→Finished)`, `SubstageAdvanced` broadcast to `live-session:{id}`) — X.4 **verifies** it, does not re-add it. **RabbitMQ is the new hard gate** (X.3 producer + X.4 smoke): publish `QuestionClosed` and `SessionResultsFinalized` integration events to a durable `topic` exchange after transactional success. This is the sprint's demonstrated producer (`:30,:33`); the consumer (HU-37A) lands separately.

Applies-where note (no new gate): HU-33B adds **no new protected endpoint** (D-4); it is publish/broadcast-centric. It is **not** in the applies-where `Proxy` set (HU-04/05/36B) — **no new `Proxy` gate**.

## Resolved decisions (committed scope)

Surfaced rather than guessed (`generator-agent.md` constraint 3), resolved 2026-07-08 against the canon authority chain (canon docs / ADR-0005 > tracker AC > existing code). These are committed scope, not pending gates.

- **D-1 — RESOLVED: session-ops computes NO puntaje/ranking; AC #1 is read through canon.** The ticket's "Al cerrar cada pregunta, el sistema calcula puntaje y ranking" predates the mission-runtime rewrite. PRD DES-70 `:271` lists "Score calculation, ranking ownership, penalties, and derived projections" **out of scope** for this service; `ddd_solution_model.md:177` + scoring-monitoring `CONTEXT.md:3` (Derived Views) assign scoring **and** ranking to `ScoringMonitoring`; `CONTEXT.md:195-197` (Runtime Authority) and ADR-0005 (`adr/0005-substage-advancement...`) make the winner/ranking **emitted, not computed**. HU-33B therefore publishes the **facts** (question closed, results finalized); `ScoringMonitoring` (HU-37A ledger, HU-37B/39B ranking) derives puntaje/ranking from them. The canon reading of AC #1/#3/#4 is: on question close and on final results, **emit the fact for async consolidation** — not compute a score.
- **D-2 — RESOLVED: reuse existing domain events; add no new domain event.** The publishable facts are `QuestionClosedEvent` (per-question close) and `SessionStateChangedEvent(→Finished)` (final results). ADR-0005 (Consequences): "No dedicated `TriviaSubstageCompletedEvent` is added until a consumer needs a signal these two cannot supply." History/consolidation is served by these two + the existing `SubstageAdvancedEvent` stream — so **no new `Domain/Events/*` type is introduced**. "SessionResultsFinalized" is an **Application integration-event contract** (a message shape) mapped from `SessionStateChangedEvent(→Finished)`, not a new domain event.
- **D-3 — RESOLVED: best-effort post-commit publish; the runtime never depends on RabbitMQ (AC #6).** Domain events dispatch inside `SaveChanges` via `DispatchDomainEventsInterceptor` (see the capture-before-persist comment in `TriviaRoundOrchestratorFacade.cs:103-105`), i.e. **after** the entity write — that is "tras el éxito transaccional". The RabbitMQ publisher **logs and swallows** connection/publish failures so a **down** broker can never fault the dispatch. Because the dispatch runs **synchronously on the runtime thread** (`SavedChangesAsync`), swallowing exceptions is **not sufficient** for a **slow/unresponsive** broker — the publish must **not block the thread on the broker ack**: bound the confirm wait with a short timeout **or** offload the publish to a background channel (`System.Threading.Channels`). An unbounded `WaitForConfirms` on the dispatch path is forbidden. **No transactional outbox in this slice** — best-effort at-most-once is sufficient for history/consolidation. _ponytail ceiling:_ add an outbox only if at-least-once delivery becomes a hard requirement for a consumer.
- **D-4 — RESOLVED: no new REST endpoint; publish/broadcast-centric X.4 (mirrors HU-33A D-3).** "Resultados finales" is the existing `Finished` state (HU-33A/21A) and its SignalR broadcast; the final-results **read** surfaces are downstream (HU-39B ranking, HU-36B post-close review). HU-33B adds the **RabbitMQ producer + contract**, not a REST route. X.4 verifies the producer + the (already-present) SignalR broadcasts and locks coverage.

## What predecessors have already landed

**Build-on (read for derivation):**

- **DES-78 (HU-33A) — Done (the direct seam).** `TriviaRoundOrchestratorFacade.CloseAndAdvanceAsync` closes the active question, captures `QuestionClosedEvent` (`QuestionClosedEvent.cs`: `LiveSessionId`, `QuestionIndex`, `ClosedAt`, `WasExpiredByTimer`), broadcasts `QuestionClosed` over SignalR, advances the substage (`SubstageAdvancedEvent`), and on the final substage routes through `SessionCompletion → MoveTo(Finished)` (raising `SessionStateChangedEvent(→Finished)`). **These are exactly the facts HU-33B publishes to RabbitMQ** — HU-33B adds a transport, it does not change the runtime. Domain events are dispatched by `DispatchDomainEventsInterceptor` on `SaveChanges` and MediatR auto-registers `INotificationHandler<>`s from the Application assembly.
- **DES-76 (HU-21A) — Done.** `SessionStateChangedEvent(previous, current, changedAt)` + `TransitionSessionStateFacade` + the existing `SessionStateChangedNotificationHandler` (bridges the state-change domain event onto a SignalR broadcast). **This handler is the exact mirror** for HU-33B's publish handlers (domain event → transport port). Do not touch the transition matrix or the state broadcast.
- **DES-77 (HU-22) — Done.** `AuthoritativeSessionTimerWorker` drives `CloseAndAdvanceAsync` on question-timer expiry — the trigger that produces each `QuestionClosedEvent`. HU-33B does **not** touch the worker; it hangs a publish handler off the fact the worker already causes.

**Landed, untouched by this HU:** DES-22 (HU-15) / DES-75 (HU-16) snapshot content, DES-25/26/27 (HU-18/19/20 team/operator/reads), DES-11/12 (HU-07A/07B membership/reconnect).

**Not a predecessor:** DES-44 (HU-33A cycle-1, Canceled — superseded by DES-78); DES-51 (HU-37A, `scoring-monitoring-service`, Todo — the downstream **consumer**, a different service, not read here).

**Coverage:** session-operations-service carries the HU-07/15/16/17/18/19/20/21A/22/33A baseline; measure the real service percentage against the ADR-0005 (coverlet) repo gate at X.4 — do not assume a carried-forward number.

## What this HU adds

| Concern | New work |
|---|---|
| Integration-event port | `IIntegrationEventPublisher.PublishAsync(evt, ct)` (`Application/Common/Interfaces/`) — the outbound-publication seam, mirroring the existing broadcaster ports. |
| Integration-event contracts | `QuestionClosedIntegrationEvent` + `SessionResultsFinalizedIntegrationEvent` message shapes (`Application/Sessions/Common/`, mirror `QuestionClosedNotificationDto`/`SessionStateChangedNotificationDto`). Carry only history-correlation fields — **no** score/ranking (D-1). |
| Publish handlers | MediatR `INotificationHandler<QuestionClosedEvent>` and `INotificationHandler<SessionStateChangedEvent>` (guard `CurrentState == Finished`) that map the domain fact → integration event → `IIntegrationEventPublisher` (`Application/Sessions/EventHandlers/`, mirror `SessionStateChangedNotificationHandler`). |
| RabbitMQ producer (bootstrap) | `RabbitMqIntegrationEventPublisher : IIntegrationEventPublisher` + `RabbitMqOptions` + a long-lived connection/channel, publishing to a durable `topic` exchange with stable routing keys, persistent messages, publisher confirms, and `message_id`/`type`/`content_type` headers (`rabbitmq-events-dotnet` skill). **First AMQP publisher in the backend.** |
| Resilience | Connection/publish failures **logged and swallowed** — the domain-event dispatch (and the runtime flow) never faults on a broker problem (D-3, AC #6). |
| Config + DI | `RabbitMq` appsettings section (host `rabbitmq`, port `5672`, exchange, vhost, guest credentials for the compose image); register the publisher in `AddInfrastructureServices` (mirror the options + singleton pattern). |
| Contract doc + smoke | Document the published contract (exchange, routing keys, message schemas) for HU-37A/history; smoke that a closed question and a finished session land messages on the exchange. |
| Verify (not re-add) | The `QuestionClosed` / `SessionStateChanged(→Finished)` SignalR broadcasts already exist (HU-33A/21A); X.4 verifies them alongside the new RabbitMQ publish. |

## Touched surfaces

- `backend/services/session-operations-service/` — domain (verify the publishable facts are State-gated; **no new type**, D-2), application (`IIntegrationEventPublisher` port + integration-event contracts + two publish handlers), infrastructure (`RabbitMqIntegrationEventPublisher` + options + connection + DI; add `RabbitMQ.Client`), api (composition-root wiring already covered by `AddInfrastructureServices`; contract doc + smoke + coverage).
- `frontend/` — **none.** HU-33B is a backend async-publication slice; the live UI already renders close/finish over SignalR (HU-33A). The frontend step is a no-op stub (D-4).
- Runtime/contract boundary: **new RabbitMQ contract** (exchange `umbral.session-operations`, routing keys `session.question.closed` / `session.results.finalized`, message schemas) — the integration seam `ScoringMonitoring`/history (HU-37A) consumes. **No REST contract change** (D-4). **No SignalR contract change** (reused).

## Committed phases

| Commit | Phase | Description |
|---|---|---|
| _pending_ | X.1 Domain | Lock the publishable facts (`QuestionClosedEvent`, `SessionStateChangedEvent(→Finished)`) as State-gated; confirm no new event needed (D-2); no score computed |
| _pending_ | X.2 Application | `IIntegrationEventPublisher` port + integration-event contracts + two publish handlers bridging the domain facts to the port |
| _pending_ | X.3 Infrastructure | `RabbitMqIntegrationEventPublisher` + `RabbitMqOptions` + connection + DI; publishes to the durable topic exchange, resilient (D-3) |
| _pending_ | X.4 Api | Publish smoke + SignalR-broadcast verify + contract doc + ADR-0005 coverage; no new REST endpoint (D-4) |

## Known quirks / gotchas

- **Do NOT compute puntaje/ranking (D-1).** PRD `:271` + Runtime-Authority + ADR-0005 put score/ranking in `ScoringMonitoring`. If a phase starts summing `TriviaQuestionScore` or ordering a `SessionRanking` in session-ops, it has crossed the boundary — stop and publish the fact instead.
- **Do NOT add a new domain event (D-2).** Reuse `QuestionClosedEvent` + `SessionStateChangedEvent(→Finished)`. ADR-0005 explicitly defers a dedicated completion event until a consumer needs one these two cannot supply — they can.
- **Publish must be best-effort (D-3).** The publisher runs inside the `SaveChanges` domain-event dispatch; if it throws, it can roll back the runtime write. It **must** catch+log broker failures **and must not block the runtime thread on the broker ack** — a *slow* broker is as forbidden as a *down* one (bounded confirm timeout or background-channel offload). The gate asserts neither a throwing nor a slow/unresponsive publisher faults or stalls the flow.
- **The broker already exists; the .NET producer does not.** `docker-compose.yml:47` runs `rabbitmq:3-management`; **no** service references `RabbitMQ.Client` yet. X.3 adds the package + connection. Point the connection at host `rabbitmq` (compose service name), port `5672`, default `guest`/`guest`.
- **MediatR auto-registers handlers** (`RegisterServicesFromAssembly` in `Application/DependencyInjection.cs`) — the two publish handlers need **no** explicit registration; only the `IIntegrationEventPublisher` singleton is wired in `AddInfrastructureServices`.
- **Namespace is `umbral_backend.*`** across all layers; source root `src/`. Domain tests `tests/UnitTests/`, application tests `tests/Application.UnitTests/`, integration `tests/IntegrationTests/`. API layer is **Controllers** + `Program.cs` composition (not minimal-API); real-time impls live in `Api/Hubs` or `Infrastructure/Realtime`.
- **Application-layer placement (ADR-0011/0012):** the port goes in `Application/Common/Interfaces/`, the integration-event contracts + handlers under `Application/Sessions/` (Common + EventHandlers) alongside the notification DTOs/handlers they mirror. The RabbitMQ impl is Infrastructure. **No** `Handlers/`/`DTOs/` type-buckets, no handler base class (generator constraint 11).
- **RabbitMQ integration test** (X.3) should use a Testcontainers RabbitMQ (the skill's "focused integration tests"); if that is not already available in the test stack, assert resilience instead (a broker-down publish logs and does not throw) and cover the mapping via the fake publisher in X.2 — do not weaken the D-3 gate.

## Per-phase derivation — authoritative for implementation

> Primary source for the phase subagent (per `backend-agent.md` "Read first"). Do not re-read the
> full canon or re-inspect the tree; open a cited section only to fill a gap a block leaves open.
> Mode = **feature flow**; canon source = the standard doc set read **through** the realignment
> overlay + ADR-0005, and the **AC read through canon** (D-1). Publisher specifics: the
> `rabbitmq-events-dotnet` skill. Patterns `trivia_sprint_required_patterns_matrix.md:75`; placement
> ADR-0011 (`structure.md`) + ADR-0012 (genuine-vs-ceremony — Strategy is reused, not manufactured).

### Phase X.1 — Domain
**Derive** (`CONTEXT.md:63-65,177-179,185-187,195-197`; ADR-0005 Consequences; `QuestionClosedEvent.cs`, `SessionStateChangedEvent.cs`):
- The two publishable facts already exist and are the correct signals (D-2): `QuestionClosedEvent(LiveSessionId, QuestionIndex, ClosedAt, WasExpiredByTimer)` for round-close; `SessionStateChangedEvent(LiveSessionId, PreviousState, CurrentState, ChangedAt)` with `CurrentState == Finished` for final results. **No new `Domain/Events/*` type** — confirm these carry the correlation fields async history needs; if a genuine field gap exists, add the field, not a new event, and cite the gap.
- Confirm the **State-gating**: `QuestionClosedEvent` is only raised by `LiveSession.CloseActiveQuestion` (a real close), and `Finished` is reached **only** via `SessionCompletion` (`CONTEXT.md:63-65,185-187`), never operator-forced. This is the invariant the publish handlers rely on to avoid emitting spurious "results finalized".
- **No score/ranking** computed in Domain (D-1): do not add `TriviaQuestionScore` summation, `TriviaSubstageWinner`, or `SessionRanking` logic — those are `ScoringMonitoring`.

**Target files** (create | edit — file to mirror):
- keep (verify only) `src/Domain/Events/QuestionClosedEvent.cs`, `src/Domain/Events/SessionStateChangedEvent.cs`, `src/Domain/Entities/LiveSession.cs` (`CloseActiveQuestion`, `SessionCompletion → MoveTo(Finished)`) — canon-aligned (HU-33A/21A)
- edit (only if a correlation-field gap is proven) the relevant `src/Domain/Events/*.cs` — add the field; **do not** create a new event type
- edit/mirror `tests/UnitTests/Domain/Entities/LiveSessionTests.cs` — assert `QuestionClosedEvent` is raised on a real close with its fields; `SessionStateChangedEvent(→Finished)` is raised only via `SessionCompletion`; no score is computed on close/finish

**Pattern this phase owns:** `State` (facts raised only on legitimate transitions) + `Strategy` (reused seam — the close-vs-advance decision that selects the fact; **not** a new score Strategy, D-1/ADR-0012).
**Gate:** Domain build passes; a unit test locks that `QuestionClosedEvent` and `SessionStateChangedEvent(→Finished)` are raised only on legitimate transitions and carry the history-correlation fields; **no puntaje/ranking is computed in the domain** (D-1); **no new domain event type added** (D-2). **`State` verified — facts gated by the lifecycle model, not ad-hoc conditionals.**

### Phase X.2 — Application — **owns `Facade`**
**Derive** (`CONTEXT.md:209-211`; `SessionStateChangedNotificationHandler.cs` as the mirror; `TriviaRoundOrchestratorFacade.cs:70-90,103-105`; MediatR auto-registration in `Application/DependencyInjection.cs`):
- Add the outbound port `IIntegrationEventPublisher` with `Task PublishAsync(<contract>, CancellationToken)` — the seam the Facade's outbound publication is exposed through (`CONTEXT.md:209-211`), mirroring `ISessionQuestionBroadcaster`/`ISessionStateBroadcaster`.
- Add the integration-event contracts `QuestionClosedIntegrationEvent` and `SessionResultsFinalizedIntegrationEvent` (immutable records) carrying **only** history-correlation data (session id, question index / finished-at, occurred-at) — **no** score/ranking (D-1). Mirror `QuestionClosedNotificationDto.cs` / `SessionStateChangedNotificationDto.cs`.
- Add two MediatR publish handlers mirroring `SessionStateChangedNotificationHandler`: `INotificationHandler<QuestionClosedEvent>` → maps + `PublishAsync(new QuestionClosedIntegrationEvent(...))`; `INotificationHandler<SessionStateChangedEvent>` → **guard `notification.CurrentState == SessionState.Finished`**, then `PublishAsync(new SessionResultsFinalizedIntegrationEvent(...))`. These run in the `SaveChanges` domain-event dispatch = **after transactional success** (D-3, AC #6). The `TriviaRoundOrchestratorFacade` / `TransitionSessionStateFacade` remain the single orchestration entry point that raises the facts — the handlers are the transport bridge (`Facade` obligation).
- **No RabbitMQ dependency in Application** — depend only on the `IIntegrationEventPublisher` port; the impl is Infrastructure.

**Target files** (create | edit — file to mirror):
- create `src/Application/Common/Interfaces/IIntegrationEventPublisher.cs` — mirror `ISessionQuestionBroadcaster.cs`
- create `src/Application/Sessions/Common/QuestionClosedIntegrationEvent.cs`, `src/Application/Sessions/Common/SessionResultsFinalizedIntegrationEvent.cs` — mirror `QuestionClosedNotificationDto.cs` / `SessionStateChangedNotificationDto.cs`
- create `src/Application/Sessions/EventHandlers/PublishQuestionClosedIntegrationEventHandler.cs`, `src/Application/Sessions/EventHandlers/PublishSessionResultsFinalizedIntegrationEventHandler.cs` — mirror `SessionStateChangedNotificationHandler.cs`
- keep `src/Application/Sessions/Common/TriviaRoundOrchestratorFacade.cs`, `src/Application/Sessions/Commands/TransitionSessionState/TransitionSessionStateFacade.cs` (unchanged — they already raise the facts), `src/Application/DependencyInjection.cs` (handlers auto-register — **no edit**)
- create `tests/Application.UnitTests/Sessions/EventHandlers/PublishQuestionClosedIntegrationEventHandlerTests.cs` (+ the results-finalized handler test) — assert: a `QuestionClosedEvent` publishes one `QuestionClosedIntegrationEvent` via a fake `IIntegrationEventPublisher`; a `SessionStateChangedEvent(→Finished)` publishes one `SessionResultsFinalizedIntegrationEvent`, and a **non-Finished** state change publishes **nothing**; **a throwing fake publisher does not propagate** out of the handler (D-3)

**Pattern this phase owns:** `Facade` — the close/finish path stays the single entry point that raises the facts; publication bridges off those facts, not scattered across worker/endpoint/repository.
**Gate:** Application build passes; tests prove each domain fact publishes exactly one mapped integration event through the port (Finished-only for results; nothing on non-Finished transitions), the contracts carry **no** score/ranking (D-1), and a broker/publisher failure is swallowed so the flow is unaffected (D-3). **`Facade` verified — publication triggered off the orchestrator's facts, not duplicated in handlers/worker.**

### Phase X.3 — Infrastructure
**Derive** (`rabbitmq-events-dotnet` skill — SKILL.md §§1-4, REFERENCE/EXAMPLES; `Infrastructure/DependencyInjection.cs` options + singleton pattern; `docker-compose.yml:47-51`):
- Add `RabbitMqOptions` (`SectionName = "RabbitMq"`: `HostName` `rabbitmq`, `Port` `5672`, `Exchange` `umbral.session-operations`, `VirtualHost` `/`, `UserName`/`Password` `guest`) bound via `builder.Services.Configure<RabbitMqOptions>(...)` (mirror `MissionRuntimeSourceOptions`).
- Add `RabbitMqIntegrationEventPublisher : IIntegrationEventPublisher` — a **long-lived** connection + channel (never per-publish — skill Rule 1), declaring a **durable `topic`** exchange, publishing **persistent** JSON messages with **publisher confirms** and stable routing keys (`session.question.closed`, `session.results.finalized`) and `MessageId`/`Type`/`ContentType` properties. On connect/publish failure: **log and return** (D-3) — never throw into the dispatch. The publish path must be **non-blocking w.r.t. the broker**: bound the confirm wait with a short timeout **or** enqueue to a background channel and confirm off-thread — never an unbounded `WaitForConfirms` on the runtime thread. D-3 forbids a *slow* broker adding latency, not only a *down* one throwing.
- Register it in `AddInfrastructureServices` as a singleton (mirror `AddSingleton<ISessionTimerBroadcaster, SignalRSessionTimerBroadcaster>`). Add the `RabbitMQ.Client` package to `Infrastructure.csproj`.
- Add the `RabbitMq` section to `Api/appsettings.json` (+ `appsettings.Development.json` if host differs locally).

**Target files** (create | edit — file to mirror):
- create `src/Infrastructure/Messaging/RabbitMqOptions.cs` — mirror `Infrastructure/Integrations/MissionDesign/MissionRuntimeSourceOptions.cs`
- create `src/Infrastructure/Messaging/RabbitMqIntegrationEventPublisher.cs` — new (no backend mirror); ground on the `rabbitmq-events-dotnet` skill publisher template
- edit `src/Infrastructure/DependencyInjection.cs` — `Configure<RabbitMqOptions>` + `AddSingleton<IIntegrationEventPublisher, RabbitMqIntegrationEventPublisher>`
- edit `src/Infrastructure/Infrastructure.csproj` — add `RabbitMQ.Client`
- edit `src/Api/appsettings.json` — add the `RabbitMq` section
- create `tests/IntegrationTests/Messaging/RabbitMqIntegrationEventPublisherTests.cs` — publish a `QuestionClosedIntegrationEvent` and assert it lands on a bound queue (Testcontainers RabbitMQ); **and** a resilience test that a broker-down publish logs and does not throw (D-3)

**Pattern this phase owns:** none.
**Gate:** Infrastructure build passes; the publisher declares a durable topic exchange and publishes persistent, confirmed messages on the stable routing keys; an integration test proves a published integration event is received on a bound queue (or, if no Testcontainers RabbitMQ is available, a resilience test proves a broker-down publish logs + does not throw); **a slow/unresponsive broker adds no latency to the dispatch — the confirm wait is bounded or offloaded, never an unbounded block on the runtime thread**; the connection is long-lived (not per-publish). **RabbitMQ transport bootstrapped.**

### Phase X.4 — Api
**Derive** (`Program.cs` composition; `Api/Hubs/*` existing broadcasts; ADR-0005 coverage; D-4):
- **No new REST endpoint** (D-4). Confirm the publisher is composed via `builder.AddInfrastructureServices()` (already called in `Program.cs`) and the app starts with the broker configured. Verify the **existing** SignalR broadcasts still fire: `QuestionClosed` (HU-33A) and `SessionStateChanged(→Finished)` (HU-21A) to `live-session:{id}` — these satisfy AC #5; HU-33B does not change them.
- Document the published RabbitMQ contract (exchange, routing keys, message schemas) for the HU-37A/history consumer — in the service `README.md` / `structure.md` (the integration boundary).
- Smoke (Step 8.5): drive a trivia round through the gateway and observe messages on the exchange (management UI `:15672` or a test consumer).

**Target files** (create | edit — file to mirror):
- keep (verify, do not reshape) `src/Api/Program.cs`, `src/Api/Hubs/SessionsHub.cs`, `src/Api/Hubs/SignalRSessionQuestionBroadcaster.cs`
- edit `services/session-operations-service/README.md` (or `structure.md`) — document the published integration-event contract
- create/mirror `tests/IntegrationTests/Api/…` — an end-to-end check that closing a question and finishing a session produce the two integration events on the exchange (reuse the X.3 test harness), and that the SignalR `QuestionClosed`/`SessionStateChanged(→Finished)` broadcasts still reach `live-session:{id}`; auth enforced on the reads

**Pattern this phase owns:** none new — `State` in X.1, `Facade` in X.2; the reads inherit the standard operator/participant `AuthorizationBehaviour`/gateway guard (ADR-0001/0002); HU-33B is not in the applies-where `Proxy` set → no new gate. **RabbitMQ + SignalR transports verified here.**
**Gate:** an end-to-end test proves a closed question and a finished session publish the two integration events on the exchange **and** the existing SignalR broadcasts still reach `live-session:{id}`; the RabbitMQ contract is documented for HU-37A; **ADR-0005 coverage** (service ≥ repo gate); **no new REST endpoint added** (D-4).

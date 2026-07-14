# Prompt Example — HU-21 Auditoría de cambios de estado de sesión (Feature Slice)

Concrete prompt sequence for driving DES-29 (HU-21) through a full feature slice on
`feature/hu-21-session-state-change-audit`. Follows the pattern in
[workflow_for_prompts.md](./workflow_for_prompts.md).

**Key difference for this slice:** HU-21A (DES-76) already shipped and locked the session state
machine, the CoR transition validators, the transition handler, the `PATCH …/state` endpoint, and the
**SignalR** `SessionStateChanged` broadcast; the **MassTransit EF Core transactional bus outbox**
(GH #164→#166 + outbox) is also shipped on `develop`. This slice is the **audit-write half** that was
split out of the old `HU-21B`: it (1) records each valid transition as an **append-only `SessionEvent`**
capturing date + responsible user + reason, and (2) after transactional success **publishes
`SessionStateChanged`** for async audit through the bus outbox — without the transition depending on the
broker. It adds **no** new endpoint, **no** new state/edge/validator, and **no** frontend surface. The
durable queryable history (`AuditHistory`) is DES-56/HU-40A's, not this slice's.

For backend steps point to `@backend/.agents/backend-agent.md`. There is no frontend implementation in
this slice (see Step 9).

---

## Stop 1 acceptance guard

Reject this generated prompt before implementation if it does not explicitly scope all of the following:

- each valid transition appends an **append-only `SessionEvent`** (date + actor + reason)
- the **responsible operator user** is captured on the change (numeric `UserId`, not the Keycloak id)
- the **reason** is preserved when supplied
- after transactional success, `SessionStateChanged` is **published for async audit through the bus outbox** (`[EntityName("session-state-changed")]`), **off the critical path** (the broker is unreachable on the write path only via async delivery; a publish/outbox-insert failure **propagates and rolls back** — it is not swallowed; the transition still commits when the broker is down)
- the state machine, transition graph, CoR gates, and SignalR broadcast are **verified/inherited, not modified** (HU-21A)
- **no** queryable audit-history endpoint/projection is built (that is DES-56/HU-40A)
- publishing goes through the shipped **MassTransit** publish handlers (`IPublishEndpoint`, routed by `OutboxDomainEventDispatcher` pre-commit) — **not** a reintroduced `IIntegrationEventPublisher` / raw `RabbitMQ.Client` / routing-key switch (deleted by #166)

---

## Required design patterns

- `State` (mandated, X.1) — verified/inherited from HU-21A; the transition graph is unchanged. This slice only *records* the audited fact of a transition.
- `Chain of Responsibility` (mandated, X.2) — verified/inherited from HU-21A; no gate added or reordered.
- `Facade` (mandated, X.2) — ADR-0004 "session orchestration **+ outbound event publication**"; matrix "**Facade publishes the event**." Realized by the transition handler + a `PublishSessionStateChangedIntegrationEventHandler` routed **pre-commit** from `OutboxDomainEventDispatcher` (no standalone `*Facade.cs`, per ADR-0012).
- Transport `MassTransit / RabbitMQ` (X.2/X.3/X.4) — `SessionStateChanged` published for async audit after transactional success via the EF Core bus outbox (`IPublishEndpoint.Publish` = local `OutboxMessage` insert, committed with the transition, drained by `BusOutboxDeliveryService`). **SignalR** is HU-21A's and untouched.

> Gate obligation: `State` and `Chain of Responsibility` appear as **verify** gates in X.1/X.2; the
> `Facade`/publication boundary is a **build** gate in X.2 (a MassTransit publish handler whose failure
> **propagates**, dispatched pre-commit through the outbox) proven end-to-end in X.4.

---

## Pre-resolved orient (as of 2026-07-13)

> Step 1 has already been run. Paste this section into any agent session that needs context before
> picking up a phase — no need to re-run the orient prompt.

### What predecessors have already landed

DES-76 (HU-21A) is **Done**: the six-state machine, `State`-pattern classes, `SessionStateTransitionPolicy`,
the CoR transition chain (`CurrentStateGate → OperatorAssignmentGate → ParticipantReadinessGate`), the
`TransitionSessionStateCommand`/handler, the `PATCH /api/sessions/{liveSessionId}/state` endpoint, and the
SignalR `SessionStateChanged` broadcast are all shipped and locked. `LiveSession.MoveTo(nextState, occurredAt,
transitionPolicy, reason)` already sets `LastStateChangedAt`/`StateReason` and raises `SessionStateChangedEvent`;
the handler already authorizes + resolves the acting operator. DES-26 (HU-19) landed `LiveSession.AssignedOperatorUserId`
(`int?`). The **MassTransit EF-outbox backbone** is already in the service: integration events are
`[EntityName("…")]` MassTransit records in `Application/Sessions/Common/`, each with a plain publish handler
(`Publish<X>IntegrationEventHandler` injecting `IPublishEndpoint`) routed by `OutboxDomainEventDispatcher`
**pre-commit** from `DispatchDomainEventsInterceptor.SavingChanges`, so the publish is a local `OutboxMessage`
insert on the business transaction, drained to RabbitMQ by `BusOutboxDeliveryService`. No same-service
predecessor is In Progress, so the branch base is `develop`.

### What HU-21 adds on top (per DES-29 + DES-70 US14)

| Concern | New work |
|---|---|
| Append-only `SessionEvent` | New child entity of `LiveSession` (`OccurredAt`, `ActorType` Operator/System, `ActorId`, `EventType`, `PayloadSummary`, `CorrelationId`); appended on each valid transition. |
| Enriched domain event | `SessionStateChangedEvent` gains `ResponsibleUserId` + `Reason` (+ actor type), added as **defaulted** ctor params so HU-33B's consumers/tests keep compiling. |
| Responsible-user threading | Handler surfaces the operator numeric `UserId` (via `IAuthenticatedActorProfileAccessClient`, already fetched in `SessionAdministrationAuthorizationProxy`) → `SessionTransitionContext` → `MoveTo` → event + `SessionEvent`. |
| MassTransit audit publish | `SessionStateChangedIntegrationEvent` (`[EntityName("session-state-changed")]`) + `PublishSessionStateChangedIntegrationEventHandler` (mirror the existing publish handlers), routed from `OutboxDomainEventDispatcher` alongside the results-finalized publisher; published every transition, committed via the outbox, failure propagates. |
| Persistence | New `live_session_events` table — `OwnsMany` child in `LiveSessionConfiguration.cs` + migration; `live_sessions` and the outbox tables unchanged. |
| API / Frontend | No new endpoint; no frontend. X.4 verifies the audit + publish end-to-end. |

### Branch state and prerequisite

`feature/hu-21-session-state-change-audit` branches from `develop`. No same-service predecessor is In
Progress. Do **not** re-read the PRD for scope; the derivation lives in `@backend/docs/hu21-context.md`.

### Linear state (as of 2026-07-13)

- DES-29 (HU-21): **Todo**, labels: `svc:session-operations-service`, `Feature`, `ready-for-agent`. Blocked by DES-76 (Done). Blocks DES-56 (HU-40A).
- DES-70 (PRD): local file authority — `@backend/docs/prd/DES-70-primera-implementacion-de-session-operations-service-hu-15-a-hu-36.md`.

> Linear live state may have changed. Use the Linear MCP to verify DES-29 status/labels if needed, but do
> not re-fetch PRD scope from Linear.

---

## 1. Orient — read service state, PRD, and context

> **Skip this step if you have read the pre-resolved orient above.** Run it only if the service source,
> README, or Linear state may have changed since 2026-07-13.

```text
Read and summarise:
- @backend/services/session-operations-service/CONTEXT.md — §SessionState, §SessionEvent, §Facade
- @backend/docs/prd/DES-70-primera-implementacion-de-session-operations-service-hu-15-a-hu-36.md — US12-14 (:96-102)
- @backend/docs/hu21-context.md — the pre-resolved HU-21 context (your spec)
- @backend/docs/hu21a-context.md — the predecessor state-machine context (what is already shipped/locked)

Then use the Linear MCP to fetch only the current live state of DES-29 (HU-21) — status and labels.

Output: what HU-21A already shipped (do not rebuild), what HU-21 adds (SessionEvent record + MassTransit
SessionStateChanged publish through the bus outbox), and confirmation that no queryable audit-history surface
is in scope. Do not start planning or implementing yet.
```

---

## 2. Label DES-29 as ready-for-agent

```text
Use the Linear MCP to confirm DES-29 still carries svc:session-operations-service and ready-for-agent.
If ready-for-agent is missing, add it. Output the updated DES-29 ticket state and labels.
```

---

## 3. Confirm slice readiness

```text
Use the Linear MCP to confirm DES-29 carries both svc:session-operations-service and ready-for-agent, and
output its current status and acceptance criteria.

The PRD scope is in the local file @backend/docs/prd/DES-70-primera-implementacion-de-session-operations-service-hu-15-a-hu-36.md.
Do not re-fetch PRD scope from Linear.

Before planning, explicitly confirm the Stop 1 acceptance guard:
- each valid transition appends an append-only SessionEvent (date + actor + reason)
- the responsible operator user is captured (numeric UserId, not the Keycloak id)
- the reason is preserved when supplied
- after transactional success, SessionStateChanged is published for async audit through the bus outbox, off the critical path
- the state machine, CoR gates, and SignalR broadcast are verified/inherited, not modified
- no queryable audit-history endpoint/projection is built (DES-56/HU-40A)
- publishing goes through the shipped MassTransit publish handlers (IPublishEndpoint via OutboxDomainEventDispatcher) — no IIntegrationEventPublisher / raw RabbitMQ.Client / routing-key switch

Output the confirmed HU id, title, acceptance criteria, labels, and the guard confirmation before planning.
```

In the remaining examples, `HU-21` / `DES-29` are the resolved values; `DES-70` is the shared PRD reference
(local file authority).

---

## 4. Start the slice

```text
Prepare the session-state-change audit slice on branch feature/hu-21-session-state-change-audit (base develop).
This slice affects backend session-operations-service only; there is no frontend.

The pre-resolved orient above lists what HU-21A already shipped (verify/inherit, do not rebuild) and what
HU-21 adds. Do not re-read the PRD for scoping unless resolving a precise implementation detail.

Move DES-29 to In Progress and output the exact scope, branch name, base branch, and touched surfaces.
```

---

## 5. Backend phase X.1 — Domain layer

```text
Use @backend/.agents/backend-agent.md.
Implement backend phase X.1 for HU-21 in session-operations-service, per the
**X.1 derivation block in @backend/docs/hu21-context.md** (your spec — do not re-read the canon or
re-inspect the tree; open a cited canon section only to fill a gap the block leaves open).

Gate:
- Domain build passes; unit tests prove each valid MoveTo appends exactly one append-only SessionEvent
  (OccurredAt + ActorType/ActorId + PayloadSummary previous→next + reason)
- a system-driven transition (CompleteActiveSubstageAndAdvance → Finished) records ActorType = System,
  ActorId = null, no reason
- SessionStateChangedEvent now carries ResponsibleUserId + Reason (added as defaulted ctor params so the
  locked HU-33B consumers/tests keep compiling)
- a rejected edge appends no SessionEvent and raises no event
- Gate: State pattern verified — transitions still decided by per-state types; the transition graph is unchanged

Do not touch other backend layers or frontend. Do not modify the state classes, the CoR gates, or the SignalR broadcast.
```

Commit:

```text
feat(session-operations): phase X.1 - domain layer (HU-21)

Ref: HU-21
Ref: DES-29
Ref: DES-70
```

---

## 6. Backend phase X.2 — Application layer

```text
Use @backend/.agents/backend-agent.md.
Implement backend phase X.2 for HU-21 in session-operations-service, per the
**X.2 derivation block in @backend/docs/hu21-context.md** (your spec — do not re-read the canon or
re-inspect the tree; open a cited canon section only to fill a gap the block leaves open).

Gate:
- Application build passes; the existing CoR chain is still exercised (order + first-failure short-circuit) — verified, not changed
- the handler threads the responsible operator UserId (from IAuthenticatedActorProfileAccessClient) into MoveTo
- PublishSessionStateChangedIntegrationEventHandler maps SessionStateChangedEvent → SessionStateChangedIntegrationEvent
  ([EntityName("session-state-changed")]) and publishes via IPublishEndpoint; a publish/outbox-insert failure
  PROPAGATES (act.Should().ThrowAsync()), rolling the transaction back — it is not swallowed
- OutboxDomainEventDispatcher routes SessionStateChangedEvent to BOTH the results-finalized and the new audit publisher
  (sequential await on the shared scoped DbContext)
- TransitionSessionStateCommand remains [Authorize(Roles = "Operator")]
- Gate: Chain of Responsibility verified — ordered composable validators; Facade/event-publication boundary present
  as a pre-commit outbox publish handler (no standalone *Facade.cs)

Do not touch Infrastructure, Api, or frontend.
```

Commit:

```text
feat(session-operations): phase X.2 - application layer (HU-21)

Ref: HU-21
Ref: DES-29
Ref: DES-70
```

---

## 7. Backend phase X.3 — Infrastructure layer

```text
Use @backend/.agents/backend-agent.md.
Implement backend phase X.3 for HU-21 in session-operations-service, per the
**X.3 derivation block in @backend/docs/hu21-context.md** (your spec — do not re-read the canon or
re-inspect the tree; grep the model snapshot rather than full-reading it, as the block instructs).

Gate:
- Infrastructure build passes
- the SessionEvents OwnsMany child is added inside LiveSessionConfiguration.cs (mirror the TriviaAnswerSubmissions block);
  ef migrations add produces a migration adding live_session_events (append-only child of live_sessions) with
  occurred_at/actor_type/actor_id/payload_summary/correlation_id, and no change to live_sessions or the outbox tables
- a persistence integration test round-trips the SessionEvent appended by a transition (date + actor + reason)
- no transport wiring — MassTransit topology is resolved by [EntityName("session-state-changed")]; there is no
  IIntegrationEventPublisher / RabbitMqIntegrationEventPublisher / routing-key switch to touch (deleted by #166),
  no bespoke outbox
- Gate: MassTransit/RabbitMQ transport is the existing EF Core bus outbox — no reintroduced raw publisher

Do not touch Api or frontend. (ef migrations add / integration tests need Docker — run with the sandbox disabled.)
```

Commit:

```text
feat(session-operations): phase X.3 - infrastructure layer (HU-21)

Ref: HU-21
Ref: DES-29
Ref: DES-70
```

---

## 8. Backend phase X.4 — API layer

```text
Use @backend/.agents/backend-agent.md.
Implement backend phase X.4 for HU-21 in session-operations-service, per the
**X.4 derivation block in @backend/docs/hu21-context.md** (your spec — do not re-read the canon or
re-inspect the tree; open a cited canon section only to fill a gap the block leaves open).

Gate:
- no new endpoint — PATCH /api/sessions/{liveSessionId}/state is verified, not reshaped
- end-to-end integration test: a valid transition → 200, a SessionEvent persisted (date + responsible user + reason),
  and SessionStateChanged enqueued to the outbox and delivered to the broker
  (mirror Messaging/OutboxDeliveryOnRecoveryTests + Api/RoundClosePublicationEndToEndTests)
- the transition still commits when the broker is unavailable and retains a durable pending outbox row
  (bus outbox off the critical path, AC #4)
- service coverage reaches the repo gate target (ADR-0005)
- Gate: no queryable audit-history endpoint/projection introduced (that is DES-56/HU-40A)

Do not touch frontend.
```

Commit:

```text
feat(session-operations): phase X.4 - api layer (HU-21)

Ref: HU-21
Ref: DES-29
Ref: DES-70
```

---

## 8.5. Docker rebuild and smoke

```text
From the monorepo root, rebuild and restart the backend stack after the API phase:

docker compose build session-operations-service api-gateway
docker compose up -d session-operations-service api-gateway rabbitmq

Run curl smoke checks through the gateway:
- PATCH /api/sessions/{liveSessionId}/state with a valid target (as the assigned Operator) → 200
- confirm a SessionEvent row was appended (persistence/log) and a SessionStateChanged message
  ([EntityName("session-state-changed")]) was enqueued to the outbox and delivered to RabbitMQ
- stop the rabbitmq container and repeat the transition → it must still return 200 (bus outbox off the critical
  path); confirm a pending outbox row is retained and drains once rabbitmq is back up

Output: container status, smoke results, and confirmation the transition commits (durable pending row) with the broker down.
```

---

## 9. Frontend slice

**None.** HU-21 is a backend audit-write + async-publish slice with no API contract change and no UI surface.
The session-event history read/consultation surface is owned by **DES-56 (HU-40A)**, not this HU — do not
generate a frontend plan for HU-21. (If a future ticket needs it, that plan belongs to DES-56.)

---

## 10. Close-out

```text
Use the Linear MCP to re-check DES-29 acceptance criteria and labels.
Verify the final implementation against the Stop 1 acceptance guard:
- each valid transition appends an append-only SessionEvent (date + actor + reason)
- the responsible operator user is captured (numeric UserId)
- the reason is preserved when supplied
- after transactional success, SessionStateChanged is published for async audit through the bus outbox, off the critical path
- the state machine, CoR gates, and SignalR broadcast are verified/inherited, not modified
- no queryable audit-history endpoint/projection built (DES-56/HU-40A)

Run final backend verification required by the repo instructions (build + tests + ADR-0005 coverage).
Summarise: commits created, the new SessionEvent + SessionStateChanged event, tests and gates run, and any
unresolved ambiguity for DES-56 follow-up.

Create the PR:

gh pr create \
  --base develop \
  --head feature/hu-21-session-state-change-audit \
  --title "feat(session-operations): audit session state changes + publish SessionStateChanged (HU-21)" \
  --body "Implements DES-29/HU-21: records each valid session state transition as an append-only SessionEvent (date, responsible operator, reason) and publishes SessionStateChanged for async audit through the MassTransit EF Core bus outbox after transactional success, off the critical path. Verifies/inherits HU-21A's State machine, CoR validators, and SignalR broadcast; adds no new endpoint and no frontend. Producer for DES-56/HU-40A."
```

---

## Rationale

HU-21A (DES-76) already shipped the lifecycle: the `State`-pattern machine, the `Chain of Responsibility`
transition validators, the transition handler, the endpoint, and the SignalR broadcast — all locked with
tests. That slice deliberately deferred the **audit event** (it was the old `HU-21B`). This slice completes
the audit half: every valid transition is durably recorded as an append-only `SessionEvent` (satisfying
DES-70 US14 "every state change recorded with actor, time, and context") and, after transactional success,
published as `SessionStateChanged` for asynchronous audit/history — through the service's shipped **MassTransit
EF Core transactional bus outbox** (`IPublishEndpoint.Publish` → local `OutboxMessage` insert on the transition's
transaction, drained to RabbitMQ by `BusOutboxDeliveryService`), so the transition never depends on broker
availability while still guaranteeing no committed-but-unpublished window. This supersedes the pre-#166 raw
`RabbitMQ.Client` / `IIntegrationEventPublisher` transport (deleted by the MassTransit migration): the new
publisher mirrors the three shipped publish handlers, dispatched pre-commit by `OutboxDomainEventDispatcher`,
and a publish failure propagates to roll the transaction back rather than being swallowed. The queryable
consolidation of that history is intentionally **out of scope** here: it is owned by DES-56 (HU-40A) and
scoring-monitoring (DES-85), which consume this event. This is why DES-29's original query AC was stripped and
why DES-29 `blocks` DES-56.

# HU-21 — Driver brief

_Driver reads only this file. The full prompt + context are for the subagent
and human review; the driver never loads them._

**Nature of this HU:** feature build **on top of a shipped lifecycle** — HU-21A (DES-76) already shipped and
locked the state machine, CoR transition validators, transition handler, `PATCH …/state` endpoint, and the
**SignalR** broadcast; the MassTransit EF-outbox backbone (GH #164→#166 + transactional outbox) is also
shipped on `develop`. HU-21 adds only the **audit-write half**: X.1 a new append-only `SessionEvent` child
entity + enriched `SessionStateChangedEvent`; X.2 threads the responsible operator user + a MassTransit publish
handler routed pre-commit through the outbox; X.3 the `live_session_events` table; X.4 end-to-end verify. Do
**not** authorize a subagent to modify the transition graph / state classes / CoR gates / SignalR broadcast,
to reintroduce `IIntegrationEventPublisher` / raw `RabbitMQ.Client` / a routing-key switch (deleted by #166),
or to build a queryable audit-history endpoint or projection (that is DES-56/HU-40A).

## Slice
| HU | DES | PRD | Service | Branch | Base |
|----|-----|-----|---------|--------|------|
| HU-21 — Auditoría de cambios de estado de sesión | DES-29 | DES-70 | session-operations-service | feature/hu-21-session-state-change-audit | develop |

## Required pattern(s) → owning phase
- `State` (phase X.1) — lifecycle needs an explicit state model — obligation: **verified/inherited from HU-21A**; transition graph unchanged, this HU only records the fact of a transition.
- `Chain of Responsibility` (phase X.2) — ordered transition validators — obligation: **verified/inherited from HU-21A**; no gate added or reordered.
- `Facade` (phase X.2) — ADR-0004 "session orchestration + outbound event publication"; matrix "Facade publishes the event" — obligation: outbound `SessionStateChanged` publication realized by the transition handler + a `PublishSessionStateChangedIntegrationEventHandler` routed **pre-commit** from `OutboxDomainEventDispatcher` (no standalone `*Facade.cs`, ADR-0012).
- Transport `MassTransit / RabbitMQ` (X.2/X.3/X.4) — `SessionStateChanged` published for async audit through the **EF Core transactional bus outbox**: `IPublishEndpoint.Publish` is a local `OutboxMessage` insert that commits with the transition and drains to RabbitMQ async, so the broker is **off the write path** (AC #4 — not by swallowing; a publish failure propagates and rolls back). SignalR is HU-21A's, untouched.

## Per phase — gate + owned pattern
| Phase | Gate | Pattern |
|-------|------|---------|
| X.1 Domain | Domain build; unit tests — each valid `MoveTo` appends one append-only `SessionEvent` (OccurredAt + ActorType/ActorId + PayloadSummary previous→next + reason); system-driven `Finished` records `System`/null actor, no reason; `SessionStateChangedEvent` now carries `ResponsibleUserId` + `Reason` (added as defaulted params); a rejected edge appends no event | `State` (verify — graph unchanged) |
| X.2 Application | App build; CoR chain still exercised (order + short-circuit) — verified not changed; handler threads responsible operator `UserId` into `MoveTo`; `PublishSessionStateChangedIntegrationEventHandler` maps + publishes via `IPublishEndpoint` and a publish failure **propagates** (`act.Should().ThrowAsync()`); `OutboxDomainEventDispatcher` routes `SessionStateChangedEvent` to **both** results-finalized and the new audit publisher; `[Authorize(Roles="Operator")]` | `Chain of Responsibility` (verify) + `Facade` (pre-commit outbox publish boundary, no standalone class) |
| X.3 Infrastructure | Infra build; migration adds `live_session_events` (append-only `OwnsMany` child in `LiveSessionConfiguration`) with occurred_at/actor_type/actor_id/payload_summary/correlation_id, **no change to live_sessions or the outbox tables**; persistence test round-trips the appended `SessionEvent`; no routing-key/publisher wiring (MassTransit topology via `[EntityName("session-state-changed")]`) | — (MassTransit/RabbitMQ transport via existing outbox) |
| X.4 Api | **No new endpoint**; end-to-end test — valid transition → 200 + `SessionEvent` persisted + `SessionStateChanged` enqueued to the outbox and delivered; transition still commits (durable pending outbox row) with broker down; coverage gate (ADR-0005) | — |

Commit subjects — copy each phase's exact subject from the prompt's Steps 5–8 verbatim:
- X.1 `feat(session-operations): phase X.1 - domain layer (HU-21)`
- X.2 `feat(session-operations): phase X.2 - application layer (HU-21)`
- X.3 `feat(session-operations): phase X.3 - infrastructure layer (HU-21)`
- X.4 `feat(session-operations): phase X.4 - api layer (HU-21)`

Trailer (every phase): `Ref: HU-21` / `Ref: DES-29` / `Ref: DES-70`

## Acceptance criteria
- each state change is recorded with date and time (append-only `SessionEvent`)
- each change records the responsible user (numeric operator `UserId`)
- when applicable, the change preserves the associated reason
- after transactional success, the change is published as a domain event (`SessionStateChanged`) to RabbitMQ for async audit/history, without the main flow depending on RabbitMQ (bus outbox: local insert commits with the transition, drained async)

## Endpoints + smoke (driver verifies at Stop 2)
_No new endpoints — the transition endpoint from HU-21A is reused; the new fact travels via the async event._
- `PATCH /api/sessions/{liveSessionId}/state` — as the assigned Operator, valid target → expect **200**; verify a `SessionEvent` row was appended (date + actor + reason) and a MassTransit message for `SessionStateChangedIntegrationEvent` (`[EntityName("session-state-changed")]`) was enqueued to the outbox and delivered to RabbitMQ
- same transition with the `rabbitmq` container stopped → expect **200** still (bus outbox off the critical path); a pending outbox row is retained and drains on recovery

## Frontend slice
**None** — backend audit-write + async-publish only; no API contract change, no UI. The session-event history
read surface is owned by DES-56/HU-40A. Do not generate a frontend plan for HU-21.

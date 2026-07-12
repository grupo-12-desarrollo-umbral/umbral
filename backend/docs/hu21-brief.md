# HU-21 — Driver brief

> ⚠️ **STALE TRANSPORT — DO NOT DRIVE AS-IS. Regenerate after GH #164→#166.**
> Decision (2026-07-12): land the MassTransit chain **GH #164 → #165 → #166 first**. #166 deletes the raw
> `RabbitMQ.Client` publisher this brief tells you to mirror, so the transport line below ("Raw `RabbitMQ.Client`
> via existing `IIntegrationEventPublisher` — no MassTransit") **will be wrong**. Re-run the generator for HU-21
> after #166 merges; do not hand-patch. Domain + persistence (`SessionEvent` table) is transport-independent
> and unaffected.

_Driver reads only this file. The full prompt + context are for the subagent
and human review; the driver never loads them._

**Nature of this HU:** feature build **on top of a shipped lifecycle** — HU-21A (DES-76) already shipped and
locked the state machine, CoR transition validators, transition handler, `PATCH …/state` endpoint, and the
**SignalR** broadcast. HU-21 adds only the **audit-write half**: X.1 a new append-only `SessionEvent` child
entity + enriched `SessionStateChangedEvent`; X.2 threads the responsible operator user + a RabbitMQ publish
handler; X.3 the `session_events` table + routing key; X.4 end-to-end verify. Do **not** authorize a subagent
to modify the transition graph / state classes / CoR gates / SignalR broadcast, to introduce MassTransit, or to
build a queryable audit-history endpoint or projection (that is DES-56/HU-40A).

## Slice
| HU | DES | PRD | Service | Branch | Base |
|----|-----|-----|---------|--------|------|
| HU-21 — Auditoría de cambios de estado de sesión | DES-29 | DES-70 | session-operations-service | feature/hu-21-session-state-change-audit | develop |

## Required pattern(s) → owning phase
- `State` (phase X.1) — lifecycle needs an explicit state model — obligation: **verified/inherited from HU-21A**; transition graph unchanged, this HU only records the fact of a transition.
- `Chain of Responsibility` (phase X.2) — ordered transition validators — obligation: **verified/inherited from HU-21A**; no gate added or reordered.
- `Facade` (phase X.2) — ADR-0004 "session orchestration + outbound event publication"; matrix "Facade publishes the event" — obligation: outbound `SessionStateChanged` publication realized by the transition handler + a **post-commit** `PublishSessionStateChangedIntegrationEventHandler` (no standalone `*Facade.cs`, ADR-0012).
- Transport `RabbitMQ` (X.2/X.3/X.4) — `SessionStateChanged` published after transactional success, **off the critical path** (broker failure swallowed). SignalR is HU-21A's, untouched. Raw `RabbitMQ.Client` via existing `IIntegrationEventPublisher` — no MassTransit.

## Per phase — gate + owned pattern
| Phase | Gate | Pattern |
|-------|------|---------|
| X.1 Domain | Domain build; unit tests — each valid `MoveTo` appends one append-only `SessionEvent` (OccurredAt + ActorType/ActorId + PayloadSummary previous→next + reason); system-driven `Finished` records `System`/null actor, no reason; `SessionStateChangedEvent` now carries `ResponsibleUserId` + `Reason`; a rejected edge appends no event | `State` (verify — graph unchanged) |
| X.2 Application | App build; CoR chain still exercised (order + short-circuit) — verified not changed; handler threads responsible operator `UserId` into `MoveTo`; `PublishSessionStateChangedIntegrationEventHandler` maps + publishes and **swallows** broker failure; `[Authorize(Roles="Operator")]` | `Chain of Responsibility` (verify) + `Facade` (publish boundary, no standalone class) |
| X.3 Infrastructure | Infra build; migration adds `session_events` (append-only child) with occurred_at/actor_type/actor_id/payload_summary/correlation_id, **no change to live_sessions**; persistence test round-trips the appended `SessionEvent`; `session.state.changed` routing key + switch arm wired in `RabbitMqIntegrationEventPublisher` | — (RabbitMQ transport) |
| X.4 Api | **No new endpoint**; end-to-end test — valid transition → 200 + `SessionEvent` persisted + `SessionStateChanged` published on `session.state.changed`; transition still commits with broker down; coverage gate (ADR-0005) | — |

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
- after transactional success, the change is published as a domain event (`SessionStateChanged`) to RabbitMQ for async audit/history, without the main flow depending on RabbitMQ

## Endpoints + smoke (driver verifies at Stop 2)
_No new endpoints — the transition endpoint from HU-21A is reused; the new fact travels via the async event._
- `PATCH /api/sessions/{liveSessionId}/state` — as the assigned Operator, valid target → expect **200**; verify a `SessionEvent` row was appended (date + actor + reason) and a message reached the RabbitMQ topic exchange `umbral.session-operations` with routing key `session.state.changed`
- same transition with the `rabbitmq` container stopped → expect **200** still (broker off the critical path)

## Frontend slice
**None** — backend audit-write + async-publish only; no API contract change, no UI. The session-event history
read surface is owned by DES-56/HU-40A. Do not generate a frontend plan for HU-21.

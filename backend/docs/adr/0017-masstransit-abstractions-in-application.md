# 0017 — Allow transport-neutral MassTransit abstractions in Application

## Status

Accepted. Supersedes the outbound-messaging boundary stated in
[ADR-0011](0011-application-layer-vertical-slice-organization.md) §5 and the
"CQRS handlers never talk to RabbitMQ directly" rule in
[`plans/application-layer-cqrs-refactor.md`](../../plans/application-layer-cqrs-refactor.md).
ADR-0011's vertical-slice and folder-placement decisions remain unchanged.

**Amended 2026-07-12 (transactional outbox).** The *layering* decisions below — points 1-3
(Application may reference `MassTransit.Abstractions`/contracts/`IPublishEndpoint`; RabbitMQ
transport, host, and topology stay in Infrastructure; no policy-free `IIntegrationEventPublisher`
wrapper) — **still hold and are unchanged**. What changed is the *delivery semantics*: the
post-commit, bounded-blocking, best-effort publish with a linked five-second cancellation timeout
whose failures were "logged and swallowed" is **superseded** by a MassTransit EF Core
**transactional bus outbox**, per
[`session-operations-masstransit-outbox-design.md`](../session-operations-masstransit-outbox-design.md)
(shipped 2026-07-12). Under the outbox, `IPublishEndpoint.Publish` no longer talks to RabbitMQ on the
hot path — it inserts a local `OutboxMessage` row into the tracked `ApplicationDbContext` **pre-commit**,
riding the same `SaveChanges` transaction as the business write, so publish + write commit atomically.
The five-second timeout and the swallow-on-failure are **gone**: a publish is now a local DB insert, so
a failure faults the transaction and rolls back (no silent loss) rather than being swallowed. Delivery
to the broker is asynchronous, handled by the hosted `BusOutboxDeliveryService`, so a broker outage no
longer stalls gameplay for ~5s and no longer drops the event. Wherever the Context and Decision point 4
below describe the "post-commit, five-second-bounded, logged-and-swallowed" behaviour, and wherever the
Consequences say "no transactional outbox", read the outbox semantics stated here and in the design doc.

## Context

The session-operations MassTransit migration publishes integration events from
post-commit MediatR notification handlers through `IPublishEndpoint`. Requiring a
project-owned `IIntegrationEventPublisher` in front of that transport-neutral
abstraction would add a forwarding seam with no policy and leave two active,
contradictory implementation rules.

> **Superseded by the 2026-07-12 amendment (transactional outbox).** The paragraph below
> records the original best-effort rationale and no longer describes the shipped behaviour.
> Publishing is now a pre-commit local outbox insert with asynchronous delivery; see the
> amendment above and
> [`session-operations-masstransit-outbox-design.md`](../session-operations-masstransit-outbox-design.md).

MassTransit publishing is not structurally non-blocking when RabbitMQ is down.
Because domain events are dispatched after commit and the dispatch path waits for
the handler, each publish must retain the D-3 safety guard established after #164:
a linked five-second cancellation timeout, with publication failures logged and
swallowed. This is bounded-blocking, best-effort delivery; it is not an outbox and
does not provide zero-latency broker failure handling.

## Decision

1. Application may reference `MassTransit.Abstractions`, integration contracts,
   `[EntityName]`, `IPublishEndpoint`, and, where the service architecture places
   inbound adapters in Application, MassTransit messaging abstractions.
2. RabbitMQ transport packages and APIs, `UsingRabbitMq`, broker credentials and
   connections, and endpoint or topology configuration remain in Infrastructure.
   MassTransit/RabbitMQ registration stays in `Infrastructure/Messaging`, is
   exposed through Infrastructure dependency injection, and is invoked by Api as
   the composition root.
3. Do not introduce or restore `IIntegrationEventPublisher` around
   `IPublishEndpoint` when it would only forward calls. Add an Application-owned
   port only when it represents real application policy that MassTransit's
   abstraction does not provide.
4. ~~Publishing remains post-commit through the existing domain-event dispatch.
   Every publish on that path must use a cancellation token linked to the caller
   and bounded to five seconds. Timeout and other publication failures are logged
   and swallowed so a broker outage cannot fault the already-committed operation.~~
   **Superseded 2026-07-12 (see amendment above):** publishing happens **pre-commit**
   as a MassTransit EF Core bus-outbox insert that rides the business `SaveChanges`
   transaction (`DispatchDomainEventsInterceptor.SavingChanges` →
   `IPublishEndpoint.Publish` → `OutboxMessage` row). There is no five-second timeout
   and no swallow: a publish failure is a DB fault that rolls the transaction back, and
   the broker is reached asynchronously by `BusOutboxDeliveryService`. See
   [`session-operations-masstransit-outbox-design.md`](../session-operations-masstransit-outbox-design.md).
5. Future consumers remain thin transport adapters: map, validate, and deduplicate
   as required, then dispatch an Application command or query. Scoring and audit
   business rules do not belong in consumer bodies.

## Consequences

- Application depends directly on a transport-neutral third-party messaging
  abstraction, while all RabbitMQ mechanics remain outside Application.
- There is one active outbound-messaging boundary and no policy-free forwarding
  wrapper.
- ~~The current delivery semantics are preserved: post-commit, bounded-blocking
  best effort, with no transactional outbox or custom retry/topology layer.~~
  **Superseded 2026-07-12 (see amendment above):** delivery is now via a MassTransit
  EF Core **transactional bus outbox** — publish is a pre-commit local insert that
  commits atomically with the business write, and `BusOutboxDeliveryService` drains it
  to RabbitMQ asynchronously with retry. No bespoke retry/topology layer is added (the
  outbox is MassTransit's built-in), and points 1-3 above are unaffected.


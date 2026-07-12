# 0017 — Allow transport-neutral MassTransit abstractions in Application

## Status

Accepted. Supersedes the outbound-messaging boundary stated in
[ADR-0011](0011-application-layer-vertical-slice-organization.md) §5 and the
"CQRS handlers never talk to RabbitMQ directly" rule in
[`plans/application-layer-cqrs-refactor.md`](../../plans/application-layer-cqrs-refactor.md).
ADR-0011's vertical-slice and folder-placement decisions remain unchanged.

## Context

The session-operations MassTransit migration publishes integration events from
post-commit MediatR notification handlers through `IPublishEndpoint`. Requiring a
project-owned `IIntegrationEventPublisher` in front of that transport-neutral
abstraction would add a forwarding seam with no policy and leave two active,
contradictory implementation rules.

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
4. Publishing remains post-commit through the existing domain-event dispatch.
   Every publish on that path must use a cancellation token linked to the caller
   and bounded to five seconds. Timeout and other publication failures are logged
   and swallowed so a broker outage cannot fault the already-committed operation.
5. Future consumers remain thin transport adapters: map, validate, and deduplicate
   as required, then dispatch an Application command or query. Scoring and audit
   business rules do not belong in consumer bodies.

## Consequences

- Application depends directly on a transport-neutral third-party messaging
  abstraction, while all RabbitMQ mechanics remain outside Application.
- There is one active outbound-messaging boundary and no policy-free forwarding
  wrapper.
- The current delivery semantics are preserved: post-commit, bounded-blocking
  best effort, with no transactional outbox or custom retry/topology layer.


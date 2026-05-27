---
name: rabbitmq-events-dotnet
description: Designs and implements RabbitMQ-based event publishing and consumption in modern .NET applications using the Generic Host, DI, options, and background services. Use when the user mentions RabbitMQ, AMQP, queues, exchanges, consumers, publishers, DLQ/DLX, retries, event-driven integration, or reliable messaging in C#/.NET.
---

# RabbitMQ Events for .NET

Implement production-grade RabbitMQ event flows in modern .NET while adapting to the host application's conventions instead of imposing a fixed architecture.

## Quick Start

1. Inspect the project first:
   - hosting model: worker, ASP.NET Core, console, library
   - current DI/config/logging conventions
   - existing messaging abstractions, serializers, and transaction boundaries
2. Choose the event shape and delivery semantics:
   - command vs event
   - at-most-once vs at-least-once
   - transient vs durable
   - classic vs quorum queue
3. Implement the smallest reliable slice:
   - validated RabbitMQ options
   - long-lived connection management
   - topology declaration
   - publisher with confirms
   - consumer with manual ack and bounded prefetch
4. Add failure handling:
   - mandatory publish handling
   - retry or delayed retry path
   - DLX/DLQ
   - idempotent consumer behavior
5. Verify with focused integration tests and operational checks.

## Workflow

### 1. Shape the topology

- Prefer explicit exchanges over publishing to queues directly.
- Use `topic` exchanges for domain events unless routing requirements are truly simple.
- Use stable routing keys such as `orders.created` or `billing.invoice-issued`.
- Declare durable topology for durable workflows.
- Prefer RabbitMQ policies for broker-side queue behavior such as DLX, TTL, and delivery limits.

### 2. Implement publishers

- Reuse long-lived connections and channels.
- Do not share a publishing channel concurrently across threads.
- Enable publisher confirms for anything that must not be silently lost.
- Use persistent messages for durable flows.
- Set `message_id`, `type`, `content_type`, correlation data, and useful headers.
- Handle unroutable mandatory publishes.

### 3. Implement consumers

- Run consumers in `BackgroundService` or an equivalent hosted service.
- Use manual acknowledgements.
- Set a bounded prefetch and tune it deliberately.
- Deserialize or copy the delivery body before the handler returns.
- Keep handlers idempotent and cancellation-aware.
- Ack only after the side effects and persistence boundary succeed.

### 4. Operate safely

- Prefer separate connections for publishers and consumers.
- Set a client-provided connection name.
- Keep heartbeats enabled.
- Use automatic recovery, but still implement startup retry logic.
- Add health checks, structured logging, and metrics.

## Rules

- Never open a new connection per publish.
- Never rely on auto-ack for important business processing.
- Never assume RabbitMQ gives exactly-once delivery.
- Never hardcode queue arguments that should be managed by policies unless there is no broker-admin path.
- Never use polling consumption (`BasicGet`) for normal event processing.

## References

- Implementation guidance: [REFERENCE.md](REFERENCE.md)
- Code templates and examples: [EXAMPLES.md](EXAMPLES.md)

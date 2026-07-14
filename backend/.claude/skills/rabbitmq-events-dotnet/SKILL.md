---
name: rabbitmq-events-dotnet
description: Implements integration events with MassTransit over RabbitMQ in the Umbral .NET services. Use for event contracts, publishers, consumers, topology, failure queues, and messaging tests.
---

# MassTransit over RabbitMQ

Use MassTransit as the single messaging path. Do not add direct `RabbitMQ.Client` publishers,
connection/channel management, or a policy-free project-owned publisher wrapper.

## Workflow

1. Inspect the owning bounded context, its contracts, and existing MassTransit registration.
2. Define the integration-event record in Application and give it a stable, domain-meaningful
   exchange name with `[EntityName("...")]`.
3. Inject MassTransit's transport-neutral `IPublishEndpoint` directly into the MediatR event handler
   and call `Publish` after the domain event is dispatched. Use a cancellation token linked to the
   caller and bounded to five seconds; log and swallow timeout or publication failures because the
   operation has already committed.
4. Keep RabbitMQ host credentials and `UsingRabbitMq` topology configuration in Infrastructure.
5. Implement consumers with `IConsumer<T>` and register them through `AddMassTransit` /
   `ConfigureEndpoints`.
6. Verify handler mapping with unit tests and the real transport path with Testcontainers-based
   integration tests that skip only when Docker is unavailable.

## Project conventions

- Application may reference `MassTransit.Abstractions`; it must not reference the RabbitMQ transport.
- Infrastructure owns `MassTransit.RabbitMQ`, host configuration, credentials, and bus registration.
- Publish through `IPublishEndpoint`; do not introduce `IIntegrationEventPublisher` forwarding seams.
- Every post-commit publish uses a caller-linked five-second cancellation timeout and logs and
  swallows publication failures.
- Use one exchange per message type via `[EntityName]`; do not restore the former shared topic
  exchange or routing-key switch.
- Use MassTransit's serialized envelope and topology rather than hand-rolled JSON bodies.
- Let MassTransit route unhandled consumer faults to its automatic `<endpoint>_error` queue. Do not
  add a custom retry/outbox policy unless an accepted design explicitly requires one.
- Do not claim exactly-once delivery. Consumers must tolerate redelivery and make side effects
  idempotent where the use case requires it.

## References

- Repository-specific guidance: [REFERENCE.md](REFERENCE.md)
- Code examples: [EXAMPLES.md](EXAMPLES.md)

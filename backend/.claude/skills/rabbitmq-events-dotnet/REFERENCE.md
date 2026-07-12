# MassTransit + RabbitMQ Reference

## Layer ownership

Application owns event contracts and publish intent. Add `MassTransit.Abstractions`, annotate each
contract with `[EntityName]`, and inject `IPublishEndpoint` directly into its notification handler.
This dependency is transport-neutral and is the repository-approved boundary.

Infrastructure owns the transport. Register the bus with `AddMassTransit`, configure RabbitMQ with
`UsingRabbitMq`, read host settings through options, and call `ConfigureEndpoints(context)`. Never
expose RabbitMQ connections, channels, routing keys, or client types to Application.

## Contracts and topology

- Use immutable integration-event records containing only cross-context facts.
- Choose stable kebab-case exchange names in domain language, for example
  `[EntityName("session-question-closed")]`.
- MassTransit creates exchange-per-message-type topology and uses its standard message envelope.
- Consumers implement `IConsumer<T>` and receive their own endpoints through registration.
- Do not recreate the retired `umbral.session-operations` shared topic exchange.

## Publishing

Inject `IPublishEndpoint` into the MediatR notification handler and publish the mapped integration
event with a cancellation token linked to the caller and bounded to five seconds. Because publishing
is post-commit and best-effort, log and swallow timeout and other publication failures so a broker
outage cannot fault an already-committed operation. Do not hide MassTransit behind a
forwarding-only interface.

## Failures and delivery

MassTransit moves a message that exhausts its configured processing attempts to the receive
endpoint's automatic `_error` queue. Treat delivery as at-least-once: consumers should be
idempotent or deduplicate using stable business identifiers. Do not add custom retries, an outbox,
or dead-letter topology without an accepted requirement or ADR.

## Testing

- Unit-test domain-event to integration-event mapping by mocking `IPublishEndpoint`.
- Integration-test each contract with a real MassTransit bus and Testcontainers RabbitMQ.
- Assert that a registered `IConsumer<T>` receives the published contract through its `[EntityName]`
  exchange.
- Skip gracefully only when Docker/Testcontainers is unavailable; startup, authentication,
  configuration, and assertion failures must fail the test.

## Authoritative references

- MassTransit RabbitMQ transport: https://masstransit.io/documentation/configuration/transports/rabbitmq
- MassTransit producers: https://masstransit.io/documentation/concepts/producers
- MassTransit exceptions and error queues: https://masstransit.io/documentation/concepts/exceptions

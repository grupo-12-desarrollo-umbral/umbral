# RabbitMQ Events in Modern .NET

This reference is written for reusable use across worker services, ASP.NET Core apps, console hosts, and shared libraries running on modern .NET host patterns.

## Authoritative References

Primary sources used for this skill:

- RabbitMQ .NET client API guide: https://www.rabbitmq.com/client-libraries/dotnet-api-guide
- RabbitMQ .NET API reference: https://rabbitmq.github.io/rabbitmq-dotnet-client/api/RabbitMQ.Client.IChannel.html
- RabbitMQ exchanges guide: https://www.rabbitmq.com/docs/exchanges
- RabbitMQ consumers guide: https://www.rabbitmq.com/docs/consumers
- RabbitMQ consumer acknowledgements and publisher confirms: https://www.rabbitmq.com/docs/3.13/confirms
- RabbitMQ dead letter exchanges guide: https://www.rabbitmq.com/docs/3.13/dlx
- RabbitMQ quorum queues guide: https://www.rabbitmq.com/docs/quorum-queues
- RabbitMQ heartbeats guide: https://www.rabbitmq.com/docs/heartbeats
- RabbitMQ production deployment checklist: https://www.rabbitmq.com/docs/4.2/production-checklist
- Microsoft .NET worker services: https://learn.microsoft.com/en-us/dotnet/core/extensions/workers
- Microsoft .NET options pattern: https://learn.microsoft.com/en-us/dotnet/core/extensions/options
- ASP.NET Core health checks: https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/health-checks?view=aspnetcore-10.0

## Outcome

When using this skill, produce code that:

- fits the existing solution structure before adding abstractions
- uses `Microsoft.Extensions.Hosting`, DI, logging, and options cleanly
- treats RabbitMQ as at-least-once infrastructure unless the user explicitly accepts data loss
- favors operational clarity over clever abstractions

## Primary Design Choices

Decide these before writing code:

1. Event contract
   - What is the event name?
   - What payload versioning scheme exists?
   - Which fields belong in headers vs body?
2. Topology
   - Exchange type: usually `topic`
   - Routing keys: dot-separated domain vocabulary
   - Queue type: classic or quorum
   - Retry path: immediate requeue, retry queue, delayed retry, or dead-letter only
3. Delivery guarantee
   - Publisher confirms required?
   - Manual ack required?
   - Consumer idempotency strategy?
4. Startup ownership
   - Will the app declare topology?
   - Or only passively verify broker resources?
   - Or leave topology fully to infrastructure?

## Recommended Defaults

Use these defaults unless the codebase or workload argues otherwise:

- Generic Host registration with hosted consumers
- validated `RabbitMqOptions`
- one long-lived connection for publishers
- one long-lived connection per consumer service or consumer group
- one channel per publisher worker path or per consumer loop
- `topic` exchange for domain events
- durable exchanges and queues for business events
- manual consumer acknowledgements
- bounded prefetch, starting small and tuning with measurements
- publisher confirms for all important publishes
- mandatory publish handling for routing mistakes
- JSON payloads with explicit `content_type`
- idempotent handlers keyed by `message_id` or a business event id
- DLX/DLQ managed by policy when possible

## Project Discovery Checklist

Before editing code, inspect:

- project SDK and host type
- whether there is already a worker/hosted-service pattern
- serializer conventions: `System.Text.Json`, Newtonsoft, protobuf, etc.
- existing resilience libraries and logging format
- whether MediatR, outbox, inbox, or event bus abstractions already exist
- how secrets and configuration are loaded
- whether tests already spin up infrastructure with Docker/Testcontainers

Do not introduce a new messaging abstraction if the project already has one that can be extended cleanly.

## Configuration Pattern

Prefer an options class bound from configuration and validated on startup.

Suggested fields:

- `HostName` or `Endpoints`
- `Port`
- `VirtualHost`
- `UserName`
- `Password`
- `RequestedHeartbeatSeconds`
- `AutomaticRecoveryEnabled`
- `NetworkRecoveryIntervalSeconds`
- `PublisherConnectionName`
- `ConsumerConnectionName`
- topology names such as exchange, queue, routing keys
- prefetch settings

Use options validation so the app fails early on invalid config. This aligns with modern .NET options guidance.

## Connection and Channel Lifecycle

Follow these rules closely:

- Keep connections long-lived.
- Keep channels long-lived when healthy.
- Recreate a channel after channel-level exceptions.
- Do not open a connection per publish or per message.
- Do not share an `IChannel` for concurrent publishing without explicit serialization.

Why:

- RabbitMQ's .NET client guide says connections are meant to be long-lived and opening one per operation is highly discouraged.
- The same guide says concurrent use of one `IChannel` by multiple threads should be avoided and is a hard requirement for publishers.

Practical pattern:

- Use a singleton connection provider for publishers.
- Use a dedicated channel inside a publisher component, guarded by a semaphore if shared.
- Give each consumer service its own channel.
- If the app has several unrelated high-volume consumers, isolate them across services and possibly connections.

## Publisher Design

A good publisher implementation usually does all of the following:

- resolves a connection and channel once
- ensures confirms are enabled if reliability matters
- publishes to an exchange, not to a queue name
- uses `mandatory: true` when silent dropping is unacceptable
- logs and surfaces `BasicReturn` events for unroutable messages
- sets message metadata:
  - `MessageId`
  - `Type`
  - `ContentType`
  - `CorrelationId`
  - `Timestamp`
  - `AppId`
  - optional version header

For business-critical publishing, combine:

- durable exchange and queue
- persistent messages
- publisher confirms

For current RabbitMQ .NET client APIs, verify the exact publish failure surface in the version being used. The official API reference shows `BasicPublishAsync` surfacing publish failures such as broker nacks or unroutable mandatory publishes via `PublishException`, while the broader RabbitMQ reliability guidance still expects confirm-aware publisher design.

Do not claim a publish succeeded just because `BasicPublishAsync` returned. The current RabbitMQ docs are explicit that messages published while the connection is down are lost unless the application uses publisher confirms and accounts for connection failures.

## Consumer Design

Use a hosted background process when the app owns long-lived consumption.

Preferred behavior:

- declare or verify topology during startup
- set QoS/prefetch before subscribing
- use push-based consumption, not polling
- process one delivery through an application handler
- ack on success
- nack or reject on failure according to retry policy

### Ack Rules

- Ack only after the message's side effects are committed.
- If the handler persists data and then publishes follow-up events, consider outbox or equivalent transactional coordination.
- Avoid `autoAck = true` except for intentionally lossy telemetry or debug flows.

### Prefetch

Start conservatively. RabbitMQ guidance warns that unlimited prefetch or auto-ack can drive memory growth. For many workloads, values around the low hundreds can be reasonable, but the correct value is workload-specific. Start smaller if handlers are expensive or non-idempotent.

### Concurrency

The .NET client currently dispatches consumer callbacks sequentially on a channel by default. If the app opts into concurrent dispatch:

- be explicit about parallelism limits
- avoid multiple acks in one operation
- acknowledge one delivery at a time
- ensure all handler dependencies are thread-safe

### Memory Safety

Current RabbitMQ .NET client guidance says delivery bodies are exposed as `ReadOnlyMemory<byte>` and must be copied or deserialized before the handler returns. Do not stash the buffer for later work.

## Topology Recommendations

### Exchange Choice

Use:

- `topic` for most domain events
- `direct` when routing is exact and intentionally simple
- `fanout` when every bound queue should receive every event

Avoid making the default exchange your application topology. It is useful for its special behavior, not as a general design choice.

### Queue Choice

Use classic queues when:

- workloads are simple
- low latency matters more than replication semantics
- queues are temporary or high churn

Use quorum queues when:

- data safety is important
- consumers use manual acks
- publishers use confirms
- the queue is durable and long-lived

Do not use quorum queues by default for:

- temporary or exclusive queues
- very large backlogs
- large fanout use cases where streams are a better fit

### Policies vs Hardcoded Arguments

Prefer broker policies for:

- dead-letter exchange
- dead-letter routing key
- TTL
- delivery limits

RabbitMQ's DLX docs strongly recommend against hardcoded queue `x-arguments` for mutable operational behavior because changing them can require deleting and redeclaring queues.

## Retry and Failure Handling

Choose one retry strategy explicitly:

1. Immediate requeue
   - only for clearly transient failures
   - dangerous if it causes hot loops
2. Retry queue with TTL and DLX
   - common and easy to reason about
   - best when delayed retries are acceptable
3. Quorum queue delayed retry or delivery-limit features
   - useful when the broker is already standardized on quorum queues
4. Dead-letter directly
   - use when replay is manual or failures are non-transient

Rules:

- Do not requeue poison messages forever.
- Capture enough metadata to diagnose failures.
- Preserve correlation and message identity into retries and dead letters.
- Make reprocessing paths explicit.

## Idempotency

At-least-once delivery means duplicates will happen.

Require one of:

- dedupe by `message_id`
- dedupe by a business event id carried in the payload or headers
- naturally idempotent state transitions
- inbox table / processed-message store

If a handler is not idempotent, say so explicitly and propose the storage boundary needed to make it safe.

## Recovery Model

Use RabbitMQ automatic recovery for dropped established connections, but do not confuse it with startup retry.

Current RabbitMQ docs say:

- automatic recovery restores connection and topology after eligible connection failures
- it does not recover from initial connection failure
- it does not recover from channel-level semantic errors
- published messages sent while the connection is down are not queued by the client for later delivery

Implementation implications:

- add startup retry with backoff around initial connection creation
- fail fast on semantic topology errors
- recreate bad channels
- log recovery attempts with connection names

## Heartbeats and Connection Names

Keep heartbeats enabled. RabbitMQ's official heartbeat guidance says very low values are discouraged and that 5 to 20 seconds is optimal for most environments. The broker default suggestion is 60 seconds. Pick a value deliberately; do not disable heartbeats unless TCP keepalives are known to be configured correctly end to end.

Set `ClientProvidedName` so operators can identify the app and component from broker logs and the management UI.

Suggested naming:

- `app:billing component:event-publisher env:prod`
- `app:billing component:invoice-consumer env:staging`

## Observability

Add:

- structured logs for publish attempt, confirm result, return, ack, nack, retry, dead-letter
- metrics for publish count, confirm latency, consumer lag indicators, retries, dead letters
- health checks that reflect whether the service can communicate with RabbitMQ or at least whether the hosted consumer is started and connected

For HTTP-hosted apps, use ASP.NET Core health check endpoints using the built-in framework facilities.

## Testing Strategy

Prefer integration tests over pure mocks for messaging behavior.

Minimum useful test set:

- topology declaration succeeds
- publish routes to the intended queue
- consumer acks on success
- consumer nacks or dead-letters on failure
- duplicate delivery does not duplicate side effects
- unroutable mandatory publish is surfaced

If the project supports Docker-based tests, prefer Testcontainers or equivalent so a real broker is exercised.

## Implementation Order

Follow this order when writing code:

1. configuration object and validation
2. connection factory/provider
3. topology declaration or verification
4. publisher abstraction and implementation
5. consumer hosted service
6. retry/DLQ behavior
7. observability and tests

## Anti-Patterns

Flag these and correct them:

- connection-per-message publishing
- polling with `BasicGet` for steady-state event processing
- auto-ack on business-critical consumers
- shared publishing channel across concurrent callers
- endless requeue loops
- no message identity or correlation metadata
- mixing temporary topology with durable business flows
- assuming RabbitMQ provides exactly-once processing
- hiding every broker concern behind an over-abstracted "event bus" with no operational knobs

## When to Push Back

Recommend an alternative if the requested design conflicts with the workload:

- use streams for extremely high fanout or very large backlogs
- use quorum queues for durable critical workflows
- use classic queues for transient/high-churn queues
- use an outbox when the app needs reliable "save state then publish event" behavior
- use broker-managed policies when the team wants runtime-tunable DLX/TTL behavior

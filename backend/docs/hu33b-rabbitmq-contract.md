# HU-33B — RabbitMQ integration-event contract (for HU-37A consumers)

`session-operations-service` is the runtime authority: it emits round-close and final-results
**facts** to RabbitMQ after transactional success, for asynchronous history/consolidation. It
computes **no** score/ranking — that is `ScoringMonitoring`'s job, derived downstream from these
facts (D-1). This document is the producer contract HU-37A (and any other consumer) binds against.

## Exchange

| Property | Value |
|---|---|
| Name | `umbral.session-operations` |
| Type | `topic` |
| Durable | yes |
| Auto-delete | no |

The producer declares this exchange on connect (idempotent). Consumers should declare it with the
same properties before binding, then bind their own durable queue(s) to the routing keys below.

## Message delivery guarantees

- **Persistent** messages (`delivery_mode = 2`) so they survive a broker restart while queued.
- **Publisher-confirmed** — the producer publishes on a confirm-tracking channel.
- **Best-effort, at-most-once** from the producer: publishes are handed to a background channel so a
  slow/down broker never blocks or faults the session runtime (D-3, AC #6). On broker failure the
  message is **logged and dropped** — there is no producer-side outbox/replay. Durable redelivery is
  the consumer's concern.
- Message properties: `content_type = application/json`, `type = <event type name>`,
  `message_id = <random GUID, hex "n">`.

## Routing key: `session.question.closed`

Published when a trivia question closes (a real `LiveSession.CloseActiveQuestion`).

`type = QuestionClosedIntegrationEvent`

| Field | JSON | Type | Meaning |
|---|---|---|---|
| `LiveSessionId` | `liveSessionId` | GUID | Session the question belongs to |
| `QuestionIndex` | `questionIndex` | int | Zero-based index of the closed question within the session |
| `ClosedAt` | `closedAt` | ISO-8601 offset | When the question closed |

## Routing key: `session.results.finalized`

Published when a session reaches `Finished` **only** via `SessionCompletion` (never operator-forced).

`type = SessionResultsFinalizedIntegrationEvent`

| Field | JSON | Type | Meaning |
|---|---|---|---|
| `LiveSessionId` | `liveSessionId` | GUID | Session that finished |
| `FinishedAt` | `finishedAt` | ISO-8601 offset | When the session reached final results |

Neither contract carries score, ranking, or per-team results (D-1).

## Idempotency (required consumer behaviour)

`MessageId` is a random GUID (not a business identifier) — consumers (HU-37A) MUST dedupe on
`LiveSessionId` + event-specific fields (e.g. question index / finished-at), NOT on `MessageId`.

# Handoff: SessionOperations MassTransit five-second gameplay stalls

## Purpose of this session

Diagnose an approximately five-second delay observed while following
`frontend/docs/hu-171-manual-test.md`:

- mobile answer submission showed a spinner for about five seconds;
- the operator and mobile timers appeared out of sync;
- after question 1 reached zero, question 2 appeared about five seconds late and
  was already near 24 seconds remaining.

The user suspected the recent MassTransit integration. This session was
diagnostic only; no application code was changed.

## Confirmed diagnosis

The suspicion was correct. RabbitMQ was stopped while SessionOperations remained
running, and gameplay-critical persistence synchronously awaited MassTransit
publishes with an exact five-second timeout.

Runtime evidence collected from Docker:

- `backend-rabbitmq-1` was `Exited (0)` and had received `SIGTERM` hours earlier;
- SessionOperations repeatedly logged `Connection Failed: rabbitmq://rabbitmq/`,
  `BrokerUnreachableException`, and MassTransit connection retries;
- the other workload containers, including SessionOperations and the gateway,
  remained running.

The authoritative timer worker is not configured with a five-second polling
interval. Its tick interval is one second in
`services/session-operations-service/src/Infrastructure/Realtime/AuthoritativeSessionTimerWorker.cs`.

## Causal chain

### Answer submission

1. Mobile calls the trivia-answer endpoint and keeps `isSubmitting` true until
   the response settles (`mobile/src/lib/realtime/use-submit-answer.ts`).
2. `SubmitTriviaAnswerCommandHandler` updates the `LiveSession`.
3. `DispatchDomainEventsInterceptor.SavedChangesAsync` awaits MediatR domain-event
   handlers before `SaveChangesAsync` returns.
4. `PublishAnswerRegisteredIntegrationEventHandler` awaits
   `IPublishEndpoint.Publish` with `PublishTimeout = TimeSpan.FromSeconds(5)`.
5. With RabbitMQ unreachable, publication consumes that timeout, the exception is
   logged/swallowed, and only then does the HTTP response return. This produces
   the mobile spinner delay.

Relevant files:

- `services/session-operations-service/src/Application/Sessions/Commands/SubmitTriviaAnswer/SubmitTriviaAnswerCommandHandler.cs`
- `services/session-operations-service/src/Application/Sessions/EventHandlers/PublishAnswerRegisteredIntegrationEventHandler.cs`
- `services/session-operations-service/src/Infrastructure/Persistence/Interceptors/DispatchDomainEventsInterceptor.cs`

### Question transition

1. `AuthoritativeSessionTimerWorker` detects expiry on its one-second tick.
2. `TriviaRoundOrchestratorFacade.CloseAndAdvanceAsync` closes the question and
   awaits the repository update.
3. That save dispatches `QuestionClosedEvent`; its integration-event handler
   awaits MassTransit for up to five seconds.
4. Only after the repository update returns does the facade broadcast
   `QuestionClosed` and activate/broadcast the next question.
5. The next question's authoritative activation time uses the earlier worker
   timestamp, so clients receive it about five seconds into its timer window.
   This explains question 2 first appearing around 24 seconds.

Relevant files:

- `services/session-operations-service/src/Application/Sessions/Common/TriviaRoundOrchestratorFacade.cs`
- `services/session-operations-service/src/Application/Sessions/EventHandlers/PublishQuestionClosedIntegrationEventHandler.cs`
- `services/session-operations-service/src/Infrastructure/Realtime/AuthoritativeSessionTimerWorker.cs`

## Immediate environment recovery

Start the missing broker/consumer and restart SessionOperations so MassTransit
reconnects cleanly:

```bash
docker compose -f backend/docker-compose.yml up -d rabbitmq scoring-monitoring-service
docker compose -f backend/docker-compose.yml restart session-operations-service
```

Then verify all services are up and the broker errors have stopped:

```bash
docker compose -f backend/docker-compose.yml ps
docker compose -f backend/docker-compose.yml logs --tail=50 session-operations-service rabbitmq
```

This restores normal local behavior, but does not remove the architectural
coupling: a future broker outage would recreate the delay.

## Recommended permanent fix

Implement a transactional outbox for SessionOperations integration events.

Required behavior:

1. Persist the `LiveSession` mutation and outbound integration message atomically
   in PostgreSQL.
2. Allow the answer request and SignalR question progression to continue without
   awaiting RabbitMQ.
3. Publish stored messages asynchronously through MassTransit.
4. Retry when RabbitMQ recovers without losing or duplicating the business fact
   at the consumer boundary.

MassTransit's EF Core transactional outbox is the likely implementation path,
but the next session must verify the exact API against the repository's pinned
MassTransit/EF versions before designing the change.

Do **not** treat these as permanent fixes:

- merely reducing the five-second timeout;
- starting an untracked fire-and-forget task;
- swallowing publication without durable storage;
- broadcasting earlier while leaving event delivery lossy.

Those approaches reduce visible latency but do not provide durable event
delivery across process or broker failures.

## Regression signal for the next session

Build the regression test before changing the publication path. The correct seam
must demonstrate broker-unavailable behavior, not only mock a fast publisher.

Minimum acceptance:

- with RabbitMQ unavailable, an accepted answer returns promptly rather than
  waiting approximately five seconds;
- with RabbitMQ unavailable, an expired question closes and question 2 is
  activated/broadcast promptly;
- the corresponding integration messages remain durably pending;
- after RabbitMQ becomes available, pending messages are published and consumed;
- normal broker-available answer and question-close flows remain green.

Use the sandbox-hardened backend commands from `backend/AGENTS.md` for builds and
tests. Do not invoke `dotnet` directly.

## Suggested skills

- `tdd` — drive the permanent fix from broker-unavailable regression tests.
- `diagnose` — re-run the original timing feedback loop after the fix and verify
  that the five-second symptom no longer reproduces.
- Repository-local `rabbitmq-events-dotnet` guidance — use when selecting and
  wiring the MassTransit outbox, after checking its instructions and pinned
  package versions.

## Existing repository state

The worktree was already dirty before diagnosis. In particular,
`frontend/docs/hu-171-manual-test.md` and unrelated backend documentation had
user changes, alongside several untracked artifacts. Preserve all unrelated
changes. This handoff document is the only workspace file created by this
session.


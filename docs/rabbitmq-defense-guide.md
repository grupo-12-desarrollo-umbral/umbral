# Defensa — cómo responder preguntas sobre la cola de RabbitMQ

Guide for answering live questions about UMBRAL's MassTransit/RabbitMQ setup: what to say, what to open,
what to change on the spot, and what **not** to claim. Everything here is verified against the running
stack — file paths and line numbers are real.

Companion docs: [`alignment_rabbitmq_docs.md`](./alignment_rabbitmq_docs.md) (requirements mapping),
[`backend/docs/integration-event-wiring-audit.md`](../backend/docs/integration-event-wiring-audit.md)
(findings + the check playbook).

---

## 1. The 30-second answer

If asked "how is RabbitMQ set up?", lead with this and stop:

> The bus is MassTransit over RabbitMQ. **Domain and Api have zero MassTransit references.** Application
> depends only on `MassTransit.Abstractions` — contracts, `IPublishEndpoint`, `IConsumer<T>` — so it
> declares *what* is published and consumed. Infrastructure has `MassTransit` + `MassTransit.RabbitMQ` and
> decides *how* and *over what transport*. All the broker wiring is one ~40-line file per service:
> `Infrastructure/Messaging/MassTransitMessagingRegistration.cs`.

That's the Clean Architecture answer, and the `.csproj` files prove it mechanically rather than by
convention — open them if pushed:

| Layer | Package | Files |
|---|---|---|
| Domain | *none* | 0 |
| Application | `MassTransit.Abstractions` | 18 (session-ops) / 13 (scoring) |
| Infrastructure | `MassTransit`, `.RabbitMQ`, `.EntityFrameworkCore` | 5 / 1 |
| Api | *none* | 0 |

`session-operations-service/src/Application/Application.csproj:12` carries the decision in a comment:
`<!-- MassTransit contracts only (IPublishEndpoint, [EntityName]) — no transport in Application (#164). -->`

---

## 2. The one line that explains the queue names

Inside `UsingRabbitMq`:

```csharp
cfg.ConfigureEndpoints(context);
```

Convention over configuration: MassTransit takes each **registered consumer's class name**, strips the
`Consumer` suffix, and that becomes the **queue name**.

- `AnswerRegisteredConsumer` → queue `AnswerRegistered`
- `SessionEventHistoryConsumer` → queue `SessionEventHistory`

Nobody named those queues — the class names did. This is the single most useful fact to have ready, because
almost every "how would you change the queue?" question resolves to it.

**Exchange** names are separate and come from the contract's `[EntityName("kebab-name")]` attribute
(e.g. `session-state-changed`). So: **exchange = attribute on the contract, queue = consumer class name.**
One consumer class can implement `IConsumer<T>` for several contracts — `SessionEventHistoryConsumer` does
exactly that for three — which is why one `SessionEventHistory` queue is bound to three exchanges.

---

## 3. "Change something in the queue" — the playbook

### Safe to do live

| Ask | Do | Notes |
|---|---|---|
| **Add a consumer** | one line: `bus.AddConsumer<MyConsumer>();` | queue auto-creates on boot, named after the class |
| **Point at another broker** | `appsettings.json` → `RabbitMq` section, or `RabbitMq__HostName` env in `docker-compose.yml:176` | **config, not code** — no rebuild |
| **Change credentials / vhost / port** | same place (`UserName`, `Password`, `VirtualHost`, `Port`) | already externalized, not hardcoded — say so |
| **Add retry** | `cfg.UseMessageRetry(r => r.Interval(3, TimeSpan.FromSeconds(5)));` inside `UsingRabbitMq` | applies to all endpoints |
| **Change concurrency / prefetch** | `cfg.PrefetchCount = 16;` | same place |
| **Rename a queue** | rename the consumer class (convention), **or** pin it: `cfg.ReceiveEndpoint("my-queue", e => e.ConfigureConsumer<T>(context));` | if you pin it, don't *also* leave it under `ConfigureEndpoints` — you'd get two queues bound to the same exchange and messages splitting between them |

### Dangerous — do not improvise under questioning

| Ask | Why it bites |
|---|---|
| **Rename an exchange** (`[EntityName]`) | the contract exists **twice** — publisher-side and consumer-side. Change one and *nothing throws*: it publishes fine, lands in a queue nobody reads, and `_skipped` grows silently. That's failure **F3/F1**. |
| **Change a contract's shape** | same two-copy problem → deserialization yields nulls/defaults. Failure **F4**. |
| **"Just consume it somewhere else"** | a consumer that reads `ICurrentUser` gets **nothing** — there's no HTTP actor in a bus context. Failure **F6** (this is a real past incident, HU-25B). |

The honest line if a teacher pushes you here: *"I'd change it in both copies and then verify with the R1
check, because this failure class is silent — it's exactly how two of our bugs shipped."* That answer is
worth more than a confident wrong one.

### The live-demo gotcha — and how to not hit it

**Wiring changes do not hot-reload. Ever. This is by design, not a bug**, and knowing *why* is the
difference between a confident answer and a panicked one.

The dev stack (`backend/docker-compose.override.yml`) runs each service from the SDK image as
`dotnet watch run` with the source bind-mounted, so editing a `.cs` file on the host normally recompiles in
place. But **Hot Reload patches method bodies inside the already-running process — it never re-runs
startup.** The composition root (`AddMassTransitMessaging`, DI registration, `bus.AddConsumer<T>()`) executes
exactly **once**, when the host builds its service provider. So a wiring edit either reports *"No C# changes
to apply"* or hot-applies "successfully", and **either way the container was already built and keeps the old
wiring**. `dotnet watch` looks perfectly healthy while the app runs stale code. That's what cost a debugging
session during the `SessionEventHistoryConsumer` work (audit doc, Finding 2).

**The fix — always works, ~5 seconds:**

```bash
docker compose restart scoring-monitoring-service
```

Rule of thumb: **edit a handler or a query → hot reload is fine. Edit anything that runs at startup →
restart the container.** If a teacher asks you to add a consumer live, restart without hesitating and say
why: *"the DI container is built once at startup, so registration changes need a restart — hot reload only
patches running methods."* That's a better answer than the change silently doing nothing.

> **Do not try to fix this with `--no-hot-reload`.** It was tried on 2026-07-15 and is **worse**. On a file
> change, watch launches the new process before the old one releases `:8080`; the new one aborts with
> `Failed to bind to address http://[::]:8080: address already in use` (exit 134) and watch dead-ends at
> *"Waiting for a file to change before restarting"*. The old process survives, so health stays `200` while
> the watcher is dead — stale code with no reload at all. Reproduced twice, then reverted;
> the trap is documented in the override file itself. `DOTNET_WATCH_RESTART_ON_RUDE_EDIT` doesn't help
> either — a body edit to a registration method isn't a rude edit, so there's nothing for it to fire on.

### Restart timing — the thing that will actually embarrass you

**`health=200` does NOT mean the bus is ready.** Measured on this stack: after
`docker compose restart scoring-monitoring-service`, HTTP health returns `200` about **15 seconds before**
the MassTransit consumers finish attaching to their queues. Demo something in that window and the queues
read `0 consumers` and your message just sits there — which looks exactly like a bug you didn't write.

Don't wait on health. Wait on **consumers**:

```bash
docker compose exec rabbitmq rabbitmqctl list_queues name messages consumers
```

Every production endpoint should read **1 consumer** before you touch anything. This is a good pre-flight to
run once, quietly, before the demo starts:

```bash
# all five scoring endpoints attached?
docker compose exec -T rabbitmq rabbitmqctl list_queues name consumers | grep -E \
  '^(SessionEventHistory|AnswerRegistered|TargetResolved|ScoreEntryRegistered|LiveSessionOperatorAssigned)'
```

---

## 4. The check that proves it worked

One command, and it's the highest-signal check in the whole doc:

```bash
cd backend && docker compose exec rabbitmq rabbitmqctl list_queues name messages consumers \
  | grep -E '_skipped|_error' | awk '$2 != 0'
```

**No output = healthy.** Any row printed is a live routing bug: `_skipped` = the message arrived but no
consumer matched its URN (F1); `_error` = the consumer faulted (F5 or a consumer bug).

Management UI: `localhost:15672` (guest/guest) — good to have open on a second tab; the Exchanges and
Queues tabs make the topology visual without you typing anything.

To show a queue is bound to its exchanges:

```bash
docker compose exec rabbitmq rabbitmqctl list_bindings source_name destination_name destination_kind \
  | grep SessionEventHistory
```

---

## 5. Questions you should expect, with the honest answer

**"Show me a real flow end-to-end."**
Four are demonstrated live: scan→score, penalty→recalc, evidence→trace, and state-change→history. The §15
bar is "at least one". The history one is the newest: an operator transition, plus a full session run-out
that produced `QuestionClosed` ×3, `Active→Finished` and `SessionResultsFinalized` as real
`session_events` rows.

**"Why aren't notifications on RabbitMQ? §11 says notificaciones."**
Deliberate split, and say it as a decision, not an omission:
> **RabbitMQ** = asynchronous server-to-server decoupling (auditoría, recálculo).
> **WebSockets/SignalR** = real-time client push (notificaciones, ranking, cambios de estado).

That satisfies RNF-03 (tiempo real sobre WebSockets) and RF-12/RF-13. `QuestionClosed` is the clean example
of both halves from one domain event: a SignalR broadcast to the UI *and* a Rabbit publish to the history.

**"What happens if RabbitMQ goes down?"**
⚠️ **Two different answers — do not give one for both.**
- **session-operations-service**: safe. It publishes through the **EF transactional outbox** — the publish
  is an `OutboxMessage` insert that commits atomically with the business write, and a hosted service drains
  it to the broker later. A broker outage stalls delivery; it never loses the event. Publish handlers also
  rethrow, so a failure rolls the transaction back.
- **scoring-monitoring-service**: **not** safe, and this is **Finding 4 (open)**. It has no outbox; its one
  publish (`ScoreEntryRegistered`) goes straight to the broker **post-commit** and **swallows the
  exception**. Broker down → score committed, event lost, ranking silently stale, no dead-letter.

If you're asked, say the second half. It's a known, documented, scoped gap with a written fix — that reads
as engineering maturity. Claiming "we use the outbox, we're safe" and then being asked *which service* is
how a defense goes wrong.

**"How do you know a message wasn't lost?"**
R1 (`_skipped`/`_error` at 0) plus R2 (the effect landed in the DB). Then volunteer the limit, because it's
the interesting part: **R1 clean and a row present do not prove the row is *correct*.** We hit exactly that
— a consumer whose upsert silently no-oped on the update half, so the trace row existed and every check
read healthy while the audit trail was quietly incomplete (Finding 1's residual). Finding 4 is the same
lesson one level up: a message that was never published can't dead-letter.

**"Is it idempotent? MassTransit is at-least-once."**
Yes, and it was learned the hard way. A redelivery burst dead-lettered 16 messages on a read-then-`Add`
TOCTOU race. Fix: the upsert catches the duplicate-key (`23505`) as an idempotent no-op, and the merge rule
lives in the domain (`EvidenceTraceEntry.MergeFrom`). The history projection applies the lesson up front via
a deterministic `SourceEventKey` on a unique index, so a redelivered source event collapses onto the same
row instead of appending a duplicate.

**"Is this tested?"**
A Testcontainers harness boots a real RabbitMQ + the **real production DI graph**, publishes the
**publisher's own contract**, and asserts the **production consumer's** effect in the DB — across all six
cross-service pairs. That's what catches F1/F3/F4, which same-namespace tests cannot. It runs in CI
(`.github/workflows/backend-tests.yml`, matrix over all four services, Docker preflight + zero-skips
assertion) — green on PR and on `develop`. Note the harness publishes the contract itself, so it does not
cover the Finding 4 publisher.

---

## 6. Things NOT to claim

- ❌ *"Both services use the outbox."* False — only session-ops. (Finding 4.)
- ❌ *"Every event is guaranteed delivered."* False for `ScoreEntryRegistered`.
- ❌ *"Finished is an operator action."* False — `LiveSession.cs:1314`: *"Finished is reached ONLY here,
  never operator-forced."* It's timer-driven, when the last question of the last substage closes. A
  `PATCH .../state` with `Finished` will not do it.
- ❌ *"Notifications go over RabbitMQ."* They go over SignalR — by design.
- ❌ *"All MassTransit is in Infrastructure."* The **transport** is; the **abstractions** are in
  Application, deliberately. That's the more precise (and better) answer.
- ❌ *"I'll just edit the wiring and it'll hot-reload."* It won't — the composition root runs once at
  startup. Restart the container.

---

## 7. Files to have open

| Purpose | Path |
|---|---|
| Bus setup (scoring) | `backend/services/scoring-monitoring-service/src/Infrastructure/Messaging/MassTransitMessagingRegistration.cs` |
| Bus setup + outbox (session-ops) | `backend/services/session-operations-service/src/Infrastructure/Messaging/MassTransitMessagingRegistration.cs` |
| Layering proof | `backend/services/*/src/Application/Application.csproj` |
| A contract | `.../src/Application/Sessions/Common/SessionResultsFinalizedIntegrationEvent.cs` (14 lines — shows `[EntityName]` + correlation-only payload) |
| A multi-contract consumer | `.../scoring-monitoring-service/src/Application/SessionEvents/Consumers/SessionEventHistoryConsumer.cs` |
| Broker config | `backend/services/*/src/Api/appsettings.json` → `RabbitMq`; `backend/docker-compose.yml:176` |
| Findings + checks | `backend/docs/integration-event-wiring-audit.md` |

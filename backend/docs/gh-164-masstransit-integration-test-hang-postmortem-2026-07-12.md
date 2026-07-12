# POSTMORTEM — MassTransit migration hung the session-operations integration gate

**Date:** 2026-07-12
**Branch:** `feature/gh-164-bootstrap-masstransit` (GitHub #164)
**Service:** `session-operations-service`
**Symptom:** `make gate SVC=session-operations-service` appeared "very slow" — it was actually **deadlocked**, never producing a verdict.

This documents why the integration gate hung after `#164` introduced MassTransit, so the failure mode (and the two-part fix) is not rediscovered from scratch next time a service adopts MassTransit.

---

## TL;DR

Adding MassTransit to the app's composition root made every integration test that closes a question **hang for the connection-retry window**, because `IPublishEndpoint.Publish(...)` does **not fail fast** when the broker is unreachable — it blocks awaiting a connection. The test host boots with the bus pointed at the compose-only hostname `rabbitmq:5672`, which doesn't resolve, so the post-commit publish inside `SaveChanges` blocked and the gate deadlocked. The handler's `try/catch` didn't help — a blocked `await` never throws. Fixed by (1) bounding the publish with a 5s timeout token and (2) stubbing `IPublishEndpoint` in the test factory so broker-less tests never touch a live bus. A third, separate defect (an e2e test still asserting `QuestionClosed` on the *old* RabbitMQ topology) was refocused. Gate returned to **GREEN, 96.5% line coverage**.

---

## Symptom / how it presented

- `make gate` ran for 10+ minutes with no output after `Starting test execution, please wait...`.
- The `testhost` process was **idle** — ~20s of CPU over 12+ minutes (blocked, not computing).
- `develop` (no MassTransit) ran the same suite green in ~50s. Only `#164` hung → the delta was MassTransit.
- `docker ps` during the hang showed the Postgres testcontainer up, but **no `rabbitmq:3-management` container** — i.e. nothing was waiting on a broker container; the app itself was blocked trying to reach a broker that wasn't there.

## Root cause #1 — `Publish` blocks on an unreachable broker (the deadlock)

`#164` wired the real MassTransit bus into the app composition root (`AddMassTransitMessaging()` in `Infrastructure/DependencyInjection.cs`) and migrated `QuestionClosed` to publish via `IPublishEndpoint` from `PublishQuestionClosedIntegrationEventHandler`.

The chain:

1. A test ticks the timer → a question closes → raises `QuestionClosedEvent`.
2. MediatR dispatches the handler **post-commit inside `SaveChanges`**; it does `await _publishEndpoint.Publish(...)`.
3. The bus is bound (from `appsettings.json`) to host **`rabbitmq:5672`** — a docker-**compose** service name that does not resolve from the test host. DNS fails (`SocketException (11)`).
4. MassTransit's `Publish` **does not fail fast** — it awaits a broker connection under its retry policy (`Retrying 00:00:28…: Broker unreachable: guest@rabbitmq:5672/`). So the `await` blocks, the handler blocks, the test blocks.

Two things made this non-obvious:

- **The handler's `catch (Exception)` is useless against this.** It catches *thrown* exceptions; a blocked/retrying `await` never throws, so the D-3 "never fault the runtime" guard did not cover the "broker unreachable → block" case.
- **Host startup being non-blocking misled the design.** MassTransit's *bus start* is non-blocking (background connect), which the original handoff relied on. But an actual `Publish()` call while disconnected **does** block. Those are different code paths.

This is also a **production risk**, not just a test artifact: if RabbitMQ is down in prod, closing a question would stall the request/`SaveChanges` for the retry window instead of failing fast.

### D-3 (non-blocking, best-effort) is downgraded by the migration, not preserved

This is the subtle part, and it directly affects prior decisions (see "Impact on prior decisions" below). The old and new publish paths achieve non-blocking **very differently**, and only the old one is non-blocking *structurally*:

- **Old hand-rolled publisher — structurally non-blocking.** `RabbitMqIntegrationEventPublisher.PublishAsync` does `_outbox.Writer.TryWrite(message)` into a bounded `DropOldest` channel and returns immediately; the broker round-trip runs on a background drain task. The commit thread **literally cannot block on the broker** — a broker outage adds **0s** latency to `SaveChanges`, and overflow silently drops (best-effort, by design / D-3).
- **MassTransit path — bounded-*blocking*, not non-blocking.** The handler `await`s `Publish(...)` **inline**, and `DispatchDomainEventsInterceptor.SavedChanges` runs it **synchronously on the commit thread** (`.GetAwaiter().GetResult()`). Because `Publish` retry-blocks on a down broker, the only thing preventing an indefinite stall is the added 5s linked-timeout token.

Net: after the migration, "never faults" still holds (the `catch` swallows the timeout's `OperationCanceledException`), but **"non-blocking / zero added commit latency" does NOT** — a broker outage now stalls the commit for up to the 5s timeout instead of 0s. And this safety is **per-call, not structural**: forget the timeout token on any single publish and the indefinite hang returns. MassTransit does not give you the old publisher's structural hand-off for free.

## Root cause #2 — e2e test asserted the old RabbitMQ topology (separate defect)

Once the deadlock was fixed, one test still failed: `RoundClosePublicationEndToEndTests`. It binds a queue to the **old** hand-rolled topology (exchange `umbral.session-operations`, routing key `session.question.closed`) and asserts `QuestionClosed` arrives there. But `#164` moved `QuestionClosed` onto MassTransit, which publishes to a different exchange — `session-question-closed` (MassTransit fanout, from `[EntityName("session-question-closed")]`) with a MassTransit envelope. The message lands elsewhere; the old-topology queue stays empty. This was a pre-existing gap in the migration, unrelated to the hang.

## The fix (three edits, all on `#164`)

1. **Production fail-fast** — `src/Application/Sessions/EventHandlers/PublishQuestionClosedIntegrationEventHandler.cs`: bound the publish with a linked timeout so a down broker can never stall the runtime beyond it:
   ```csharp
   using var publishTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
   publishTimeout.CancelAfter(PublishTimeout); // TimeSpan.FromSeconds(5)
   await _publishEndpoint.Publish(new QuestionClosedIntegrationEvent(...), publishTimeout.Token);
   ```
   The existing `catch (Exception)` now catches the resulting `OperationCanceledException`.
2. **Test isolation** — `tests/IntegrationTests/Api/SessionOperationsApiWebApplicationFactory.cs`: `RemoveAll<IPublishEndpoint>()` + register a no-op `NoOpPublishEndpoint`, and remove the MassTransit bus `IHostedService`, so factory-booted tests never touch a live bus. Mirrors how the factory already fakes `IParticipantMembershipAccessClient` et al.
3. **E2E refocus** — `tests/IntegrationTests/Api/RoundClosePublicationEndToEndTests.cs`: dropped the `QuestionClosed`-on-old-exchange assertion (its integration-event delivery is covered end-to-end by `MassTransitQuestionClosedPublishTests`); kept the `SessionResultsFinalized` (old publisher) + both SignalR assertions. Renamed the test to `...PublishesSessionResultsFinalizedAndSignalRBroadcasts`.

Result: **gate GREEN, 96.5% line coverage, 788 tests pass**, `MassTransitQuestionClosedPublishTests` runs (not skipped). Integration project ~1m22s.

## Also encountered — leaked Testcontainers starving Docker (separate environment issue)

Repeated killed/hung gate runs left **~10 orphaned `postgres:16` Testcontainers** running (Ryuk did not reap them), which independently starved Docker and made runs slower/flakier. Cleared with `docker rm -f $(docker ps -q --filter ancestor=postgres:16)` (leaving the `backend-*` compose stack untouched). If gate runs get progressively slower, check `docker ps` for accumulated random-named test containers.

---

## Prevention — guardrails for the next service that adopts MassTransit

1. **Never let a `WebApplicationFactory`-booted test hit a live bus.** Stub `IPublishEndpoint` (and drop the MassTransit `IHostedService`) in the test factory, exactly like other external clients. Only a dedicated messaging test with its own Testcontainers broker should exercise the real publish.
2. **Never let a post-commit `Publish` block the runtime.** MassTransit `Publish` does *not* fail fast when the broker is down — it retry-blocks. Any publish dispatched inside `SaveChanges` must be bounded (timeout token, or a bus-level short connection timeout), or a broker outage stalls the request. A `try/catch` alone is insufficient — it does not cover a hang.
3. **When migrating an event's transport, update its e2e assertions to the new topology.** A raw-RabbitMQ.Client test written for the hand-rolled publisher (topic exchange + routing key + bare-JSON body) will silently get an empty queue once the event moves to MassTransit's `[EntityName]` fanout exchange + envelope. Prefer covering the migrated path with a dedicated MassTransit test and refocusing the legacy e2e test onto what still uses the old path.
4. **Diagnose hangs, don't wait them out.** Idle `testhost` CPU + a frozen log = deadlock. Reach for `--blame-hang-timeout` to name the culprit and `docker ps` to spot a missing/expected broker container.

## Forward guardrails specific to #165 and DES-92 (the remaining MassTransit work)

These follow directly from the root causes above and were confirmed against the current code. #165 migrates `SessionResultsFinalized` + `AnswerRegistered`; DES-92 publishes a new `ClueReleased` event MassTransit-native. Each MUST:

1. **Carry the fail-fast timeout on every post-commit publish.** Any `IPublishEndpoint.Publish(...)` dispatched from the `DispatchDomainEventsInterceptor` path must wrap the call in `CreateLinkedTokenSource(ct) + CancelAfter(5s)` and swallow the resulting `OperationCanceledException` — mirroring `PublishQuestionClosedIntegrationEventHandler.cs`. The interceptor blocks the commit thread synchronously (`.GetAwaiter().GetResult()`), so without the token a broker outage stalls the request indefinitely. This is a **production stall risk**, not just a test artifact.
2. **Refocus `RoundClosePublicationEndToEndTests` again when #165 lands.** It still binds `SessionResultsFinalized` on the **old** `umbral.session-operations` topology and asserts one message there. The moment #165 moves that event to a MassTransit `[EntityName]` exchange, this assertion gets an empty queue and fails — identically to the `QuestionClosed` defect. Drop the old-topology `SessionResultsFinalized` assertion here and cover the migrated path with a dedicated MassTransit test like `MassTransitQuestionClosedPublishTests`.
3. **Don't lose `AnswerRegistered` delivery coverage before #166.** `RabbitMqIntegrationEventPublisherTests` asserts `AnswerRegistered` on the old exchange; #166 deletes the publisher (and those tests). #165 should give `AnswerRegistered`'s migrated path MassTransit-native coverage before that happens.
4. **Stub `IPublishEndpoint` in the WebApplicationFactory** for DES-92 / DES-51 test infra — now a standing requirement, per guardrail #1 in the previous section.

## Impact on prior decisions — `rabbitmq-clue-release-audit-scope-handoff-2026-07-11.md`

Validated decision-by-decision: **all scope/sequencing/domain decisions in that handoff remain valid** (state-changes-off-bus, no `TeamWon`, `Finished` via `SessionResultsFinalized`, DES-92 split, build-DES-51-first, exchange naming, ticket ACs) — they are independent of the publish mechanism.

**One characterization in that doc needs amending:** its statement that publishing is *"correctly decoupled … non-blocking … best-effort by design (D-3) … not a bug"* is accurate for the **old** hand-rolled publisher but is **silently falsified by the MassTransit migration** unless every post-commit publish carries the 5s fail-fast timeout (see "D-3 is downgraded…" above). The MassTransit plan (#164/#165/#166), the "born on the bus" `ClueReleased` decision, and the DES-92 split are all still correct, but each becomes **VALID-WITH-CAVEAT**: they must absorb the forward guardrails in the section above.

## References

- Issue: `gh api repos/grupo-12-desarrollo-umbral/umbral/issues/164` (`gh issue view` fails on Projects-classic).
- Original implementation handoff: `/tmp/HANDOFF-gh-164-masstransit-2026-07-12.md` (flagged the bus-startup watch-out but assumed publish was non-blocking).
- Scope/decisions this postmortem amends: `docs/rabbitmq-clue-release-audit-scope-handoff-2026-07-11.md` (line 12 D-3 characterization).
- Changed files: see the three edits above; `git diff` on the branch.

## Suggested skills

- `rabbitmq-events-dotnet` — messaging design/impl in .NET (note: still documents the hand-rolled approach; no MassTransit content yet — its refresh is #166).
- `aspnet-backend-testing` — Testcontainers integration tests, factory service stubbing, coverage gate.
- `cqrs-mediatr-aspnetcore` — the MediatR notification handler that publishes.

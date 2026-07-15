# Integration-Event Wiring Audit — MassTransit / RabbitMQ

A reusable playbook for finding the class of bug that broke **HU-38** (operator penalty never
authorized) and **HU-25B** (scan never scored). Both were *silent* cross-service messaging failures:
the HTTP call returned 200, no exception was thrown, and the effect simply never happened because an
event never crossed the service boundary.

> **Why these hide.** A cross-service event is fire-and-forget. The publisher commits happily; the
> consumer never runs (or never matches the message). There is no stack trace on the hot path — the
> only evidence is a non-draining RabbitMQ queue or a projection row that never appears. Unit tests
> pass on both sides in isolation. **You must audit the wiring explicitly; nothing else will catch it.**

The two services that speak over the bus:

- **`session-operations-service`** publishes from `src/Application/Sessions/Common/*IntegrationEvent.cs`
  via `src/Application/Sessions/EventHandlers/Publish*IntegrationEventHandler.cs`, routed by
  `OutboxDomainEventDispatcher` (pre-commit, EF transactional outbox).
- **`scoring-monitoring-service`** consumes in `src/Application/**/Consumers/*Consumer.cs`, declaring
  its own copy of each contract in `src/Application/Scores/Common/` (scoring effects) and
  `src/Application/SessionEvents/Common/` (session-history events).

---

## The failure taxonomy

Each row is a distinct way the wire breaks. The **Detect** column points at the section below.

| # | Failure | Symptom | Root cause | Detect |
|---|---------|---------|-----------|--------|
| **F1** | **URN mismatch** (cross-namespace duplicated contract, no `[MessageUrn]`) | message reaches the queue but is never consumed → `<Consumer>_skipped` grows | consumer's message-type URN defaults to *its* namespace; publisher's differs → MassTransit can't match the deserialized type | S1, R1 |
| **F2** | **Missing / unwired publisher** | nothing is ever published; projection/effect never appears; **all queues empty** | domain event raised in-process but no `Publish*` handler, or handler not routed in `OutboxDomainEventDispatcher`, or not registered in DI | S2, S3 |
| **F3** | **`EntityName` mismatch** | message never even reaches the consumer's queue | publisher and consumer bind different exchanges | S4 |
| **F4** | **Contract shape drift** | deserialization yields nulls/defaults, or consumer reads the wrong field | field name/type/order diverged between the two copies | S5 |
| **F5** | **`[MessageUrn]` includes the `urn:message:` prefix** | `<Consumer>_error` + `TypeInitializationException` on first message | MassTransit prepends the prefix; passing it again throws | S6, R1 |
| **F6** | **Consumer-context auth** | consumer runs but produces wrong data (e.g. blank names, 403 side-calls) | `ICurrentUser` is empty inside a consumer (no HTTP actor); an authorized HTTP call made from the consumer fails | S7 |
| **F7** | **Published, no consumer** | exchange exists, nothing bound; event silently discarded | a real consumer was never written (or only a test consumer exists) | S8, R2 |

F1 was HU-25B Bug A. F2 was HU-38 (both halves: no publisher **and** the consumer lacked the URN pin,
so it would have been F1 too). F6 was HU-25B Bug C.

---

## Static checks (grep — no running stack needed)

Run from `backend/`. These use ripgrep (`rg`); plain `grep -rn` works too. They are fast and belong in
a pre-commit or CI lint.

### S1 — every cross-service consumer contract must pin `[MessageUrn]`
A contract declared in the **consumer's** namespace (`…Scores.Common`) whose publisher lives in a
**different** namespace (`…Sessions.Common`) MUST carry `[MessageUrn("<publisher-namespace>:<Type>")]`.

```bash
# List consumer-side contracts and whether they pin a URN.
for f in services/scoring-monitoring-service/src/Application/{Scores,SessionEvents}/Common/*IntegrationEvent.cs; do
  printf '%-55s ' "$(basename "$f")"
  grep -q 'MessageUrn(' "$f" && echo 'URN pinned' || echo '>>> NO MessageUrn — F1 RISK if publisher is in another namespace'
done
```
A contract with **no** `[MessageUrn]` is only safe if its publisher shares its exact namespace (an
internal same-service round-trip, e.g. `ScoreEntryRegisteredIntegrationEvent`).

### S2 — every consumed contract must have a publisher
For each `IConsumer<T>`, a `Publish` of `T` (or its publisher-side twin) must exist.

```bash
# Consumed types:
rg -oN 'IConsumer<([A-Za-z]+)>' -r '$1' services --glob '!**/tests/**' --glob '!**/coverage/**' | sort -u
# Published types (publisher-side handlers that call _publishEndpoint.Publish):
rg -l 'IPublishEndpoint' services --glob '**/EventHandlers/**' --glob '!**/tests/**'
```
Cross-reference by hand: a consumed type with no matching publisher = **F2** (the HU-38 miss).

### S3 — every `Publish*` handler must be routed AND registered
A handler file that nobody calls is dead. Both wirings are required:

```bash
D=services/session-operations-service/src/Application/Sessions/EventHandlers/OutboxDomainEventDispatcher.cs
DI=services/session-operations-service/src/Application/DependencyInjection.cs
for h in services/session-operations-service/src/Application/Sessions/EventHandlers/Publish*Handler.cs; do
  n=$(basename "$h" .cs)
  printf '%-60s routed=%s  registered=%s\n' "$n" \
    "$(grep -q "$n" "$D" && echo yes || echo NO)" \
    "$(grep -q "$n" "$DI" && echo yes || echo NO)"
done
```
Any `routed=NO` or `registered=NO` = **F2**. (This is exactly what was missing for
`PublishLiveSessionOperatorAssignedIntegrationEventHandler` before the fix.)

### S4 — `EntityName` must match across the publisher/consumer pair
```bash
rg -N 'EntityName\("([^"]+)"\)' -r '$1' services --glob '**/*IntegrationEvent.cs' --glob '!**/tests/**' \
  | sort | uniq -c
```
Each cross-service exchange name should appear **exactly twice** (once per service). A count of 1 for a
cross-service event = **F3**.

### S5 — contract shapes must match
For each shared exchange, diff the record bodies of the two copies (field names/types/order):
```bash
# example for one pair
diff <(sed -n '/public sealed record/,/;/p' services/session-operations-service/src/Application/Sessions/Common/AnswerRegisteredIntegrationEvent.cs) \
     <(sed -n '/public sealed record/,/;/p' services/scoring-monitoring-service/src/Application/Scores/Common/AnswerRegisteredIntegrationEvent.cs)
```
Any diff beyond the attribute lines = **F4**.

### S6 — no `[MessageUrn]` may contain the `urn:message:` prefix
```bash
rg 'MessageUrn\("urn:message:' services && echo '>>> F5: strip the urn:message: prefix' || echo 'ok'
```

### S7 — consumers must not depend on an HTTP actor (`ICurrentUser`)
```bash
# Flag ICurrentUser (or authorized HttpClient calls) reachable from a consumer.
rg -l 'ICurrentUser' $(rg -l 'IConsumer<' services --glob '!**/tests/**')
```
A hit means the consumer reads the current user — which is empty in a bus context (**F6**). Snapshot the
needed data onto the event instead (HU-25B Bug C: `team_display_name` now rides the event).

### S8 — published-but-unconsumed (informational)
```bash
# published exchanges:
rg -N 'EntityName\("([^"]+)"\)' -r '$1' services/session-operations-service/src --glob '**/*IntegrationEvent.cs' | sort -u
# production consumers (exclude tests):
rg -oN 'IConsumer<([A-Za-z]+)>' -r '$1' services --glob '!**/tests/**' | sort -u
```
An exchange with no production consumer is **F7** — verify it is intentional (reserved, or consumed by a
non-.NET subscriber) and not a forgotten consumer.

---

## Runtime checks (stack up)

### R1 — no `_skipped` / `_error` queue may hold messages
The single highest-signal check. After exercising a flow:
```bash
cd backend && docker compose exec rabbitmq rabbitmqctl list_queues name messages consumers \
  | grep -E '_skipped|_error' | awk '$2 != 0'
```
Any row printed is a live **F1** (`_skipped`) or **F5**/consumer fault (`_error`). Inspect the envelope:
```bash
docker compose exec rabbitmq rabbitmqadmin get queue=<Name>_skipped count=1 ackmode=ack_requeue_true
# compare the envelope "messageType" urn to the consumer's namespace
```
RabbitMQ management UI: `localhost:15672` (guest/guest).

### R2 — the effect actually landed
Prove the consumer ran, not just that the queue drained. For the assignment projection (HU-38):
```bash
docker exec backend-postgres-1 psql -U postgres -d scoring_monitoring -c \
  "SELECT live_session_id, assigned_operator_user_id FROM session_operator_assignments;"
```
For scoring/ranking (HU-25B): check `score_entries` / `ranking_rows` for the expected team + `calculation_version` bump.

---

## Live audit snapshot — 2026-07-15

Result of running the checks above against `develop` after the HU-38 fix, **refreshed after the Finding 2
+ Finding 3 fixes landed**. Static checks (S1–S8), the S5 shape-diff on all **six** cross-service pairs,
and the R1 runtime queue sweep were all run; S8 now reports **no** publish-only exchanges. The one check
**not** re-run live is R2 for the three history events — see the R2 status note below.
**Open items are collected in [Open findings](#open-findings--things-to-fix) below.**

**Coverage.** Only `session-operations-service` and `scoring-monitoring-service` use the message bus,
so they are the whole audit surface. `identity-access-service` and `mission-design-service` have **zero**
MassTransit references, and `api-gateway` (YARP) has none — no publishers, consumers, or contracts to
audit. Note that identity-access *is* still reached cross-service, but over **synchronous HTTP**
(operator eligibility, `/api/users/me` — ADR 0009), which is a different failure mode **not** covered by
this event-wiring doc.

### Cross-service (session-ops → scoring) — must pin `[MessageUrn]`

| Exchange (`EntityName`) | Contract | URN pinned (S1) | Publisher wired (S2/S3) | Status |
|---|---|---|---|---|
| `session-answer-registered` | `AnswerRegisteredIntegrationEvent` | ✅ | ✅ | healthy |
| `session-target-resolved` | `TargetResolvedIntegrationEvent` | ✅ | ✅ | healthy |
| `session-operator-assigned` | `LiveSessionOperatorAssignedIntegrationEvent` | ✅ *(fixed HU-38)* | ✅ *(fixed HU-38)* | healthy |
| `session-state-changed` | `SessionStateChangedIntegrationEvent` | ✅ | ✅ | healthy — consumed by `SessionEventHistoryConsumer` *(Finding 2)*; **R2 verified live** |
| `session-question-closed` | `QuestionClosedIntegrationEvent` | ✅ | ✅ | healthy — consumed by `SessionEventHistoryConsumer` *(Finding 2)*; bound + harness-covered, not driven live |
| `session-results-finalized` | `SessionResultsFinalizedIntegrationEvent` | ✅ | ✅ | healthy — consumed by `SessionEventHistoryConsumer` *(Finding 2)*; bound + harness-covered, not driven live |

All three history events share a **single** `SessionEventHistory` endpoint (one consumer class implementing
three `IConsumer<T>`), so the live queue is bound to three exchanges — see the R1 sweep below.

### Internal round-trips (publish + consume in one service, same namespace — no URN pin needed)

| Exchange | Contract | Owner | Status |
|---|---|---|---|
| `scoring-score-entry-registered` | `ScoreEntryRegisteredIntegrationEvent` | scoring → scoring (ranking recalc) | wiring healthy; `_error`=0 |
| `session-evidence-submission-registered` | `EvidenceSubmissionRegisteredIntegrationEvent` | session-ops → session-ops | healthy; `_error` was 10 → **Finding 1 fixed + purged** |
| `session-evidence-submission-accepted` | `EvidenceSubmissionAcceptedIntegrationEvent` | session-ops → session-ops | healthy; `_error` was 4 → **Finding 1 fixed + purged** |
| `session-evidence-submission-rejected` | `EvidenceSubmissionRejectedIntegrationEvent` | session-ops → session-ops | healthy; `_error` was 2 → **Finding 1 fixed + purged** |

### Publish-only — **F7, verify intent** (no *production* consumer; only test consumers exist)

**None.** Every exchange published by `session-operations-service` now has a production consumer. The three
former F7 entries (`session-state-changed`, `session-question-closed`, `session-results-finalized`) moved
into the cross-service table above when `SessionEventHistoryConsumer` landed — see **Finding 2 (resolved)**.

### R1 runtime queue sweep — 2026-07-15, re-run after the Finding 2 restart (`backend-rabbitmq-1`)

`docker compose exec -T rabbitmq rabbitmqctl list_queues name messages consumers`
(rabbitmqctl prints a `Timeout: 60.0 seconds ...` banner and still lists the queues — harmless):

```
name                                 messages consumers
EvidenceSubmissionAccepted_error       0   0     ok (Finding 1 backlog purged)
SessionEventHistory                    0   1     ok (NEW — bound to all 3 history exchanges)
EvidenceSubmissionRegistered_error     0   0     ok (Finding 1 backlog purged)
EvidenceSubmissionRegistered           0   1     ok (live consumer)
AnswerRegistered_skipped               0   0     ok
EvidenceSubmissionRejected             0   1     ok
LiveSessionOperatorAssigned            0   1     ok
AnswerRegistered                       0   1     ok
ScoreEntryRegistered                   0   1     ok
EvidenceSubmissionAccepted             0   1     ok
TargetResolved_error                   0   0     ok
EvidenceSubmissionRejected_error       0   0     ok
TargetResolved_skipped                 0   0     ok
TargetResolved                         0   1     ok
```

**R1 clean:** no `_skipped`/`_error` queue holds messages (the three Finding 1 `_error` backlogs are now 0),
and every production endpoint reports **1 consumer**. `SessionEventHistory` has no `_error`/`_skipped` queue
yet — MassTransit creates those lazily on first fault, so their absence is expected on a queue that has not
yet faulted (not evidence of a problem). `0 consumers` on an `_error` queue is expected — error queues are
dead-letter holding pens, not actively drained; the signal is the **message count**.

`rabbitmqctl list_bindings source_name destination_name destination_kind` for the new endpoint:

```
session-question-closed    SessionEventHistory  queue
session-results-finalized  SessionEventHistory  queue
session-state-changed      SessionEventHistory  queue
```

**R2 status.** ✅ **Live publish→row observed.** Two operator transitions on session
`7f614460-539c-4f92-a39a-b28c7f1aef2a` (`Active→Paused`, `Paused→Active`, both `200`) each appended a
`session_events` row carrying the responsible operator (`444`), with R1 clean across both. Details and the
row dump in Finding 2 below. `QuestionClosed` / `SessionResultsFinalized` have not been driven live —
they rest on the bindings above plus the Finding 3 harness's real-broker coverage.

No `[MessageUrn]` carried the `urn:message:` prefix (S6 clean). No consumer reads `ICurrentUser` (S7 clean).
S5 shape-diff: all three cross-service pairs byte-identical (no F4).

---

## Open findings — things to fix

Ranked. Update this section as items are resolved.

### Finding 1 — EvidenceSubmission `_error` backlog: non-idempotent consumer (✅ RESOLVED 2026-07-15)

**Resolution.** `EvidenceTraceRepository.UpsertAsync` now catches the duplicate-key
(`DbUpdateException` → `PostgresException { SqlState: UniqueViolation }`) and treats it as an idempotent
no-op (detaching the rejected insert), so a concurrent at-least-once redelivery no longer dead-letters.
Covered by a new concurrent-redelivery integration test
(`EvidenceTraceRepositoryIntegrationTests.Upsert_ConcurrentRedeliveryOfSameEvidenceSubmissionId_IsIdempotentAndDoesNotThrow`,
7/7 green against Testcontainers Postgres). The three `_error` queues were **purged** (verified 0). One
shared fix covers all three consumers (Registration + Resolution both funnel through `UpsertAsync`).
Root cause below, retained for the record.

**Residual closed 2026-07-15 — and it was not what this note claimed.** The residual was recorded as a
*"much rarer cross-event insert race … acceptable vs. the dead-letter storm."* It was neither rare nor a
race. `UpsertAsync` only ever called `Add`; when the row already existed it attached nothing and mutated
nothing, so `SaveChanges` wrote **nothing**. Any registration arriving after a resolution-first stub
therefore lost its context (`SubmittedByParticipantId`, `OriginReference`) **deterministically**, with no
concurrency involved — and `RecordEvidenceTraceRegistrationCommandHandler`'s "re-apply the terminal state"
block was dead code building an entry that was never persisted. It hid because that handler's unit test
mocks `IEvidenceTraceRepository`, so it asserts only that `UpsertAsync` was *called* — never that anything
landed. A repository whose `Upsert` silently no-ops on the update half is invisible to every mocked test
above it.

**Fix.** The merge rule is domain logic, so it now lives on the entity: `EvidenceTraceEntry.MergeFrom`
encodes the split — registration owns the submission context, resolution owns the terminal state; context
fills only where still unknown and a resolved entry never returns to `Pending`. `UpsertAsync` inserts when
absent, and otherwise folds the incoming entry in (skipping the merge when the caller handed back the same
tracked instance, which is the resolution path). On a genuine concurrent insert it now detaches the loser,
**re-reads the winner and merges onto it** rather than swallowing the delivery — so whichever side loses
the race, both halves survive. Two integration tests lock it: `Upsert_RegistrationAfterResolutionStub_
MergesContextAndKeepsTerminalState` (the deterministic path — **verified failing before the fix**, with
`SubmittedByParticipantId` coming back `null`) and `Upsert_ConcurrentCrossEventRace_PreservesBothContributions`
(the race, asserting both contributions in the settled row regardless of winner).

**Lesson for the taxonomy.** This is not an F1–F7 wiring failure — the messages arrived and the consumer
ran. It is the consumer's *write* that was lossy, which R1 (queues clean) and R2 (a row exists) both
report as healthy. A green `_error` queue and a present row do not prove the row is **correct**.

---

**(original diagnosis)**

**16 dead-lettered messages**: `EvidenceSubmissionRegistered_error` (10),
`EvidenceSubmissionAccepted_error` (4), `EvidenceSubmissionRejected_error` (2).

**Root cause (confirmed 2026-07-15).** `MT-Fault-ExceptionType = Npgsql.PostgresException`,
`MT-Fault-Message = 23505: duplicate key value violates unique constraint "PK_evidence_trace_entries"`,
consumer `EvidenceSubmissionRegisteredConsumer`. The consumer → `RecordEvidenceTraceRegistrationCommand`
→ `EvidenceTraceRepository.UpsertAsync`, which is a **read-then-`Add`** with no atomic guard against the
`evidence_submission_id` primary key:

```csharp
var existing = await _context.EvidenceTraceEntries.SingleOrDefaultAsync(...);
if (existing is null) _context.EvidenceTraceEntries.Add(entry);   // TOCTOU: not atomic
await _context.SaveChangesAsync(cancellationToken);
```

MassTransit is **at-least-once**. When the same evidence id is delivered concurrently (a redelivery burst
on reconnect/restart — precisely the HU-25B `_skipped`-requeue churn), both deliveries read
`existing = null` before either commits, both `INSERT`, and the second violates the PK → the consumer
faults → dead-letter. The Accepted/Rejected consumers write the **same** trace row (the handler's
"resolution arrived first / stub" path) and race identically — which is why all three `_error` queues are
non-zero. This is **not** a wiring bug (F1/F3/F4 ruled out: same-service, single contract, correct URN);
it is the messaging-skill rule *"consumers must tolerate redelivery / make side effects idempotent."*

- **Not stale.** The read-then-`Add` guard shipped in the same HU-32 commit `4869724` (2026-07-14 17:28)
  that the dead-letters (22:52 same day) postdate — so the guard was in place and still lost the race.
- **Backlog is purge-safe.** The committed rows already exist (verified `COUNT(*)=1` for a sampled
  dead-lettered `evidence_submission_id`), so the 16 messages are duplicates of successful inserts —
  **purge, do not replay.**

**Fix (pending).** Make the write idempotent against the unique constraint instead of read-then-add:
either catch `DbUpdateException` whose inner `PostgresException.SqlState == "23505"` and treat it as
success (row already present = desired end state), or push the insert to `ON CONFLICT
(evidence_submission_id) DO NOTHING`. Apply to the shared `UpsertAsync` so all three consumers are
covered. Then purge the three `_error` queues. Add the missing Testcontainers consumer test (Finding 3)
to lock in idempotency under redelivery.

### Finding 2 — three publish-only events with no production consumer (✅ RESOLVED 2026-07-15)

**Resolution.** Intent confirmed: the three events back the **session-history / audit-trail**
requirement (RF-14/RF-15), so the right answer was a real consumer, not dropping the publishes.
`SessionEventHistoryConsumer`
(`scoring-monitoring-service/src/Application/SessionEvents/Consumers/SessionEventHistoryConsumer.cs`)
now implements `IConsumer<T>` for all three contracts and projects each into a `SessionEvent` history
row (`session_events`, migration `20260715205421_AddSessionEventHistory`). Consumer-side contract
copies live in `src/Application/SessionEvents/Common/` with matching `[EntityName]` and
`[MessageUrn("umbral_backend.Application.Sessions.Common:<Type>")]` pins (S1/S4/S5/S6 re-run clean;
record shapes byte-identical to the publisher copies). Redelivery is handled by a **deterministic
`SourceEventKey`** — `<EventType>:<liveSessionId:D>:<occurredAt:O>[:<discriminator>]`, where the
discriminator is `previousState:currentState` for state changes and the question index for
`QuestionClosed` — carried on a unique index (`ux_session_events_source_event_key`), so an at-least-once
redelivery of the same source event collapses onto the same row instead of appending a duplicate.
`SessionEventHistoryRepository.AppendAsync` applies the **Finding 1 lesson up front**: it pre-checks by
`SourceEventKey` *and* catches the duplicate-key (`DbUpdateException` → `PostgresException { SqlState:
UniqueViolation, ConstraintName: ux_session_events_source_event_key }`) as an idempotent no-op, detaching
the rejected insert — so the concurrent-redelivery TOCTOU race that dead-lettered the EvidenceSubmission
consumers cannot recur here.
MassTransit binds all three exchanges to a single `SessionEventHistory` endpoint, registered at
`src/Infrastructure/Messaging/MassTransitMessagingRegistration.cs:23`.

**Evidence.** After `docker compose restart scoring-monitoring-service` (the wiring change needed a real
restart — `dotnet watch` logged *"No C# changes to apply"* for the DI/registration edits and could not
hot-apply them), the bus now reports `Configured endpoint SessionEventHistory, Consumer:
umbral_backend.Application.SessionEvents.Consumers.SessionEventHistoryConsumer` alongside the other four,
`Database.MigrateAsync()` applied `20260715205421_AddSessionEventHistory`, and `session_events` exists.
R1 is clean (`SessionEventHistory` = 0 messages / **1 consumer**; no `_skipped`/`_error` queue non-zero).
`rabbitmqctl list_bindings` confirms the live queue is bound to **all three** publisher exchanges:

```
session-state-changed      SessionEventHistory  queue
session-question-closed    SessionEventHistory  queue
session-results-finalized  SessionEventHistory  queue
```

**R2 — exercised live, rows landed.** Two real operator transitions were driven end-to-end through the
gateway (`PATCH /api/sessions/{id}/state`, `[Authorize(Policy = Operator)]`, as `op-1` via a Keycloak
direct-grant token — see `frontend/docs/hu-38-manual-test.md` §3 for the dev credentials) on live session
`7f614460-539c-4f92-a39a-b28c7f1aef2a`. Both returned `200` and both produced a history row in scoring:

```
event_type          | payload_summary | responsible_user_id | occurred_at
SessionStateChanged | Active→Paused   |                 444 | 2026-07-15 21:26:59.261505+00
SessionStateChanged | Paused→Active   |                 444 | 2026-07-15 21:27:28.726251+00
```

This closes the last unproven link — a live publish from `session-operations-service` reaching the
consumer over the real broker. `responsible_user_id = 444` (op-1) is worth noting: it arrives **on the
event**, snapshotted by the publisher, confirming the S7/F6 rule holds here — the consumer never reads
`ICurrentUser` (which is empty in a bus context). R1 stayed clean across both publishes
(`SessionEventHistory` = 0 messages / 1 consumer; no `_skipped`/`_error` non-zero), i.e. the messages were
consumed rather than skipped on a URN mismatch. The session was returned to its original `Active` state
afterwards.

Only `SessionStateChanged` has been driven live; `QuestionClosed` and `SessionResultsFinalized` rest on
the harness's real-broker coverage plus the verified bindings above.

---

**(original diagnosis)**

`session-state-changed`, `session-question-closed`, `session-results-finalized` are published and routed
but have **no production `IConsumer`** (only test consumers). Either a consumer was never written (a
forgotten downstream effect) or they are intentionally reserved / consumed off-repo. **Action:** confirm
intent per event; if truly unused, consider not publishing them (wasted outbox inserts + exchange
declarations), or document the intended subscriber. Low severity — no data loss, just dead exchanges.

### Finding 3 — no Testcontainers `IConsumer<T>` messaging test (✅ RESOLVED 2026-07-15)

**Resolution.** Added a reusable **publisher-contract → production-consumer** guardrail harness:
`tests/IntegrationTests/Messaging/CrossServiceConsumerHarness.cs` +
`CrossServiceConsumerWiringTests.cs`. The harness boots a real RabbitMQ Testcontainer and the **real
production DI graph** (`AddApplicationServices()` + `AddInfrastructureServices()`) against the shared
Testcontainers Postgres fixture, then publishes the **publisher's actual contract type** (aliased
`PublisherContracts = umbral_backend.Application.Sessions.Common`) from a separate bus and polls until
the effect lands in the DB. Because it publishes the *session-ops* record and asserts on the *scoring*
side effect, it fails on exactly the wiring bugs the taxonomy names — F1 (URN mismatch), F3 (`EntityName`
mismatch) and F4 (shape drift) — none of which a same-namespace test can catch. It skips cleanly when
Docker is unavailable (`DockerAvailability.StartOrSkipAsync`).

Pairs now covered (6 tests, all green):

| Exchange | Contract | Asserted effect |
|---|---|---|
| `session-answer-registered` | `AnswerRegisteredIntegrationEvent` | `score_entries` row for the submission |
| `session-target-resolved` | `TargetResolvedIntegrationEvent` | `score_entries` row for the evidence submission |
| `session-operator-assigned` | `LiveSessionOperatorAssignedIntegrationEvent` | `session_operator_assignments` projection row |
| `session-state-changed` | `SessionStateChangedIntegrationEvent` | `session_events` history row |
| `session-question-closed` | `QuestionClosedIntegrationEvent` | `session_events` history row |
| `session-results-finalized` | `SessionResultsFinalizedIntegrationEvent` | `session_events` history row |

Full suite green after the change: `make -C backend test SVC=scoring-monitoring-service` →
**221 passed / 0 failed** (18 Api + 74 Application + 68 Domain + **61 integration**).
Adding a new cross-service pair is now a one-`[Fact]` change against the harness.

---

**(original diagnosis)**

There is no automated test that publishes a real contract and asserts the consumer runs. This is the exact
gap that let **both** HU-25B (F1) and HU-38 (F2) ship silently, and it would have caught Finding 1 too.
**Action:** add one per cross-service pair (the `rabbitmq-events-dotnet` skill checklist requires it).

---

## Checklist — copy this when adding a new cross-service event

- [ ] Publisher contract in the **publisher's** namespace, with `[EntityName("<kebab-exchange>")]`.
- [ ] `Publish<Name>IntegrationEventHandler` created (mirror a sibling: rethrow-to-rollback under the outbox).
- [ ] Handler **routed** in `OutboxDomainEventDispatcher` switch **and** **registered** in `DependencyInjection`. *(S3)*
- [ ] Consumer contract in the **consumer's** namespace with **matching** `[EntityName]` **and**
      `[MessageUrn("<publisher-namespace>:<Type>")]` (no `urn:message:` prefix). *(S1, S6)*
- [ ] Record shapes identical (names, types, order). *(S5)*
- [ ] The event carries every field the consumer needs — **do not** call an authorized HTTP endpoint or
      read `ICurrentUser` from the consumer. *(S7)*
- [ ] A **Testcontainers `IConsumer<T>` messaging test** that publishes the real contract and asserts the
      consumer runs. This is the one test that catches F1/F3/F4 before prod — and the gap that let both
      incidents through (see the HU-25B handoff's outstanding item #1).
- [ ] After deploy: `_skipped`/`_error` queues at 0 *(R1)* and the effect landed in the DB *(R2)*.

## Related

- `backend/docs/hu-25b-scan-score-handoff-2026-07-15.md` — F1 (URN) + F6 (consumer auth) in depth.
- `frontend/docs/hu-38-manual-test.md` — F2 (missing publisher), now closed.
- `.claude/skills/rabbitmq-events-dotnet` — house conventions for authoring events.
- Auto-memory: `masstransit-contract-urn-must-match`, `scoring-ranking-keys-on-referenceteamid`.

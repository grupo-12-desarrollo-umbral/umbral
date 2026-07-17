# Mobile Concurrency Remediation Plan

**Date:** 2026-07-17  
**Status:** Proposed  
**Workloads:** Session Operations, Scoring Monitoring, Mobile  
**Scope:** Remediate the five concurrency findings discovered in multi-device participant flows.

## Outcome

The five findings are addressed through three core mechanisms:

1. Serialize every `LiveSession` aggregate write, including child-only mutations.
2. Add optimistic concurrency and whole-handler retries to ranking recalculation.
3. Replace process-local presence counting with distributed connection leases.

The plan preserves current REST and SignalR wire contracts unless the connection identifier must be carried through an internal command. Existing answer and target uniqueness constraints remain as defense-in-depth.

## Findings in Scope

1. Two teammates can resolve different final targets concurrently, leaving every target resolved without opening the substage reveal.
2. A different team's in-flight scan can be accepted and scored after another team has opened the hard-cut reveal.
3. Concurrent joins can exceed capacity, and concurrent switches can leave one participant active in multiple teams.
4. Concurrent score events can allow a stale ranking calculation to overwrite a newer complete ranking.
5. Reconnect/disconnect overlap can leave an actively connected participant recorded as disconnected, especially across service replicas.

## Guiding Decisions

### `LiveSession` consistency boundary

Every call to `ILiveSessionRepository.UpdateAsync` must update the `live_sessions` principal row. PostgreSQL `xmin` can then serialize all aggregate mutations, not only mutations that naturally change a principal property.

The existing `ConcurrencyRetryBehaviour` remains responsible for clearing tracking and rerunning the complete command. Domain rules are therefore reevaluated against the winning state rather than blindly retrying a stale save.

### Database invariants remain mandatory

Optimistic concurrency protects normal application paths, while database constraints protect against bypasses and future regressions. Existing answer and accepted-target unique indexes stay in place. Team membership receives an additional constraint for one active team per participant.

### Projection concurrency is independent

`ScoringMonitoring` owns `Ranking` and must implement its own concurrency token, conflict translation, tracking reset, and retry behavior. Session Operations implementation types must not be imported across bounded contexts.

### Presence must be distributed

An in-memory singleton cannot be authoritative when two connections may reach different replicas. Presence is coordinated through persisted, expiring connection leases and a per-participant transactional lock.

## Phase 1 — Deterministic Regression Harnesses

Before changing production behavior, add deterministic integration tests using independently loaded DbContexts and test barriers that control the read/commit order. Do not rely on probabilistic `Task.WhenAll` timing alone.

Target suites:

- `session-operations-service/tests/IntegrationTests/Persistence/LiveSessionConcurrencyIntegrationTests.cs`
- `session-operations-service/tests/IntegrationTests/Api/RegisterTargetScanEndpointTests.cs`
- `session-operations-service/tests/IntegrationTests/Api/SelectTeamEndpointTests.cs`
- `session-operations-service/tests/IntegrationTests/Api/MultiDeviceTeamSyncHubTests.cs`
- A new ranking concurrency integration suite under `scoring-monitoring-service/tests/IntegrationTests/Persistence/`
- `mobile/src/__tests__/team-join-hook.test.ts`

Required red scenarios:

- Same team, two devices, two different final targets.
- Different teams, one clearing scan and one non-final in-flight scan.
- Two participants competing for the final capacity slot.
- One participant concurrently switching to two teams.
- A stale ranking calculation saving after a calculation that observed more score entries.
- A new connection overlapping the old connection's disconnect callback.

## Phase 2 — Serialize Every `LiveSession` Aggregate Write

### Implementation

Update `Infrastructure/Persistence/Repositories/LiveSessionRepository.cs` so `UpdateAsync` explicitly marks the aggregate root's `LastModified` property as modified before `SaveChangesAsync`.

The existing audit interceptor supplies the timestamp. Marking the root modified forces EF to emit a principal update guarded by the original `xmin`, conceptually:

```sql
UPDATE live_sessions
SET updated_at = @updatedAt
WHERE id = @id AND xmin = @originalXmin;
```

If the interceptor does not reliably include the property after it is marked, adjust `AuditableEntityInterceptor` to make the timestamp update explicit. Do not add a second concurrency mechanism unless the existing `xmin` mapping proves insufficient.

Keep both repository translations:

- `DbUpdateConcurrencyException` becomes `ConcurrentModificationException`.
- Relevant unique violations become the same conflict so the handler can reread and reach the domain verdict.

### Tests

- A child-only insert changes the root's `xmin`.
- Two child-only writers loaded from the same `xmin` cannot both save without one receiving `ConcurrentModificationException`.
- `ConcurrencyRetryBehaviour` clears the stale graph and reruns the handler.
- Outbox rows from a failed attempt roll back and are emitted only by the successful attempt.

### Acceptance

- Every aggregate mutation participates in root concurrency.
- Retried commands reevaluate all domain rules against fresh state.
- Existing same-target and same-question uniqueness behavior remains unchanged.

## Phase 3 — Finding 1: Different Final Targets from One Team

With aggregate serialization active, the expected sequence is:

1. Device A resolves target X and commits.
2. Device B's target Y save loses the `xmin` race.
3. Device B retries against a graph containing X.
4. Resolving Y now satisfies `IsSubstageClearedBy` and opens the reveal.

### Regression tests

- Both different target resolutions are accepted.
- All active targets are resolved exactly once.
- `IsAwaitingSubstageRankingReveal` becomes true.
- Exactly one reveal-started domain event and outbox message are produced.
- The reveal deadline is not restarted.
- The timer worker advances or finishes the session after the reveal.

### Contract impact

None.

## Phase 4 — Finding 2: Losing-Team Scan Past the Hard Cut

The winning scan changes `_substageRevealUntil`, which updates the root. The losing child-only save must now conflict and retry. On retry, `DetermineTargetResolutionRejection` sees the active reveal and returns `SubstageAlreadyCleared`.

### Regression tests

- Team A's clearing scan commits first.
- Team B's stale save loses the concurrency race.
- Team B's retry persists a rejected evidence record with `SubstageAlreadyCleared`.
- Team B receives no `TargetResolvedEvent` and no scoring integration event.
- Accepted targets and the reveal deadline remain unchanged.
- Rejected evidence remains queryable for audit.

### Contract impact

None. The existing rejection code is reused.

## Phase 5 — Finding 3: Capacity and Active Membership

### Backend serialization

The root write barrier makes capacity checks sequential. When two participants compete for one slot, the losing request retries, observes the committed member, and raises the existing `TeamCapacityReachedException`.

Concurrent team switches become equivalent to a sequential ordering. The later successful request may win, but the participant must finish in exactly one team.

### Database invariant

Add a filtered unique index on `session_participant_id`:

```text
UNIQUE (session_participant_id)
WHERE membership_status = 'Active'
```

This is the authoritative database guard for one active team per participant. The current `(team_id, session_participant_id)` filtered index may remain for pair lookup/history if it is still useful.

Targets:

- `Infrastructure/Persistence/Configurations/LiveSessionConfiguration.cs`
- A new Session Operations EF migration
- `ApplicationDbContextModelSnapshot.cs`

### Mobile defense-in-depth

Add a synchronous `joiningRef` to `mobile/src/lib/membership/use-team-join.ts`, following the existing answer and target-scan hooks. It must be set before starting the request and cleared on terminal resolution or reset.

The server remains authoritative; the ref only collapses fast same-device double taps.

### Regression tests

- Capacity one plus two simultaneous joins produces one success and one capacity conflict.
- Concurrent switches leave exactly one active membership.
- Reloading the aggregate never makes `FindAssignedTeam` encounter multiple matches.
- A participant cannot submit evidence for two teams.
- Rejoining a previously left team still works.
- A mobile double tap sends one join request.
- A failed request re-enables joining.

### Contract impact

None.

## Phase 6 — Finding 4: Ranking Projection Concurrency

### Persistence

Within Scoring Monitoring:

1. Map PostgreSQL `xmin` as a concurrency token on `Ranking`.
2. Translate `DbUpdateConcurrencyException` into a scoring-owned concurrency exception.
3. Translate the `rankings.live_session_id` unique violation during concurrent first creation into the same exception.

Targets:

- `Infrastructure/Persistence/Configurations/RankingConfiguration.cs`
- `Infrastructure/Persistence/Repositories/RankingRepository.cs`
- A new Scoring Monitoring migration and updated model snapshot

### Application retry

Add a Scoring Monitoring retry behavior equivalent in semantics, but not source dependency, to Session Operations:

1. Catch only the scoring concurrency exception.
2. Clear the DbContext tracker.
3. Rerun `RecalculateRankingCommandHandler`.
4. Reread all committed score entries.
5. Reread the current ranking and calculate the next version.

Add the required local `IUnitOfWork` abstraction and implementation if Scoring Monitoring does not already expose tracking reset.

`GeneratedAt` must be monotonic. Processing an older event after a newer one must not move the projection timestamp backward. `CalculationVersion` must be derived from the freshly loaded ranking.

### Regression tests

- Concurrent creation of the first ranking settles to one row.
- A stale one-entry projection cannot overwrite a newer two-entry projection.
- Concurrent entries from the same team produce the full total.
- Concurrent entries from different teams produce complete ordered rows.
- Failed attempts emit no ranking-refreshed outbox message or SignalR notification.
- Final rows equal a fresh fold over every committed `ScoreEntry`.

### Contract impact

None.

## Phase 7 — Finding 5: Distributed Connection Presence

### Replace process-local authority

Replace `Api/Services/ConnectionTracker.cs` as the authoritative connection count with a distributed connection-lease registry. A small local cache may remain for renewal efficiency, but it cannot make the final disconnect decision.

Add a persisted `participant_connection_leases` model containing at least:

- Connection ID
- Live session ID
- External identity ID
- Session participant ID after successful admission
- Owning service instance ID
- Created, renewed, and expiry timestamps

Add indexes for active leases by `(live_session_id, external_identity_id)` and participant.

### Atomic connect protocol

1. Receive `Context.ConnectionId` in the internal reconnect command.
2. Acquire a per-participant PostgreSQL advisory transaction lock.
3. Insert the connection lease before making the participant active.
4. Admit or reconnect the participant.
5. Attach the resulting `SessionParticipantId` to the lease.
6. Commit the lease and aggregate presence together.
7. Roll back the lease if admission fails.

The public SignalR method and request payload do not need to expose the connection ID; the hub obtains it from `Context.ConnectionId`.

### Atomic disconnect protocol

1. Dispatch a connection-oriented disconnect command using `Context.ConnectionId`.
2. Acquire the same participant lock.
3. Remove the lease idempotently.
4. Count remaining unexpired leases.
5. Mark the participant disconnected only when the count is zero.
6. Commit lease removal and presence atomically.

### Crash recovery

Maintain a lightweight local list of connections owned by the instance and periodically renew their persisted leases. A background reaper removes expired leases from crashed instances and recalculates participant presence under the same participant lock.

### Multi-replica real-time requirement

If Session Operations runs more than one replica, configure a supported SignalR backplane in the same rollout. Distributed presence alone does not make instance-local hub groups or broadcasts cross-replica.

### Regression tests

- An old socket disconnects between new admission and connection registration.
- Two devices connect and either device disconnects first.
- Connections represented by two independent service scopes/registry instances remain present until the last lease is removed.
- Duplicate disconnect callbacks are idempotent.
- A failed reconnect leaves no lease.
- An expired lease from a crashed instance is reaped.
- Reaping the last lease marks the participant disconnected exactly once.

### Contract impact

No public wire change is expected. Internal command shapes and persistence schema change.

## Commit Sequence

Keep every commit buildable. Use red-green-refactor locally inside each commit rather than retaining deliberately failing commits.

1. `test(session-operations): reproduce cross-device aggregate races`
2. `fix(session-operations): serialize every live-session aggregate write`
3. `test(session-operations): lock target completion and hard-cut behavior`
4. `fix(session-operations): enforce one active team membership`
5. `fix(mobile): collapse concurrent team-join attempts`
6. `test(scoring-monitoring): reproduce stale ranking overwrite`
7. `fix(scoring-monitoring): retry ranking concurrency conflicts`
8. `test(session-operations): reproduce reconnect-disconnect overlap`
9. `fix(session-operations): coordinate presence with distributed leases`
10. `docs: document aggregate and presence concurrency guarantees`

## Verification Gates

### Session Operations

```bash
make -C backend build SVC=session-operations-service
make -C backend test SVC=session-operations-service
make -C backend gate SVC=session-operations-service
```

### Scoring Monitoring

```bash
make -C backend build SVC=scoring-monitoring-service
make -C backend test SVC=scoring-monitoring-service
make -C backend gate SVC=scoring-monitoring-service
```

### Mobile

```bash
cd mobile
npm test -- --runInBand
npm run lint
```

### Manual two-device matrix

Verify on physical devices or two independent clients:

1. Same team scans two different final targets at nearly the same time.
2. Different teams scan while one team clears the substage.
3. Two participants select the final team slot.
4. One participant attempts different team selections from two devices.
5. Different teams generate score changes simultaneously and observe the ranking.
6. One device reconnects while another connection for the same participant drops.

## Completion Criteria

The remediation is complete only when:

- Every `LiveSession` save checks root concurrency, including child-only mutations.
- All target-resolution outcomes are equivalent to some valid sequential ordering.
- A participant has at most one active team membership.
- Team capacity cannot be exceeded by concurrent joins.
- Ranking rows cannot regress behind committed score entries.
- Presence is correct across overlapping connections and multiple service instances.
- Failed concurrency attempts leave no business rows, outbox messages, or broadcasts behind.
- Both backend coverage gates, the mobile test suite, lint, and the manual two-device matrix pass.

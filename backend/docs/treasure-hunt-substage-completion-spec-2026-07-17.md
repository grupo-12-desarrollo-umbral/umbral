# Treasure-hunt substage completion + mission deadline — spec

Closes the mixed-mission hang: a mission whose substages alternate play modes
(Trivia → TreasureHunt → Trivia) currently stalls forever at the treasure-hunt substage.
Supersedes D-4 in `hu33a-context.md` ("treasure-hunt parks").

**Missing citation, added after the fact — it reframes this whole document.** The park is also recorded
in **`backend/adr/0005-substage-advancement-pointer-and-timer-driven-orchestration.md`** (cite by path;
`ADR-0005` is ambiguous — see `backend/AGENTS.md`), which this spec never cites. That ADR **predicted
this work rather than being superseded by it**:

> **The future treasure-hunt runtime (DES-42/HU-31, DES-37/HU-27) reads `ActiveSubstageId`** to drive
> its own substage — it does not reach into orchestrator internals. When it lands, advancing into a
> treasure-hunt substage becomes **"drive it" instead of "park"; no change to the advancement rule.**

> **A mixed trivia/treasure-hunt mission run before the treasure-hunt runtime exists parks at the
> treasure-hunt substage** (pointer advanced, nothing drives it) rather than crashing. DES-78 verifies
> end-to-end only on an all-trivia multi-substage mission and **does not claim mixed missions run**.

So the "Why the hang exists" section below is right about the *mechanism* and wrong about the
*intent*: the park was a **documented, deliberate, explicitly temporary consequence**, not an oversight,
and *"mixed missions run"* was never claimed. **Step 3 is the fulfilment of that ADR, not a supersession
of it** — and it landed exactly as the ADR required: advancement stayed generic
(`ISubstageAdvanceCoordinator`), activation stayed trivia-only (`IQuestionActivator`), and the
treasure-hunt trigger reads the pointer from the domain rather than reaching into orchestrator
internals. **The ADR needs no amendment. Do not open one.**

## Why the hang exists

`CompleteActiveSubstageAndAdvance` (`LiveSession.cs:1413`) is play-mode agnostic and would
happily advance out of a treasure-hunt substage. It has exactly one caller —
`TriviaRoundOrchestratorFacade.cs:134`, reachable only after a trivia question closes. So
substage advancement is structurally reachable only from trivia question exhaustion, and
`Finished` is reachable only by walking off the end of the substage list. Once
`ActiveSubstageId` points at a treasure-hunt substage, the pointer is immovable for the life
of the session.

Target scans already work (`RegisterTargetScan:665`) — they accept, resolve, score, and raise
`TargetResolvedEvent`. Nothing checks whether the substage is now cleared.

## Model

Two end conditions, and only two:

1. **A substage ends** when the first team clears it (treasure-hunt) or when its questions are
   exhausted (trivia). Every substage end shows the ranking for 10s, then advances — or, if it
   was the last substage, finishes the mission on the ranking.
2. **The mission ends** when `MaximumTime` is reached, wherever play happens to be. This applies
   to both play modes.

There is no per-substage clock. The substage ends on completion, not on time.

### D-1 — Treasure-hunt substage ends on first clear, for everyone

The first team to resolve every active target in the substage ends that substage for all teams.

The predicate already exists, in `BuildTreasureHuntContext` (`LiveSession.cs:1123-1145`), which
counts active targets and the team's accepted submissions side by side for the team board:

```
resolvedTargets == activeTargets.Length
```

where active targets are `TargetSnapshots.Where(t => t.SubstageSnapshotId == substage && t.IsActive)`
and `resolvedTargets` counts that team's `Accepted` submissions in the substage. Extract it to a
shared domain predicate rather than duplicating the count.

"All teams advance together" needs no work: the session has a single `ActiveSubstageId` shared by
all teams. There is no per-team pointer.

Resolution stays **free-order**. `TargetSnapshot.SequenceOrder` exists but
`DetermineTargetResolutionRejection` (`:747`) enforces only substage membership, `IsActive`, and
not-already-resolved-by-that-team. Do not add ordering.

### D-2 — Hard cut on clear

The moment a team clears, the substage is closed. In-flight scans from other teams are rejected.
Teams keep the score for targets already accepted. No grace window.

~~the ranking shown during the reveal is the settled result and must not move while displayed.~~
**Corrected — that sentence was never achievable and the code cannot deliver it.** D-2's invariant is
about **inputs**, which the hard cut genuinely delivers: once a team clears, **no further scoring input
can arrive**, so the result is settled at clear time. The **projection is a different thing**, and it
converges asynchronously:

- `TargetResolvedEvent` (`LiveSession.cs:745`) and `BeginSubstageRankingReveal` (`:760`) leave on the
  **same commit**, but by different roads. The reveal dispatches **post-commit, in-process**, straight
  to SignalR (microseconds). The score goes to an **outbox** and must cross **two hops at
  `QueryDelay = 1s` each** (session-operations → `TargetResolved` → scoring-monitoring →
  `ScoreEntryRegistered` → ranking recalc) before `RankingChanged` is pushed.
- The outbox cannot publish before the transaction commits, and the reveal fires immediately after it.
  **The ordering is not merely unguaranteed — the reveal always wins.** The window opens showing a
  ranking that provably excludes the very scan that opened it, and re-orders ~0–2s in.

**Clients MUST expect the ranking to converge during the window**, and must not treat the first frame
as final. After convergence it cannot change, because no input can. Do **not** "fix" this by
suppressing `RankingChanged` for the window: that freezes the podium on the *pre-clear* state and
hides the winning team's own clearing scan from its victory screen.

**Do not cite `LiveSession.cs:1565-1570`'s idempotency comment as satisfying this.** That guard is real
but answers a *different* race — two teams clearing in one tick re-opening the window. Idempotency of
the window cannot make an asynchronously-projected ranking synchronous. Step 3's tests never caught the
contradiction because they all stop at the session-operations boundary.

**Recorded as `backend/docs/adr/0019-ranking-reveal-opens-before-the-projection-converges.md`**, with
the rejected alternatives (watermark gating, payload inlining, client-side suppression). **Read the ADR
before changing any of this** — a spec is dated, the ADR is the durable record.

### D-3 — Every substage end shows the ranking for 10s

Uniform across both play modes. This **changes today's trivia flow**, where advance is immediate
after the last question's 5s reveal. New trivia sequence:

```
last question closes → 5s answer reveal (existing) → 10s ranking → next substage
```

The two reveals are different mechanisms at different granularities and both survive: the 5s
`QuestionRevealDuration` (`TriviaRoundOrchestratorFacade.cs:15`) is per-question; the 10s ranking
is per-substage.

Last substage → the ranking is terminal: show mission-finished state on it, session → `Finished`.

### D-4 — `MaximumTime` becomes the mission deadline

**This is a bug fix, not just a refactor.** `MaximumTime` is currently used in exactly one place —
`LiveSession.cs:1690`, seeding the treasure-hunt substage timer:

```csharp
_substageTimerTotalDuration = TimeSpan.FromMinutes(MaximumTime.Minutes);
```

It is never a mission deadline anywhere. So today **each treasure-hunt substage gets the entire
mission time budget**: a mission with two hunts and `MaximumTime = 60` can run 120+ minutes.

New semantics: one mission-wide deadline, seeded once when the session starts
(`StartedAt`, set at `LiveSession.cs:1494`), ticking across every substage of both modes. On
expiry the session ends immediately — terminal ranking, `Finished`.

**Do not delete the substage-timer machinery — repurpose it.** The `_substageTimer*` fields
(`:26-29`) already carry exactly the semantics a mission deadline needs: freeze on pause
(`FreezeSubstageTimer`, `EnterPausedSubstageTimerState:1527`), resume on unpause
(`ResumeSubstageTimer`, `EnterActiveSubstageTimerState:1512`), and expire-once
(`MarkSubstageTimerExpiredIfElapsed:439`). A mission deadline **must** freeze on pause — paused
time cannot burn the budget — so rebuilding this from `StartedAt + MaximumTime` wall-clock
arithmetic would reintroduce a bug the current code already solves. Rename to `_missionTimer*`,
seed once at start instead of per substage, and the EF columns from
`20260713070423_AddTreasureHuntSubstageTimerColumns` carry over under a rename migration.

This also erases the stale-field hazard documented in `ListActiveTimersAsync` — the substage timer
fields going stale after a TreasureHunt → Trivia advance, because `SeedSubstageTimerIfTreasureHunt`
early-returns without clearing them. A once-seeded mission timer has no such window.

## Changes

**Domain — `LiveSession.cs`**
- Extract `IsSubstageClearedBy(teamId, substageId)` from the `BuildTreasureHuntContext` count.
- In `RegisterTargetScan`, after `AcceptRegisteredTarget` + `TargetResolvedEvent`: if the substage
  is now cleared by this team, begin the ranking reveal.
- Add `BeginSubstageRankingReveal(occurredAt)` → sets `_substageRevealUntil = occurredAt + 10s`,
  raises `SubstageRankingRevealStartedEvent`. **Idempotent** — no-op if already set (two teams can
  clear in the same tick; see the race note below).
- Reject evidence while `_substageRevealUntil` is set (D-2), alongside the existing
  `EnsureActiveTreasureHuntSubstage` gate.
- `SeedSubstageTimerIfTreasureHunt` → `SeedMissionTimer`, called once from the start path
  (`:1503`), not from `CompleteActiveSubstageAndAdvance` (`:1446`).
- Update the D-4 comment block at `:1407` — it documents the parking behaviour being removed.

**Orchestration**
- The 10s reveal is mode-agnostic, so it does **not** belong in `TriviaRoundOrchestratorFacade`.
  Advancement is currently trivia-shaped only because trivia was the only caller; that's the root
  cause of this bug and shouldn't be re-entrenched. Either lift advancement into a
  `SubstageAdvanceCoordinator` both modes call, or move it behind an interface the worker drives.
- Trivia: `CompleteQuestionRevealAsync` (`:122`), when questions are exhausted, calls
  `BeginSubstageRankingReveal` instead of `AdvanceSubstageAsync` directly.
- There is no `TreasureHuntOrchestratorFacade` and this spec does not need one — the treasure-hunt
  trigger lives in the domain on scan.

**Worker — `AuthoritativeSessionTimerWorker.cs`**
- New branch, mirroring the `IsAwaitingQuestionReveal` branch (`:66-70`): if awaiting substage
  ranking reveal and elapsed → advance or finish.
- Replace the `if (!isTriviaQuestion) { continue; }` dead end (`:106-109`) — mission timer expiry
  now ends the session.
- The `isTriviaQuestion` branch (`:82`) currently uses `ActiveQuestionIndex is not null` as a proxy
  for play mode to pick *which* timer to tick. It now selects only which window fills
  `RemainingMilliseconds`; the mission timer ticks unconditionally alongside it (D-5).
- `ListActiveTimersAsync` predicate: `_substageTimer*` → `_missionTimer*`, plus
  `_substageRevealUntil != null`.

### D-5 — Mission timer rides the existing timer event

`SessionTimerUpdatedNotificationDto` gains two nullable fields rather than getting a sibling event:

```csharp
public sealed record SessionTimerUpdatedNotificationDto(
    Guid LiveSessionId,
    long RemainingMilliseconds,      // question window (trivia) — unchanged
    bool IsPaused,
    DateTimeOffset EmittedAt,
    long TotalMilliseconds,
    bool IsExpired,
    string SessionState,
    long? MissionRemainingMilliseconds,   // new
    long? MissionTotalMilliseconds);      // new
```

One broadcast per tick, so no extra traffic and the two clocks can never arrive out of step.
Additive: unknown JSON fields are ignored, so current mobile keeps working untouched until
`useSessionTimer` opts in.

Note this event is *already* multiplexed — `substage-countdown.tsx` documents the pre-game
countdown riding the same event with a short window, and `useSessionTimer` routes by context. The
mission fields are a third rider. That's the accepted cost of this option; if the routing in
`useSessionTimer` gets hard to follow, splitting the event is the escape hatch.

During a treasure-hunt substage there is no question window, so `RemainingMilliseconds` /
`TotalMilliseconds` have no substage clock to carry (D-4 removes it). Decide whether they go to
zero or mirror the mission values — mirroring is likely kinder to the existing hook.

**Contract / clients**
- New hub event for the ranking reveal (participants + operators), carrying the ranking payload and
  the 10s deadline. ~~Mobile subscribes to 5 events today (`sessions-hub.ts:90-110`) — this is a 6th.~~
  **Correction (step 3 as-built): mobile subscribes to _6_ events today — `sessions-hub.ts:89-112`,
  one `connection.on` per exported `on*` method: `SessionTimerUpdated`, `SessionStateChanged`,
  `QuestionActivated`, `QuestionClosed`, `SubstageAdvanced`, `TeamBoardUpdated`. The reveal is a
  _7th_.** See the resolved-questions section for what the event actually carries (not the ranking).
- **"participants + operators" is well-founded — operators really do subscribe, on two hubs.**
  `frontend/app/lib/realtime/session-state-client.ts` connects to `/hubs/sessions` (`:54`), joins via
  `JoinLiveSessionAsOperatorAsync` (`:359`), and takes nine events (`:302-352`); a second client,
  `ranking-client.ts:30,155`, takes `/hubs/scoring` → `RankingChanged`.
- **⚠️ The reveal will NOT "just arrive" on the frontend the way it does on mobile.** The frontend
  defensively normalizes camelCase ?? PascalCase for **every** event (`session-state-client.ts:84-266`),
  so a new event needs its own `normalize*` function there. Mobile needs no such thing — it has **no
  runtime validation at all** (no zod/io-ts anywhere in `mobile/src`; the SignalR callback is a plain
  static cast), which is *why* D-5's additive-fields claim holds for mobile and says nothing about the
  operator app.
- Ranking data already exists: `GET /api/sessions/{id}/ranking`, live via scoring hub
  `RankingChanged`. **Both verified true** — `useRanking` really is push-driven
  (`use-ranking.ts:61-69`), not REST-polling. ~~Mobile already renders `PodiumLeaderboard` from
  `useRanking` as an "ALL TEAMS" tab (`team-space.tsx:624-632`).~~ **It renders at
  `team-space.tsx:633`, and it is not a tab** — it is a collapsed disclosure gated on `teamsOpen`
  (`:624`, default `false`), opened by a `Pressable`. There is no tab bar. The reveal is a forced
  full-screen presentation of that same data, not a new projection.
- ~~`SubstageAdvancedEvent` already carries `PlayMode` — clients can distinguish.~~ **False, and
  unnecessary.** The field is **`FromPlayMode`** — the mode being *left*
  (`SubstageAdvancedEvent.cs:28`); there is no `ToPlayMode`, and `toSubstageId` is an opaque Guid. So
  this event tells a client which mode **ended**, never which mode is **beginning**. Two reasons it
  does not matter:
  - **The reveal carries its own `PlayMode`** (see the resolved-questions section), so *"a treasure hunt
    just completed"* is readable from the single reveal message.
  - **The incoming mode already arrives by another road.** Mobile branches on
    `board.activeSubstage.playMode` (`team-space.tsx:416`, `:480`), fed by the **team board** — REST
    snapshot plus the `TeamBoardUpdated` push, which `BroadcastTeamBoardNotificationHandler` re-projects
    on `SubstageAdvancedEvent` and emits *before* `SubstageAdvanced`. `SubstageAdvanced` is not, and has
    never been, mobile's source of play mode.

### D-6 — Mission expiry mid-reveal lets the reveal finish

If `MaximumTime` lands during a 10s ranking reveal, the reveal plays out and the session then ends
on it. The ranking is already the terminal screen, so this is visually identical to a normal
mission end, and it avoids flashing a screen for a fraction of a second. Concretely: the reveal
branch in the worker is checked before the mission-expiry branch.

### D-7 — Empty treasure-hunt substages are rejected at authoring

A treasure-hunt substage with no active targets can never be cleared by scanning, so it would hang
exactly like today's bug. `GET /api/missions/{id}/readiness` must fail such a substage, blocking
activation before a session ever exists — the only point where a human can actually fix it.

> **⚠️ Everything below this line was wrong when written. This guard already existed — see the
> §4 annotation. It is not cross-service work, not a contract change, and needs no `ActivationBar`
> change. Read §4 before acting on the next two paragraphs.**

**This is a cross-service change** (`mission-design-service`, not `session-operations-service`) and
therefore a contract change under the monorepo boundary rules: flag it, and update the frontend's
`ActivationBar` / readiness surfacing alongside. The runtime deliberately does **not** get a
defensive "zero targets = instantly cleared" fallback, so a mission snapshotted before this guard
lands can still hang. If any such missions exist in a live database, they need a data check.

## Concurrency — `xmin` concurrency token (prerequisite)

Two teams clearing in the same tick, or a clear racing the mission deadline, hit the same weakness
as trivia's first-answer-wins: `LiveSessionRepository.UpdateAsync` (`:172-180`) is a bare
`SaveChangesAsync` — no transaction, no row lock, no concurrency token (`IsRowVersion` appears only
on MassTransit outbox tables). Idempotent `BeginSubstageRankingReveal` keeps the domain correct
(second caller no-ops), but two concurrent writers can still collide at the DB. Unlike
first-answer-wins there is no unique index to catch it here, so the failure mode is a **lost
update** — the reveal begun and then overwritten by a concurrent scan's save.

**Status: implemented.** What shipped differs from what was first specced here, in three ways worth
recording.

**1. `xmin` is mapped by hand.** Npgsql dropped `UseXminAsConcurrencyToken()` in v9 and this repo is
on v10, so the helper does not exist. `LiveSessionConfiguration` declares the mapping the helper used
to generate:

```csharp
builder.Property<uint>("xmin")
    .HasColumnName("xmin")
    .HasColumnType("xid")
    .ValueGeneratedOnAddOrUpdate()
    .IsConcurrencyToken();
```

The scaffolded migration must have its `AddColumn<uint>("xmin")` deleted — `xmin` is a system column
that already exists, so adding it errors. **Any future migration touching `live_sessions` will
re-scaffold that AddColumn; delete it there too.**

**2. `xmin` does NOT fix first-answer-wins — that was wrong.** `TriviaAnswerSubmissions` and
`TreasureEvidenceSubmissions` are `OwnsMany`, i.e. separate child tables. EF only checks a
concurrency token when it emits an `UPDATE` on the principal row, and inserting a child touches no
principal column. Two teammates answering concurrently therefore never reach the token; they collide
on the unique index instead. The two guards are complementary and neither is sufficient:

| Race | Guard |
| --- | --- |
| State transition, timer field, `_substageRevealUntil` (D-1) | `xmin` token |
| Answer insert, target-scan insert, clue release | unique index on the child table |

`xmin` still matters for this spec — D-1's reveal sets a principal scalar, so the lost update it
would otherwise cause is genuinely prevented.

**3. Retry re-runs the handler, not the save.** A repository cannot "reload and re-apply": the
mutation already happened on the tracked instance and `UpdateAsync` does not know which operation to
replay. Retry therefore lives in `ConcurrencyRetryBehaviour` (innermost MediatR behaviour), which
re-executes the whole handler. The repository translates both `DbUpdateConcurrencyException` and
23505 `DbUpdateException` into `ConcurrentModificationException`, since Application references
neither EF nor Npgsql by design.

Re-running the handler is what makes both races correct, and it is why no bespoke error mapping was
needed: the retry re-reads and the existing domain rules produce the ordinary sequential outcome —
`DuplicateTriviaAnswerException` → 409 for a duplicate answer, and a *rejected submission* → 200 for
a duplicate scan (target duplicates are rejections, not exceptions). Only an exhausted budget (3
attempts) surfaces `ConcurrentModificationException` → 409.

**Also fixed here:** treasure evidence had **no unique index at all**, so a team could double-resolve
one target (D-7's precondition — a double count lets `resolvedTargets == activeTargets.Length` fire a
false clear). Added `ux_treasure_evidence_accepted_target` on
`(live_session_id, team_id, target_snapshot_id)`, filtered to `Accepted`. The filter is load-bearing:
a rejected re-scan keeps the target id it resolved to, so an unfiltered index would collide on the
second rejection. The FK index on `live_session_id` had to be redeclared explicitly — EF's
conventions drop it as redundant against the new index's prefix, not knowing that a *partial* index
cannot serve the unfiltered by-session load.

**The worker needs care.** It writes outside MediatR, so it gets no retry, and `xmin` gives it a
failure mode it never had. It also shares one context across every session in a tick, so a single
`SaveChanges` covers them all and one loser would fail the whole batch. `TickAsync` now catches per
session and resets tracking; every tick recomputes from stored state, so the next one re-does
whatever was abandoned.

## Verification

`LiveSessionFactory.CreateScheduledTriviaThenTreasureHunt()` already exists but is used only for
team-board projection tests (`LiveSessionTests.cs:1637-1687`). Those call
`CompleteActiveSubstageAndAdvance` directly on the domain, bypassing the facade, and assert only
board labels — they pass today while the runtime is dead, and they are exactly why this bug
survived. **They are not evidence of mixed-mode play.**

Needed:
- An integration test driving the **worker** through Trivia → TreasureHunt → Trivia to `Finished`,
  asserting the 10s reveal between each. This is the test that would have caught the bug.
- Mission-deadline expiry in each play mode, and expiry landing mid-reveal (D-6).
- Deadline freezes across a pause/resume cycle — the regression D-4 is most likely to introduce.
- Hard cut (D-2): a scan in flight when another team clears is rejected, and the clearing team's
  already-accepted targets keep their score.
- Concurrency (`xmin`): two teams clearing the same substage concurrently yields one reveal and a
  409 for the loser, not a lost update. Same test shape for two teammates answering one trivia
  question — asserting **409, not 500**.
- Readiness (D-7) rejects a treasure-hunt substage with zero active targets.

## Suggested order

1. ~~`xmin` concurrency token + 409 mapping~~ — **done**; see the concurrency section for how it
   differs from what was planned. Note it did *not* fix first-answer-wins on its own, as originally
   claimed; the retry behaviour is what fixes that.
2. ~~Mission timer rename/reseed (D-4) + nullable DTO fields (D-5)~~ — **done**. `_substageTimer*` →
   `_missionTimer*` under a column-rename migration (`RenameSubstageTimerToMissionTimer`; EF did not
   re-scaffold the `xmin` AddColumn this time, but check every future `live_sessions` migration).
   Seeded once from `EnterActiveSessionState`, so it now runs for trivia sessions too — which is why
   a tick with no active question reports the deadline rather than the zeros the unseeded substage
   timer used to give. The DTO's mission fields default to null, leaving the pre-game countdown rider
   in `TriviaRoundStartedNotificationHandler` untouched. Treasure-hunt substages mirror the mission
   values into `RemainingMilliseconds`/`TotalMilliseconds` (the D-5 open question), which is what the
   existing play-mode switch in `GetAuthoritativeSessionTimerSnapshot` already did.

   The worker still only *reads* the mission timer for the broadcast — expiry ending the session is
   step 3's `if (!isTriviaQuestion) { continue; }` replacement, so `MaximumTime` is displayed but not
   yet enforced.
3. ~~Cleared predicate + reveal + mode-agnostic advancement (D-1, D-2, D-3, D-6)~~ — **done**. The hang
   is closed: `MixedModeSessionWorkerDriveTests` drives the **worker** Trivia → TreasureHunt → Trivia to
   `Finished`. Four things differ from what is written above, all recorded in the decisions section
   below plus these two:

   - **The reveal push is keyed off the domain event, not the coordinator.** An earlier draft
     broadcast from `SubstageAdvanceCoordinator.BeginRankingRevealAsync` — which silently left the
     treasure hunt with no reveal on screen, since a clear opens the window inside `RegisterTargetScan`
     with no coordinator in the call path. `BroadcastSubstageRevealStartedNotificationHandler` handles
     `SubstageRevealStartedEvent` instead, so both modes push identically. The mixed-mode worker test
     is what caught this.
   - **The domain event is `SubstageRevealStartedEvent`, not `...RankingReveal...`.** `LiveSessionTests`
     pins that no domain event name contains "Ranking"/"Score"/"Finalized" (HU-33B D-2 — scoring and
     ranking stay downstream in scoring-monitoring). The event carries no ranking, so only the name
     violated it. The client-facing DTO is still `SubstageRankingRevealStartedNotificationDto`: the
     domain fact is "the substage is revealing", and "show the ranking" is a contract-layer concern.
   - **`TargetResolutionRejectionReason.SubstageAlreadyCleared` is checked LAST**, after the
     target-specific rejections. Checking it first told a team that cleared and re-scanned its own
     target "another team already completed this stage" — false, and less precise than the duplicate
     diagnosis. Ordering it last also makes the message true by construction: the clearing team
     resolved every target, so its own later scans are duplicates, and only a non-clearing team can
     reach that reason.
   - EF did not re-scaffold the `xmin` `AddColumn` for `AddSubstageRankingRevealColumn` either — that
     is now twice. **Still check the next one.**

4. ~~Readiness guard (D-7) — cross-service, can land in parallel~~ — **already shipped, and it
   predates this spec by a month.** No code was written for this step; D-7 was specced from an
   assumption about `mission-design-service` that turned out to be false. The guard landed in
   `4039880` (2026-06-18, "feat(hu-09): mission management rebuild"), a month before this document
   was written.

   `MissionActivationPolicy.EvaluateTreasureHunt`
   (`mission-design-service/src/Domain/Services/MissionActivationPolicy.cs:107-114`) requires an
   **active** target, not merely a target — so *zero targets* and *all targets inactive* both fail,
   which is the full extent of what D-7 asks for:

   ```csharp
   if (!substage.Targets.Any(target => target.IsActive))
   {
       failures.Add(
           $"Treasure-hunt substage '{substage.Title}' in stage '{stage.Title}' must have at least one active target.");
   }
   ```

   **It is enforced at three layers, not one**, which is why the runtime's lack of a "zero =
   cleared" fallback is safe rather than load-bearing:

   - `GET /api/missions/{id}/readiness` reports it (`GetMissionReadinessQueryHandler.cs:32-45`
     re-derives readiness live on every call rather than reading `ActivationState`).
   - `Mission.Activate()` throws `MissionNotReadyForActivationException` on it (`Mission.cs:295-300`).
   - `Mission.RefreshActivationState()` (`Mission.cs:323-341`) demotes `Ready` → `Draft` the moment
     an authoring change breaks the plan. **Every** mutation path calls it — `AddTarget`,
     `UpdateTarget` (incl. `isActive: false`), `RemoveTarget`, `SelectTriviaQuiz`, and the
     `MissionStructureEditor` branches via `RecordStructureChanged`. The one documented exception,
     `AssociateClueWithTarget`, cannot affect readiness. So a `Ready` mission **cannot** be edited
     into a target-less treasure-hunt substage without being demoted first.

   And `session-operations-service` independently re-derives it at session creation:
   `SessionCreationPolicy.EnsureMissionEligible` (`Domain/Services/SessionCreationPolicy.cs:16-27`)
   rejects any mission that is not **both** `Ready` and failure-free
   (`CreateSessionCommandHandler.cs:38-47`). A `Draft` mission cannot be snapshotted.

   **It is not a contract change and the frontend needs nothing.** Readiness failures are plain
   `IReadOnlyList<string>` of English prose; `ActivationBar.tsx:51-61` renders each one verbatim
   into `<li data-testid="readiness-failure">` and gates the button on `isReady`. There is **no**
   client-side code→copy mapping anywhere in `frontend/app/` for readiness — a new backend rule
   appears in the UI on the next fetch with no client change. (The flip side: the backend owns this
   UX copy outright, and `getMissionReadiness` returns `response.json()` unvalidated, so the
   `string[]` type is an unenforced promise. A rule returning `{code, message}` objects would make
   React throw rather than degrade.)

   **Tests already exist** for both halves, in
   `tests/UnitTests/Domain/Services/MissionActivationPolicyRuntimePlanTests.cs`:
   `EvaluateReadiness_WhenTreasureSubstageMissingTarget_ReportsMissingActiveTarget` (`:33-46`, zero
   targets) and `..._WhenTreasureTargetInactive_...` (`:48-62`, all inactive). That covers this
   spec's Verification line *"Readiness (D-7) rejects a treasure-hunt substage with zero active
   targets"*.

   **One genuine gap this step surfaced, left unfixed and deliberately not folded in here** — a
   TOCTOU race, not a missing rule. `CreateSessionCommandHandler` fetches `/readiness` (`:38`) and
   the runtime plan (`:46`) as two unsynchronized HTTP round-trips with no version/ETag pinning
   between them, and mission-design's runtime-plan endpoint does **no** readiness check of its own
   (`GetMissionRuntimePlanQueryHandler` is a pass-through; `MissionReadModelRepository.cs:116-130`
   copies *all* targets with `IsActive` riding along as data). An author removing the last active
   target inside that window yields a session snapshotted from a plan the readiness verdict never
   described. It is the only remaining route from an empty treasure-hunt substage to a
   `LiveSession` — and step 3 bounded its blast radius: such a session now ends at `MaximumTime`
   via `FinishOnMissionDeadline` instead of hanging forever. Own ticket.

## Step 3 — open questions, resolved before implementing

Four questions this spec left open (or contradicted itself on). Decided with the user; recorded here
because each one changes the shape of the work.

**1. The reveal event carries a signal, not the ranking.** The "carrying the ranking payload" line
above is not implementable as written: ranking lives in `scoring-monitoring-service` and
`session-operations-service` has no ranking projection, so inlining it would mean a cross-service
call in the hot path. The event carries a signal instead; clients render the ranking they already hold
(`useRanking` / `PodiumLeaderboard`, live via the scoring hub's `RankingChanged`). This is what the
contract section already implies — *"the reveal is a forced full-screen presentation of that same
data, not a new projection"*.

**Corrected against the as-built code** — this paragraph originally named
`SubstageRankingRevealStartedEvent`, which exists nowhere (see §3 on the domain/DTO name split), and
listed a payload of `substageSnapshotId` / `revealUntil` / `isTerminal`, **omitting `PlayMode`**. What
actually ships is:

```csharp
public sealed record SubstageRankingRevealStartedNotificationDto(
    Guid LiveSessionId,
    Guid SubstageSnapshotId,
    string PlayMode,              // omitted from the original record of this decision
    DateTimeOffset RevealUntil,
    bool IsTerminal,
    DateTimeOffset EmittedAt);
```

`PlayMode` matters to clients: the reveal is **mode-agnostic** — one event for a hunt cleared by its
first team *and* for a trivia substage out of questions. So *"a treasure-hunt substage completed"* is
`SubstageRankingRevealStarted` with `PlayMode == "TreasureHunt"`, readable from the single message.
**The contract section's suggestion that clients distinguish via `SubstageAdvancedEvent.PlayMode` is
therefore unnecessary for this event** — that correlation is not needed.

**2. The reveal does not freeze on pause.** `_substageRevealUntil` is an absolute deadline, exactly
like the `_questionRevealUntil` it sits beside. `ListActiveTimersAsync` filters `State == Active`, so
a paused session is not ticked and the reveal simply does not complete; on resume the deadline is
already past and the next tick advances. Paused time does not burn the 10s, but the remainder is not
replayed either. Chosen for consistency with the HU-35 window — freezing would need its own
fields and a migration, and would diverge from the reveal directly next to it.

**3. Mission expiry finishes immediately — no reveal of its own.** D-4 says *"ends immediately"*, and
mission expiry is not a substage end, so D-3's 10s does not apply to it. The worker transitions
straight to `Finished`. D-6 is unaffected: expiry landing *during* a reveal still lets that reveal play
out.

~~the client's finished screen is the ranking, so it looks the same.~~ **This justification was false
when written** — mobile's finished screen is a red *"Session closed. / This session has ended."* text
panel (`question-empty-state.tsx:22-31`), and the podium sits behind a `useState(false)` collapsed
drawer (`team-space.tsx:322`). The operator side is the same shape (`TriviaRoundPanel.tsx:101-105`,
*"Session complete"*). It is **accidentally true today only because nothing renders the reveal either**
— which is the opposite of the argument, and dies the moment the reveal is built.

**The decision stands, and the client contract below is what makes its justification true.**

### The ranking view — one view, two states, three paths (client contract)

Decided with the user. There is **one** ranking view. It is not a screen per ending.

| Path | What the client shows |
| --- | --- |
| Substage ends, not last (`IsTerminal: false`) | Ranking view for 10s, then advance off it |
| Substage ends, last (`IsTerminal: true`) | **The same ranking view, already showing "mission finished"**, for its 10s — then `Finished` arrives and the view simply persists |
| Mission expiry (no reveal event at all) | **The same ranking view, in its "mission finished" state**, reached directly from `Finished` |
| Session **`Cancelled`** | **Not the ranking view** — keeps today's red *"This session was cancelled by the host."* panel |

**`Cancelled` is deliberately excluded** (decided with the user). The ranking view means *the mission ran
its course*; a host abort has no legitimate result and a podium would imply one. Note this **splits a
branch that is currently fused**: `isTerminalSessionState` (`active-question-types.ts:43`) returns true
for `Finished` *and* `Cancelled`, and both land on the same panel today. The two now diverge.

This is what *"it looks the same"* was reaching for: expiry and reveal-then-finish look identical because
**they are the same view**, not because two screens resemble each other.

**The backend already supports all of it — this is purely client work that was never written down.**
`IsTerminal` exists precisely so the terminal reveal can open *already* in the finished state instead of
flashing a frame at `revealUntil` (see the DTO's own comment). Consequences for whoever builds it:

- **The finished state cannot depend on the reveal event.** Mission expiry emits **no** reveal, so the
  view must also be reachable from `sessionState === 'Finished'` alone. A client that only renders the
  ranking on `SubstageRankingRevealStarted` shows an expired mission nothing at all.
- **It must outrank the treasure-hunt board.** `team-space.tsx:480` (`if (playMode === 'TreasureHunt'
  && board)`) returns before the empty-state branch is ever reached, so a hunt participant currently
  keeps seeing the live board on `Finished`. The reveal is a *forced full-screen* presentation — it
  takes precedence over both surfaces.
- **Expect the ranking to converge on this view** — see D-2. The clearing scan's own points land ~0–2s
  into the window, so the podium re-orders while the winner watches. This is the surface where that is
  visible.
- **Refetch on open, and keep accepting pushes** (decided with the user). Both halves are load-bearing
  and neither is sufficient alone:
  - **Refetch** `GET /api/sessions/{id}/ranking` when the view opens (reveal start, or `Finished`).
    Without it the finale can be **frozen at session start**: `useRanking` silently degrades to REST-only
    when the scoring hub fails to construct (`team-space.tsx:315-321`) or fails to start/join
    (`:351-353`, *"Scoring hub unavailable — ranking remains REST-only"*), and in that fallback there is
    **no polling** — `use-ranking.ts:39-59` fetches once per `fetchNonce`. Silent, and the mission's
    last screen is the worst place for it.
  - **Keep applying `RankingChanged`.** The refetch fires microseconds after the commit that merely
    *enqueued* the clearing score, so it races the same two outbox hops the push does. Only the pushes
    deliver convergence.

**4. Advancement moves to `ISubstageAdvanceCoordinator`.** Of the two options offered above, the
coordinator — not the worker-driven interface. `AdvanceSubstageAsync` leaves
`TriviaRoundOrchestratorFacade` (the spec names its trivia-shaped advancement as the root cause);
the facade and the worker both depend on the coordinator.

**Not an open question, recorded so nobody re-opens it:** D-2's rejection needs a new
`TargetResolutionRejectionReason` member, and that needs **no mobile change**. Mobile maps 422 →
`'retained-rejection'` and renders the backend's `detail` verbatim, so a new reason arrives as copy.
Its compiler-exhaustive `TargetScanRejectionReasonCode` union is untouched.

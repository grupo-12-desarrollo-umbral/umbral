# Treasure-hunt substage completion + mission deadline — spec

Closes the mixed-mission hang: a mission whose substages alternate play modes
(Trivia → TreasureHunt → Trivia) currently stalls forever at the treasure-hunt substage.
Supersedes D-4 in `hu33a-context.md` ("treasure-hunt parks").

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
Teams keep the score for targets already accepted. No grace window — the ranking shown during the
reveal is the settled result and must not move while displayed.

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
  the 10s deadline. Mobile subscribes to 5 events today (`sessions-hub.ts:90-110`) — this is a 6th.
- Ranking data already exists: `GET /api/sessions/{id}/ranking`, live via scoring hub
  `RankingChanged`. Mobile already renders `PodiumLeaderboard` from `useRanking` as an "ALL TEAMS"
  tab (`team-space.tsx:624-632`). The reveal is a forced full-screen presentation of that same
  data, not a new projection.
- `SubstageAdvancedEvent` already carries `PlayMode` — clients can distinguish.

### D-6 — Mission expiry mid-reveal lets the reveal finish

If `MaximumTime` lands during a 10s ranking reveal, the reveal plays out and the session then ends
on it. The ranking is already the terminal screen, so this is visually identical to a normal
mission end, and it avoids flashing a screen for a fraction of a second. Concretely: the reveal
branch in the worker is checked before the mission-expiry branch.

### D-7 — Empty treasure-hunt substages are rejected at authoring

A treasure-hunt substage with no active targets can never be cleared by scanning, so it would hang
exactly like today's bug. `GET /api/missions/{id}/readiness` must fail such a substage, blocking
activation before a session ever exists — the only point where a human can actually fix it.

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
3. Cleared predicate + reveal + mode-agnostic advancement (D-1, D-2, D-3, D-6) — the feature.
4. Readiness guard (D-7) — cross-service, can land in parallel.

# The ranking reveal opens before the ranking converges, and that is deliberate

When a team clears a treasure-hunt substage, `LiveSession.RegisterTargetScan` raises
`TargetResolvedEvent` and opens the 10s ranking reveal on the **same commit** — but the two travel by
different roads, and the reveal always wins. We accept that the reveal opens showing a ranking which
provably excludes the very scan that opened it, and converges ~0–2s later, rather than coupling
Session Operations to Scoring Monitoring's projection to make the first frame correct.

## Status

accepted

## Why the reveal always wins

- The **reveal** dispatches post-commit, in-process, straight to SignalR
  (`DispatchDomainEventsInterceptor` → `BroadcastSubstageRevealStartedNotificationHandler`).
  Microseconds.
- The **score** goes to an outbox and must cross **two hops at `QueryDelay = 1s` each** — Session
  Operations → `TargetResolved` → Scoring Monitoring → `ScoreEntryRegistered` → ranking recalc —
  before `RankingChanged` is pushed.

The outbox cannot publish before the transaction commits, and the reveal fires immediately after that
same commit. This is not a race that might resolve either way: the ordering is **inverted by
construction**.

## What "settled" means

The spec's D-2 originally said *"the ranking shown during the reveal is the settled result and must not
move while displayed."* The first half is true and the second is not, and the distinction is the whole
point of this ADR:

- **Inputs are settled.** The hard cut on clear rejects in-flight scans, so no further scoring input
  can arrive. The result is fixed at clear time.
- **The projection is not yet.** It is another context's read model, reached asynchronously.

`LiveSession`'s idempotent `BeginSubstageRankingReveal` is sometimes cited as satisfying D-2. It does
not: it answers a *different* race — two teams clearing in one tick re-opening the window. Idempotency
of the window cannot make an asynchronously-projected ranking synchronous.

## Considered Options

- **Suppress `RankingChanged` on the client for the reveal window (rejected).** Satisfies D-2's original
  wording and is the fix a reader will reach for first. It freezes the podium on the **pre-clear** state,
  so the winning team's own clearing scan never appears on its victory screen. The letter of D-2, with
  its spirit inverted.
- **Gate the reveal on a projection watermark (rejected).** Delay opening until Scoring Monitoring's
  `calculationVersion` covers the clearing target. The only option that makes the first frame genuinely
  settled — at the cost of D-1's synchronous open, and of making Session Operations depend on Scoring
  Monitoring's liveness to end a substage. That inverts the dependency direction in
  `backend/CONTEXT-MAP.md`, where Session Operations emits runtime outcomes and Scoring Monitoring
  derives from them.
- **Inline the ranking into the reveal payload (rejected).** Session Operations owns no ranking
  projection, so this means a cross-service call in the hot path of every clearing scan.
- **Accept convergence, and make the client honest about it (chosen).** No coupling, no new failure
  mode, and the on-screen result is correct within ~2s of a 10s window.

## Consequences

- **Clients must not treat the reveal's first frame as final.** The ranking view is specified to
  REST-refetch on open *and* keep applying `RankingChanged` — the refetch defends against
  `useRanking` silently degrading to a single un-refreshed fetch when the scoring hub is unavailable;
  the pushes deliver convergence. Neither alone is sufficient.
- **The podium visibly re-orders while the winner watches.** This is expected, not a bug.
- **Do not "fix" this by suppressing pushes during the window.** See Considered Options.
- **The 10s window is what makes this tolerable.** Convergence costs ~0–2s of it. Shortening the reveal
  materially would make the stale frame a larger fraction of what participants see, and would need this
  decision revisited.

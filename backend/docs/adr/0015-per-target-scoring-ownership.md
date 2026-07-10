# 0015 — Per-target scoring: MissionDesign authors, SessionOperations relays, ScoringMonitoring accumulates

## Status

Accepted — 2026-07-09. Ratifies the two contract decisions that
`docs/workflow_refactor.md` ("Gating refactor — DES-86", lines 364-376) recorded as
homeless — no ticket or ADR resolved either, and "nobody has ratified" the proposed
reading. DES-86 code starts now, so the decisions are recorded here before the migration
lands. Consistent with `DES-85` (see Decision §2 for why this is not a reversal). Does
**not** alter [ADR-0011](0011-application-layer-vertical-slice-organization.md),
[ADR-0012](0012-design-pattern-placement-convention.md), or
[ADR-0014](0014-no-cross-layer-reflection.md). Touches the same treasure-hunt surface as
[ADR-0010](0010-evidence-qr-only-first-delivery.md) (the `EvidenceSubmissionRegistered` →
`TargetResolved` two-fact ordering). That ADR was **unratified** when this one was written,
so its ordering was not assumed here — this ADR defines the `TargetResolved` payload on its
own footing, and still does. *(Updated 2026-07-10: ADR-0010 is now Accepted. The two
decisions remain independent; nothing below changes.)*

**Amended 2026-07-10 (difficulty-derived scoring).** The *ownership split* below — MissionDesign
produces the point, SessionOperations relays it, ScoringMonitoring accumulates it — **still
holds and is unchanged**. What changed is *how MissionDesign produces the number*: a `Target`'s
`ScoreValue` is **no longer authored** per target. It is **derived** from the owning mission's
`Difficulty` as `ScoreValue.BaseTargetScore (50) × Difficulty.ScoreFactor` (Beginner/Intermediate/
Advanced ⇒ 50/100/150) at add/update time, and re-derived for every target when a mission's
difficulty changes (`Mission.RepriceTargets` / `Target.Reprice`). `ScoreValue.MaximumPoints` rose
`100 → 150` to fit the Advanced tier. Wherever Decision §2 below says the point is "authored" or
"set at authoring time", and wherever the range "`1`-`100`" appears, read "derived from mission
difficulty" and "`{50, 100, 150}`". Shipped in `c9e0e3b` (see
`docs/difficulty-derived-target-score-handoff-2026-07-10.md`).

## Context

- `DES-86` migrates treasure-hunt scoring from **winner-takes-all** to **per-target
  `ScoreValue`**. Origin: `docs/target-score-handoff.md` (2026-07-09). Canon docs were
  corrected that day; the code was not. `WinnerScore` still appears across
  `mission-design-service` + `session-operations-service`, and the old model is encoded in
  two domain invariants, not just a field.
- Two contract decisions sit **upstream** of DES-86's acceptance criteria and had no home:
  what `TargetResolved` carries, and which bounded context owns the authored points. If
  either is decided differently, DES-86's ACs change — so they are settled here, not
  discovered during HU-31 (`DES-42`).
- `ScoreValue` is **already** a mission-design value object today
  (`services/mission-design-service/src/Domain/ValueObjects/ScoreValue.cs`, integer
  `1`-`100`) and is already carried by `Substage.WinnerScore`
  (`services/mission-design-service/src/Domain/Entities/Substage.cs:42`, set via
  `SetWinnerScore`, `:86`). Moving the authored point from the substage to the `Target`
  reuses this value object; it is **not** a new cross-context dependency.
- The trivia side already landed this shape. `AnswerRegisteredEvent` (HU-34)
  carries `IsCorrect` + `ScoreValue` in the event so downstream scoring can consume the
  number without recomputing it
  (`services/session-operations-service/src/Domain/Events/AnswerRegisteredEvent.cs:17,27`).
  Treasure-hunt resolution is the same problem and gets the same answer.

## Decision

1. **`TargetResolved` carries the resolved target's `ScoreValue`.** `DES-51` (HU-37)
   requires every score change to produce a `ScoreEntry` stamped with its origin, so
   `scoring-monitoring` needs the point value at hand — it must not reach back into
   mission content to recompute it. The event does not exist in code yet, so it is defined
   correct from the start: the resolution fact and the authored point travel together, on
   the precedent of `AnswerRegisteredEvent`
   (`services/session-operations-service/src/Domain/Events/AnswerRegisteredEvent.cs:17,27`).

2. **Ownership split — author / relay / accumulate.**
   - **MissionDesign authors** the points: a `Target` carries its own `ScoreValue`
     (`1`-`100`), set at authoring time, reusing the existing mission-design value object
     (`Substage.cs:42`).
   - **SessionOperations relays** them: it snapshots the authored number into the
     `TargetResolved` event on resolution. It computes **no totals** — it forwards an
     authored, snapshotted value.
   - **ScoringMonitoring accumulates** them: each `TargetResolved` becomes one
     `ScoreEntry` in the ledger. **No mutable total exists outside `ScoreEntry`.**

   *Why this is consistent with `DES-85`, not a reversal.* `DES-85`'s PRD declares
   `ScoreValue` a value object of the `ScoringMonitoring` context and puts score
   **computation** in `session-operations-service` out of scope. Both clauses hold under
   this decision: session-operations still computes nothing — it relays a number authored
   and snapshotted elsewhere — and the running total lives only in ScoringMonitoring's
   `ScoreEntry` ledger. A value object being *named* in ScoringMonitoring's model does not
   forbid MissionDesign from authoring a point value on an authoring-time entity: value
   objects are context-local, and mission-design **already** declares its own `ScoreValue`
   for `Substage.WinnerScore` today. Authoring a point is not computing a score.

3. **Advancement is unchanged; zeroing is removed.** The first team to resolve all targets
   still triggers **advancement** (all teams move to the next substage). It no longer
   zeroes other teams' points — every team keeps what it accumulated per resolved target.

## Rollout shape (expand → migrate → contract)

The mission-design ↔ session-operations seam is an **HTTP call with hand-duplicated DTOs on
both sides** — the producer (`MissionsController.cs`) and the consumer
(`MissionRuntimeSource.cs`) each declare their own shape; there is **no shared contracts
assembly**. That duplication is what permits a three-step, independently-deployable
rollout instead of one lockstep change:

- **F1 — expand.** Add `ScoreValue` to `Target` and `TargetSnapshot` and to both DTO
  sides; keep `WinnerScore` in place. Emit the new field additively so producer and
  consumer deploy independently.
- **F2 — migrate.** Read and snapshot per-target points, emit `TargetResolved` with
  `ScoreValue`, and stop depending on `WinnerScore` in `SubstageSnapshot`. ScoringMonitoring
  consumes the relayed per-target value into `ScoreEntry`.
- **F3 — contract.** Remove `WinnerScore` from both services, their DTOs, and the
  migrations.

## Consequences

- **`TargetResolved` is defined once, correctly.** ScoringMonitoring never recomputes a
  point it did not author; the ledger origin is exact.
- **Value-object equality changes.** Removing `WinnerScore` from
  `SubstageSnapshot.GetEqualityComponents()` changes what makes two snapshots equal.
  **Snapshot-comparison test fallout is expected** and is part of the F3 cost.
- **`TriviaSubstageSnapshotCannotDeclareWinnerScoreException` is deleted.** Its only
  production throw guards `SubstageSnapshot`'s constructor against a `winnerScore` argument;
  once the field is gone the exception has no referent.
- **`TreasureHuntSubstageSnapshotWinnerScoreRequiredException` is re-homed, not inverted.**
  It moves from the substage to the target: the per-target score-required check lands beside
  the existing per-target walk in `EnsureSubstageInvariants`
  (`MissionRuntimeSnapshot.cs`, the `targetSnapshots`-by-`SubstageSnapshotId` guard). The
  invariant is preserved; only the entity it guards changes.
- **The seam stays two hand-written DTOs, by design.** Not extracting a shared contracts
  assembly is what buys the expand→migrate→contract sequencing across two independently
  deployable services. The cost is that the two DTO shapes must be kept in agreement by
  review, not by the compiler.
- **Sequencing, not lockstep, is the coupling control.** The producer and consumer remain
  coupled at the contract boundary, but because the seam is HTTP with duplicated DTOs, the
  rollout is intentionally additive first and contractive last. The projection stays intact
  by landing F1 → F2 → F3 in order, not by requiring one atomic two-service deployment.

## Genuine conflict recorded

One point is a real tension, not an apparent one, and is resolved deliberately above rather
than papered over: `DES-85`'s PRD names `ScoreValue` as a value object **of the
ScoringMonitoring context**. Read strictly as *only ScoringMonitoring may name a
`ScoreValue`*, that collides with mission-design already declaring its own `ScoreValue`
(`Substage.cs:42`) and now authoring one on `Target`. This ADR ratifies the context-local
reading — the same ubiquitous term legitimately has a class in more than one bounded
context — because `DES-85`'s **operative** constraint is that session-operations performs
no score computation, which this decision upholds. If a future revision of DES-85 asserts
sole naming ownership as a hard rule, that clause and this ADR conflict and one must yield;
today they do not.

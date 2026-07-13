# Parallel Implementation Batch — Verified Against Linear + Codebase (2026-07-13)

Which DES tickets can be implemented **simultaneously in separate git worktrees** right
now, derived from live Linear blocking relations and a code-level file-overlap analysis of
`session-operations-service` / `mission-design-service`. Supersedes the coarse
"one Lane-A ticket at a time" rule in `parallel-work-lanes.md`: the real constraint is not
"same service" but specific **method-level** collisions.

## Status update (2026-07-13, ~20:33 UTC)

The batch has since been picked up: **DES-42 (HU-31) and DES-95 (HU-30) are both now In Progress**,
and DES-40/DES-41 were resolved as Duplicate of DES-95. The active Todo set is now **14** (not 16).

⚠️ **Warning:** DES-42 and DES-95 are the **❌ hard-conflict pair** in the matrix below — both edit the
evidence-submission intake seam on `LiveSession.cs` (~L389). Running them as concurrent worktrees
is exactly what this doc advises against. **Sequence them** (95 → 42 or 42 → 95) and rebase the
second onto the first; do not merge both independently.

GitHub note: **GH #164** (MassTransit bootstrap) closed 2026-07-12, and **GH #148** (admin user
management) closed 2026-07-13 via PR #207 — the open-issue count is now **6**, not 7.

## TL;DR

Four previous-batch tickets landed (DES-29, DES-36, DES-39, DES-93 — all Done 2026-07-13),
which unblocked the current roots. Original batch as planned:

```
DES-94 + DES-42 + DES-38
```

Rule that makes them safe: **pick only one of {DES-95, DES-42}.**
DES-94 and DES-38 can run alongside DES-42.

Next wave (after the batch lands): **DES-13 + DES-95 + DES-51**, then DES-43.

## Startability (verified in Linear, 2026-07-13)

All 16 active-at-snapshot, non-archived Todo HUs were checked (now 14, after DES-42 and DES-95
left Todo for In Progress). At snapshot time exactly **4 were startable** (every `blockedBy`
edge Done); of those, DES-42 and DES-95 are now In Progress, leaving **DES-94 and DES-38** as the
startable Todo roots:

| Ticket | HU | Service tree(s) | Blockers (all Done) |
|---|---|---|---|
| DES-94 | ENABLER operator-panel targets | session-operations | none |
| DES-42 | HU-31 QR scan / target resolution | session-operations | DES-86, DES-77, DES-76, DES-31, DES-39 |
| DES-38 | HU-28 add operational clues live | session-operations | DES-77, DES-76, DES-31, DES-36 |
| DES-95 | HU-30 context validation and explained rejection of evidence | session-operations | DES-39 |

Every startable ticket touches `session-operations-service`.

Blocker states confirmed Done as of 2026-07-13: DES-76 (state machine), DES-77 (timer realign),
DES-31 (HU-23 board), DES-86 (per-target ScoreValue), DES-11/DES-12 (HU-07A/07B),
DES-39 (evidence intake base), DES-36 (manual clue release).

DES-13 is not startable: DES-11 and DES-12 are Done, but its live Linear relations also list
DES-42 as an active blocker.

## Recently landed (since 2026-07-12 snapshot)

| Ticket | HU | Completed | Effect |
|---|---|---|---|
| DES-29 | HU-21 session-state audit | 2026-07-13 | Unblocked DES-56 (HU-40A) |
| DES-36 | HU-26 manual clue release | 2026-07-13 | Unblocked DES-37, DES-38 |
| DES-39 | HU-29 evidence intake base | 2026-07-13 | Unblocked DES-95, DES-42 |
| DES-93 | TreasureHunt substage timer | 2026-07-13 | Leaf — unblocked nothing further |

All four were startable in the previous snapshot and landed in a single day.

## Ticket changes since snapshot

| Ticket | HU | Created | Status |
|---|---|---|---|
| DES-94 | ENABLER operator-panel targets | 2026-07-13 | Todo, startable immediately |
| DES-95 | HU-30 merged evidence validation and rejection | 2026-07-13 | Todo, replaces DES-40 and DES-41 |

DES-94 enriches `ActiveSubstageContextDto` with a per-target list for operator clue-release target
picking. Backend-only, no client changes, builds on DES-32 (Done).

DES-40 and DES-41 are canceled as duplicates of DES-95. DES-95 follows the DES-42 lifecycle:
every submission is registered first and then becomes `accepted` or `rejected` with an explicit
reason when rejected.

DES-37 remains in Todo but was archived on 2026-07-13, so it is excluded from this active batch.

## Conflict matrix

Legend: ✅ disjoint/trivial · ⚠️ mechanical (migration snapshot + field-block adjacency) · ❌ do-not-parallelize · ? unverified (no code-level analysis yet)

|            | 94 | 42 | 38 | 95 |
|------------|----|----|----|----|
| **94** panel-targets  | —  | ⚠️ | ✅ | ⚠️ |
| **42** QR/target      |    | —  | ⚠️ | ❌ |
| **38** op-clues       |    |    | —  | ✅ |
| **95** ctx-validation/rejection |    |    |    | —  |

### The ❌ hard pairs

- **DES-42 ✕ DES-95** — both touch the evidence-submission intake path on `LiveSession.cs`.
  DES-39 (now Done) created the `EvidenceSubmission` base contract and the
  `EvidenceSubmissionRegisteredEvent`. DES-42 subclasses it (`TreasureEvidenceSubmission`) for QR
  targets; DES-95 validates context and records accepted or explained-rejected outcomes.
  They share the submission-registration seam (~L389 region). Sequence **95 → 42** or **42 → 95**
  — pick one and rebase the other.

### Why DES-94 ✕ DES-42 and DES-94 ✕ DES-95 are ⚠️

DES-94 enriches `ActiveSubstageContextDto` (the operator-panel projection). DES-42 adds runtime
target resolution that feeds the same DTO's `resolvedTargets` count. DES-95 validates context
that the panel reads. All three touch the operator-panel DTO factory, but at **different fields**:
DES-94 adds a `targets` list; DES-42 updates counts; DES-95 adds validation metadata. Adjacent-line
conflicts in the DTO class and factory method, trivially resolved.

### DES-13 waits for DES-42

Lives entirely in `Api/Hubs/SessionsHub.cs` + `Application/Sessions/EventHandlers/BroadcastTeamBoardNotificationHandler.cs`
+ the reconnect DTO. No `LiveSession.cs`, no Domain, no migration, no identity-access source changes
(reuses the HU-07A/07B guards as-is). Its footprint is compatible with the other work, but its
Linear blocker prevents starting it until DES-42 lands.

## Recommended batches

- **Maximal (3 worktrees):** `DES-94 + DES-42 + DES-38`.
  Leads with DES-42 (a major downstream linchpin) and DES-38 (next clue-release wave).
  DES-94 is additive and independent.
- **Conservative (2 worktrees):** `DES-94 + DES-42` — one isolated DTO change plus the QR
  linchpin.

For the maximal batch, DES-95 is excluded because it conflicts with DES-42 at the submission
seam.

## Merge order (maximal 3)

Merge back in this order:

```
DES-94 → DES-42 → DES-38
```

Rationale per position:

1. **DES-94** — no migration (additive DTO field only). Its DTO changes are a trivial rebase onto
   anything; merging it first keeps it out of the way of the migration tickets.
2. **DES-42** — migration ticket (likely adds migration for `TreasureEvidenceSubmission` table).
   Merge it before DES-38 so the snapshot regen is straightforward.
3. **DES-38** — migration ticket (likely adds migration for operational-clue tables).
   Rebase onto DES-42's snapshot and regenerate.

Mechanical step for **#2, #3** (each in turn) — do **not** hand-merge the snapshot file:

```
git checkout <branch> && git rebase develop
dotnet ef migrations remove          # drop the now-stale migration
dotnet ef migrations add <SameName>  # re-add against the updated model
# verify, then merge
```

**After all three land:** run the full suite and `dotnet ef database update` against a fresh DB to
confirm the migration chain applies cleanly.

## Sequencing after the batch

```
DES-42 ──▶ DES-51 (Lane B ledger) ──▶ DES-54 (ranking) ──▶ DES-34/35/48/33
DES-42 + DES-95 ──▶ DES-43
DES-42 ──▶ DES-53 ──▶ DES-56 ──▶ DES-57
```

After the batch, the next wave is DES-13 + DES-95 + DES-51. DES-43 becomes startable after both
DES-42 and DES-95 land.

DES-42 remains a major linchpin for the downstream scoring, traceability, and history work.

## Caveats

- Footprints for DES-38 and DES-95 are **predicted from where the code lives today**
  (existing seams and marker comments), not from written diffs. The ❌ pairs are moderate
  confidence; a fresh code-level conflict analysis before the next implementation wave is
  recommended.
- DES-94 is new (created 2026-07-13) and its footprint in `OperatorSessionPanelDtoFactory` and
  `ActiveSubstageContextDto` is narrow. The ⚠️ with DES-42 and DES-95 is at the DTO level only.
- The migration model-snapshot conflict is **certain** for any two migration-adding tickets — plan
  the merge order and regenerate rather than hand-merging.
- Downstream note (not in this batch): **DES-56** (HU-40A history) is `blockedBy` **DES-92**, an
  enabler currently in **Backlog**. Its prerequisite **GH #164** (MassTransit bootstrap) closed
  2026-07-12, so DES-92 is no longer GH-gated (its Linear `blockedBy` is now empty) — it simply
  hasn't been pulled from Backlog. The history layer still can't complete on the active Todo
  tickets alone, because DES-92 sits outside that set.

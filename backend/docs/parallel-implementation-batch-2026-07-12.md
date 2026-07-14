# Parallel Implementation Batch — Verified Against Linear + Codebase (2026-07-14)

Which DES tickets can be implemented **simultaneously in separate git worktrees** right
now, derived from live Linear blocking relations and a code-level file-overlap analysis of
`session-operations-service` / `mission-design-service`. Supersedes the coarse
"one Lane-A ticket at a time" rule in `parallel-work-lanes.md`: the real constraint is not
"same service" but specific **method-level** collisions.

## Status update (2026-07-14)

**The 2026-07-12 batch has landed in full.** DES-94 (PR #213), DES-95/HU-30 (PR #214) and
DES-42/HU-31 (PR #215) are all **Done** as of 2026-07-14. The hard-conflict pair was sequenced
correctly: DES-95 merged before DES-42 (95 → 42), exactly as this doc advised. DES-38/HU-28 is
now **In Progress** (started 2026-07-14, domain layer committed). The active Todo set is now **12**.

New follow-up ticket: **DES-97** — a validate-criteria check on whether HU-31's
`TargetResolutionPolicy` naming divergence in PR #215 should be reconciled with canon. Not a
feature; not part of any parallel batch.

GitHub note: the open-issue count is now **4**. **GH #139** (controller try/catch vs.
ProblemDetails) was resolved by ADR-0018 (PR #211) and **GH #144** (forgot-password) closed via
PR #212, on top of the earlier #164/#148 closures.

## TL;DR

The batch `DES-94 + DES-42 + DES-38` (plus DES-95 sequenced against DES-42) is **complete** —
DES-94, DES-95 and DES-42 merged; DES-38 is the remaining member, now In Progress.

DES-42 was the major downstream linchpin. Its merge, together with DES-95, unblocked the next
wave. The current startable roots are:

```
DES-13 + DES-43 + DES-51
```

All three were `blockedBy` DES-42 (and DES-43 also by DES-95); both merged 2026-07-14. A fresh
code-level file-overlap analysis of this trio is recommended before running them as concurrent
worktrees — see Caveats.

## Startability (verified in Linear, 2026-07-14)

The previous batch roots (DES-94, DES-42, DES-95, DES-38) have all left Todo. The new startable
Todo roots, every `blockedBy` edge now Done:

| Ticket | HU | Service tree(s) | Blockers (all Done) |
|---|---|---|---|
| DES-13 | HU-08 multi-device team sync | session-operations, identity-access | DES-11, DES-12, DES-42 |
| DES-43 | HU-32 evidence traceability | session-operations, scoring-monitoring | DES-42, DES-95 |
| DES-51 | HU-37 score ledger | scoring-monitoring | DES-42 |

Blocker states confirmed Done as of 2026-07-14: DES-42 (HU-31 QR resolution), DES-95 (HU-30
context validation), DES-11/DES-12 (HU-07A/07B), plus the earlier chain (DES-76 state machine,
DES-77 timer realign, DES-31 HU-23 board, DES-86 per-target ScoreValue, DES-39 evidence intake,
DES-36 manual clue release).

## Recently landed (since 2026-07-12 snapshot)

| Ticket | HU | Completed | PR | Effect |
|---|---|---|---|---|
| DES-94 | ENABLER operator-panel targets | 2026-07-14 | #213 | Additive DTO field; merged first, out of the way of migrations |
| DES-95 | HU-30 context validation + explained rejection | 2026-07-14 | #214 | Sequenced 95 → 42; unblocked DES-43 |
| DES-42 | HU-31 QR scan / target resolution | 2026-07-14 | #215 | Linchpin — unblocked DES-13, DES-43, DES-51 |
| DES-29 | HU-21 session-state audit | 2026-07-13 | #204 | Unblocked DES-56 (HU-40A) |
| DES-36 | HU-26 manual clue release | 2026-07-13 | — | Unblocked DES-38 (DES-37 archived) |
| DES-39 | HU-29 evidence intake base | 2026-07-13 | #206 | Unblocked DES-95, DES-42 |
| DES-93 | TreasureHunt substage timer | 2026-07-13 | #205 | Leaf |

## Ticket changes since snapshot

| Ticket | HU | Status |
|---|---|---|
| DES-38 | HU-28 add operational clues live | In Progress (started 2026-07-14) — last remaining member of the 07-12 batch |
| DES-97 | Check: HU-31 `TargetResolutionPolicy` naming vs canon | Todo, created 2026-07-14 — validate-criteria follow-up on PR #215, not a batch member |

DES-40 (HU-30A) and DES-41 (HU-30B) were resolved as Duplicate of DES-95. DES-37 (HU-27) remains
archived and out of the active batch.

## Conflict matrix (historical — the 07-12 batch)

Retained for reference; every ticket below has since merged except DES-38 (In Progress).

Legend: ✅ disjoint/trivial · ⚠️ mechanical (migration snapshot + field-block adjacency) · ❌ do-not-parallelize

|            | 94 | 42 | 38 | 95 |
|------------|----|----|----|----|
| **94** panel-targets  | —  | ⚠️ | ✅ | ⚠️ |
| **42** QR/target      |    | —  | ⚠️ | ❌ |
| **38** op-clues       |    |    | —  | ✅ |
| **95** ctx-validation/rejection |    |    |    | —  |

The ❌ pair **DES-42 ✕ DES-95** — both touched the evidence-submission intake seam on
`LiveSession.cs` (~L389). They were sequenced **95 → 42** and rebased, as advised; both are now
Done, so the conflict is resolved.

## Recommended batch (current wave)

The next wave is the startable trio:

```
DES-13 + DES-43 + DES-51
```

Rough footprints (to be confirmed by a fresh code-level analysis before parallelizing):

- **DES-13 (HU-08)** — lives in `Api/Hubs/SessionsHub.cs`,
  `Application/Sessions/EventHandlers/BroadcastTeamBoardNotificationHandler.cs`, and the reconnect
  DTO. No `LiveSession.cs`, no Domain, no migration. Reuses the HU-07A/07B guards as-is. Its
  Linear blocker (DES-42) is now cleared.
- **DES-51 (HU-37)** — score ledger, lives in `scoring-monitoring-service` (a different tree from
  DES-13/DES-43's session-operations work), so it parallelizes cleanly. Likely a migration ticket
  (new `ScoreEntry` table).
- **DES-43 (HU-32)** — evidence traceability, spans session-operations and scoring-monitoring;
  reads the validation metadata DES-95 added. Overlaps the operator-panel projection surface — the
  most likely to collide with DES-13, so verify before running both concurrently.

Conservative alternative: run **DES-51** (isolated in scoring-monitoring) alongside **one** of
{DES-13, DES-43}.

## Merge order (current wave)

```
DES-51 → DES-13 → DES-43
```

1. **DES-51** — separate service tree; migration ticket. Merge first so its `ScoreEntry` model
   snapshot is settled before other migration tickets rebase.
2. **DES-13** — SignalR/reconnect only, no migration; trivial rebase.
3. **DES-43** — spans both trees and reads DES-51's ledger + DES-95's validation metadata; merge
   last and rebase onto the others.

Mechanical step for any migration-adding ticket — do **not** hand-merge the snapshot file:

```
git checkout <branch> && git rebase develop
dotnet ef migrations remove          # drop the now-stale migration
dotnet ef migrations add <SameName>  # re-add against the updated model
# verify, then merge
```

**After the wave lands:** run the full suite and `dotnet ef database update` against a fresh DB to
confirm the migration chain applies cleanly.

## Sequencing after the wave

```
DES-51 (ledger) ──▶ DES-54 (ranking) ──▶ DES-34/35/48/33
DES-51 ──▶ DES-53 ──▶ DES-56 ──▶ DES-57
DES-43 + DES-54 ──▶ DES-33
```

DES-51 is now the linchpin for the downstream scoring, ranking, and history work.

## Caveats

- Footprints for the current wave (DES-13, DES-43, DES-51) are **predicted from where the code
  lives today**, not from written diffs. DES-13 vs DES-43 both touch the operator-panel/board
  projection surface — a fresh code-level conflict analysis before running them as concurrent
  worktrees is recommended.
- The migration model-snapshot conflict is **certain** for any two migration-adding tickets (e.g.
  DES-51 and DES-43 if both add tables) — plan the merge order and regenerate rather than
  hand-merging.
- Downstream note: **DES-56** (HU-40A history) is `blockedBy` **DES-92**, an enabler
  (`ClueReleased` → RabbitMQ/MassTransit) currently in **Backlog**. Its prerequisite GH #164
  (MassTransit bootstrap) closed 2026-07-12, so DES-92 is no longer GH-gated — it simply hasn't
  been pulled from Backlog. The history layer still can't complete on the active Todo tickets
  alone, because DES-92 sits outside that set.

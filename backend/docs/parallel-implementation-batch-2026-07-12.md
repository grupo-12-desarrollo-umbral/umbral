# Parallel Implementation Batch — Verified Against Linear + Codebase (2026-07-12)

Which DES tickets can be implemented **simultaneously in separate git worktrees** right
now, derived from live Linear blocking relations and a code-level file-overlap analysis of
`session-operations-service` / `mission-design-service`. Supersedes the coarse
"one Lane-A ticket at a time" rule in `parallel-work-lanes.md`: the real constraint is not
"same service" but two specific **method-level** collisions.

## TL;DR

Run these **five in parallel worktrees**:

```
DES-13 + DES-39 + DES-36 + DES-29 + DES-93
```

Rule that makes them safe: **pick only one of {DES-39, DES-42}, and only one of {DES-36, DES-38}.**
Everything else composes.

Next wave (after the batch lands): **DES-42** (needs DES-39), **DES-38** (needs DES-36), then the
newly-unblocked DES-40, DES-41, DES-51.

## Startability (verified in Linear, 2026-07-12)

All 21 active Todo HUs were checked. Exactly **7 are startable** (every `blockedBy` edge is Done):

| Ticket | HU | Service tree(s) | Blockers (all Done) |
|---|---|---|---|
| DES-42 | HU-31 QR scan / target resolution | session-operations | DES-86, DES-77, DES-76, DES-31 |
| DES-29 | HU-21 session-state audit | session-operations | DES-76 |
| DES-36 | HU-26 manual clue release | session-operations | DES-76, DES-77, DES-31 |
| DES-38 | HU-28 add operational clues live | session-operations | DES-77, DES-76, DES-31 |
| DES-39 | HU-29 evidence intake umbrella | session-operations | DES-76, DES-77, DES-31 |
| DES-93 | TreasureHunt substage timer | session-operations + mission-design | none |
| DES-13 | HU-08 multi-device sync | session-operations + identity-access | DES-11, DES-12 |

Every startable ticket touches `session-operations-service`, which is why the lanes doc
serialized them. With worktrees, filesystem collision disappears; the binding constraint
becomes **merge-time file/method overlap**.

Blocker states confirmed Done: DES-76 (state machine), DES-77 (timer realign), DES-31 (HU-23 board),
DES-86 (per-target ScoreValue), DES-46 (HU-34), DES-11/DES-12 (HU-07A/07B), plus
DES-45/DES-26/DES-27/DES-32 (blockers of the downstream 14).

## Conflict matrix

Legend: ✅ disjoint/trivial · ⚠️ mechanical (migration snapshot + field-block adjacency) · ❌ do-not-parallelize

|            | 13 | 39 | 42 | 36 | 38 | 29 | 93 |
|------------|----|----|----|----|----|----|----|
| **13** sync         | —  | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ |
| **39** evid-base    |    | —  | ❌ | ✅ | ✅ | ⚠️ | ✅ |
| **42** QR/target    |    |    | —  | ⚠️ | ⚠️ | ⚠️ | ⚠️ |
| **36** clue-release |    |    |    | —  | ❌ | ⚠️ | ⚠️ |
| **38** op-clues     |    |    |    |    | —  | ⚠️ | ⚠️ |
| **29** state-audit  |    |    |    |    |    | —  | ⚠️ |
| **93** TH-timer     |    |    |    |    |    |    | —  |

### The two ❌ hard pairs

- **DES-39 ✕ DES-42** — compile dependency. DES-39 *creates* `EvidenceSubmissionRegisteredEvent`
  and the base intake contract on `EvidenceSubmission`; DES-42 *raises* that event and subclasses
  the base (`TreasureEvidenceSubmission`). Sequence **39 → 42**.
- **DES-36 ✕ DES-38** — both rewrite the *same* `CollectVisibleClues()` method family in
  `LiveSession.cs` (L669/L691/L711) and both need the identical `teamId` signature change threaded
  from `ProjectParticipantTeamBoard`. Sequence **36 → 38** (or extract the projection seam first).

### Why everything else is only ⚠️ (mechanical, not semantic)

The tickets edit **different methods** of `LiveSession.cs`:

| Ticket | `LiveSession.cs` footprint |
|---|---|
| DES-29 | `MoveTo` (L281) + audit child collection; call-site in `CompleteActiveSubstageAndAdvance` (L778) |
| DES-39 | evidence-registration seam (~L389), extract shared accept steps |
| DES-42 | `RegisterTargetResolution`/`ScanQrTarget` (~L389 region), `BuildTreasureHuntContext` `resolvedTargets` (L635) |
| DES-36 | clue-projection family `CollectVisibleClues` (L669) + `ReleaseClue` |
| DES-93 | timer machinery (L808–949), `GetAuthoritativeSessionTimerSnapshot` (L297–302), seed/freeze hooks |
| DES-13 | **none** — hub + event-handler layer only |

What the ⚠️ pairs actually share:

1. **EF migration model-snapshot** `Infrastructure/Migrations/ApplicationDbContextModelSnapshot.cs` —
   DES-29, DES-36, DES-38, DES-42, DES-93 each add a migration and regenerate it wholesale.
   **Never hand-merge**: merge one branch, then rebase each subsequent branch and re-run the
   migration to regenerate the snapshot (~1 min/branch). (DES-39 may add no migration — it's a base contract.)
2. **`LiveSession.cs` field-declaration block** (top of class) — each adds a private collection/timer
   field → adjacent-line conflicts, trivially resolved.
3. **DES-29 ↔ DES-93** both touch `CompleteActiveSubstageAndAdvance` (L755–789), ~4 lines apart,
   independent edits — coordinate, not a blocker.
4. **DES-42 ↔ DES-93** share the runtime-snapshot trio (`SubstageSnapshot.cs`,
   `MissionRuntimeSnapshot.cs`, `CreateSessionCommandHandler.cs`) — relevant only if both run together
   in a later wave.

### DES-13 is free

Lives entirely in `Api/Hubs/SessionsHub.cs` + `Application/Sessions/EventHandlers/BroadcastTeamBoardNotificationHandler.cs`
+ the reconnect DTO. No `LiveSession.cs`, no Domain, no migration, no identity-access source changes
(reuses the HU-07A/07B guards as-is). Bolt it onto any batch at zero cost.

## Recommended batches

- **Maximal (5 worktrees):** `DES-13 + DES-39 + DES-36 + DES-29 + DES-93`.
  Leads the evidence lane with **DES-39** (not DES-42) on purpose — 39 is the compile-root that
  *unblocks* DES-42 for the next wave instead of fighting it.
- **Cleanest (3 worktrees):** `DES-13 + DES-29 + DES-36` — one isolated + two disjoint `LiveSession`
  methods; only tax is one migration-snapshot regen (29 vs 36).

## Merge order (maximal 5)

Merge back in this order:

```
DES-13 → DES-39 → DES-93 → DES-29 → DES-36
```

Rationale per position:

1. **DES-13** — no migration, no `LiveSession.cs`. Goes in with zero conflicts; establishes a clean
   baseline, nothing to rebase.
2. **DES-39** — evidence base contract (likely no migration). Merging it early **unblocks DES-42** for
   the next wave; its `LiveSession.cs` seam (~L389) is disjoint from every other ticket's methods.
3–5. **The migration tickets, one at a time** (they each regenerate `ApplicationDbContextModelSnapshot.cs`):
   - **DES-93 then DES-29 adjacent** — they're the only pair sharing a method
     (`CompleteActiveSubstageAndAdvance`); resolving it back-to-back keeps the context fresh.
   - **DES-36 last** — its clue-projection method is independent of the other two, so it's the cheapest
     to rebase onto everything.

Mechanical step for **#3, #4, #5** (each in turn) — do **not** hand-merge the snapshot file:

```
git checkout <branch> && git rebase develop
dotnet ef migrations remove          # drop the now-stale migration
dotnet ef migrations add <SameName>  # re-add against the updated model
# verify, then merge
```

The three migrations touch **different tables** (audit table / clue-release table / substage-snapshot
column), so there is no logical ordering hazard — only the snapshot file needs regenerating.

**After all five land:** run the full suite and `dotnet ef database update` against a fresh DB to
confirm the migration chain applies cleanly — the catch-all for snapshot drift.

## Sequencing after the batch

```
DES-39 ──▶ DES-42 ──▶ DES-51 (Lane B ledger) ──▶ DES-54 (ranking) ──▶ DES-34/35/48/33
DES-36 ──▶ DES-38
DES-42 ──▶ DES-41, DES-43
```

DES-42 is the linchpin: upstream of ~12 of the 14 currently-blocked tickets. DES-38, DES-93, DES-13
are leaves (unblock nothing further in the Todo set).

## Caveats

- Footprints are **predicted from where the code lives today** (existing seams and `HU-26/HU-28/HU-31`
  marker comments), not from written diffs. The ❌ pairs and DES-13's isolation are high-confidence;
  a ticket that grows scope mid-implementation could reach further. If an agent's plan starts touching
  a shared method, pause and coordinate.
- The migration model-snapshot conflict is **certain** for any two migration-adding tickets — plan the
  merge order and regenerate rather than hand-merging.
- Downstream note (not in this batch): **DES-56** (HU-40A history) is `blockedBy` **DES-92**, an enabler
  currently in **Backlog** and gated by **GH #164** (MassTransit bootstrap). The history layer can't
  complete on the 21 Todo tickets alone.

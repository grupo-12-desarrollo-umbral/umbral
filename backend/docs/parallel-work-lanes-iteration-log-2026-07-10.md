# Parallel work lanes — iteration log (2026-07-10)

Why this exists: the "run tickets in parallel without conflicts" plan went through
several shapes before landing as `parallel-work-lanes.md`. This log keeps the
iterations so the reasoning behind the final format isn't lost, plus a status
check from later the same day showing where the plan and reality diverged.

---

## Iteration 1 — first pass, unverified

> "Validate that this can be done (run all of these in parallel)"

```
Bucket A — Backend main line (session-operations service): DES-49, DES-31, DES-32,
  DES-53, DES-29, DES-42, DES-39, DES-36… → run one at a time, they share one service.
Bucket B — Identity/Keycloak (identity-access service): GH #137, #140, #141, #142,
  #143, #144, #148.
Bucket C — Mobile (mobile app): DES-81, DES-82, DES-83, DES-84.
Bucket D — Frontend/Geo (web frontend + geo): ...
```

Not yet checked against Linear — a guess at shape, not a verified plan.

## Iteration 2 — checked against the Linear backlog

> "check what linear backlog and then give me the correct buckets"

Re-derived the buckets against live `umbral-equipo-12` data instead of guessing.
Corrected several tickets' lane assignments (e.g. GH #144 moved out of a clean
lane once it was confirmed to span identity + frontend + mobile).

## Iteration 3 — tried a visual graph

> "give it to me in a more easy to understand way or a graph"
> "pull includeRelations to double check and then give it to me in a more easy to
> understand way or a graph"

Produced an HTML board artifact (`backend/docs/parallel-work-board.html`) with
`includeRelations` pulled from Linear to double-check blockedBy edges. Rejected —
too hard to read at a glance:

> "i still dont understand, could you just give me something like
> Bucket A Y -> Bucket B X ->"

## Iteration 4 — plain ordered lists

Switched to arrow-chain plain text, one bucket per line:

```
BUCKET A — session-operations-service (backend)
  DES-49 -> DES-31 -> DES-32 -> DES-53 -> DES-29 -> DES-42 -> DES-39 -> DES-36 -> ...
BUCKET B — identity-access-service (Keycloak)
  GH#137 -> GH#140 -> GH#141 -> GH#142 -> GH#143
BUCKET C — mobile
  DES-81 -> DES-82 -> DES-83 -> DES-84 -> GH#155 -> GH#156
BUCKET D — frontend (web)
  GH#146 -> GH#148
BUCKET E — mission-design-service (backend)
  DES-80
BUCKET F — api-gateway (backend)
  GH#147

Run first, alone (touches everything): GH#149
Fits nowhere clean, run alone in a gap: GH#144
```

This format stuck — it's the direct ancestor of the final file.

## Iteration 5 — durable file + full queue

> "give me a markdown file to see which hus and issues can I run in parallel
> without conflicts (one per service at time i think)"
> "okay so give me the full list like the one before so i can run them in parallel"

Saved as `backend/docs/parallel-work-lanes.md` — the canonical, still-current
version. Expanded Lane A's queue to the full 27-ticket order and added the
per-lane caveats (DES-13 secretly touches identity, GH #148 needs GH #142 first,
GH #140 is a security fix and should be pulled early, etc).

## Iteration 6 — round-1 sanity check

> "so in round 1 I can run 5 subagents to implement all of these in parallel
> without problems? Round 1: A: DES-49  B: GH#137  C: DES-81  D: GH#146
> E: DES-80  F: GH#147"

Confirmed: 6 lanes (not 5), each in a distinct folder → no merge conflicts.
Flagged three caveats before greenlighting:
1. Each agent needs its **own git worktree + branch** — "different folders"
   only protects you if agents aren't sharing one working directory.
2. **GH #149** was supposed to run alone, first (repo-wide coverage-gate
   change) — running Round 1 before it means retrofitting branch tests into
   DES-49 / DES-80 / GH #147 afterward.
3. **GH #137 is an ADR**, not implementation work — different kind of ticket
   than the rest of the round.

---

## Status check — later the same day (2026-07-10, evening)

Re-verified Round 1 against live GitHub + Linear state:

| Lane | Ticket | Result |
|---|---|---|
| A `session-operations` | DES-49 | Still **In Progress** in Linear (started 22:28) — not merged |
| B `identity-access` | GH #137 | Done — PR #160 merged |
| C `mobile` | DES-81 | Done — PR #162 merged |
| D `frontend` | GH #146 | Done — closed |
| E `mission-design` | DES-80 | **Not started** — still Backlog, no PR |
| F `api-gateway` | GH #147 | Done — PR #161 merged |
| (solo, first) | GH #149 | Done — PR #159 merged |

**Linear/reality mismatch found:** `DES-81` was merged (PR #162) but Linear
still showed it as `status: Todo`, `startedAt: null`, `completedAt: null` —
never even marked started. `DES-49` and `DES-80` were *not* mismatches — Linear
correctly reflected their true (unfinished) state. Action: mark DES-81 Done
in Linear.

**Next actionable batch** (given the above):
```
B: GH#140    C: DES-82    E: DES-80
```
Lane A must finish DES-49 before starting DES-31 (same tree). Lane D is
stalled — GH #148 needs GH #142, which hasn't landed yet (queued behind
GH #140/#141 in Lane B).

---

> **Superseded status:** a newer live verification (Linear + GitHub) was done on
> **2026-07-11** — see `parallel-work-lanes.md` for current lane state. This log is
> frozen at 2026-07-10.

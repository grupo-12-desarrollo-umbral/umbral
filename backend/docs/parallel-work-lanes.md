# Parallel Work Lanes — one agent per service, no conflicts

**Rule:** a conflict = two agents editing the same folder at the same time.
So each lane below is **one service / codebase**. Run **one ticket at a time inside a
lane**, and run **different lanes in parallel**.

> Re-verified against live Linear (`umbral-equipo-12`) + GitHub on 2026-07-12 — `svc:` labels + `blockedBy` edges.
> `GH #NNN` = GitHub issue (no Linear ticket). `DES-NN` = Linear HU.
> 2026-07-12 re-check: `GH #141` (SMTP) and `GH #173` (trivia RBAC) closed after the earlier 07-12 pass; lanes B/D/E heads updated. `GH #171` (mobile) and `GH #177` (cross D+E) added — both were untracked.

---

## Current lane heads

| Lane | Service / tree | Run this | HU / what it is |
|------|----------------|----------|-----------------|
| **A** | `session-operations-service` | **DES-32** (`In Progress`) | Next clean single-service head after `DES-49` and `DES-31` both landed |
| **B** | `identity-access-service` | **GH #142** | SMTP + email verification landed (`GH #141` shipped); invitations next |
| **C** | `mobile/` | **GH #171** | Mixed-play-mode substage progress in team space — ungated, mobile-only |
| **E** | `mission-design-service` | **GH #177** | `DES-80` + RBAC sweep (`GH #173`) landed; remove trivia-question sequence-order, also touches `frontend/` |

- Lane **D** (`frontend/`) shares **GH #177** with Lane E; **GH #148** remains blocked by GH #142 (Lane B).
- Lane **F** (`api-gateway`) is **empty** — GH #147 shipped.

### ✅ Already landed since the 07-10 plan (no longer startable)
- **GH #149** (repo-wide branch-coverage gate) — done (PR #159), it no longer needs to run first.
- **GH #140** (confidential client) and **GH #155** (mobile play surface) are closed.
- **GH #146** (quiz preview), **GH #147** (gateway handler, PR #161).
- **GH #141** (SMTP + email verification) and **GH #173** (trivia question authoring RBAC) are closed.
- **GH #137** has ADR text landed in PR #160, but the issue itself is still open.
- **Lane C mobile chain DES-81→82→83→84** — all four DONE (EN-M1 / HU-M1 / HU-M3 / HU-M2 shipped).

---

## The full queue per lane (finish one → start the next)

### Lane A — `session-operations-service` (the big backend line)
Run one at a time, in this order:

```
DES-32  →  [MassTransit: GH #164 → GH #165 → GH #166]  →  DES-29
        →  DES-87  →  DES-42  →  DES-39  →  DES-40  →  DES-41  →  DES-43
        →  DES-36  →  DES-38  →  DES-37  →  DES-92
        →  DES-51  →  DES-53  →  DES-54  →  DES-50  →  DES-48
        →  DES-34  →  DES-35  →  DES-33  →  DES-61
        →  DES-56  →  DES-57  →  DES-60  →  DES-13  →  DES-59
```
- **Just landed:** DES-49 is `Done` (2026-07-11) and DES-31 is `Done` (2026-07-12, PR #178).
- **Open heads now:** DES-32 is `In Progress` (started 2026-07-12 on Linear); DES-29 is still `Todo` and ungated; DES-53 is now **blocked by DES-51** (it consumes/extends the `ScoreEntry` ledger), so it lands after DES-51 in the queue above; DES-87 is also open.
- DES-42 / DES-39 / DES-36 / DES-38 are now unblocked by Linear state because DES-31 is done, but this lane keeps the stricter planned order above.
- **GH #145** and **GH #154** also edit this service → run them *inside this lane*, not in parallel.
- 🔗 **MassTransit sub-track (GitHub-only): GH #164 → GH #165 → GH #166** also lives in `session-operations-service`. It has **no Linear DES id** (its tickets DES-88/89/90 were canceled and replaced by this GH chain). It **collides with this lane** — serialize it *inside* Lane A, never parallel to it.
- 🆕 **DES-92** ("ENABLER — Publicar ClueReleased a RabbitMQ (MassTransit)") is gated on **GH #164** (MassTransit bus) **plus** the clue-release flows DES-36/38/37; it publishes the `ClueReleased` events those emit and **blocks DES-56 (HU-40A), its only consumer** — hence its slot after DES-37 and before DES-56.
- ⚠️ **DES-13** secretly also touches `identity-access-service` → **don't run it while Lane B is active.**
- 🆕 **DES-93** (HU-23 treasure-hunt substage timer, ungated) touches **both** this service (Phase 2: `SubstageSnapshot` relay + `LiveSession` timer) **and** `mission-design-service` (Phase 1: `Substage.MaximumTime` + migration + authoring). One two-service ticket on a `des-93` branch — **don't run it while Lane E is active**, and don't split it across parallel lanes. See the cross-lane table.

### Lane B — `identity-access-service` (Keycloak / login)
```
~~GH #141~~ ✅  →  GH #142  →  GH #143
```
- **GH #140** is closed, and **GH #141** (SMTP + email verification) is now closed too — the live backend identity chain opens on **GH #142** (admin-initiated invitations).
- **GH #137** has PR #160 merged, but the issue is still open; treat the ADR text as landed and the issue as bookkeeping still to close.
- ❌ **Not in this lane:** GH #148 (that's frontend → Lane D), GH #144 (see "solo" below).

### Lane C — `mobile/`
```
GH #171  ;  GH #156
```
- ✅ **DES-81 → DES-82 → DES-83 → DES-84 all Done** (EN-M1 / HU-M1 / HU-M3 / HU-M2 shipped) — the mobile trivia chain is finished.
- ✅ **GH #155** is closed (2026-07-12).
- **GH #171** (mixed-play-mode substage progress in team space) is ungated, mobile-only, and startable now; the runtime snapshot already carries ordered substage metadata, so no backend service is touched.
- **GH #156** (real map view on the treasure-hunt play surface) is still blocked by **GH #154** (backend target coordinates — `mission-design` + `session-operations`, run inside Lane A).
- **DES-58** (React Native enabler) is still **Backlog** — pull it into this lane when it's ready.

### Lane D — `frontend/` (web)
```
GH #177  →  GH #148
```
- ✅ **GH #146** (quiz preview) is Done. **GH #173** (trivia RBAC) is also Done.
- **GH #177** removes the sequence-order field from trivia question authoring across `frontend/` and `mission-design-service` — same cross-lane shape as `#173` was; serialize it with Lane E.
- **GH #148** (operator invite UI) is **still blocked** — it needs the backend invitation endpoints from **GH #142** (Lane B) to land first.

### Lane E — `mission-design-service`
```
GH #177
```
- ✅ **DES-80** is done (2026-07-12). **GH #173** (trivia RBAC) is also done.
- **GH #177** is now the lane head. It edits `frontend/` too (removes the sequence-order input and sort logic from the trivia question create/edit form), so serialize it with Lane D.
- 🆕 **DES-93 Phase 1** (`Substage.MaximumTime` field + EF migration + authoring command, for `TreasureHunt` substages) starts here; its Phase 2 lands in Lane A (`session-operations`). Ungated two-service ticket — **don't run it while Lane A is active.** See the cross-lane table.

### Lane F — `api-gateway`
```
(empty)
```
- ✅ **GH #147** (missing gateway exception handler) is Done (PR #161) — this lane is currently empty.

---

## Can't parallelize — run these ALONE in a quiet window

| Ticket | Why it can't share |
|--------|--------------------|
| ~~**GH #149**~~ | ✅ Done (PR #159) — repo-wide coverage gate already landed; no longer blocks fan-out. |
| ~~**GH #173**~~ | ✅ Done — trivia question authoring RBAC landed. Was cross-lane D+E; no longer conflicts. |
| **GH #144** | Spans identity **+** frontend **+** mobile — conflicts with B, C and D at once. |
| **GH #177** | Spans `mission-design-service` **+** `frontend/` — removes trivia-question sequence-order end-to-end; serialize with D and E. |
| **DES-13** | Touches session-ops **+** identity — fine in Lane A, but not while Lane B runs. |
| **DES-93** | Spans `mission-design-service` (Phase 1) **+** `session-operations-service` (Phase 2) — the HU-23 treasure-hunt timer. One ticket; conflicts with Lanes A and E, so don't run those in parallel while it's live. |

---

## Quick conflict cheatsheet

- ✅ **Safe in parallel:** A + B + C + D + E (F is empty; five distinct folders), except for cross-lane tickets called out below.
- ⚠️ **A + B together?** Fine — *unless* Lane A is on **DES-13** (it touches identity too).
- ⚠️ **Inside Lane A:** the MassTransit chain GH #164→#165→#166 and DES-92 share `session-operations-service` with the DES-* queue — serialize, never parallel.
- ⚠️ **D + E together?** Fine unless either lane is on **GH #177**, which touches both `frontend/` and `mission-design-service`.
- ⚠️ **A + E together?** Fine unless either lane is on **DES-93**, which spans both `session-operations-service` and `mission-design-service`.
- ❌ **Never parallel:** GH #144 (alone).
- 📁 **The test for any pair:** do they edit the same folder under `backend/services/…`, `frontend/`, or `mobile/`? If no → safe. If yes → serialize.

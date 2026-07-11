# Parallel Work Lanes — one agent per service, no conflicts

**Rule:** a conflict = two agents editing the same folder at the same time.
So each lane below is **one service / codebase**. Run **one ticket at a time inside a
lane**, and run **different lanes in parallel**.

> Verified against live Linear (`umbral-equipo-12`) on 2026-07-10 — `svc:` labels + `blockedBy` edges.
> `GH #NNN` = GitHub issue (no Linear ticket). `DES-NN` = Linear HU.

---

## ✅ Start these 4 RIGHT NOW (one per service, provably no overlap)

| Lane | Service / tree | Run this | HU / what it is |
|------|----------------|----------|-----------------|
| **A** | `session-operations-service` | **DES-49** | HU-36A — operator sees answered / not-answered |
| **B** | `identity-access-service` | **GH #137** | ADR: Keycloak account-creation flow |
| **C** | `mobile/` | **DES-81** | EN-M1 — mobile trivia contract spike |
| **D** | `frontend/` | **GH #146** | Quiz-question preview in the editor |

Want more than 4 at once? Add these two — they're also free, separate trees:

| Lane | Service / tree | Run this | HU / what it is |
|------|----------------|----------|-----------------|
| **E** | `mission-design-service` | **DES-80** | RemoveTriviaQuestion command |
| **F** | `api-gateway` | **GH #147** | Add missing gateway exception handler |

### ⚠️ Do this ONE alone, first
- **GH #149** (branch-coverage gate) — edits `cover-gate.sh` + ADRs + docs **repo-wide**, so it
  collides with every lane. Land it before you fan out (or you'll retrofit tests later).

---

## The full queue per lane (finish one → start the next)

### Lane A — `session-operations-service` (the big backend line)
Run one at a time, in this order:

```
DES-49  →  DES-31  →  DES-32  →  DES-53  →  DES-29
        →  DES-87  →  DES-42  →  DES-39  →  DES-40  →  DES-41  →  DES-43
        →  DES-36  →  DES-38  →  DES-37
        →  DES-51  →  DES-54  →  DES-50  →  DES-48
        →  DES-34  →  DES-35  →  DES-33  →  DES-61
        →  DES-56  →  DES-57  →  DES-60  →  DES-13  →  DES-59
```
- **Startable today:** DES-49, DES-31 (unlocks 8 downstream tickets), plus DES-32 / DES-53 / DES-29 (ungated).
- Everything after DES-29 is currently **blocked** — it opens up as the ones above land.
- **GH #145** and **GH #154** also edit this service → run them *inside this lane*, not in parallel.
- ⚠️ **DES-13** secretly also touches `identity-access-service` → **don't run it while Lane B is active.**

### Lane B — `identity-access-service` (Keycloak / login)
```
GH #137  →  GH #140  →  GH #141  →  GH #142  →  GH #143
```
- **GH #140 ∥ GH #141** can actually go together (both only need #137 first).
- **GH #140 is a security fix** (shared admin credential) — pull it early.
- ❌ **Not in this lane:** GH #148 (that's frontend → Lane D), GH #144 (see "solo" below).

### Lane C — `mobile/`
```
DES-81  →  DES-82  →  DES-83  →  DES-84  →  GH #155  →  GH #156
```
- **DES-84** needs **DES-82** first. It does **not** need GH #143 — the seeded `participant` account is enough.
- **GH #155 / GH #156** are mobile screens (play surface + map) → they belong here, not in "frontend".

### Lane D — `frontend/` (web)
```
GH #146  →  GH #148
```
- **GH #148** (operator invite UI) needs the backend invitation endpoints from **GH #142** to land first.

### Lane E — `mission-design-service`
```
DES-80
```
- Ungated, own tree, zero blockers. A clean free lane any time.

### Lane F — `api-gateway`
```
GH #147
```
- Ungated, own tree. Not affected by GH #149.

---

## Can't parallelize — run these ALONE in a quiet window

| Ticket | Why it can't share |
|--------|--------------------|
| **GH #149** | Repo-wide (coverage gate + ADRs + docs) — collides with every lane. **Run first.** |
| **GH #144** | Spans identity **+** frontend **+** mobile — conflicts with B, C and D at once. |
| **DES-13** | Touches session-ops **+** identity — fine in Lane A, but not while Lane B runs. |

---

## Quick conflict cheatsheet

- ✅ **Safe in parallel:** A + B + C + D + E + F (six different folders).
- ⚠️ **A + B together?** Fine — *unless* Lane A is on **DES-13** (it touches identity too).
- ❌ **Never parallel:** GH #149 (alone, first) · GH #144 (alone).
- 📁 **The test for any pair:** do they edit the same folder under `backend/services/…`, `frontend/`, or `mobile/`? If no → safe. If yes → serialize.

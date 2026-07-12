# Parallel Work Lanes — one agent per service, no conflicts

**Rule:** a conflict = two agents editing the same folder at the same time.
So each lane below is **one service / codebase**. Run **one ticket at a time inside a
lane**, and run **different lanes in parallel**.

> Verified against live Linear (`umbral-equipo-12`, 83 tickets) + GitHub on 2026-07-11 — `svc:` labels + `blockedBy` edges.
> `GH #NNN` = GitHub issue (no Linear ticket). `DES-NN` = Linear HU.

---

## ✅ Start these RIGHT NOW (one per service, provably no overlap)

| Lane | Service / tree | Run this | HU / what it is |
|------|----------------|----------|-----------------|
| **A** | `session-operations-service` | *(occupied)* | DES-49 (HU-36A) is **In Progress** — finish it before starting DES-31 |
| **B** | `identity-access-service` | **GH #140** | Shared-admin-credential security fix (∥ GH #141) |
| **C** | `mobile/` | **GH #155** | Mobile play surface screen |
| **E** | `mission-design-service` | **DES-80 → GH #173** | RemoveTriviaQuestion, then trivia-question authoring RBAC sweep (GH #173 also touches `frontend/`) |

- Lane **D** (`frontend/`) has **GH #173** after `DES-80`; **GH #148** remains blocked by GH #142 (Lane B).
- Lane **F** (`api-gateway`) is **empty** — GH #147 shipped.

### ✅ Already landed since the 07-10 plan (no longer startable)
- **GH #149** (repo-wide branch-coverage gate) — done (PR #159), it no longer needs to run first.
- **GH #137** (Keycloak ADR, PR #160), **GH #146** (quiz preview), **GH #147** (gateway handler, PR #161).
- **Lane C mobile chain DES-81→82→83→84** — all four DONE (EN-M1 / HU-M1 / HU-M3 / HU-M2 shipped).

---

## The full queue per lane (finish one → start the next)

### Lane A — `session-operations-service` (the big backend line)
Run one at a time, in this order:

```
DES-49  →  DES-31  →  DES-32  →  DES-53  →  DES-29
        →  DES-87  →  DES-42  →  DES-39  →  DES-40  →  DES-41  →  DES-43
        →  DES-36  →  DES-38  →  DES-37  →  DES-92
        →  DES-51  →  DES-54  →  DES-50  →  DES-48
        →  DES-34  →  DES-35  →  DES-33  →  DES-61
        →  DES-56  →  DES-57  →  DES-60  →  DES-13  →  DES-59
```
- **In progress now:** DES-49 (started, not merged). DES-31 (unlocks 8 downstream) + DES-32 / DES-53 / DES-29 are ungated and open up once DES-49 lands.
- Everything after DES-29 is currently **blocked** — it opens up as the ones above land.
- **GH #145** and **GH #154** also edit this service → run them *inside this lane*, not in parallel.
- 🔗 **MassTransit sub-track (GitHub-only): GH #164 → GH #165 → GH #166** also lives in `session-operations-service`. It has **no Linear DES id** (its tickets DES-88/89/90 were canceled and replaced by this GH chain). It **collides with this lane** — serialize it *inside* Lane A, never parallel to it.
- 🆕 **DES-92** ("ENABLER — Publicar ClueReleased a RabbitMQ (MassTransit)") is gated on **GH #164** (MassTransit bus) **plus** the clue-release flows DES-36/38/37; it publishes the `ClueReleased` events those emit and **blocks DES-56 (HU-40A), its only consumer** — hence its slot after DES-37 and before DES-56.
- ⚠️ **DES-13** secretly also touches `identity-access-service` → **don't run it while Lane B is active.**

### Lane B — `identity-access-service` (Keycloak / login)
```
GH #140  →  GH #141  →  GH #142  →  GH #143
```
- ✅ **GH #137** (Keycloak account-flow ADR) is Done (PR #160) — the chain now opens on GH #140.
- **GH #140 ∥ GH #141** can actually go together (both only needed #137 first, now landed).
- **GH #140 is a security fix** (shared admin credential) — pull it early.
- ❌ **Not in this lane:** GH #148 (that's frontend → Lane D), GH #144 (see "solo" below).

### Lane C — `mobile/`
```
GH #155  →  GH #156
```
- ✅ **DES-81 → DES-82 → DES-83 → DES-84 all Done** (EN-M1 / HU-M1 / HU-M3 / HU-M2 shipped) — the mobile trivia chain is finished.
- Live queue is just **GH #155 → GH #156** (mobile play surface + map screens) → they belong here, not in "frontend".
- **DES-58** (React Native enabler) is still **Backlog** — pull it into this lane when it's ready.

### Lane D — `frontend/` (web)
```
GH #173  →  GH #148
```
- ✅ **GH #146** (quiz preview) is Done.
- **GH #173** changes trivia question authoring RBAC in the web dashboard/nav and server actions.
  It also touches `mission-design-service`, so run it after Lane E's `DES-80` and do not run it in parallel with Lane E.
- **GH #148** (operator invite UI) is **still blocked** — it needs the backend invitation endpoints from **GH #142** (Lane B) to land first.

### Lane E — `mission-design-service`
```
DES-80  →  GH #173
```
- **DES-80** is ungated, own tree, zero blockers. A clean free lane any time.
- **GH #173** follows `DES-80` so add/update/remove trivia-question authoring can move from Administrator-only to Operator-only together. It also edits `frontend/`, so serialize it with Lane D.

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
| **GH #144** | Spans identity **+** frontend **+** mobile — conflicts with B, C and D at once. |
| **GH #173** | Spans `mission-design-service` **+** `frontend/` — run after DES-80 and do not share with D or E. |
| **DES-13** | Touches session-ops **+** identity — fine in Lane A, but not while Lane B runs. |

---

## Quick conflict cheatsheet

- ✅ **Safe in parallel:** A + B + C + D + E (F is empty; five distinct folders), except for cross-lane tickets called out below.
- ⚠️ **A + B together?** Fine — *unless* Lane A is on **DES-13** (it touches identity too).
- ⚠️ **Inside Lane A:** the MassTransit chain GH #164→#165→#166 and DES-92 share `session-operations-service` with the DES-* queue — serialize, never parallel.
- ⚠️ **D + E together?** Fine unless either lane is on **GH #173**, which touches both `frontend/` and `mission-design-service`.
- ❌ **Never parallel:** GH #144 (alone).
- 📁 **The test for any pair:** do they edit the same folder under `backend/services/…`, `frontend/`, or `mobile/`? If no → safe. If yes → serialize.

# Trivia Sprint — Build Order & Priority

> Source: [`trivia-7-requirements-coverage.md`](./trivia-7-requirements-coverage.md) +
> [`../backend/docs/delegation_hus_order_sprint1.md`](../backend/docs/delegation_hus_order_sprint1.md) +
> [`../mobile/plans/post-hu-34a-mobile-trivia-breakdown.md`](../mobile/plans/post-hu-34a-mobile-trivia-breakdown.md).
> Date: 2026-06-04.

## Suggested build order (top → bottom)

| Step | Build this | Closes requirement | Depends on | Cut if no time? |
|------|-----------|--------------------|-----------|-----------------|
| ✅ done | HU-19 operator panel | 1, 3 | — | — |
| **1** | **HU-21A** — session state (Scheduled→Active→Finished) | 2 | — | ❌ never |
| **2** | **HU-22** — timer ticks + state broadcast *(+ wire web panel to real hub)* | 2, 6 | 21A | ❌ never |
| **3** | **HU-33A (thin)** — activate question + auto-close | 4, 5 | 22 | ❌ never |
| **4** | **HU-34A (+34B)** — first answer per team → publish to RabbitMQ | 7 | 33A | ❌ never (keystone) |
| **5** | **HU-37A** — consume answer → score ledger | 7 | 34A | ❌ never |
| **6** | **HU-36A** — operator answered/not-answered dashboard | 4 | 34A | ⚠️ trim to minimal |
| **7** | **EN-M1 → HU-M1 → HU-M2 → HU-M3** — mobile gameplay | 5 | 34A | ⚠️ M2/M3 droppable |
| ⏸️ later | HU-M4 / HU-M5 (reveal, ranking) | extras | 35/37B | ✂️ already deferred |

**Read it like this:**

- Steps **1→5 are one straight line you cannot break** — that's the demo that proves all the hard gates (real-time + RabbitMQ).
- Steps **6 and 7 fan out after step 4 lands** — give 6 (and ideally 5) to Salomon, you take 7.
- After step 7, **all 7 requirements are demoed**.
- If the clock beats you: **protect 1→5, shave 6, then 7. Never the spine.**

## The 7 requirements

> From the coverage table in `trivia-7-requirements-coverage.md:16-24`.

| # | Requirement | In Trivia form, this means… | Status after this plan |
|---|-------------|------------------------------|------------------------|
| 1 | Mission Management | Authoring quizzes (questions/options) | ✅ already done (HU-11/12/13/14) |
| 2 | Live Session Management | Create a session + drive it through states + run the timer | ✅ steps 1–2 (21A, 22) |
| 3 | Participating team management | Teams joined to a session + operator assigned | ✅ already done (HU-18, HU-19) |
| 4 | Operator dashboard | Operator sees live who answered / who hasn't | ✅ step 6 (36A) |
| 5 | Team dashboard | Player sees the question, timer, score, and can submit | ✅ step 7 (mobile M1–M3) |
| 6 | Real-time monitoring | Live updates over SignalR (ticks, state, events) — no polling | ✅ steps 2–7 |
| 7 | Asynchronous processing | Answer flows through RabbitMQ: publish → consume → score | ✅ steps 4–5 (34A → 37A) |

## How the build steps map back to requirements

| Build step | Lights up requirement(s) |
|------------|--------------------------|
| ✅ HU-19 (done) | 1, 3 |
| 1 — HU-21A | 2 |
| 2 — HU-22 | 2, 6 |
| 3 — HU-33A | (sets up 4, 5) |
| 4 — HU-34A | 7 |
| 5 — HU-37A | 7 |
| 6 — HU-36A | 4 |
| 7 — Mobile M1–M3 | 5 |

Requirements **1 and 3 are already satisfied today**. Everything from HU-21A onward exists to close
**2, 6, 7, 4, 5** — in that order. Finishing step 7 means all 7 are demoable.

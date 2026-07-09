# Handoff — HU-33B (DES-45) generation + scope findings (2026-07-08)

**Session outcome:** ran `generator-agent` for HU-33B DES-45. Three files generated, **uncommitted**, awaiting **Stop 1** human review. No implementation started. This note carries the scope findings a reviewer/driver needs.

## What was generated (in `backend/docs/`, on `develop`)

- `hu33b-context.md` — per-phase implementation spec (the subagent's source)
- `prompt_example_feature_hu33b.md` — full per-phase prompt sequence + Stop-1 guard + rationale
- `hu33b-brief.md` — the driver's resident brief

Slice: **HU-33B / DES-45**, `session-operations-service`, branch `feature/hu-33b-trivia-round-close-results-publication`, base **`develop`**, PRD **DES-70**.

## Scope thesis (one line)

HU-33B is the **async-publication sibling** of the already-**Done** HU-33A (DES-78). HU-33A landed the trivia runtime (question close, substage advancement, `SessionCompletion → Finished`) + SignalR broadcasts. HU-33B adds the one thing HU-33A deferred: **publish the round-close + final-results facts to RabbitMQ after transactional success** (AC #6), for async history/consolidation, without the runtime depending on the broker. It is the **first RabbitMQ publisher in the backend** (broker already runs in `docker-compose.yml:47`; no `.cs` publishes to it yet). It computes **no** puntaje/ranking.

## Three things worth a reviewer's eyes at Stop 1

1. **AC #1 drift (decision D-1).** The ticket says session-ops "calcula puntaje y ranking," but PRD DES-70 `:271` puts *"Score calculation, ranking ownership, penalties, and derived projections"* **out of scope** for this service, and ADR-0005 makes the winner **emitted, not computed**. HU-33B was scoped to **publish the facts**; `ScoringMonitoring` (HU-37A/37B/39B) computes. This is the biggest reinterpretation — grounded in canon, but flagged. (Now doubly-sourced — see ranking-ownership section below.)

2. **Predecessor substitution.** DES-45's Linear `blockedBy` still names the **Canceled DES-44**; substituted its rebuild successor **DES-78 (HU-33A, Done)** per the realignment map's supersession rule (`canon-realignment-after-mission-runtime-rewrite.md:69,146`). The other blocker, **DES-51 (HU-37A)**, is **Todo** and in a **different service** (`scoring-monitoring-service`) — it is the downstream **consumer** of HU-33B's events, **not** a predecessor to read. Unlike HU-33A's D-1 (which deferred RabbitMQ *because* no consumer existed), HU-33B's AC #6 **mandates** the publish, so the producer lands here regardless of HU-37A.

3. **Strategy mandate is reused, not newly realized (ADR-0012 genuine-vs-ceremony).** The trivia matrix mandates Facade+State+**Strategy** for HU-33B (`trivia_sprint_required_patterns_matrix.md:75`). The "score/ranking variation" Strategy it cites lives **downstream** in `ScoringMonitoring`; manufacturing a score Strategy in session-ops would be ceremony **and** cross the Runtime-Authority boundary. Carried as a **reused-seam gate** (the existing `IQuestionActivationStrategy` close-vs-advance decision), stated explicitly. Facade (X.2) and State (X.1) are genuinely re-exercised.

The four committed decisions in full (D-1 no score/ranking · D-2 reuse existing domain events, add none · D-3 best-effort post-commit publish, runtime independent of RabbitMQ · D-4 no new REST endpoint / no frontend) live in the generated files.

## Where does ranking (and scoring) belong? — `scoring-monitoring-service`, with reasoning

**Answer: the `ScoringMonitoring` bounded context (`scoring-monitoring-service`) owns scoring *and* ranking — not `session-operations`.** Verified against the authoritative ownership docs (the frontend `condensed_roadmap_umbral.md` is **not** the owner for this — its own line 22 delegates bounded-context/aggregate ownership to `ddd_solution_model.md` + the service `CONTEXT.md`s).

Evidence:
- **`services/scoring-monitoring-service/CONTEXT.md`** — *"realizes the `ScoringMonitoring` bounded context. It owns scoring facts, penalty traceability, **ranking derivation**, audit history, and monitoring-oriented read models."* Defines `ScoreEntry`, `ScoreValue`, `Ranking`, `ResolutionTime` here.
  - Boundary rule **Derived Views**: *"`Ranking`, `AuditHistory`, and monitoring views are derived models owned by `ScoringMonitoring`; they **do not replace runtime authority in `SessionOperations`**."*
  - Its required **`Strategy`** pattern = *"Score calculation and difficulty-based, mode-specific normalization or evaluation policies"* — i.e. the very "score/ranking variation" Strategy the trivia matrix cites for HU-33/HU-37 lives **here**, confirming finding #3.
- **`ddd_solution_model.md:177`** — *"`ScoringMonitoring` | `scoring-monitoring-service` | Owns scoring, ranking, and monitoring views"*; commands `RecordScoreEntry`/`RecalculateRanking`, domain events `ScoreEntryRecorded`/`RankingRecalculated`, `RankingPolicy` (the Strategy), `IRankingReadModelRepository`.
- **`session-operations-service/CONTEXT.md:195-197` (Runtime Authority)** — session-ops owns live progression/participation/evidence; *"Other services may provide … **derived scoring views**, but they do not control runtime state transitions here."* `SessionRanking`/`SessionTeamWinner` appear in session-ops' glossary only as **terms it emits facts toward**, not values it computes (ADR-0005: winner **emitted, not computed**).
- **`scoring-monitoring-service/src` has 0 `.cs` files** — an empty skeleton. HU-37A/37B/39B are unbuilt (consistent with DES-51 = Todo). So there is currently **no** service computing ranking; HU-33B must **not** fill that gap in the wrong service.

**Why the split (the reasoning, for defense):**
- **Bounded-context ownership** — scoring/ranking is a *supporting* context (`ddd_solution_model.md:66`) with its own persistence and `ScoreEntry` ledger; folding computation into session-ops collapses the boundary the microservice decomposition exists to preserve.
- **Ledger-derived, not mutated** — canon (`condensed_roadmap_umbral.md:103`, realignment `:38-39`) requires ranking to be **derived from immutable `ScoreEntry` records** (high→low, `ResolutionTime` tie-break), never mutated total state. That derivation is `ScoringMonitoring`'s `RankingPolicy`.
- **Runtime authority vs derived views** — session-ops decides *when* a question closes / the session finishes (runtime facts); scoring derives score/ranking from those facts asynchronously. HU-33B is exactly that seam: it **emits** `QuestionClosed` + `SessionResultsFinalized` to RabbitMQ; `ScoringMonitoring` (HU-37A ledger, HU-37B/39B ranking) **consumes and computes**.
- **Roadmap phases agree** — the roadmap even separates **Phase 5 Trivia Flow** from **Phase 6 Scoring and Monitoring**; §3.4's "the system computes score and ranking" is a whole-platform behavioral line, not a service-ownership assignment.

**Nuance to keep straight:** there are *two* ranking-ish terms in `session-operations/CONTEXT.md` (`SessionRanking`, `SessionTeamWinner`, `SessionCompletion` "followed by calculation of the `SessionTeamWinner`"). These describe the **session outcome concept**; the actual calculation is `ScoringMonitoring`'s per the Derived-Views boundary + ADR-0005 (emitted-not-computed). If a future ticket tries to compute `SessionTeamWinner`/`SessionRanking` inside session-ops, that's a boundary violation — route it through the scoring context.

## Open follow-ups (not blocking HU-33B)

- **Optional Stop-1 refinement:** strengthen D-1's citations in the three generated files to add `ddd_solution_model.md:177` + scoring-monitoring `CONTEXT.md` (Derived Views) alongside the existing PRD `:271` + ADR-0005. Surgical edit only; offered, not yet applied.
- **HU-37A (DES-51)** is the consumer of HU-33B's RabbitMQ contract (exchange `umbral.session-operations`, routing keys `session.question.closed` / `session.results.finalized`). It is unbuilt and in an empty service — whoever picks it up bootstraps `scoring-monitoring-service` + the RabbitMQ **consumer** + the `ScoreEntry` ledger/`RankingPolicy` Strategy.

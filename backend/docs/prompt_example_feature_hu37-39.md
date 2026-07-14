# Prompt Example — HU-37 + HU-39 Ledger de puntaje y ranking en tiempo real (Feature Slice)

Concrete prompt sequence for driving the merged **DES-99** slice (HU-37 score
ledger + HU-39 real-time ranking) through a full feature slice on
`feature/hu-37-39-ledger-ranking`. Follows the pattern in
[workflow_for_prompts.md](./workflow_for_prompts.md).

**Key difference for this slice:** `scoring-monitoring-service` is **greenfield**
and DES-99 is **one merged workstream** — HU-37's append-only `ScoreEntry` ledger
and HU-39's derived session `Ranking` land together because the ranking is derived
directly from the ledger and both share the same score-event flow. Each backend
phase is **ledger-first, then ranking-derived-from-ledger**. This service
**consumes** runtime facts (trivia answers) and **derives** views; it never owns
session progression, admission, clue release, or evidence acceptance.

When working from the monorepo root, make the target workload explicit in each
prompt. For backend steps, point to `@backend/.agents/backend-agent.md`. For
frontend steps, point to `@frontend/AGENTS.md`. Do not ask for backend and
frontend implementation in the same phase prompt; coordinate them as separate
scoped steps tied together by the verified API contract.

---

## Stop 1 acceptance guard

Reject this generated prompt before implementation if it does not explicitly scope
all of the following:

- `ScoreEntry` is an **append-only** ledger; the team total is **derived** from
  entries, never a mutable field
- each entry records `sourceEntityType` + `sourceEntityId` (origin traceability)
- score awarding is an `IScorePolicy` (**Strategy**), not handler branching
- the ledger is fed by **consuming** `AnswerRegisteredIntegrationEvent` bound to
  the per-type exchange **`session-answer-registered`** (not the routing key), and
  is **idempotent** on the source id
- `ScoreEntryRegistered` is published **post-commit**; the record flow does **not**
  depend on RabbitMQ
- `Ranking` is derived, **one per `LiveSession`**, via `IRankingPolicy`
  (**Strategy**): descending total, `ResolutionTime` tie-break, equal /
  non-comparable share rank
- the ranking is **refreshed by consuming `ScoreEntryRegistered`** (async from the
  record flow) and **pushed over SignalR** when it changes
- **no** `Penalty` command / entity (HU-38), **no** audit/monitoring projections
  (HU-40), **no** mutable session total, **no** ranking computed in
  session-operations

---

## Required design patterns

- **`Strategy`** (mandated for **both** HU-37 and HU-39 —
  `required_patterns_matrix.md`; service `CONTEXT.md`; PRD Implementation Decisions)
  - Why: score ledger entries come from interchangeable scoring policies
    (`ScorePolicy`); real-time ranking depends on score-policy outcomes and
    tie-breaking (`RankingPolicy`, `ResolutionTime`). Difficulty weighting,
    normalization, and tie-break logic must not spread across handlers.
  - Phase owner: `IScorePolicy` + `IRankingPolicy` defined in **X.1 Domain**,
    consumed in **X.2 Application** (`RecordScoreEntry` / `RecalculateRanking`).
  - Gate obligation: each policy is an **interface + a single `sealed` impl in
    `Domain/Services/`**, injected and selected at runtime — no scoring/tie-break
    `if`/`switch` in handlers, consumers, or the projection. Single impl, no
    selector until a second variant lands.
- **`Proxy`: not mandated here.** The matrix assigns `Proxy` to HU-38/HU-40, not
  HU-37/HU-39. Ranking reads inherit the standard gateway + `AuthorizationBehaviour`
  guard — **no new pattern gate**.
- **Transport gates:** HU-37 → **RabbitMQ/MassTransit** (consume
  `session-answer-registered`, publish `scoring-score-entry-registered`); HU-39 →
  **SignalR** (push the refreshed ranking; refresh by consuming
  `scoring-score-entry-registered`).

---

## Pre-resolved orient (as of 2026-07-14)

> Step 1 has already been run. Paste this section into any agent session that needs
> context before picking up a phase — no need to re-run the orient prompt.

### What predecessors have already landed

`scoring-monitoring-service` is **greenfield** — only the target-folder scaffold
(`README.md`, `CONTEXT.md`, `structure.md`, `src/{Domain,Application,Infrastructure,
Api}` gitkeeps, `Application/{Scores,Metrics,Alerts,Common}`, `Directory.*.props`,
empty test projects). **No** same-service predecessor is Done or In Progress, so the
branch base is `develop`. The build-on surface is entirely cross-service:

- **HU-34 / DES-46 (session-operations)** publishes the consumed
  `AnswerRegisteredIntegrationEvent` `(LiveSessionId, TeamId,
  TriviaAnswerSubmissionId, TriviaSubstageSnapshotId, QuestionSequenceOrder,
  SelectedOptionSequenceOrder, IsCorrect, ScoreValue, SubmittedAt)` — the concrete
  first ledger input pinned by the PRD (correct answer → `ScoreEntry` grant).
- **MassTransit migration #164/#165/#166** establishes the conventions (per-type
  `[EntityName]` exchange, `RabbitMqOptions`, `ConfigureEndpoints`, post-commit
  publish with a bounded timeout). **#166 removed the topic exchange**, so bind the
  per-type exchange **`session-answer-registered`**.
- **HU-33B / DES-45** publishes `QuestionClosedIntegrationEvent` /
  `SessionResultsFinalizedIntegrationEvent` — a *later* ranking-refresh input, not
  the first path.
- **HU-31 / DES-42 (QR)** landed `TargetResolution` server-side but **no
  `TargetResolved` integration event is published yet** — the QR ledger path is
  **blocked upstream** (see Rationale).

### What this slice adds (per DES-99 + PRD DES-85)

| Concern | New work |
|---|---|
| Ledger (HU-37) | Append-only `ScoreEntry` aggregate; team total derived by folding entries. |
| Traceability (HU-37) | `sourceEntityType` (`TargetResolution`\|`TriviaAnswerSubmission`\|`Penalty`) + `sourceEntityId`. |
| Score policy (HU-37) | `IScorePolicy` (**Strategy**) awards `ScoreValue`; no handler branching. |
| Consume (HU-37) | `IConsumer<AnswerRegisteredIntegrationEvent>` bound to `session-answer-registered`; **idempotent**; correct answer → one grant. |
| Publish (HU-37) | Post-commit `ScoreEntryRegistered` (`scoring-score-entry-registered`); record flow independent of RabbitMQ. |
| Ranking (HU-39) | `Ranking` derived read-model, one per `LiveSession`, via `IRankingPolicy` (**Strategy**). |
| Order + tie-break (HU-39) | Descending total; lower comparable `ResolutionTime` first; equal/non-comparable share rank. |
| Refresh (HU-39) | Consume `ScoreEntryRegistered` (async) → recompute → `RankingRefreshed`. |
| Real-time (HU-39) | SignalR push of the new snapshot to the session's participants + operators. |
| Read (HU-39) | `GET /api/sessions/{liveSessionId}/ranking` (ordered snapshot). |

**Deferred:** `Penalty`/`ApplyPenalty`/`PenaltyPolicy`/`Proxy` → HU-38;
`AuditHistory`/monitoring/score-history → HU-40; QR ledger path → blocked on
upstream `TargetResolved`.

### Branch state and prerequisite

`feature/hu-37-39-ledger-ranking` branches from `develop`. No same-service
predecessor is In Progress. Everything under `scoring-monitoring-service/src` is
first-delivery greenfield — do not look for existing scoring code to extend.

### Linear state (as of 2026-07-14)

- **DES-99 (HU-37 + HU-39):** **Todo**, labels: `backend-only`, `canon-realign`,
  `svc:scoring-monitoring-service`, `Feature`, `ready-for-agent`. Absorbs the
  **cancelled** DES-51 (HU-37) and DES-54 (HU-39).
- **DES-85 (PRD):** local file authoritative.
- **Blocked-by (satisfied for the concrete path):** DES-46 (HU-34) provides the
  consumed answer event; DES-45 (HU-33B) and DES-42 (HU-31) named but not on the
  first path.

> Linear live state may have changed. Use the Linear MCP to verify DES-99 status
> and labels if needed, but do not re-fetch PRD scope — read the local file at
> `@backend/docs/prd/DES-85-primera-implementacion-de-scoring-monitoring-service-hu-37-a-hu-40.md`.
> `canon-realign` on DES-99 reflects the 2026-07-14 merge reword (greenfield), not
> an existing-code realignment; there is no `⚠️ canon` comment, so the body AC is
> authoritative.

---

## 1. Orient — read service state, PRD, and consumed contract

> **Skip this step if you have read the pre-resolved orient section above.** Run it
> only if the service source, README, or Linear state may have changed since
> 2026-07-14.

```text
Read the following files and summarise what has been decided and implemented so far:
- @backend/services/scoring-monitoring-service/README.md - current service status
- @backend/services/scoring-monitoring-service/CONTEXT.md - bounded-context language and Strategy expectation
- @backend/services/scoring-monitoring-service/structure.md - scaffold state
- @backend/docs/prd/DES-85-primera-implementacion-de-scoring-monitoring-service-hu-37-a-hu-40.md - service PRD (authoritative)
- @backend/services/session-operations-service/src/Application/Sessions/Common/AnswerRegisteredIntegrationEvent.cs - the consumed contract
- @backend/services/session-operations-service/src/Infrastructure/Messaging/MassTransitMessagingRegistration.cs - MassTransit wiring convention
- @backend/docs/hu37-39-context.md - the pre-resolved merged context

Then use the Linear MCP to fetch only the current live state of:
- DES-99 (HU-37 + HU-39) - status and labels
- DES-85 (PRD) - status and labels

Output:
- confirmation the service is greenfield (no scoring code to extend)
- the concrete first path: consume session-answer-registered -> record an append-only ScoreEntry grant -> publish scoring-score-entry-registered -> consume it to refresh the session Ranking -> push over SignalR
- confirmation there is no Penalty command, no audit/monitoring projection, no mutable total, and no ranking computed in session-operations in this slice
- current Linear status and labels for DES-99

Do not start planning or implementing yet.
```

---

## 2. Label DES-99 as ready-for-agent

> DES-99 already carries `ready-for-agent` as of 2026-07-14. Use this step to
> confirm the label remains present before execution.

```text
Use the Linear MCP to confirm DES-99 still carries the label ready-for-agent.
If it is missing, add it.
Output the updated DES-99 ticket state and labels, including svc:scoring-monitoring-service and canon-realign.
```

---

## 3. Confirm slice readiness

```text
Use the Linear MCP to confirm DES-99 carries both svc:scoring-monitoring-service
and ready-for-agent labels and output its current status and acceptance criteria.

The PRD scope is already in the local file at
@backend/docs/prd/DES-85-primera-implementacion-de-scoring-monitoring-service-hu-37-a-hu-40.md.
Do not re-fetch PRD scope from Linear; read local files if you need implementation decisions.

Before planning, explicitly confirm the Stop 1 acceptance guard:
- ScoreEntry is append-only; team total derived from entries
- each entry records sourceEntityType + sourceEntityId
- score awarding is IScorePolicy (Strategy), not handler branching
- ledger fed by consuming AnswerRegisteredIntegrationEvent on session-answer-registered; idempotent on source id
- ScoreEntryRegistered published post-commit; record flow independent of RabbitMQ
- Ranking derived, one per LiveSession, via IRankingPolicy (Strategy): descending total, ResolutionTime tie-break, equal/non-comparable share rank
- ranking refreshed by consuming ScoreEntryRegistered (async) and pushed over SignalR
- no Penalty command/entity, no audit/monitoring projection, no mutable total, no ranking in session-operations

Output the confirmed HU ids (HU-37 + HU-39), title, acceptance criteria, labels, and the guard confirmation before planning the slice.
```

In the remaining examples below, `HU-37 + HU-39` and `DES-99` are the resolved
values for this slice. `DES-85` is the shared PRD reference for
`scoring-monitoring-service`; its content lives in the local file above.

---

## 4. Start the slice

```text
Prepare the scoring ledger + ranking slice on branch feature/hu-37-39-ledger-ranking.
Use the HU ids (HU-37 + HU-39) and DES id (DES-99) resolved from Linear in the previous step.
This slice affects backend scoring-monitoring-service and (later) frontend.

The pre-resolved orient at the top of this document lists the cross-service seams
this slice consumes and what DES-99 adds. Do not re-read the PRD for scoping unless
you need to resolve a precise implementation detail.

Treat scoring-monitoring-service as greenfield: create the Domain/Application/
Infrastructure/Api structure fresh; do not look for existing scoring types to extend.

Move DES-99 to In Progress and output the exact scope, branch name, base branch (develop), and touched surfaces.
```

---

## 5. Backend phase X.1 — Domain layer

```text
Use @backend/.agents/backend-agent.md.
Implement backend phase X.1 for HU-37 + HU-39 in scoring-monitoring-service, per the
**X.1 derivation block in @backend/docs/hu37-39-context.md** (your spec — do not
re-read the canon or re-inspect the tree; open a cited canon section only to fill a
gap the block leaves open).

Gate:
- Domain build passes; unit test per new domain type (ScoreEntry, Ranking, ScoreValue, ResolutionTime, both policies)
- ScoreEntry.Grant raises ScoreEntryRegistered and exposes NO mutator (append-only); team total is a fold over entries, never a stored field
- ScoreValue enforces its validity rules; ResolutionTimeRankingPolicy orders descending total, ResolutionTime tie-break, equal/non-comparable share rank
- Gate: scoring/ranking behaviour is a Strategy — IScorePolicy and IRankingPolicy are interface + sealed impl in Domain/Services/, injected/selected at runtime, with NO scoring/tie-break if/switch
- no mutable session total; no runtime-authority concept (this is a derived-view context)

Do not touch other backend layers or frontend.
```

Commit:

```text
feat(scoring-monitoring): phase X.1 - domain layer (HU-37+HU-39)

Ref: HU-37
Ref: HU-39
Ref: DES-99
Ref: DES-85
```

---

## 6. Backend phase X.2 — Application layer

```text
Use @backend/.agents/backend-agent.md.
Implement backend phase X.2 for HU-37 + HU-39 in scoring-monitoring-service, per the
**X.2 derivation block in @backend/docs/hu37-39-context.md** (your spec — do not
re-read the canon or re-inspect the tree; open a cited canon section only to fill a
gap the block leaves open).

Gate:
- App build passes; consumer/handler/validator unit tests cover: correct answer -> exactly one Grant ScoreEntry; incorrect answer -> none; redelivered TriviaAnswerSubmissionId -> no second entry (idempotent)
- RecalculateRanking produces an ordered snapshot honoring the ResolutionTime tie-break
- Gate: IScorePolicy injected in RecordScoreEntryHandler and IRankingPolicy injected in RecalculateRankingHandler - no scoring/tie-break if/switch in handlers or consumers
- ScoreEntryRegistered raised for post-commit publish; the ranking refresh runs off the CONSUMED ScoreEntryRegistered (ScoreEntryRegisteredConsumer), not a synchronous call from the record handler
- the consumed contract copy carries [EntityName("session-answer-registered")]; the published contract carries [EntityName("scoring-score-entry-registered")]
- application layer does not leak infrastructure concerns

Do not touch Infrastructure, Api, or frontend.
```

Commit:

```text
feat(scoring-monitoring): phase X.2 - application layer (HU-37+HU-39)

Ref: HU-37
Ref: HU-39
Ref: DES-99
Ref: DES-85
```

---

## 7. Backend phase X.3 — Infrastructure layer

```text
Use @backend/.agents/backend-agent.md.
Implement backend phase X.3 for HU-37 + HU-39 in scoring-monitoring-service, per the
**X.3 derivation block in @backend/docs/hu37-39-context.md** (your spec — do not
re-read the canon or re-inspect the tree; grep any model snapshot rather than
full-reading it).

Gate:
- Infra build passes
- dotnet ef migrations add AddScoringLedgerAndRanking succeeds and represents the append-only ledger + one-per-session Ranking
- repository integration test round-trips an append-only ScoreEntry and proves the UNIQUE-INDEX dedupe on (sourceEntityType, sourceEntityId); Ranking snapshot replace round-trips
- MassTransit integration test: publishing AnswerRegisteredIntegrationEvent to session-answer-registered writes a ScoreEntry and publishes ScoreEntryRegistered; a broker outage does NOT fail the record path (publish swallowed)
- guard against the known MassTransit integration-test hang (bounded/harness-controlled bus, hard timeout)

Do not touch Api or frontend.
```

Commit:

```text
feat(scoring-monitoring): phase X.3 - infrastructure layer (HU-37+HU-39)

Ref: HU-37
Ref: HU-39
Ref: DES-99
Ref: DES-85
```

---

## 8. Backend phase X.4 — API layer

```text
Use @backend/.agents/backend-agent.md.
Implement backend phase X.4 for HU-37 + HU-39 in scoring-monitoring-service, per the
**X.4 derivation block in @backend/docs/hu37-39-context.md** (your spec — do not
re-read the canon or re-inspect the tree; open a cited canon section only to fill a
gap the block leaves open).

Gate:
- endpoint test: GET /api/sessions/{liveSessionId}/ranking returns rows ordered descending by total with the ResolutionTime tie-break, equal/non-comparable teams sharing rank
- SignalR test: a RankingRefreshed pushes RankingChanged to the session group (/hubs/scoring)
- ranking reads inherit the standard gateway + AuthorizationBehaviour guard - no ad-hoc role if and no new Proxy gate
- no API request/response contains a mutable session total; ranking is not computed in session-operations
- service coverage reaches the repo gate target (ADR-0005)

Do not touch frontend.
```

Commit:

```text
feat(scoring-monitoring): phase X.4 - api layer (HU-37+HU-39)

Ref: HU-37
Ref: HU-39
Ref: DES-99
Ref: DES-85
```

---

## 8.5. Docker rebuild and smoke

```text
From the monorepo root, rebuild and restart the backend stack after the API phase:

docker compose build scoring-monitoring-service api-gateway
docker compose up -d scoring-monitoring-service api-gateway

Run smoke checks:
- publish a test AnswerRegisteredIntegrationEvent (IsCorrect=true) onto session-answer-registered and confirm a ScoreEntry is recorded and scoring-score-entry-registered is published
- curl GET /api/sessions/{liveSessionId}/ranking through the gateway and confirm the ordered snapshot
- connect a SignalR client to /hubs/scoring and confirm a RankingChanged frame after a score event

Output:
- container status
- smoke command results
- any API/contract changes the frontend phase must consume (ranking snapshot shape + hub method)
```

---

## 9. Frontend slice

```text
Generate a multi phase plan in a markdown file — following the frontend plan concreteness rule (below), modelled on the exemplar closest to this slice's shape (`@frontend/plans/hu-03-frontend-role-permission-assignment.md` for a small 1-few-endpoint surface; `@frontend/plans/hu-10a-frontend-mission-hierarchy-authoring.md` for a large/multi-endpoint or partially-blocked surface) — save it in `@frontend/plans/` for the following:
Use @frontend/AGENTS.md.
Implement the frontend slice for HU-39 real-time session ranking.

Scope:
- add a ranking/leaderboard data client + types matching the backend snapshot contract (GET /api/sessions/{liveSessionId}/ranking)
- render the ordered ranking for a live session: position, team, total score, and the ResolutionTime tie-break made legible
- subscribe to the SignalR hub (/hubs/scoring) and update the ranking live on RankingChanged, joining the session group by liveSessionId
- expose the view to both participant (own-session standing) and operator (supervision) surfaces per @frontend/AGENTS.md role routing
- reflect shared/equal rank (ties) correctly rather than forcing a strict 1..N order

Gate:
- frontend typecheck/build passes
- ranking view renders the ordered snapshot from the real API contract and updates live on a RankingChanged push
- Gate: ranking order (descending total, ResolutionTime tie-break, shared rank) is presented from backend data, not recomputed on the client
- Gate: no client-side mutable total and no client-side ranking computation

Do not modify backend code in this step.
```

> **Frontend plan concreteness rule (embed in the Step 9 plan):**
> 1. **Proportion concreteness to certainty.** Write code-complete detail — exact
>    DTO/request types, real component skeletons, exact client-fn + server-action
>    bodies, a `data-testid` contract — only for the fully-knowable near-term
>    increments (the foundation + first ranking view). Keep later/large/blocked
>    increments at contract + gate altitude (contract table, scope, gate) with no
>    invented bodies. Never write code for an increment blocked on an open question.
> 2. **Verify every code anchor against the real source before writing it.** Open
>    the files the plan names (exported vs. private helpers, exact signatures, the
>    env/const it reads, the SignalR client setup it reuses) and write only what the
>    source supports. A confident-but-wrong anchor is worse than an altitude note;
>    if a detail is not verifiable, state the assumption under Open Questions.
> 3. **Required sections** (a plan missing one is a defect): Context · Verified
>    Backend Contract (endpoint/hub/shape table) · Architecture Decisions ·
>    Environment (env vars / config consts reused) · data-testid contract · phased
>    Scope + Gate per increment · Acceptance-criteria → test mapping · Open
>    Questions / Dependencies · Out of Scope.
> 4. **Final forms only, sequential by default.** Write only the final version of
>    each anchor — no "wrong → revised" trails — and keep increments sequential
>    unless the slice genuinely parallelizes.

Commit:

```text
feat(frontend): real-time session ranking - HU-39

Ref: HU-37
Ref: HU-39
Ref: DES-99
Ref: DES-85
```

---

## 9b. Implement the frontend plan

> Run only after the Step 9 plan is written and reviewed.

```text
Use @frontend/AGENTS.md and the Step 9 plan at @frontend/plans/<the-ranking-plan>.md.
Implement phase by phase in the plan's order, per the plan's own Scope / Gate / Commit Sequence.
The plan is the source of truth and supersedes the Step 9 seed scope.

Stop at any increment the plan marks blocked on an Open Question (name it).
Do not re-generate the plan. Do not modify backend code.
```

---

## 10. Close-out

```text
Use the Linear MCP to re-check DES-99 acceptance criteria and labels.
Verify the final implementation against the Stop 1 acceptance guard:
- ScoreEntry append-only; team total derived from entries; sourceEntityType + sourceEntityId recorded
- score awarding via IScorePolicy (Strategy); ledger fed by consuming session-answer-registered; idempotent on source id
- ScoreEntryRegistered published post-commit; record flow independent of RabbitMQ
- Ranking derived one per LiveSession via IRankingPolicy (Strategy): descending total, ResolutionTime tie-break, equal/non-comparable share rank
- ranking refreshed by consuming ScoreEntryRegistered (async) and pushed over SignalR
- no Penalty command/entity, no audit/monitoring projection, no mutable total, no ranking in session-operations

Run final backend and frontend verification required by the repo instructions.
Summarise:
- commits created (backend X.1-X.4 + frontend)
- backend API contract changes (ranking snapshot shape + /hubs/scoring method) and the published/consumed exchanges
- frontend plan/file produced
- tests and gates run
- any unresolved ambiguity for HU-38 (penalties) / HU-40 (audit) follow-up, and the QR-path upstream dependency

Create the PR:

gh pr create \
  --base develop \
  --head feature/hu-37-39-ledger-ranking \
  --title "feat(scoring-monitoring): score ledger + real-time ranking (HU-37 + HU-39)" \
  --body "Implements DES-99: an append-only ScoreEntry ledger fed by consuming session-answer-registered (idempotent, correct trivia answers), with post-commit ScoreEntryRegistered publication; and a derived per-LiveSession Ranking (Strategy: descending total, ResolutionTime tie-break) refreshed by consuming the score event and pushed over SignalR. Greenfield scoring-monitoring-service. Penalties (HU-38), audit/monitoring (HU-40), and the QR ledger path (upstream TargetResolved event) are out of scope."
```

---

## Rationale

- **Why one merged slice.** DES-99 fuses HU-37 (DES-51, ledger) and HU-39 (DES-54,
  ranking) because the ranking is derived *directly* from the ledger and both run on
  the same score-event flow — "se implementan como un único workstream backend." The
  four phases are therefore ledger-first, ranking-second **within each layer**, not
  two separate slices. Both HUs are carried in every commit trailer (`Ref: HU-37`,
  `Ref: HU-39`).
- **Why `Strategy` and not `Proxy`.** The matrix mandates `Strategy` for both HUs
  (`ScorePolicy`, `RankingPolicy`) so difficulty/normalization/tie-break logic never
  collapses into handler conditionals. `Proxy` belongs to HU-38 (operator penalties)
  and HU-40 (guarded audit reads), not here — the ranking read is a participant +
  operator view that inherits the standard gateway guard. Single `sealed` impl per
  policy, no selector until a second scoring/ranking variant actually lands.
- **Why RabbitMQ decouples the refresh.** HU-39 AC requires the ranking projection
  to update by consuming the published score events "de forma asíncrona respecto al
  flujo principal." So the record handler raises/publishes `ScoreEntryRegistered`
  and returns; a separate consumer recomputes the ranking. That keeps the ledger
  write independent of the broker (HU-37 AC "sin que el flujo principal dependa de
  RabbitMQ") and lets the projection lag/retry without blocking scoring.
- **Ambiguity — the QR/treasure-hunt ledger path (Constraint 3).** The HU-37 AC
  names QR-resolved evidence (`TargetResolution`, HU-31) as a ledger source, but the
  repo publishes **no `TargetResolved` integration event** yet, and the PRD's pinned
  "canonical demo flow" concretely names only `AnswerRegisteredIntegrationEvent`
  (trivia). This slice therefore wires the **trivia** path concretely and models
  `TargetResolution` as a first-class `ScoreSourceType` that is **not yet fed** —
  when session-operations publishes the target-resolved fact, a second consumer maps
  it to the same `RecordScoreEntry` path with **no** domain change. Flag the missing
  upstream event as a cross-service item at close-out; do not stub a fake producer.
- **Ambiguity — the `session-answer-registered` binding (Constraint 3).** The
  producer record currently lacks `[EntityName]` and its doc cites the legacy routing
  key. DES-99 pins the post-#166 target: bind the per-type exchange
  `session-answer-registered`. The scoring side owns a structurally identical copy of
  the contract with the matching `[EntityName]`; if the producer still needs the
  attribute added, that is a session-operations contract fix to flag, not to make
  from this service.

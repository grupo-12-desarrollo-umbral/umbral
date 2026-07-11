# Prompt Example — HU-23 Tablero de equipo en vivo (Feature Slice)

Concrete prompt sequence for driving `DES-31` / `HU-23` through a backend feature slice on `feature/hu-23-live-team-board`, then a participant frontend/mobile slice. Follows the pattern in [workflow_for_prompts.md](./workflow_for_prompts.md).

**Key difference for HU-23:** this is a **canon-realigned live read surface**, not clue-based progression and not scoring ownership. The board shows the participant's own team score, the existing authoritative timer, active-substage target progress, and optional visible clues. The realignment comment overrides the stale wording that made "pistas habilitadas" the progress model: treasure-hunt progress is based on `Target` resolution; clues are optional guidance. Full score ledger/ranking remains `ScoringMonitoring` scope, so this board can show zero/session-owned current score before the ledger exists.

When working from the monorepo root, make the target workload explicit in each prompt. For backend steps, point to `@backend/.agents/backend-agent.md`. For the frontend step, point to `@frontend/AGENTS.md`. Do not ask for backend and frontend implementation in the same phase prompt; coordinate them as separate scoped steps tied together by the verified API contract.

---

## Stop 1 acceptance guard

Reject this generated prompt before implementation if it does not explicitly scope all of the following:

- board progress is **target-based active-substage progress**, not enabled/completed clues
- clues are optional visible guidance and never advance progress or resolve a target
- timer is reused from HU-22's authoritative `SessionTimerSnapshotDto`; no treasure-hunt countdown is invented
- score is current/session-owned or zero until the `ScoringMonitoring` ledger exists; no score ledger/ranking implementation in this HU
- participant access is team-scoped through `IRuntimeParticipationGuard`, not ad-hoc `if` checks
- live updates use SignalR/WebSockets without manual reload and send team-only payloads to the team group, not broadly to all participants
- no QR target validation, evidence intake, clue release authoring, ranking, penalties, or `ScoringMonitoring` work is included

---

## Required design patterns

No mandated pattern from `backend/docs/adr/0004-required-domain-patterns.md` for HU-23.

- Matrix: `required_patterns_matrix.md` marks `HU-23` as no mandated pattern and `SignalR` transport. The row notes that `Proxy` scopes the read to the team, but this is an inherited protected-read guard, not a new HU-specific `Proxy` mandate.
- Transport: **SignalR / WebSockets** is mandatory and verified in X.4.
- Gate obligation: participant/team scoping goes through `[Authorize(Participant)]` + `IRuntimeParticipationGuard` and existing hub team-group admission. No new Proxy gate and no ad-hoc role/team authorization logic in handlers/controllers.

---

## Pre-resolved orient (as of 2026-07-11)

> Step 1 has already been run. Paste this section into any agent session that needs context before picking up a phase; no need to re-run the orient prompt unless source or Linear state changed.

### What predecessors have already landed

`DES-31` is live, not superseded, and has both required labels. It is `canon-realign` without `needs-rebuild`, so the issue AC is tightened by the DES-31 canon-debt comment: board = target-based active-substage progress + optional visible clues; timer from DES-77/HU-22.

- **DES-22 / HU-15 + DES-24 / HU-17** — `LiveSession` is created from exactly one mission source and owns an immutable `MissionRuntimeSnapshot` with stages/substages, `TargetSnapshot`s, and trivia questions. No session-level `SessionMode`.
- **DES-25 / HU-18** — runtime teams exist on `LiveSession`; `Team` carries early board fields (`CurrentScore`, progress/clue fields) but score is not the downstream ledger.
- **DES-76 / HU-21A** — canonical lifecycle + state broadcasts landed.
- **DES-77 / HU-22** — authoritative timer snapshot and participant timer endpoint landed; HU-23 reuses them.
- **DES-78 / HU-33A** — active-substage pointer and synchronized trivia active question landed.
- **DES-86** — per-target score refactor landed; `TargetSnapshot.Score` is per target, not substage winner-takes-all.
- **DES-11/DES-12** — participant admission/reconnect seams landed; mirror participant runtime guard usage.
- Light mirror: **DES-49 / HU-36A** for read-projection query/DTO/endpoint shape. Landed-untouched: DES-26, DES-27, DES-45.

Superseded originals are excluded: DES-23, DES-28, DES-30, DES-44, and merged DES-47. No same-service predecessor is currently In Progress, so the branch base is `develop`.

### What HU-23 adds on top

| Concern | New work |
|---|---|
| Participant team board read | Team-scoped CQRS read for one participant's runtime team |
| Score display | Current/session-owned team score or zero; no score ledger/ranking |
| Target progress | Active treasure-hunt targets resolved / total active targets; no clue-as-progress |
| Visible clues | Optional guidance visible to that team only |
| Timer | Existing `SessionTimerSnapshotDto` embedded/reused |
| Live updates | Team-scoped SignalR board updates without reload |
| Backend contract | `GET /api/sessions/{liveSessionId}/participants/team-board` + board update payload |
| Frontend/mobile | Participant live board consuming snapshot + SignalR updates |

### Branch state and prerequisite

`feature/hu-23-live-team-board` should be branched from `develop`. No same-service predecessor is currently **In Progress**, so there is no feature-branch dependency to inherit first.

### Linear state (as of 2026-07-11)

- DES-31 (HU-23): **Todo**, labels: `canon-realign`, `svc:session-operations-service`, `Feature`, `ready-for-agent`
- DES-70 (PRD): **Backlog**, labels: `svc:session-operations-service`, `canon-realign`, `ready-for-agent`
- Same-service Done build-on predecessors: DES-22, DES-24, DES-25, DES-76, DES-77, DES-78, DES-86, DES-11, DES-12
- Same-service In Progress issues: none

> Linear live state may have changed. Use the Linear MCP to verify DES-31 status and labels if needed, but do not re-fetch PRD scope — read the local PRD file at `@backend/docs/prd/DES-70-primera-implementacion-de-session-operations-service-hu-15-a-hu-36.md` and the resolved context at `@backend/docs/hu23-context.md`.

---

## 1. Orient — read service state and the resolved HU-23 context

> **Skip this step if you have read the pre-resolved orient section above.** Run it only if the service source, README, or Linear state may have changed since 2026-07-11.

```text
Read the following files and summarize what has already landed and what HU-23 must add:
- @backend/services/session-operations-service/README.md
- @backend/services/session-operations-service/CONTEXT.md
- @backend/docs/prd/DES-70-primera-implementacion-de-session-operations-service-hu-15-a-hu-36.md
- @backend/docs/hu23-context.md

Then inspect only the current session-operations seams HU-23 anchors on:
- @backend/services/session-operations-service/src/Domain/Entities/LiveSession.cs
- @backend/services/session-operations-service/src/Domain/Entities/Team.cs
- @backend/services/session-operations-service/src/Domain/Entities/MissionRuntimeSnapshot.cs
- @backend/services/session-operations-service/src/Application/Sessions/Queries/GetParticipantSessionTimerSnapshot/GetParticipantSessionTimerSnapshotQueryHandler.cs
- @backend/services/session-operations-service/src/Api/Hubs/SessionsHub.cs

Then use the Linear MCP to fetch only the current live state of:
- DES-31 (HU-23 - Tablero de equipo en vivo)
- DES-70 (PRD - SessionOperations)

Output:
- the live DES-31 status and labels
- confirmation that HU-23 is not superseded and is comment-only canon realignment, not needs-rebuild
- the canonical scope: score/current-or-zero, target-based progress, optional visible clues, existing timer, team-scoped live updates
- the explicit exclusions: score ledger/ranking, QR target validation, clue release authoring, evidence intake

Do not start planning or implementing yet.
```

---

## 2. Label DES-31 as ready-for-agent

```text
Use the Linear MCP to confirm DES-31 still carries the label ready-for-agent.
If it is missing, add it.
Output the updated DES-31 ticket state and labels, including canon-realign and svc:session-operations-service.
```

---

## 3. Confirm slice readiness

```text
Use the Linear MCP to confirm DES-31 carries both svc:session-operations-service
and ready-for-agent labels and output its current status and acceptance criteria.

The PRD scope is already in the local file at
@backend/docs/prd/DES-70-primera-implementacion-de-session-operations-service-hu-15-a-hu-36.md.
Do not re-fetch PRD scope from Linear.

Before planning, explicitly confirm the Stop 1 acceptance guard:
- board progress is target-based active-substage progress, not clue progress
- clues are optional visible guidance only
- timer is reused from HU-22; no treasure-hunt countdown is invented
- score is current/session-owned or zero until ScoringMonitoring exists; no score ledger/ranking
- participant access goes through IRuntimeParticipationGuard
- live updates are team-scoped over SignalR/WebSockets
- QR target validation, evidence intake, clue release authoring, ranking, penalties, and ScoringMonitoring are out of scope

Output the confirmed HU id, title, acceptance criteria, labels, and the guard confirmation before planning the slice.
```

In the remaining examples below, `HU-23` and `DES-31` are the resolved values for this slice. `DES-70` is the shared PRD reference for `session-operations-service`.

---

## 4. Start the slice

```text
Prepare the HU-23 slice on branch feature/hu-23-live-team-board.
Use the HU id and DES id resolved from Linear in the previous step.
This slice affects backend `session-operations-service` and the participant frontend/mobile surface.

The pre-resolved orient at the top of this document lists what existing code has already
landed and what HU-23 adds. Do not re-read the PRD for scoping unless you need to resolve

Treat HU-23 as a live read surface: add a team-board projection + participant-guarded query + endpoint + team-scoped SignalR update over the existing `LiveSession` runtime. Do not build score ledger/ranking, QR target validation, clue release authoring, evidence intake, or ScoringMonitoring.

Move DES-31 to In Progress if the team process requires it, and output the exact scope,
branch name, base branch, and touched surfaces.
```

---

## 5. Backend phase X.1 — Domain layer

```text
Use @backend/.agents/backend-agent.md.
Implement backend phase X.1 for HU-23 in session-operations-service, per the
**X.1 derivation block in @backend/docs/hu23-context.md** (your spec — do not
re-read the canon or re-inspect the tree; open a cited canon section only to
fill a gap the block leaves open).

Gate:
- Domain build passes; unit tests cover the new team-board projection/value objects
- board resolves only the requested team
- score is current/session-owned or zero; no score ledger/ranking calculation
- treasure-hunt progress counts active targets, never clues
- visible clues are guidance only and do not advance progress
- trivia board includes active-question/timer context without target-progress invention

Do not touch other backend layers or frontend.
```

Commit:

```text
feat(session-operations): phase X.1 - domain layer (HU-23)

Ref: HU-23
Ref: DES-31
Ref: DES-70
```

---

## 6. Backend phase X.2 — Application layer

```text
Use @backend/.agents/backend-agent.md.
Implement backend phase X.2 for HU-23 in session-operations-service, per the
**X.2 derivation block in @backend/docs/hu23-context.md** (your spec — do not
re-read the canon or re-inspect the tree; open a cited canon section only to
fill a gap the block leaves open).

Gate:
- Application build passes; handler + mapper tests cover the participant team-board query
- IRuntimeParticipationGuard.EnsureAllowedAsync is called before board data is returned
- DTO includes score, timer, target progress, and visible clues with no unauthorized team data
- NotFound/rejection branches are covered through existing exception types
- no ad-hoc participant/team authorization check appears in the handler

Do not touch Infrastructure, Api, or frontend.
```

Commit:

```text
feat(session-operations): phase X.2 - application layer (HU-23)

Ref: HU-23
Ref: DES-31
Ref: DES-70
```

---

## 7. Backend phase X.3 — Infrastructure layer

```text
Use @backend/.agents/backend-agent.md.
Implement backend phase X.3 for HU-23 in session-operations-service, per the
**X.3 derivation block in @backend/docs/hu23-context.md** (your spec — do not
re-read the canon or re-inspect the tree; open a cited canon section only to
fill a gap the block leaves open).

Gate:
- Infrastructure build passes
- repository/integration test proves the board read hydrates teams, active-substage target/question snapshot data, and any persisted progress/clue state used by the projection
- no winner-takes-all substage score / `winner_score` regression appears in model snapshot
- no migration is added unless the missing minimal board state is explicitly justified in the phase output

Do not touch Api or frontend.
```

Commit:

```text
feat(session-operations): phase X.3 - infrastructure layer (HU-23)

Ref: HU-23
Ref: DES-31
Ref: DES-70
```

---

## 8. Backend phase X.4 — Api layer

```text
Use @backend/.agents/backend-agent.md.
Implement backend phase X.4 for HU-23 in session-operations-service, per the
**X.4 derivation block in @backend/docs/hu23-context.md** (your spec — do not
re-read the canon or re-inspect the tree; open a cited canon section only to
fill a gap the block leaves open).

Gate:
- endpoint integration tests cover authorized participant 200 and unauthorized/other-team rejection
- response shape includes score, timer, target progress, and visible clues
- SignalR test proves board update reaches `team:{teamId}` and not a different team
- access remains `[Authorize(Participant)]` + runtime guard; no ad-hoc authorization in controller/hub
- `backend/docs/adr/0005-coverlet-msbuild-for-aggregate-coverage.md` coverage gate passes

Do not touch frontend.
```

Commit:

```text
feat(session-operations): phase X.4 - api layer (HU-23)

Ref: HU-23
Ref: DES-31
Ref: DES-70
```

---

## 8.5. Docker rebuild and smoke

```text
docker compose build session-operations-service api-gateway
docker compose up -d session-operations-service api-gateway

Smoke through the gateway:
- authenticate/reconnect a participant into a live session team
- GET /api/sessions/{liveSessionId}/participants/team-board?teamId={teamId} -> 200 for that participant/team
- verify the response contains: team identity, score/current-or-zero, timer snapshot, active-substage target progress, visible clues
- try another team id -> 403/ProblemDetails (or the service's existing forbidden mapping)
- connect to SignalR and verify a board update reaches the participant's `team:{teamId}` group without reload
```

---

## 9. Frontend slice

```text
Generate a multi phase plan in a markdown file — following the **frontend plan concreteness rule** (below), modelled on the exemplar closest to this slice's shape (`@frontend/plans/hu-03-frontend-role-permission-assignment.md` for a small 1–few-endpoint surface; `@frontend/plans/hu-10a-frontend-mission-hierarchy-authoring.md` for a large/multi-endpoint or partially-blocked surface) — save it in `@frontend/plans/` for the following:
Use @frontend/AGENTS.md.

Build the participant live team board for HU-23 against the verified backend contract:
- HTTP snapshot: GET /api/sessions/{liveSessionId}/participants/team-board?teamId={teamId}&token={token}
- SignalR: subscribe to team-scoped board updates for the participant's team
- render accumulated/current team score, authoritative timer, active-substage target progress, and optional visible clues
- do not show ranking/penalties/score ledger details or clue-as-progress language
- handle zero/current score before ScoringMonitoring ledger exists
- preserve participant/team scoping in route loaders, client calls, and UI states

Frontend plan concreteness rule:
1. **Proportion concreteness to certainty.** Write code-complete detail — exact DTO/request types,
   real component skeletons, exact client-fn + server-action bodies, a `data-testid` contract — only
   for the **fully-knowable near-term increments** (typically the foundation + first authoring
   increment). Keep later, large, or blocked increments at **contract + gate altitude**: a contract
   table, scope, and gate, with no invented bodies. Never write code for an increment blocked on an
   open question.
2. **Verify every code anchor against the real source before writing it.** Open the files the plan
   names — exported vs. private helpers, exact signatures, the const/env it reads, the line a refactor
   targets — and write only what the source actually supports. A confident-but-wrong anchor (e.g.
   "reuse `getIdentityHeaders`" when it is not exported) is worse than an altitude note. If a detail
   is not verifiable, state the assumption under Open Questions rather than inventing it.
3. **Required sections** (both exemplars carry these; a plan missing one is a defect): Context ·
   Verified Backend Contract (endpoint/shape table) · Architecture Decisions · **Environment**
   (env vars / config consts reused) · **data-testid contract** · phased Scope + Gate per increment ·
   **Acceptance-criteria → test mapping** · Open Questions / Dependencies · Out of Scope.
4. **Final forms only, sequential by default.** Write only the final version of each anchor — no
   "wrong → revised" trails — and keep increments sequential unless the slice genuinely parallelizes.
```

Commit:

```text
feat(frontend): live team board — HU-23

Ref: HU-23
Ref: DES-31
Ref: DES-70
```

---

## 9b. Implement frontend plan

```text
Use @frontend/AGENTS.md.
Use the HU-23 frontend plan saved under @frontend/plans/.

Implement phase by phase in the plan's order, per the plan's own Scope / Gate / Commit Sequence.
The plan is the source of truth and supersedes the Step 9 seed scope.

Stop at any increment the plan marks blocked on an Open Question (name it). Do not re-generate the plan. Do not modify backend code. Do not restate per-phase scope or commit subjects from this prompt; the plan owns them.
```

---

## 10. Close-out

Acceptance criteria to verify before PR:

- participant sees accumulated/current score for their own team
- participant sees optional visible clues without treating clues as progress
- participant sees active-substage target progress where treasure-hunt target data exists
- participant sees the authoritative timer from HU-22
- board updates without manual reload via SignalR/WebSockets
- unauthorized participant/team access is rejected
- no score ledger/ranking, QR target validation, clue release authoring, evidence intake, or ScoringMonitoring scope was added

```bash
gh pr create \
  --base develop \
  --head feature/hu-23-live-team-board \
  --title "feat(session-operations): live team board (HU-23)" \
  --body "Adds DES-31/HU-23: a participant team-scoped live board over SessionOperations. The board shows current/session-owned score (or zero before ScoringMonitoring ledger), HU-22 authoritative timer, target-based active-substage progress, and optional visible clues. It updates without manual reload through SignalR and keeps participant access team-scoped through the runtime guard. It does not implement scoring ledger/ranking, QR target validation, clue release authoring, evidence intake, or ScoringMonitoring." \
  --draft
```

## Rationale

HU-23 has no mandated design pattern because it is a live read projection over existing runtime authority rather than a new orchestration, lifecycle, validation, or scoring policy. The real design risk is canon drift: the original ticket describes progress through enabled clues, but the canon rewrite made treasure-hunt progress target-based and clues optional guidance. The generated slice therefore treats SignalR as the hard transport gate, runtime participant scoping as inherited guard behavior, and score as a displayed current value rather than a new scoring ledger.

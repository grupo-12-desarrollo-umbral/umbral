# Prompt Example — HU-24A Panel del operador en tiempo real de estado y progreso (Feature Slice)

Concrete prompt sequence for driving `DES-32` / `HU-24A` through a backend feature slice on `feature/hu-24a-operator-live-session-panel`, then an operator **web** frontend slice. Follows the pattern in [workflow_for_prompts.md](./workflow_for_prompts.md).

**Key difference for HU-24A:** this is the **operator, all-teams analog of HU-23** — a guarded live read projection of **session state + every team's progress**, scoped to the assigned operator. Unlike HU-23 (`—` pattern, participant, one team, team-scope guard), HU-24A carries a **mandated `Proxy`** (matrix HU-24, `CONTEXT.md` §Proxy "operator dashboards, protected panels"): the read and its live subscription pass through the existing `ISessionAdministrationAccessResolver` ownership resolver — Administrator all / Operator owns-else-`Forbidden`. Ranking, events, and evidence are the **sibling HU-24B (DES-33)**, not this slice.

When working from the monorepo root, make the target workload explicit in each prompt. For backend steps, point to `@backend/.agents/backend-agent.md`. For the frontend step, point to `@frontend/AGENTS.md` (the operator surface is the Next.js web app). Do not ask for backend and frontend implementation in the same phase prompt; coordinate them as separate scoped steps tied together by the verified API contract.

---

## Stop 1 acceptance guard

Reject this generated prompt before implementation if it does not explicitly scope all of the following:

- the panel is an **all-teams** operator read of **session state + per-team progress**, not a single-team board
- access is gated by the **mandated `Proxy`** — `ISessionAdministrationAccessResolver.GetAuthorizedSessionAsync` (Administrator all / Operator `AssignedOperatorUserId == actor.UserId` else `ForbiddenAccessException`), no ad-hoc role/owner `if` in handler, controller, or hub
- progress is **target-based active-substage progress** (`resolvedTargets`/`totalActiveTargets`), never enabled/completed clues
- score is current/session-owned or zero until the `ScoringMonitoring` ledger exists; no score ledger/ranking/penalties/winner in this HU
- timer is reused from HU-22's authoritative `SessionTimerSnapshotDto`; no invented countdown
- live updates use SignalR/WebSockets over the **operator-only** `live-session-operators:{id}` group (reused), never `live-session:{id}` broadly; a broadcaster **method** is added, not a new group
- ranking, events, and evidence (HU-24B) are out of scope; existing mutation endpoints are not re-guarded

---

## Required design patterns

- `Proxy` **(mandated — `required_patterns_matrix.md` HU-24 row, lines 43 and 112; `CONTEXT.md` §Proxy `:225-227`)**
  - Owning phases: **X.2 Application** (resolver Proxy in the handler) **and X.4 Api** (endpoint authorization policy + reused hub-join ownership guard).
  - Obligation: the panel read passes through `ISessionAdministrationAccessResolver` / `SessionAdministrationAuthorizationProxy` (ADR-0009 + ADR-0012 §resource-ownership guard). Administrator sees all; Operator only where `LiveSession.AssignedOperatorUserId == actor.UserId`, else `ForbiddenAccessException`. Coarse role gate stays `[Authorize(Policy = AuthorizationPolicies.Operator)]`. **No ad-hoc role/owner `if` checks** anywhere.
- Transport: **SignalR / WebSockets** (`required_patterns_matrix.md:57,112`) — verified in X.4. Panel pushes to the operator-only `live-session-operators:{id}` group; the initial snapshot comes from the GET endpoint.

---

## Pre-resolved orient (as of 2026-07-12)

> Step 1 has already been run. Paste this section into any agent session that needs context before picking up a phase; no need to re-run the orient prompt unless source or Linear state changed.

### What predecessors have already landed

`DES-32` is live, not superseded, and has both required labels. It carries **no** `canon-realign`/`needs-rebuild` label → feature flow; its blockers were re-pointed onto rebuilds DES-76/DES-27 (both Done).

- **DES-31 / HU-23 (PRIMARY)** — `LiveSession.ProjectParticipantTeamBoard(...)` + target-based progress helpers + `BroadcastTeamBoardNotificationHandler` (re-projects on `SessionStateChangedEvent` + `SubstageAdvancedEvent`) + `ITeamBoardBroadcaster`. HU-24A generalizes the projection to all teams and mirrors the broadcaster operator-scoped.
- **DES-49 / HU-36A (PRIMARY)** — the operator-guarded projection shape: `GetOperatorTriviaAnsweredMonitor` query (`[Authorize(Operator)]` + `ISessionAdministrationAccessResolver.GetAuthorizedSessionAsync` + static DTO factory) and the operator-only group `live-session-operators:{id}`.
- **DES-76 / HU-21A** — `LiveSession.State` + `SessionStateChangedEvent` + broadcast to `live-session:{id}` (operators join it). Reflected/reused, never mutated here.
- **DES-27 / HU-20 + DES-26 / HU-19** — operator assigned-session reads + `AssignedOperatorUserId` (the ownership seam the Proxy checks).
- **DES-77 / HU-22** — authoritative timer snapshot reused. **DES-78 / HU-33A** — `ActiveSubstageId`/`SubstageAdvancedEvent` progress triggers. **DES-86** — per-target score. **DES-22/DES-24** — `LiveSession` + immutable snapshot, no `SessionMode`.
- Landed-untouched: DES-25 (HU-18), DES-46 (HU-34), DES-45 (HU-33B), DES-11/DES-12 (HU-07A/07B).

Superseded originals are excluded: DES-23, DES-28, DES-30, DES-44, DES-47. No same-service predecessor is currently In Progress, so the branch base is `develop`.

### What HU-24A adds on top

| Concern | New work |
|---|---|
| Operator session panel read | All-teams domain projection: session `State` + per-team score/progress/active-context/timer |
| Operator-guarded read | `GetOperatorSessionPanel` query via the ownership resolver Proxy |
| Live updates | Operator-group SignalR push re-projecting the panel on state/substage transitions |
| Backend contract | `GET /api/sessions/{liveSessionId}/operator-panel` + operator-group panel-update payload |
| Frontend | Operator live session panel (web) consuming snapshot + operator-group push |

### Branch state and prerequisite

`feature/hu-24a-operator-live-session-panel` should be branched from `develop`. No same-service predecessor is currently **In Progress**, so there is no feature-branch dependency to inherit first.

### Linear state (as of 2026-07-12)

- DES-32 (HU-24A): **Todo**, labels: `svc:session-operations-service`, `Feature`, `ready-for-agent`
- DES-70 (PRD): **Backlog**, labels: `svc:session-operations-service`, `canon-realign`, `ready-for-agent`
- Same-service Done build-on predecessors: DES-31, DES-49, DES-76, DES-27, DES-26, DES-77, DES-78, DES-86, DES-22, DES-24
- Same-service In Progress issues: none

> Linear live state may have changed. Use the Linear MCP to verify DES-32 status and labels if needed, but do not re-fetch PRD scope — read the local PRD file at `@backend/docs/prd/DES-70-primera-implementacion-de-session-operations-service-hu-15-a-hu-36.md` and the resolved context at `@backend/docs/hu24a-context.md`.

---

## 1. Orient — read service state and the resolved HU-24A context

> **Skip this step if you have read the pre-resolved orient section above.** Run it only if the service source, README, or Linear state may have changed since 2026-07-12.

```text
Read the following files and summarize what has already landed and what HU-24A must add:
- @backend/services/session-operations-service/README.md
- @backend/services/session-operations-service/CONTEXT.md
- @backend/docs/prd/DES-70-primera-implementacion-de-session-operations-service-hu-15-a-hu-36.md
- @backend/docs/hu24a-context.md

Then inspect only the current session-operations seams HU-24A anchors on:
- @backend/services/session-operations-service/src/Domain/Entities/LiveSession.cs (ProjectParticipantTeamBoard + BuildActiveSubstageContext helpers)
- @backend/services/session-operations-service/src/Application/Sessions/Queries/GetOperatorTriviaAnsweredMonitor/GetOperatorTriviaAnsweredMonitorQueryHandler.cs
- @backend/services/session-operations-service/src/Application/Sessions/Common/Authorization/SessionAdministrationAuthorizationProxy.cs
- @backend/services/session-operations-service/src/Application/Sessions/EventHandlers/BroadcastTeamBoardNotificationHandler.cs
- @backend/services/session-operations-service/src/Api/Hubs/SessionsHub.cs
- @backend/services/session-operations-service/src/Api/Hubs/SignalRTeamAnsweredBroadcaster.cs

Then use the Linear MCP to fetch only the current live state of:
- DES-32 (HU-24A - Panel del operador en tiempo real de estado y progreso)
- DES-70 (PRD - SessionOperations)

Output:
- the live DES-32 status and labels
- confirmation that HU-24A is not superseded and is feature flow (no canon-realign/needs-rebuild)
- the canonical scope: all-teams session-state + progress panel, Proxy-guarded, target-based progress, score current-or-zero, timer reused, operator-group live push
- the explicit exclusions: ranking/events/evidence (HU-24B), score ledger/ranking, QR target validation, clue release, re-guarding existing mutation endpoints

Do not start planning or implementing yet.
```

---

## 2. Label DES-32 as ready-for-agent

```text
Use the Linear MCP to confirm DES-32 still carries the label ready-for-agent.
If it is missing, add it.
Output the updated DES-32 ticket state and labels, including svc:session-operations-service.
```

---

## 3. Confirm slice readiness

```text
Use the Linear MCP to confirm DES-32 carries both svc:session-operations-service
and ready-for-agent labels and output its current status and acceptance criteria.

The PRD scope is already in the local file at
@backend/docs/prd/DES-70-primera-implementacion-de-session-operations-service-hu-15-a-hu-36.md.
Do not re-fetch PRD scope from Linear.

Before planning, explicitly confirm the Stop 1 acceptance guard:
- all-teams operator panel of session state + per-team progress, not a single-team board
- access via the mandated Proxy (ISessionAdministrationAccessResolver.GetAuthorizedSessionAsync); no ad-hoc role/owner if checks
- target-based active-substage progress; clues never count as progress
- score current/session-owned or zero; no score ledger/ranking/penalties/winner
- timer reused from HU-22; no invented countdown
- live updates over the operator-only live-session-operators:{id} group (reused); a broadcaster method, not a new group
- ranking/events/evidence (HU-24B) and re-guarding existing mutation endpoints are out of scope

Output the confirmed HU id, title, acceptance criteria, labels, and the guard confirmation before planning the slice.
```

In the remaining examples below, `HU-24A` and `DES-32` are the resolved values for this slice. `DES-70` is the shared PRD reference for `session-operations-service`.

---

## 4. Start the slice

```text
Prepare the HU-24A slice on branch feature/hu-24a-operator-live-session-panel.
Use the HU id and DES id resolved from Linear in the previous step.
This slice affects backend `session-operations-service` and the operator web frontend surface.

The pre-resolved orient at the top of this document lists what existing code has already
landed and what HU-24A adds. Do not re-read the PRD for scoping unless you need to resolve an ambiguity.

Treat HU-24A as a guarded live read surface: add an all-teams operator session-panel projection + operator-guarded query (Proxy) + endpoint + operator-group SignalR push over the existing `LiveSession` runtime. Do not build score ledger/ranking, QR target validation, clue release, evidence intake, HU-24B ranking/events, or re-guard existing mutation endpoints.

Move DES-32 to In Progress if the team process requires it, and output the exact scope,
branch name, base branch, and touched surfaces.
```

---

## 5. Backend phase X.1 — Domain layer

```text
Use @backend/.agents/backend-agent.md.
Implement backend phase X.1 for HU-24A in session-operations-service, per the
**X.1 derivation block in @backend/docs/hu24a-context.md** (your spec — do not
re-read the canon or re-inspect the tree; open a cited canon section only to
fill a gap the block leaves open).

Gate:
- Domain build passes; unit tests cover the new all-teams operator-panel projection/value objects
- the panel carries the session State
- the panel includes every team (ordered), each with score defaulting to 0
- treasure-hunt progress counts active targets, never clues
- trivia teams include active-question/timer context without invented target progress
- no score ledger/ranking/penalties/winner calculation

Do not touch other backend layers or frontend.
```

Commit:

```text
feat(session-operations): phase X.1 - domain layer (HU-24A)

Ref: HU-24A
Ref: DES-32
Ref: DES-70
```

---

## 6. Backend phase X.2 — Application layer

```text
Use @backend/.agents/backend-agent.md.
Implement backend phase X.2 for HU-24A in session-operations-service, per the
**X.2 derivation block in @backend/docs/hu24a-context.md** (your spec — do not
re-read the canon or re-inspect the tree; open a cited canon section only to
fill a gap the block leaves open).

Gate:
- Application build passes; handler + mapper tests cover the operator session-panel query
- access goes through ISessionAdministrationAccessResolver.GetAuthorizedSessionAsync (the Proxy); ForbiddenAccessException for a non-owning operator; no ad-hoc role/owner if in the handler
- [Authorize(Roles="Operator")] on the query
- the DTO carries session state + all-teams progress; no score ledger/ranking
Gate (pattern): access enforced through the resource-ownership resolver Proxy — no inline role/owner checks (Proxy).

Do not touch Infrastructure, Api, or frontend.
```

Commit:

```text
feat(session-operations): phase X.2 - application layer (HU-24A)

Ref: HU-24A
Ref: DES-32
Ref: DES-70
```

---

## 7. Backend phase X.3 — Infrastructure layer

```text
Use @backend/.agents/backend-agent.md.
Implement backend phase X.3 for HU-24A in session-operations-service, per the
**X.3 derivation block in @backend/docs/hu24a-context.md** (your spec — do not
re-read the canon or re-inspect the tree; open a cited canon section only to
fill a gap the block leaves open).

Gate:
- Infrastructure build passes
- repository/integration test proves the panel read hydrates session State + all teams + active-substage target/question snapshot data
- no winner-takes-all substage score / `winner_score` regression in the model snapshot
- no migration is added (no new persisted state); if a gap is discovered, justify it explicitly in the phase output

Do not touch Api or frontend.
```

Commit:

```text
feat(session-operations): phase X.3 - infrastructure layer (HU-24A)

Ref: HU-24A
Ref: DES-32
Ref: DES-70
```

---

## 8. Backend phase X.4 — Api layer

```text
Use @backend/.agents/backend-agent.md.
Implement backend phase X.4 for HU-24A in session-operations-service, per the
**X.4 derivation block in @backend/docs/hu24a-context.md** (your spec — do not
re-read the canon or re-inspect the tree; open a cited canon section only to
fill a gap the block leaves open).

Gate:
- endpoint integration tests cover 200 for the assigned operator and 403 (RFC 7807) for a non-owning operator
- response shape includes session state + per-team progress (score/target progress/timer)
- SignalR test proves the panel push reaches `live-session-operators:{id}` and not participant connections; a state/substage transition pushes an updated panel without reload
- access remains [Authorize(Policy=Operator)] + the ownership resolver; hub-join ownership guard reused; no ad-hoc authorization in controller/hub
- `backend/docs/adr/0005-coverlet-msbuild-for-aggregate-coverage.md` coverage gate passes
Gate (pattern): endpoint authorized via the Operator policy AND the ownership resolver Proxy; live push gated to the operator group by the reused hub-join guard — no ad-hoc role/owner checks (Proxy). SignalR transport gate verified here.

Do not touch frontend.
```

Commit:

```text
feat(session-operations): phase X.4 - api layer (HU-24A)

Ref: HU-24A
Ref: DES-32
Ref: DES-70
```

---

## 8.5. Docker rebuild and smoke

```text
docker compose build session-operations-service api-gateway
docker compose up -d session-operations-service api-gateway

Smoke through the gateway:
- authenticate as the assigned operator of a live session
- GET /api/sessions/{liveSessionId}/operator-panel -> 200; verify the response contains: session state, and per-team entries with team identity, score/current-or-zero, active-substage target progress (resolvedTargets/totalActiveTargets), and timer snapshot
- authenticate as a different (non-owning) operator, GET the same endpoint -> 403/ProblemDetails (RFC 7807)
- connect to the SignalR hub, JoinLiveSessionAsOperatorAsync for the owned session, and verify a panel update reaches the operator (`live-session-operators:{id}`) after a session-state transition or substage advance, without reload; confirm it does not reach participant connections
```

---

## 9. Operator web frontend slice

```text
Generate a multi phase plan in a markdown file — following the **frontend plan concreteness rule** (below), modelled on the exemplar closest to this slice's shape (@frontend/plans/hu-03-frontend-role-permission-assignment.md for a small 1-few-endpoint surface; @frontend/plans/hu-10a-frontend-mission-hierarchy-authoring.md for a large/multi-endpoint or partially-blocked surface) — save it in @frontend/plans/ for the following:
Use @frontend/AGENTS.md.

Build the operator live session panel (web) for HU-24A against the verified backend contract:
- HTTP snapshot: GET /api/sessions/{liveSessionId}/operator-panel (operator-authenticated) returning session state + ordered per-team progress (teamId, teamCode, displayName, score, active-substage target progress, timer)
- SignalR: after JoinLiveSessionAsOperatorAsync, subscribe to the operator-panel update method on the `live-session-operators:{id}` group (the operator already joins it on the hub — no new invoke/group)
- render the current SessionState and a per-team progress rollup (score current-or-zero, resolvedTargets/totalActiveTargets, timer); do NOT render ranking, events, evidence, or clue-as-progress (those are HU-24B)
- reuse the existing operator auth/session context; a non-owning operator sees a 403/forbidden state, not another operator's session
- drive the countdown from the panel's timer snapshot (HU-22 semantics), consistent with the existing operator timer view

Frontend plan concreteness rule:
1. Proportion concreteness to certainty. Write code-complete detail — exact DTO/request types, real
   component skeletons, exact client-fn + server-action bodies, a data-testid contract — only for the
   fully-knowable near-term increments (the panel snapshot fetch + the panel component + the SignalR
   subscription). Keep later, large, or blocked increments at contract + gate altitude: a contract
   table, scope, and gate, with no invented bodies. Never write code for an increment blocked on an
   open question.
2. Verify every code anchor against the real source before writing it. Open the files the plan names —
   the operator session views, the existing SignalR client/hook, the identity/header helpers, the host
   const/env it reads — and write only what the source actually supports. A confident-but-wrong anchor
   is worse than an altitude note. If a detail is not verifiable, state the assumption under Open
   Questions rather than inventing it.
3. Required sections (a plan missing one is a defect): Context · Verified Backend Contract
   (endpoint/shape table) · Architecture Decisions · Environment (env vars / config consts reused) ·
   data-testid contract · phased Scope + Gate per increment · Acceptance-criteria -> test mapping ·
   Open Questions / Dependencies · Out of Scope.
4. Final forms only, sequential by default. Write only the final version of each anchor — no
   "wrong -> revised" trails — and keep increments sequential unless the slice genuinely parallelizes.
```

Commit:

```text
feat(frontend): operator live session panel — HU-24A

Ref: HU-24A
Ref: DES-32
Ref: DES-70
```

---

## 9b. Implement the operator web frontend plan

```text
Use @frontend/AGENTS.md.
Use the HU-24A frontend plan saved at @frontend/plans/<the plan file written in Step 9>.

Implement phase by phase in the plan's order, per the plan's own Scope / Gate / Commit Sequence.
The plan is the source of truth and supersedes the Step 9 seed scope.

Stop at any increment the plan marks blocked on an Open Question (name it). Do not re-generate the plan. Do not modify backend code. Do not restate per-phase scope or commit subjects from this prompt; the plan owns them.
```

---

## 10. Close-out

Acceptance criteria to verify before PR:

- the operator sees only sessions assigned/authorized to them per policy (non-owned session → 403)
- the panel reflects session-state changes without manual reload
- the operator monitors team progress in real time
- the system blocks access/subscription to unauthorized sessions
- the panel reflects state and progress in real time via SignalR/WebSockets without manual reload
- no score ledger/ranking, QR target validation, clue release, evidence intake, HU-24B ranking/events, or re-guarding of existing mutation endpoints was added

```bash
gh pr create \
  --base develop \
  --head feature/hu-24a-operator-live-session-panel \
  --title "feat(session-operations): operator live session panel — HU-24A" \
  --body "Adds the HU-24A operator real-time panel (DES-32): an all-teams projection of session state + per-team progress over LiveSession, gated to the assigned operator through the existing ISessionAdministrationAccessResolver ownership Proxy (Administrator all / Operator owns-else-403). Exposes GET /api/sessions/{id}/operator-panel for the initial snapshot and pushes the re-projected panel to the operator-only live-session-operators:{id} SignalR group on session-state and substage-advance transitions, plus an operator web panel view. Progress is target-based and score is current/session-owned or zero; it does not implement scoring ledger/ranking, penalties, QR target validation, clue release, evidence intake, HU-24B ranking/events, or re-guard existing mutation endpoints." \
  --draft
```

## Rationale

HU-24A's mandated pattern is `Proxy` because the operator panel is a **guarded projection** of protected supervision data: the matrix lists HU-24 in the `Proxy` column (unlike the sibling HU-23, marked `—`) and `CONTEXT.md` §Proxy names "operator dashboards, protected panels" explicitly. The design risk is twofold: (1) re-inventing an ownership check inline instead of routing through the existing `ISessionAdministrationAccessResolver` (the ceremony ADR-0012 forbids), and (2) leaking the operator-scoped all-teams rollup to participants by broadcasting on `live-session:{id}` instead of the operator-only `live-session-operators:{id}` group. The slice therefore treats the ownership resolver as the mandated Proxy gate (X.2 handler + X.4 endpoint/hub), SignalR as the hard transport gate over the reused operator group, and progress/score as target-based/session-owned reads — deferring ranking, events, and evidence to the sibling HU-24B (DES-33).

**Ambiguity noted (PRD):** the AC line "the system blocks actions on unauthorized sessions" is a read-surface obligation here — the Proxy denies a non-owning operator any panel read or live subscription. Re-authorizing the existing mutation endpoints (`PATCH …/state`, `POST …/teams`) is **not** in HU-24A scope; those belong to their own HUs. This is recorded so the driver does not authorize scope creep into mutation guards.

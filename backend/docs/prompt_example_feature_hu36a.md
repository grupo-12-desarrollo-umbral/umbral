# Prompt Example — HU-36A Monitoreo restringido de respondido/no respondido en trivia (Feature Slice)

Concrete prompt sequence for driving `DES-49` / `HU-36A` through a backend feature slice
on `feature/hu-36a-trivia-answered-monitor`, then a web frontend slice. Follows the pattern in
[workflow_for_prompts.md](./workflow_for_prompts.md).

**Key difference for HU-36A:** this is not new runtime — it is a **CQRS read surface over
`LiveSession`**. HU-34 (DES-46) already landed the trivia answer, the answered *fact*
(`AnswerRegisteredEvent`), and the operator-only SignalR transport (`live-session-operators:{id}`
group + `TeamAnswered` event, option-free by design). HU-36A adds the **pre-close answered/not-answered
dashboard projection** an operator sees on connect/refresh: for the active question, which teams have
answered and which have not — **never** the chosen option, correctness, or points. It reuses the
existing operator-only group and event for live updates; it introduces no broadcaster and no migration.

When working from the monorepo root, make the target workload explicit in each prompt.
For backend steps, point to `@backend/.agents/backend-agent.md`. For the frontend step,
point to `@frontend/AGENTS.md`. Do not ask for backend and web implementation in the same
phase prompt; coordinate them as separate scoped steps tied together by the verified API contract.

This slice consumes the HU-34 contract handed off in `prompt_example_feature_hu34.md` Step 9
(`DES-49` is the named owner of the web operator answered/not-answered monitoring surface).

---

## Stop 1 acceptance guard

Reject this generated prompt before implementation if it does not explicitly scope all of the following:

- pre-close monitor shows only **answered / not-answered per team**, never the chosen option, correctness, or points
- answered = an **accepted** `TriviaAnswerSubmission` exists for the team on the active `(ActiveSubstageId, QuestionSequenceOrder)` question
- teams with no accepted answer render as **not answered yet** (derived by enumerating the session's teams)
- access is gated by the **operator-ownership resolver `Proxy`** (`ISessionAdministrationAccessResolver`), not ad-hoc `if` checks
- live updates **reuse** the existing operator-only `TeamAnswered` event + `live-session-operators:{id}` group — no new broadcaster, no participant-group send
- **no migration** and no new persisted state (read over HU-34's persistence)
- correctness reveal (HU-35) and post-close full review + points (HU-36B) are out of scope

---

## Required design patterns

- `Proxy` **(mandated — `required_patterns_matrix.md` HU-36, line 134)**
  - Why: restricted answer monitoring is a **guarded projection** — protected supervision data (which teams
    answered) gated to the session's assigned operator, and option-free before close.
  - Phase owner: **X.2 Application** (resolver proxy in the query handler) **and X.4 Api** (endpoint
    authorization policy + reused hub-join ownership guard).
  - Gate obligation: the read passes through `ISessionAdministrationAccessResolver` /
    `SessionAdministrationAuthorizationProxy` (Administrator all / Operator `AssignedOperatorUserId ==
    actor.UserId` else `ForbiddenAccessException`, ADR-0009); no role/owner `if` in handler, controller, or hub.

- Transport: **SignalR** — reuse HU-34's operator-only `live-session-operators:{id}` group and `TeamAnswered`
  event. HU-36A adds the initial-snapshot query only.

---

## Pre-resolved orient (as of 2026-07-10)

> Step 1 has already been run. Paste this section into any agent session that needs context
> before picking up a phase; no need to re-run the orient prompt unless source or Linear state changed.

### What predecessors have already landed

`DES-49` is a standard feature build on top of the realigned session runtime. The service went through the
mission-runtime realignment, so predecessor scope is anchored on the **rebuild** ticket ids only:

- **DES-46 / HU-34** — trivia answer registration; `TriviaAnswerSubmission`, `LiveSession.TriviaAnswerSubmissions`,
  `AnswerRegisteredEvent`, and the **operator-only** `TeamAnswered` transport (`SignalRTeamAnsweredBroadcaster`,
  group `live-session-operators:{id}`, option-free `TeamAnsweredNotificationDto`). HU-34 owns the answered
  *fact*; HU-36A owns the *dashboard projection* (hu34-context.md:122).
- **DES-78 / HU-33A** — synchronized trivia substage runtime; `LiveSession.ActiveSubstageId` /
  `ActiveQuestionIndex`, `ResolveActiveTriviaQuestion()`, `TriviaQuestionSnapshot` / `SubstageSnapshot`.
- **DES-77 / HU-22 + operator reads** — the operator-ownership resolver Proxy
  (`ISessionAdministrationAccessResolver` / `SessionAdministrationAuthorizationProxy`, ADR-0009) and the
  Query to mirror (`GetOperatorSessionTimerSnapshot`).

Superseded originals are **not** cited: DES-44 (→DES-78), DES-30 (→DES-77), DES-28 (→DES-76), DES-23 (→DES-75),
DES-47 (merged into DES-46). No same-service predecessor is currently In Progress, so the branch base is `develop`.

### What HU-36A adds on top

| Concern | New work |
|---|---|
| Answered projection | Domain read over the active question marking each team answered vs not-answered |
| No-leak invariant | Projection type structurally omits option / correctness / score |
| Operator-guarded read | `GetOperatorTriviaAnsweredMonitor` query behind the ownership resolver Proxy |
| Live without reload | Initial-snapshot query on connect/refresh; existing `TeamAnswered` event keeps it live |
| Backend contract | `GET /api/sessions/{liveSessionId}/answered-monitor` (operator-only) |
| Web follow-up | Operator answered/not-answered board consumes the snapshot + `TeamAnswered` event |

### Branch state and prerequisite

`feature/hu-36a-trivia-answered-monitor` should be branched from `develop`. No same-service predecessor is
currently **In Progress**, so there is no feature-branch dependency to inherit first.

### Linear state (as of 2026-07-10)

- DES-49 (HU-36A): **Todo**, labels: `svc:session-operations-service`, `Feature`, `ready-for-agent`
- DES-70 (PRD): **Backlog**, labels: `svc:session-operations-service`, `canon-realign`, `ready-for-agent`
- Same-service Done build-on predecessors: DES-46, DES-78, DES-77
- Blockers DES-78 (HU-33A rebuild) and DES-46 (HU-34) are both **Done** — the ticket is unblocked

> Linear live state may have changed. Use the Linear MCP to verify DES-49 status and labels if needed, but do
> not re-fetch PRD scope — read the local PRD file at
> `@backend/docs/prd/DES-70-primera-implementacion-de-session-operations-service-hu-15-a-hu-36.md`.

---

## 1. Orient — read service state and the resolved HU-36A context

> **Skip this step if you have read the pre-resolved orient section above.** Run it only if
> the service source, README, or Linear state may have changed since 2026-07-10.

```text
Read the following files and summarize what has already landed and what HU-36A must add:
- @backend/services/session-operations-service/README.md
- @backend/services/session-operations-service/CONTEXT.md
- @backend/docs/prd/DES-70-primera-implementacion-de-session-operations-service-hu-15-a-hu-36.md
- @backend/docs/hu36a-context.md

Then inspect only the current session-operations seams HU-36A anchors on:
- @backend/services/session-operations-service/src/Domain/Entities/LiveSession.cs
- @backend/services/session-operations-service/src/Domain/Entities/TriviaAnswerSubmission.cs
- @backend/services/session-operations-service/src/Application/Sessions/Common/Authorization/SessionAdministrationAuthorizationProxy.cs
- @backend/services/session-operations-service/src/Application/Sessions/Queries/GetOperatorSessionTimerSnapshot/GetOperatorSessionTimerSnapshotQueryHandler.cs
- @backend/services/session-operations-service/src/Api/Hubs/SignalRTeamAnsweredBroadcaster.cs

Then use the Linear MCP to fetch only the current live state of:
- DES-49 (HU-36A - Monitoreo restringido de respondido/no respondido en trivia)
- DES-70 (PRD - SessionOperations)

Output:
- the live DES-49 status and labels
- confirmation that HU-34's answered fact + operator-only transport already exist and are reused, not rebuilt
- the ADD gap: an answered/not-answered projection + operator-guarded read query + GET endpoint
- the privacy constraint: answered-or-not only, never the option/correctness/points before close

Do not start planning or implementing yet.
```

---

## 2. Label DES-49 as ready-for-agent

```text
Use the Linear MCP to confirm DES-49 still carries the label ready-for-agent.
If it is missing, add it.
Output the updated DES-49 ticket state and labels.
```

---

## 3. Confirm slice readiness

```text
Use the Linear MCP to confirm DES-49 carries both svc:session-operations-service
and ready-for-agent labels and output its current status and acceptance criteria.

The PRD scope is already in the local file at
@backend/docs/prd/DES-70-primera-implementacion-de-session-operations-service-hu-15-a-hu-36.md.
Do not re-fetch PRD scope from Linear.

Before planning, explicitly confirm the Stop 1 acceptance guard:
- pre-close monitor shows only answered / not-answered per team, never the option/correctness/points
- answered = an accepted TriviaAnswerSubmission for the active (ActiveSubstageId, QuestionSequenceOrder)
- teams with no accepted answer render as not-answered-yet
- access gated by the operator-ownership resolver Proxy (ISessionAdministrationAccessResolver), no ad-hoc if
- live updates reuse the existing operator-only TeamAnswered event + live-session-operators:{id} group
- no migration, no new persisted state
- correctness reveal (HU-35) and post-close review + points (HU-36B) are out of scope

Output the confirmed HU id, title, acceptance criteria, labels, and the guard confirmation before planning the slice.
```

In the remaining examples below, `HU-36A` and `DES-49` are the resolved values for this slice.
`DES-70` is the shared PRD reference for `session-operations-service`.

---

## 4. Start the slice

```text
Prepare the HU-36A slice on branch feature/hu-36a-trivia-answered-monitor.
Use the HU id and DES id resolved from Linear in the previous step.
This slice affects backend `session-operations-service` and the web frontend.

The pre-resolved orient at the top of this document lists what existing code has already
landed and what HU-36A adds. Do not re-read the PRD for scoping unless you need to resolve
a precise implementation detail.

Treat HU-36A as a read surface: add a projection + operator-guarded query + endpoint over the
existing HU-34 answer persistence and HU-33A active-question runtime. Do not rebuild the answer
domain, add a SignalR broadcaster, or introduce a migration.

Move DES-49 to In Progress if the team process requires it, and output the exact scope,
branch name, base branch, and touched surfaces.
```

---

## 5. Backend phase X.1 — Domain layer

```text
Use @backend/.agents/backend-agent.md.
Implement backend phase X.1 for HU-36A in session-operations-service, per the
**X.1 derivation block in @backend/docs/hu36a-context.md** (your spec — do not
re-read the canon or re-inspect the tree; open a cited canon section only to
fill a gap the block leaves open).

Gate:
- Domain build passes; unit tests cover the new projection method and value objects
- answered iff an accepted TriviaAnswerSubmission exists for the active (ActiveSubstageId, QuestionSequenceOrder)
- teams with no accepted answer are projected as not-answered
- the view/status types expose no SelectedOptionSequenceOrder, IsCorrect, or ScoreValue
- the projection method rejects when there is no active trivia question / non-trivia active substage

Do not touch other backend layers or frontend.
```

Commit:

```text
feat(session-operations): phase X.1 - domain layer (HU-36A)

Ref: HU-36A
Ref: DES-49
Ref: DES-70
```

---

## 6. Backend phase X.2 — Application layer

```text
Use @backend/.agents/backend-agent.md.
Implement backend phase X.2 for HU-36A in session-operations-service, per the
**X.2 derivation block in @backend/docs/hu36a-context.md** (your spec — do not
re-read the canon or re-inspect the tree; open a cited canon section only to
fill a gap the block leaves open).

Gate:
- clean build passes; handler unit tests cover the authorized-operator path and the non-owner rejection
- Gate: access enforced through the Proxy-style ownership resolver (ISessionAdministrationAccessResolver /
  SessionAdministrationAuthorizationProxy) — non-owning operator gets ForbiddenAccessException; no ad-hoc
  role/owner if checks in the handler
- the query carries [Authorize(Roles="Operator")]; the result DTO carries no option/correctness/points
- the handler calls the domain projection; no projection/branching logic leaks into the handler

Do not touch Infrastructure, Api, or frontend.
```

Commit:

```text
feat(session-operations): phase X.2 - application layer (HU-36A)

Ref: HU-36A
Ref: DES-49
Ref: DES-70
```

---

## 7. Backend phase X.3 — Infrastructure layer

```text
Use @backend/.agents/backend-agent.md.
Implement backend phase X.3 for HU-36A in session-operations-service, per the
**X.3 derivation block in @backend/docs/hu36a-context.md** (your spec — do not
re-read the canon or re-inspect the tree; grep the model snapshot rather than
full-reading it, as the block instructs).

Gate:
- infrastructure build passes
- the operator read path hydrates the session's teams and accepted trivia answers for the active question
- a repository/integration test proves the answered/not-answered derivation round-trips for a session with
  some teams answered and some not on the active question
- no migration is added and no new persisted state is introduced (read over HU-34's persistence);
  confirm ApplicationDbContextModelSnapshot already carries TriviaAnswerSubmission

Do not touch Api or frontend.
```

Commit:

```text
feat(session-operations): phase X.3 - infrastructure layer (HU-36A)

Ref: HU-36A
Ref: DES-49
Ref: DES-70
```

---

## 8. Backend phase X.4 — Api layer

```text
Use @backend/.agents/backend-agent.md.
Implement backend phase X.4 for HU-36A in session-operations-service, per the
**X.4 derivation block in @backend/docs/hu36a-context.md** (your spec — do not
re-read the canon or re-inspect the tree; open a cited canon section only to
fill a gap the block leaves open).

Gate:
- GET /api/sessions/{liveSessionId}/answered-monitor returns 200 with per-team answered/not-answered
  for the assigned operator
- a non-owning operator receives 403 as consistent RFC 7807 ProblemDetails
- Gate: endpoint carries the Operator authorization policy and the ownership resolver Proxy decides access —
  no ad-hoc checks; the reused hub-join ownership guard still gates the operator-only group
- the payload never contains the chosen option, correctness, or points; the operator-only monitor does not
  reach participant connections
- service coverage meets the repo gate (ADR-0005)

Do not touch frontend.
```

Commit:

```text
feat(session-operations): phase X.4 - api layer (HU-36A)

Ref: HU-36A
Ref: DES-49
Ref: DES-70
```

---

## 8.5. Docker rebuild and smoke

```text
Rebuild and restart the backend runtime for HU-36A:

docker compose build session-operations-service api-gateway
docker compose up -d session-operations-service api-gateway

Then smoke the new read path and the reused transport through the gateway:
- GET /api/sessions/{liveSessionId}/answered-monitor as the assigned operator -> 200 with answered/not-answered per team, no option
- same endpoint as a different (non-owning) operator -> 403 RFC 7807
- open an operator SignalR connection, join as operator, submit a team answer -> the TeamAnswered pulse arrives
  and the answered-monitor snapshot reflects it; verify it does not reach a participant connection

Output:
- container status
- smoke command results
- the verified answered-monitor response shape + reused TeamAnswered event shape the frontend must consume
```

---

## 9. Frontend slice

```text
Generate a multi phase plan in a markdown file — following the **frontend plan concreteness rule** (below),
modelled on the exemplar closest to this slice's shape (`@frontend/plans/hu-03-frontend-role-permission-assignment.md`
for a small 1–few-endpoint surface; `@frontend/plans/hu-10a-frontend-mission-hierarchy-authoring.md` for a
large/multi-endpoint or partially-blocked surface) — save it in `@frontend/plans/` for the following:
Use @frontend/AGENTS.md.
Implement the web frontend slice for HU-36A operator answered/not-answered trivia monitoring.

This is a small surface (one GET endpoint + one reused SignalR event) — use the hu-03 exemplar shape.

Scope:
- add a data client + types for GET /api/sessions/{liveSessionId}/answered-monitor (operator-only)
- render an operator answered/not-answered board for the active question: each team as answered or not-answered-yet,
  never the chosen option, correctness, or points
- subscribe to the existing operator-only TeamAnswered SignalR event to update the board live without reload;
  fetch the snapshot on connect/refresh so a mid-question load shows the full roster
- gate the view to the operator's assigned sessions (surface the 403 as a not-authorized state, do not crash)

Gate:
- frontend typecheck/build passes
- the board exercises the snapshot fetch + live TeamAnswered update against the verified contract
- Gate: no UI type/copy/state ever exposes the chosen option, correctness, or points before close
- Gate: the board renders teams with no accepted answer as not-answered-yet
- Gate: a non-owning operator sees an authorization state, not another operator's session data

Do not modify backend code in this step.

Frontend plan concreteness rule (embed verbatim in the generated plan's method):
1. Proportion concreteness to certainty. Write code-complete detail — exact DTO/request types, real component
   skeletons, exact client-fn + server-action bodies, a data-testid contract — only for the fully-knowable
   near-term increments (typically the foundation + first authoring increment). Keep later, large, or blocked
   increments at contract + gate altitude: a contract table, scope, and gate, with no invented bodies. Never
   write code for an increment blocked on an open question.
2. Verify every code anchor against the real source before writing it. Open the files the plan names — exported
   vs. private helpers, exact signatures, the const/env it reads, the line a refactor targets — and write only
   what the source actually supports. A confident-but-wrong anchor (e.g. "reuse getIdentityHeaders" when it is
   not exported) is worse than an altitude note. If a detail is not verifiable, state the assumption under Open
   Questions rather than inventing it.
3. Required sections (both exemplars carry these; a plan missing one is a defect): Context · Verified Backend
   Contract (endpoint/shape table) · Architecture Decisions · Environment (env vars / config consts reused) ·
   data-testid contract · phased Scope + Gate per increment · Acceptance-criteria → test mapping · Open
   Questions / Dependencies · Out of Scope.
4. Final forms only, sequential by default. Write only the final version of each anchor — no "wrong → revised"
   trails — and keep increments sequential unless the slice genuinely parallelizes.
```

Commit:

```text
feat(frontend): operator trivia answered/not-answered monitor — HU-36A

Ref: HU-36A
Ref: DES-49
Ref: DES-70
```

---

## 9b. Implement the frontend plan

```text
Use @frontend/AGENTS.md and the Step 9 plan at @frontend/plans/<the plan file written in Step 9>.
Implement phase by phase in the plan's order, per the plan's own Scope / Gate / Commit Sequence.

The plan is the source of truth and supersedes the Step 9 seed scope above. Stop at any increment the plan
marks blocked on an Open Question (name it) rather than inventing the blocked behaviour. Do not re-generate the
plan and do not modify backend code.
```

---

## 10. Close-out

```text
Before opening the PR, confirm all DES-49 acceptance criteria are satisfied:
- during the active question, the operator sees only whether each team answered or not
- before close, the operator cannot see the option chosen by a team
- the monitoring view updates during the session without manual reload
- monitoring respects the operator's authorized sessions
- answered/not-answered updates are broadcast to the operator in real time via SignalR, without manual reload

Also confirm the Stop 1 acceptance guard and the scope boundary:
- correctness reveal is HU-35; post-close full answer + points review is HU-36B — neither is in this PR
- no new SignalR broadcaster and no migration were introduced

Run final backend and frontend verification required by the repo instructions.
Summarise:
- commits created
- backend API contract (answered-monitor endpoint) + reused TeamAnswered event
- frontend plan/file produced
- tests and gates run
- any unresolved ambiguity for HU-36B follow-up

Create the PR:

gh pr create \
  --base develop \
  --head feature/hu-36a-trivia-answered-monitor \
  --title "feat(session-operations): HU-36A restricted trivia answered/not-answered operator monitor" \
  --body "Implements HU-36A / DES-49: a pre-close operator monitor showing only answered/not-answered per team for the active trivia question, gated by the operator-ownership Proxy (ADR-0009) and option-free by construction. Adds a LiveSession projection, an operator-guarded GetOperatorTriviaAnsweredMonitor query, and a GET /api/sessions/{id}/answered-monitor endpoint; live updates reuse HU-34's operator-only TeamAnswered SignalR transport. No migration, no new broadcaster. Correctness reveal (HU-35) and post-close review + points (HU-36B) remain out of scope."
```

---

## Rationale

- **Why `Proxy` and not a new pattern:** HU-36 is matrix-mandated `Proxy` (line 134) because restricted
  monitoring is a *guarded projection* — the access decision (this operator owns this session) must precede
  data exposure, and it must be a resource-ownership decision on the loaded `LiveSession`
  (`AssignedOperatorUserId == actor.UserId`), which is exactly the resolver-proxy shape ADR-0012 documents.
  A coarse `[Authorize(Roles="Operator")]` alone is insufficient — any operator could otherwise read any
  session's board.
- **Why this reuses HU-34's transport instead of adding one:** HU-34 deliberately introduced the operator-only
  `live-session-operators:{id}` group and the option-free `TeamAnswered` event precisely so HU-36A could build
  the full monitor on top (hu34-context.md:122, and the HU-34 rationale on the privacy boundary). Adding a
  second broadcaster or sending to `live-session:{id}` would re-open the participant leak HU-34 closed.
- **Why no migration:** the answered/not-answered state is derived at read time from `LiveSession.Teams` and the
  accepted `TriviaAnswerSubmission`s HU-34 already persists. HU-36A is a CQRS read surface (PRD line 222), so the
  only new persistence work is confirming the read hydrates what the projection needs.
- **Why the option-free invariant lives in the domain:** modeling the projection as value objects that structurally
  cannot carry `SelectedOptionSequenceOrder`/`IsCorrect`/`ScoreValue` makes the fairness rule impossible to violate
  by a later mapping mistake, rather than relying on every DTO mapper to remember to drop the option.

# Prompt Example — HU-34 Team trivia answer first-write-wins

Concrete prompt sequence for driving `DES-46` / `HU-34` through the full backend + frontend slice
on `feature/hu-34-trivia-team-answer-first-write-wins`. Follows
[workflow_for_prompts.md](./workflow_for_prompts.md).

**Key difference for HU-34:** unlike HU-33A/33B's runtime-orchestration patterns, this slice is an
intake-and-validation slice. The invariant is not "advance the round" but "accept exactly one
in-time answer per team for the active question and reject every later/late attempt with one
reused validation sequence." The merged `DES-46` already absorbed `DES-47`; do not split the
accept/reject branches back apart.

When working from the monorepo root, make the target workload explicit in each prompt.
For backend steps, point to `@backend/.agents/backend-agent.md`. For frontend steps,
point to `@frontend/AGENTS.md`. Do not ask for backend and frontend implementation in
the same phase prompt; coordinate them as separate scoped steps tied together by the
verified API contract.

---

## Required design patterns

- `Template Method`
  - Why: `HU-34` must keep one stable answer-registration workflow under trivia's synchronized
    timer window while allowing success/rejection branches to share the same ordered steps.
  - Phase owner: X.1 Domain + X.2 Application.
  - Gate obligation: one fixed skeleton handles session/runtime admission, active question, timer
    window, first-write-wins, base evidence + trivia specialization, and fact publication. No
    split accept/reject handlers.

- `Chain of Responsibility`
  - Why: trivia answer acceptance and rejection must be composed from ordered validators instead of
    one large handler branch chain.
  - Phase owner: X.2 Application.
  - Gate obligation: ordered links validate runtime participation, active question/substage mode,
    timer window, and duplicate-team-answer; short-circuit on first failure.

Transport obligations:
- **SignalR** — broadcast an operator-only answered indicator with no option/correctness leakage
- **RabbitMQ** — publish `AnswerRegistered` after transactional success through the existing
  service-owned publisher seam

---

## Pre-resolved orient (as of 2026-07-09)

> Step 1 has already been run. Paste this section into any agent session that needs context
> before picking up a phase; no need to re-run the orient prompt unless source or Linear state changed.

### What predecessors have already landed

`DES-46` is a standard feature build on top of the realigned session runtime:

- **DES-78 / HU-33A** — synchronized trivia substage orchestration, active-question runtime,
  `QuestionActivated` / `QuestionClosed` / `SubstageAdvanced`
- **DES-45 / HU-33B** — existing RabbitMQ publisher seam and event-bridge pattern
- **DES-77 / HU-22** — authoritative timer keyed to the active trivia question only
- **DES-76 / HU-21A** — lifecycle gates (`Paused` / `Finished` / `Cancelled`)
- **DES-11 / DES-12** — participant admission/reconnect seams already define the runtime team/session context

`DES-47` is **Canceled** and merged into `DES-46`; do not cite or branch from it.
No same-service predecessor is currently In Progress, so the branch base is `develop`.

### What HU-34 adds on top

| Concern | New work |
|---|---|
| Trivia evidence | `EvidenceSubmission` base + `TriviaAnswerSubmission` specialization inside `LiveSession` |
| First-write-wins | accept the first valid answer per team/question; reject duplicates and late attempts |
| Fixed workflow | one stable answer-registration skeleton; later shared extraction to HU-29/HU-30A |
| Ordered validators | runtime participation, active question, timer window, duplicate-team-answer |
| Accepted-answer facts | operator-only answered signal + `AnswerRegistered` async publish |
| Frontend | participant answer-submission surface and contract plumbing |

### Branch state and prerequisite

`feature/hu-34-trivia-team-answer-first-write-wins` should be branched from `develop`.
No same-service predecessor is currently **In Progress**, so there is no feature-branch
dependency to inherit first.

### Linear state (as of 2026-07-09)

- DES-46 (HU-34): **Todo**, labels: `svc:session-operations-service`, `Feature`, `ready-for-agent`
- DES-70 (PRD): **Backlog**, labels: `svc:session-operations-service`, `canon-realign`, `ready-for-agent`
- Same-service Done build-on predecessors: DES-78, DES-45, DES-77, DES-76

> Linear live state may have changed. Use the Linear MCP to verify DES-46 status and labels if
> needed, but do not re-fetch PRD scope — read the local PRD file at
> `@backend/docs/prd/DES-70-primera-implementacion-de-session-operations-service-hu-15-a-hu-36.md`.

---

## 1. Orient — read service state and the resolved HU-34 context

> **Skip this step if you have read the pre-resolved orient section above.** Run it only if
> the service source, README, or Linear state may have changed since 2026-07-09.

```text
Read the following files and summarize what has already landed and what HU-34 must add:
- @backend/services/session-operations-service/README.md
- @backend/services/session-operations-service/CONTEXT.md
- @backend/docs/prd/DES-70-primera-implementacion-de-session-operations-service-hu-15-a-hu-36.md
- @backend/docs/canon-realignment-after-mission-runtime-rewrite.md
- @backend/docs/hu34-context.md

Then inspect only the current session-operations answer/runtime seams you need to anchor on:
- @backend/services/session-operations-service/src/Domain/Entities/LiveSession.cs
- @backend/services/session-operations-service/src/Application/Sessions/Common/TriviaRoundOrchestratorFacade.cs
- @backend/services/session-operations-service/src/Application/Sessions/Common/RuntimeParticipationGuard.cs
- @backend/services/session-operations-service/src/Infrastructure/Messaging/RabbitMqIntegrationEventPublisher.cs
- @backend/services/session-operations-service/src/Api/Hubs/SessionsHub.cs

Then use the Linear MCP to fetch only the current live state of:
- DES-46 (HU-34 - Registro y rechazo de respuestas de equipo en trivia)
- DES-70 (PRD - SessionOperations)

Output:
- the live DES-46 status and labels
- confirmation that DES-47 is merged/canceled and not cited
- the direct build-on seams (HU-33A/33B + HU-22/21A)
- the accepted generation decision: inline the trivia-specific intake now rather than waiting for HU-29/HU-30A
- the transport/privacy constraint: the answered indicator must be operator-only, not broadcast to participants

Do not start planning or implementing yet.
```

---

## 2. Label DES-46 as ready-for-agent

```text
Use the Linear MCP to confirm DES-46 still carries the label ready-for-agent.
If it is missing, add it.
Output the updated DES-46 ticket state and labels.
```

---

## 3. Confirm slice readiness

```text
Use the Linear MCP to confirm DES-46 carries both svc:session-operations-service
and ready-for-agent labels and output its current status and acceptance criteria.

The PRD scope is already in the local file at
@backend/docs/prd/DES-70-primera-implementacion-de-session-operations-service-hu-15-a-hu-36.md.
Do not re-fetch PRD scope from Linear.

Before planning, explicitly confirm:
- DES-47 is canceled / merged and is not separate scope
- HU-34 is one first-write-wins invariant, not two deliverables
- the trivia intake is inlined now (shaped for later HU-29/HU-30A extraction)
- the answered indicator is operator-only and leaks neither option nor correctness
- RabbitMQ reuses the existing service-owned publisher seam

Output the confirmed HU id, title, acceptance criteria, labels, and the guard confirmation before planning the slice.
```

In the remaining examples below, `HU-34` and `DES-46` are the resolved values for this slice.
`DES-70` is the shared PRD reference for `session-operations-service`.

---

## 4. Start the slice

```text
Prepare the HU-34 slice on branch feature/hu-34-trivia-team-answer-first-write-wins.
Use the HU id and DES id resolved from Linear in the previous step.
This slice affects backend session-operations-service and frontend.

The pre-resolved orient at the top of this document lists what existing code has already
landed and what HU-34 adds. Do not re-read the PRD for scoping unless you need to resolve
a precise implementation detail.

Move DES-46 to In Progress if the team process requires it, and output the exact scope,
branch name, base branch, and touched surfaces.
```

---

## 5. Backend phase X.1 — Domain layer

```text
Use @backend/.agents/backend-agent.md.
Implement backend phase X.1 for HU-34 in session-operations-service, per the
**X.1 derivation block in @backend/docs/hu34-context.md** (your spec — do not
re-read the canon or re-inspect the tree; open a cited canon section only to
fill a gap the block leaves open).

Gate:
- Domain build passes; unit tests cover every new public domain type
- first valid answer per team/question is accepted exactly once
- duplicate and late answers are rejected through the same fixed workflow
- paused/finished/cancelled or no-active-question submissions are rejected
- accepted answer snapshots correctness/score internally and raises `AnswerRegisteredEvent` only on success
- Template Method verified: one answer-registration skeleton, no split accept/reject implementations

Do not touch other backend layers or frontend.
```

Commit:

```text
feat(session-operations): phase X.1 - domain layer (HU-34)

Ref: HU-34
Ref: DES-46
Ref: DES-70
```

---

## 6. Backend phase X.2 — Application layer

```text
Use @backend/.agents/backend-agent.md.
Implement backend phase X.2 for HU-34 in session-operations-service, per the
**X.2 derivation block in @backend/docs/hu34-context.md** (your spec — do not
re-read the canon or re-inspect the tree; open a cited canon section only to
fill a gap the block leaves open).

Gate:
- clean build passes; handler + validator tests cover valid path plus each rejection branch
- `SubmitTriviaAnswer` is one vertical slice, not split into accept/reject handlers
- Chain of Responsibility verified: ordered links validate runtime participation, active question, timer window, duplicate-team-answer; short-circuit on first failure
- accepted-answer result leaks no correctness/points to the participant response
- `AnswerRegistered` is bridged onto RabbitMQ through the existing publisher seam and `TeamAnswered` is bridged onto the operator-only SignalR contract

Do not touch Infrastructure, Api, or frontend.
```

Commit:

```text
feat(session-operations): phase X.2 - application layer (HU-34)

Ref: HU-34
Ref: DES-46
Ref: DES-70
```

---

## 7. Backend phase X.3 — Infrastructure layer

```text
Use @backend/.agents/backend-agent.md.
Implement backend phase X.3 for HU-34 in session-operations-service, per the
**X.3 derivation block in @backend/docs/hu34-context.md** (your spec — do not
re-read the canon or re-inspect the tree; open a cited canon section only to
fill a gap the block leaves open).

Gate:
- infrastructure build passes
- `ef migrations add` succeeds for the new trivia-answer evidence persistence
- accepted-answer persistence round-trips through the existing session aggregate repository
- the uniqueness rule (one accepted answer per team/question/session) is enforced at the persisted model boundary
- RabbitMQ transport reuses the existing service-owned publisher seam; no second publisher stack or exchange bootstrap is introduced

Do not touch Api or frontend.
```

Commit:

```text
feat(session-operations): phase X.3 - infrastructure layer (HU-34)

Ref: HU-34
Ref: DES-46
Ref: DES-70
```

---

## 8. Backend phase X.4 — Api layer

```text
Use @backend/.agents/backend-agent.md.
Implement backend phase X.4 for HU-34 in session-operations-service, per the
**X.4 derivation block in @backend/docs/hu34-context.md** (your spec — do not
re-read the canon or re-inspect the tree; open a cited canon section only to
fill a gap the block leaves open).

Gate:
- participant answer endpoint returns acceptance metadata only on first valid answer
- repeat and late attempts return consistent RFC 7807 ProblemDetails
- operator-only `TeamAnswered` SignalR notification leaks neither option nor correctness and does not reach participant connections
- RabbitMQ `AnswerRegistered` publish is observable after transactional success
- service coverage passes the repo gate

Do not touch frontend.
```

Commit:

```text
feat(session-operations): phase X.4 - api layer (HU-34)

Ref: HU-34
Ref: DES-46
Ref: DES-70
```

---

## 8.5. Docker rebuild

```text
Rebuild and restart the backend runtime for HU-34:

docker compose build session-operations-service api-gateway
docker compose up -d session-operations-service api-gateway

Then smoke the new write path and transports through the gateway:
- first valid participant answer -> accepted response
- second attempt or late attempt -> RFC 7807 rejection
- operator-only `TeamAnswered` signal arrives and does not leak to participant connections
- `AnswerRegistered` publish is observable on the RabbitMQ exchange
```

---

## 9. Frontend slice — generate the implementation plan

```text
Generate a multi phase plan in a markdown file — following the **frontend plan concreteness rule** (below), modelled on the exemplar closest to this slice's shape (`@frontend/plans/hu-03-frontend-role-permission-assignment.md` for a small 1–few-endpoint surface; `@frontend/plans/hu-10a-frontend-mission-hierarchy-authoring.md` for a large/multi-endpoint or partially-blocked surface) — save it in `@frontend/plans/` for the following:
Use @frontend/AGENTS.md.

Backend contract to plan against:
- new participant write endpoint for the accepted answer path
- accepted-answer response returns acceptance metadata only
- new operator-only `TeamAnswered` SignalR notification
- existing timer / `QuestionActivated` / `QuestionClosed` / `SubstageAdvanced` contracts remain in force

Existing verified frontend anchors to inspect before writing the plan:
- `frontend/app/lib/definitions.ts` — current timer + trivia round DTOs already exist
- `frontend/app/actions/sessions.ts` — current operator timer action pattern
- `frontend/app/lib/realtime/session-state-client.ts` and `frontend/app/lib/realtime/use-trivia-round-state.ts` — current SignalR trivia-runtime plumbing
- `frontend/app/dashboard/DashboardClient.tsx`, `TriviaRoundPanel.tsx`, `OperatorSessionTimerPanel.tsx` — current operator runtime surface
- `frontend/app/dashboard/DashboardClient.tsx` participant branch is still a minimal placeholder

Scope the frontend plan to:
- add the participant-side answer submission flow on top of the existing trivia runtime
- define the DTO / server-action / client-call contract for the new answer endpoint
- wire the accepted-answer response into participant state without leaking correctness/points
- consume the operator-only `TeamAnswered` signal where the current source supports it
- if the full operator answered/not-answered UI is still downstream HU-36A scope, record that as an explicit boundary and keep this slice to contract/state plumbing only
- do not modify backend code in the frontend plan

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

Gate:
- the plan reflects the verified backend contract and real frontend anchors
- participant answer flow is the primary frontend surface
- any operator monitor UI work beyond the transport/state seam is either concretely sourced or explicitly left to HU-36A
- no backend code changes are included

Commit message:

feat(frontend): team trivia answer first-write-wins — HU-34

Ref: HU-34
Ref: DES-46
Ref: DES-70
```

---

## 9b. Implement the frontend plan

```text
Use @frontend/AGENTS.md and the Step 9 plan path.
Implement phase by phase in the plan's order, per the plan's own Scope / Gate / Commit Sequence.

The Step 9 plan is the source of truth and supersedes the Step 9 seed scope.
Stop at any increment the plan marks blocked on an Open Question (name it).
Do not re-generate the plan.
Do not modify backend code.
```

---

## 10. Close-out

```text
Before opening the PR, confirm all DES-46 acceptance criteria are satisfied:
- any authenticated team member can submit the active question answer
- the first valid in-time answer is recorded as the team's final answer for that question
- the accepted answer is associated with team, session, question, and timestamp
- the registration respects active-session context
- the "team answered" indicator is pushed to operator monitoring in real time without revealing the option
- after transactional success, `AnswerRegistered` is published for asynchronous consumers
- late answers are rejected
- repeated attempts for the same team/question are rejected
- the final accepted answer is not overwritten
- the rejection reason is consistent

Then open the PR:

gh pr create \
  --base develop \
  --head feature/hu-34-trivia-team-answer-first-write-wins \
  --title "feat(session-operations): HU-34 team trivia answer first-write-wins" \
  --body "Implements HU-34 / DES-46: participant answer submission for the synchronized active trivia question with first-write-wins semantics, typed late/duplicate rejection, accepted-answer transport to operator-only SignalR and RabbitMQ, and the paired frontend participant answer flow. Reuses the existing HU-33 runtime/timer seams and HU-33B RabbitMQ publisher. DES-47 remains merged/canceled and is not cited as separate scope."
```

---

## Rationale

- **Why the pattern set changes from HU-33A/33B:** HU-33A/33B orchestrate lifecycle progression and
  outbound publication, so they center on `Facade`/`State`/`Strategy`. HU-34 is a synchronous
  intake-and-validation slice under one tight invariant, so `Template Method` + `Chain of
  Responsibility` are the right obligations.
- **Why the intake is inlined instead of waiting for HU-29/HU-30A:** `workflow_refactor.md`
  explicitly calls out that HU-29/HU-30A are not blocker edges for DES-46. The canonical move is
  to land the trivia-specific path now, but shape it so the later shared `EvidenceSubmission`
  pipeline can extract it cleanly.
- **Why the answered signal is operator-only:** `SessionsHub` currently places both operators and
  participants in `live-session:{id}`. Reusing that group would leak supervision state to players.
  HU-34 must introduce the privacy boundary now; HU-36A can build the full monitor UI on top of it later.
- **Why the RabbitMQ contract follows the current service seam:** the historical sprint handoff
  froze an older exchange/routing-key sketch, but the current codebase already standardized on a
  service-owned publisher seam in HU-33B. HU-34 stays coherent with the as-built transport.

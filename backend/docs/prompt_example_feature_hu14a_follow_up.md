# Prompt Example — HU-14A follow-up RemoveTriviaQuestion (Feature Slice)

Concrete prompt sequence for driving DES-80 (HU-14A follow-up) through a full slice on `feature/hu-14a-follow-up-remove-trivia-question`. Follows the pattern in [workflow_for_prompts.md](./workflow_for_prompts.md).

**Key difference from HU-14A:** HU-14A already shipped trivia question and option authoring, but it deliberately deferred per-question removal because the aggregate had no removal slot. DES-80 is that deferred follow-up: it opens the question-removal seam on top of the landed trivia authoring baseline, reusing the same `Template Method` authoring workflow rather than rebuilding question management.

When working from the monorepo root, make the target workload explicit in each prompt.
For backend steps, point to `@backend/.agents/backend-agent.md`. For frontend steps,
point to `@frontend/AGENTS.md`. Do not ask for backend and frontend implementation in
the same phase prompt; coordinate them as separate scoped steps tied together by the
verified API contract.

---

## Required design patterns

- `Template Method`
  - Why: question authoring uses a shared validation flow with question-specific steps; remove must extend that same stable workflow rather than creating a detached rule path.
  - Phase owner: X.1 Domain and X.2 Application
  - Gate obligation: the fixed trivia-question authoring workflow must stay explicit in phase scope and in the gate for both phases; remove must reuse that workflow with remove-specific steps instead of duplicating or scattering rule checks.

> Applies-where note (no new gate): `DELETE /api/trivias/{triviaQuizId}/questions/{questionId}` is a protected administrator mutation, but this slice is not matrix-tagged for `Proxy` and not in the applies-where set. It inherits the standard trivia `[Authorize]` / `AuthorizationBehaviour` guard.

---

## Pre-resolved orient (as of 2026-07-11)

> Step 1 has already been run. Paste this section into any agent session that needs context
> before picking up a phase - no need to re-run the orient prompt.

### What predecessors have already landed

DES-80 is **In Progress** and builds on the landed trivia authoring baseline. Its build-on predecessors are all **Done**: DES-17 (HU-11), DES-20 (HU-14A), DES-21 (HU-14B), DES-18 (HU-12), and DES-19 (HU-13). No same-service predecessor is currently In Progress, so the branch base is `develop`.

**Domain layer**

- `TriviaQuiz` already exists as the trivia-side aggregate root baseline from HU-11
- HU-14A added `TriviaQuestion` / `TriviaOption` authoring via `AddQuestion` / `UpdateQuestion`
- HU-14A explicitly recorded that `RemoveTriviaQuestion` was deferred because no per-question removal slot existed yet
- HU-14B landed the explicit question validity baseline; remove must preserve that model
- HU-12 established that published/archived quizzes are not editable authoring states
- HU-13 already distinguishes question/quiz authoring from whole-quiz duplicate/retire/destructive-remove behavior

**Application layer**

- trivia-side create/update/query use cases and repository/read-model abstractions already exist
- add/update question command slices and the shared `Trivias/Common/Authoring/` seam are the direct mirror for this slice
- authorization and validation behaviours are already part of the service pipeline

**Infrastructure / API**

- trivia question/option persistence and mutation endpoints already exist from HU-14A
- current API surface already has add/update question routes and whole-quiz delete, but no per-question `DELETE`
- the existing persistence model should already support question/option storage; verify whether question removal needs any configuration change before adding a migration

**Frontend**

- trivia quiz create/edit/detail UI already exists
- question and option authoring UI already exists from HU-14A and is the direct baseline for adding question removal

**Coverage:** no stable aggregate percentage is captured in predecessor docs for `mission-design-service`; verify the real service coverage during phase X.4.

### What HU-14A follow-up adds on top (per DES-80 and PRD DES-62)

| Concern | New work |
|---|---|
| Per-question deletion | Add the missing remove-question capability on top of the existing trivia question authoring baseline |
| Domain event | Raise `TriviaQuestionRemoved` on successful removal |
| Sequence integrity | Reconcile remaining `TriviaQuestion.sequenceOrder` values after deletion |
| Lifecycle guard | Reject removal when the quiz is published or archived |
| Backend contract | Add `DELETE /api/trivias/{triviaQuizId}/questions/{questionId}` and preserve the existing trivia mutation/detail response contract |
| Frontend flow | Extend the quiz authoring UI with question removal against the verified delete contract |

### Branch state and prerequisite

`feature/hu-14a-follow-up-remove-trivia-question` should be branched from `develop`. All build-on dependencies are Done; no same-service predecessor is In Progress.

**Before starting:** confirm the current source already has the trivia question add/update baseline (`TriviaQuiz.AddQuestion` / `UpdateQuestion`, application command slices, persistence, and controller endpoints). Extend that baseline for per-question removal; do not rebuild question authoring or whole-quiz delete semantics.

### Linear state (as of 2026-07-11)

- DES-80 (HU-14A follow-up): **In Progress**, labels: `Feature`, `ready-for-agent`, `svc:mission-design-service`
- DES-17 (HU-11), DES-20 (HU-14A), DES-21 (HU-14B), DES-18 (HU-12), DES-19 (HU-13): **Done**
- DES-62 (PRD): local file authority `@backend/docs/prd/DES-62-mission-design-service-baseline.md`

> Linear live state may have changed. Use the Linear MCP to verify DES-80 status and labels if needed, but do not re-fetch PRD scope - read the local file at `@backend/docs/prd/DES-62-mission-design-service-baseline.md` instead.

---

## 1. Orient — read service state and local PRD before planning

> **Skip this step if you have read the pre-resolved orient section above.** Run it only
> if the service source or Linear state may have changed since 2026-07-11.

```text
Read the following and summarise what is already implemented vs. what DES-80 adds:
- @backend/docs/hu14a-follow-up-context.md - the pre-resolved DES-80 context (primary)
- @backend/docs/hu14a-context.md - the original HU-14A context that recorded the deferral
- @backend/docs/hu12-context.md / @backend/docs/hu13-context.md - the landed trivia lifecycle and reuse baseline
- @backend/docs/prd/DES-62-mission-design-service-baseline.md - authoritative local PRD note for RemoveTriviaQuestion
- @backend/services/mission-design-service/CONTEXT.md - bounded-context language and pattern expectations

Then inspect the existing trivia-side source only enough to confirm the current baseline:
- @backend/services/mission-design-service/src/Domain/Entities/TriviaQuiz.cs
- @backend/services/mission-design-service/src/Application/Trivias/
- @backend/services/mission-design-service/src/Infrastructure/Persistence/
- @backend/services/mission-design-service/src/Api/Controllers/TriviasController.cs

Then use the Linear MCP to fetch only the current live state of DES-80.

Output:
- what same-service trivia baseline already exists from HU-11 / HU-14A / HU-14B / HU-12 / HU-13
- what DES-80 adds on top: per-question remove slot, TriviaQuestionRemoved, sequence-order reconciliation, edit-blocked guard, delete endpoint
- whether current persistence/config already supports per-question delete without schema changes
- current Linear status and labels for DES-80

Do not start planning or implementing yet.
```

---

## 2. Label DES-80 as ready-for-agent

```text
Use the Linear MCP to confirm DES-80 still carries the label ready-for-agent.
If it is missing, add it.
Output the updated DES-80 ticket state and labels.
```

---

## 3. Confirm slice readiness

```text
Use the Linear MCP to confirm DES-80 carries both svc:mission-design-service
and ready-for-agent labels and output its current status and acceptance criteria.

The PRD scope is already in the local file at
@backend/docs/prd/DES-62-mission-design-service-baseline.md.
Do not re-fetch PRD scope from Linear.

Output the confirmed HU id, title, acceptance criteria, and labels before planning the slice.
```

In the remaining steps, `HU-14A` and `DES-80` are the resolved values for this follow-up slice. `DES-62` is the shared mission-design PRD reference in the local file above.

---

## 4. Start the slice

```text
Prepare the trivia-question-removal follow-up slice on branch
feature/hu-14a-follow-up-remove-trivia-question.
Use the HU id and DES id resolved from Linear in the previous step.
This slice affects backend mission-design-service and frontend.

The pre-resolved orient at the top of this document lists what the landed trivia baseline
already provides and what DES-80 adds. Do not re-read the PRD for scoping unless you need a
precise implementation detail.

Before implementation, confirm the current service source already has the trivia question
add/update baseline from HU-14A and the lifecycle/editability baseline from HU-12/HU-13.
Extend that baseline for per-question removal; do not create parallel trivia abstractions or
alter whole-quiz delete/retire semantics.

Move DES-80 to In Progress and output the exact scope, branch name, base branch, and touched surfaces.
```

---

## 5. Backend phase X.1 — Domain layer

```text
Use @backend/.agents/backend-agent.md.
Implement backend phase X.1 for HU-14A follow-up in mission-design-service, per the
**X.1 derivation block in @backend/docs/hu14a-follow-up-context.md** (your spec — do not
re-read the canon or re-inspect the tree; open a cited canon section only to fill a gap the block leaves open).

Gate:
- Domain build passes; unit tests lock successful question removal, sequence-order reconciliation, missing-question rejection, and published/archived edit-blocked rejection
- removing a question also removes its options through the aggregate's existing composition model
- Gate: the domain realizes remove through the existing `Template Method` trivia-question authoring workflow, with one stable skeleton and remove-specific steps, not a duplicated remove-only rule path

Do not touch other backend layers or frontend.
```

Commit:

```text
feat(mission-design): phase X.1 — domain layer (HU-14A)

Ref: HU-14A
Ref: DES-80
Ref: DES-62
```

---

## 6. Backend phase X.2 — Application layer

```text
Use @backend/.agents/backend-agent.md.
Implement backend phase X.2 for HU-14A follow-up in mission-design-service, per the
**X.2 derivation block in @backend/docs/hu14a-follow-up-context.md** (your spec — do not
re-read the canon or re-inspect the tree; open a cited canon section only to fill a gap the block leaves open).

Gate:
- Application build passes; handler tests cover valid remove, missing quiz, missing question, and edit-blocked rejection
- validator tests cover valid and invalid ids
- administrator-only authorization is preserved for the mutation
- Gate: remove validation/authoring is enforced through the same stable `Template Method` workflow family used by trivia question authoring, not a detached remove-only rule sequence

Do not touch Infrastructure, Api, or frontend.
```

Commit:

```text
feat(mission-design): phase X.2 — application layer (HU-14A)

Ref: HU-14A
Ref: DES-80
Ref: DES-62
```

---

## 7. Backend phase X.3 — Infrastructure layer

```text
Use @backend/.agents/backend-agent.md.
Implement backend phase X.3 for HU-14A follow-up in mission-design-service, per the
**X.3 derivation block in @backend/docs/hu14a-follow-up-context.md** (your spec — do not
re-read the canon or re-inspect the tree; inspect the current trivia persistence mapping first and add a migration only if the mapping truly needs one).

Gate:
- Infrastructure build passes
- repository integration test proves question removal persists correctly and the remaining question order round-trips coherently
- migration/model snapshot remain consistent; if no schema change is needed, prove the no-op rather than inventing one

Do not touch Api or frontend.
```

Commit:

```text
feat(mission-design): phase X.3 — infrastructure layer (HU-14A)

Ref: HU-14A
Ref: DES-80
Ref: DES-62
```

---

## 8. Backend phase X.4 — Api layer

```text
Use @backend/.agents/backend-agent.md.
Implement backend phase X.4 for HU-14A follow-up in mission-design-service, per the
**X.4 derivation block in @backend/docs/hu14a-follow-up-context.md** (your spec — do not
re-read the canon or re-inspect the tree; open a cited canon section only to fill a gap the block leaves open).

Gate:
- endpoint tests pass for successful delete, non-admin rejection, missing quiz/question not-found, and edit-blocked rejection
- the endpoint uses the verified trivia mutation/detail response shape and shows the updated question list/order after deletion
- no regression on the existing trivia add/update/publish/archive or whole-quiz delete endpoints
- repo coverage gate passes for `mission-design-service`

Do not touch frontend.
```

Commit:

```text
feat(mission-design): phase X.4 — api layer (HU-14A)

Ref: HU-14A
Ref: DES-80
Ref: DES-62
```

---

## 8.5. Docker rebuild and smoke

```text
Rebuild and restart the local backend stack for mission-design verification:

1. Run `docker compose -f backend/docker-compose.yml build mission-design-service api-gateway`
2. Run `docker compose -f backend/docker-compose.yml up -d mission-design-service api-gateway`
3. Wait for services to become healthy/started
4. Smoke test through the gateway/service route after auth is in place:
   - `DELETE /api/trivias/{triviaQuizId}/questions/{questionId}` as admin -> expect success and an updated trivia detail/mutation response with the question removed and remaining order reconciled
   - the same route as non-admin -> expect `403`
   - deleting a missing quiz/question -> expect not-found ProblemDetails

Output:
- build result
- container status
- smoke command results
```

---

## 9. Frontend slice

```text
Generate a multi phase plan in a markdown file — following the **frontend plan concreteness rule** (below), modelled on the exemplar closest to this slice's shape (`@frontend/plans/hu-03-frontend-role-permission-assignment.md` for a small 1–few-endpoint surface; `@frontend/plans/hu-10a-frontend-mission-hierarchy-authoring.md` for a large/multi-endpoint or partially-blocked surface) — save it in `@frontend/plans/` for the following:
Use @frontend/AGENTS.md.
Implement the frontend slice for HU-14A follow-up question removal.

This is a focused single-endpoint contract addition on top of an existing trivia authoring screen — choose the hu-03 exemplar.

Scope:
- extend the existing trivia quiz authoring UI so an administrator can remove a question from an editable quiz
- keep the remaining rendered questions in the reconciled order returned by the verified backend contract
- hide or disable remove controls when the quiz is not editable, aligned with the backend lifecycle contract
- preserve the existing add/update question authoring flow without regressions

Gate:
- frontend uses the verified backend delete contract, not guessed payloads
- unauthorized users cannot access the remove mutation UI
- removing a question updates the rendered list/order in the intended detail/edit flow
- no regression on existing HU-11 / HU-14A / HU-12 / HU-13 trivia administration behavior

Do not modify backend code in this step.
```

**Frontend plan concreteness rule (embed verbatim in the generated plan's altitude choice):**

1. **Proportion concreteness to certainty.** Write code-complete detail — exact DTO/request types, real component skeletons, exact client-fn + server-action bodies, a `data-testid` contract — only for the **fully-knowable near-term increments** (typically the foundation + first authoring increment). Keep later, large, or blocked increments at **contract + gate altitude**: a contract table, scope, and gate, with no invented bodies. Never write code for an increment blocked on an open question.
2. **Verify every code anchor against the real source before writing it.** Open the files the plan names — exported vs. private helpers, exact signatures, the const/env it reads, the line a refactor targets — and write only what the source actually supports. A confident-but-wrong anchor (e.g. "reuse `getIdentityHeaders`" when it is not exported) is worse than an altitude note. If a detail is not verifiable, state the assumption under Open Questions rather than inventing it.
3. **Required sections** (both exemplars carry these; a plan missing one is a defect): Context · Verified Backend Contract (endpoint/shape table) · Architecture Decisions · **Environment** (env vars / config consts reused) · **data-testid contract** · phased Scope + Gate per increment · **Acceptance-criteria → test mapping** · Open Questions / Dependencies · Out of Scope.
4. **Final forms only, sequential by default.** Write only the final version of each anchor — no "wrong → revised" trails — and keep increments sequential unless the slice genuinely parallelizes.

Commit:

```text
feat(frontend): remove trivia question — HU-14A

Ref: HU-14A
Ref: DES-80
Ref: DES-62
```

---

## 9b. Implement the frontend plan

> Run only after the Step 9 plan is written and reviewed. The plan is the source of truth and supersedes the Step 9 seed scope — including Step 9's single seed commit: commit per the plan's own per-phase Commit Sequence, not the one above.

```text
Use @frontend/AGENTS.md. Implement the Step 9 plan at @frontend/plans/hu-14a-follow-up-remove-trivia-question.md,
phase by phase in the plan's order, per the plan's own Scope / Gate / Commit Sequence.

STOP at any increment the plan marks blocked on an Open Question (name it). Do not re-generate the plan.
Do not modify backend code in this step.
```

---

## 10. Close-out

```text
Use the Linear MCP to re-check DES-80 acceptance criteria and labels.
Verify the final implementation against the slice acceptance criteria:
- an administrator can remove a question from an editable quiz, and the question/options no longer exist while remaining order stays consistent
- removing a question from a published/archived quiz is rejected with a clear edit-blocked domain path
- missing quiz/question lookups return not-found
- the domain emits TriviaQuestionRemoved on successful delete
- remove validation reuses the same trivia authoring Template Method workflow instead of a duplicated rule path
- coverage includes domain remove/order/state-guard tests, handler/validator tests, and a delete-endpoint smoke

Run final backend and frontend verification required by the repo instructions.
Summarise:
- commits created
- backend API contract changes
- frontend plan/file produced
- tests and gates run
- any follow-on work intentionally left out of DES-80

Create the PR:

gh pr create \
  --base develop \
  --head feature/hu-14a-follow-up-remove-trivia-question \
  --title "feat(mission-design): add trivia question removal follow-up" \
  --body-file backend/docs/hu14a-follow-up-context.md
```

---

## Rationale

DES-80 exists because HU-14A's question-authoring slice stopped at add/update by design, not by accident. The PRD already named `RemoveTriviaQuestion`, but the original aggregate did not expose a per-question removal slot, so HU-14A recorded the deferral instead of inventing new domain behavior mid-slice. This follow-up is intentionally narrow: add the missing `RemoveQuestion` aggregate behavior, the `TriviaQuestionRemoved` fact, order reconciliation, and the delete contract on top of the landed trivia baseline.

The required pattern stays `Template Method` for the same reason as HU-14A: the risk is validation drift. Question removal can easily become a one-off handler/controller branch that re-implements editability and lookup sequencing separately from the existing authoring flow. DES-80 keeps one stable trivia-question authoring workflow and extends it with remove-specific steps, instead of creating a detached rule path.

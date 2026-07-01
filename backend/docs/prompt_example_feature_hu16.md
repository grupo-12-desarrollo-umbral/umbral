# Prompt Example - HU-16 Realineacion: runtime-snapshot fidelity de la subetapa de trivia (Feature Slice)

Concrete prompt sequence for driving DES-75 (HU-16 realign) through a full slice on `feature/hu-16-runtime-snapshot-trivia-substage`. Follows the pattern in [workflow_for_prompts.md](./workflow_for_prompts.md).

> Supersedes the old DES-23 HU-16 ("crear sesion de trivia desde un quiz publicado"). That model - quiz-as-`SessionSource`, session-level `SessionMode`, a standalone trivia session - is retired. Trivia is now a `Substage` of the mission that assigns one **whole** published `TriviaQuiz` (`TriviaQuizSelection`), and its questions are frozen into the immutable `MissionRuntimeSnapshot` when the `LiveSession` is created.

**Key difference for the DES-75 slice:** this is **not** a fresh rebuild. HU-15 (DES-22, Done) already ships the immutable `MissionRuntimeSnapshot` **including the full trivia-question copy** (`TriviaQuestionSnapshot`/`TriviaOptionSnapshot` VOs, `CreateSessionFacade.BuildMissionRuntimeSnapshot`), and HU-17 (DES-24, Done) already locked the single-source invariant and tore out the two-source debris. HU-16 **locks the snapshot-content-fidelity invariant** - the whole published quiz is frozen (all questions/options/correct/score/timer, no partial `TriviaQuestionSelection`) - with explicit tests across all four layers, and retires the last stale quiz-as-source docs. It adds **no** new aggregate, endpoint, or migration, and there is **nothing to delete in code** (HU-15/HU-17 already did the teardown). Treat every phase as verification + test-locking, not re-implementation.

When working from the monorepo root, make the target workload explicit in each prompt.
For backend steps, point to `@backend/.agents/backend-agent.md`. For frontend steps,
point to `@frontend/AGENTS.md`. Do not ask for backend and frontend implementation in
the same phase prompt; coordinate them as separate scoped steps.

---

## Stop 1 acceptance guard

Reject this generated prompt before implementation if it does not explicitly scope all of the following:

- a trivia `Substage` assigns one **whole** published `TriviaQuiz` (`TriviaQuizSelection`); **all** its questions are copied into the immutable `MissionRuntimeSnapshot` at `LiveSession` creation - no partial or ordered-subset selection
- there is **no** `TriviaQuestionSelection` type and no partial-selection path anywhere in the model
- the snapshot copy carries every question's prompt/options/correct-answer/score/timer in strict mission order and is **immutable** after creation
- there is **no** standalone trivia-session / quiz-as-source creation route (a `TriviaQuiz` cannot create a `LiveSession`)
- the immutable snapshot copy is orchestrated through the **single** `CreateSessionFacade` (`Facade`) - no ad-hoc handler snapshot logic, no second creation/snapshot path
- HU-15's snapshot machinery and HU-17's single-source lock are **verified and locked, not rebuilt**; DES-23's behaviour is superseded and documented

---

## Required design patterns

- `Facade` **(mandated)** - `required_patterns_matrix.md` HU-16 row: "Runtime snapshot orchestrates the immutable copy of the mission Composite tree (substages, targets, clues, questions) into `MissionRuntimeSnapshot`." The same row's canon note states the Linear HU-16 ticket (quiz-sourced trivia-session creation) is retired and "this Facade is realized via `HU-15`/`HU-17`." HU-16 owns the pattern at **X.2 Application** but adds **no new facade/pattern class** - it verifies the immutable snapshot copy rides on HU-15's single `CreateSessionFacade` and locks the fidelity obligation with a test. Canon home per ADR-0012: Application layer, in the slice it orchestrates (`Application/Sessions/Commands/CreateSession/`).

> Applies-where note (no new gate): `POST /api/sessions` is a protected mutation, but HU-16 is not in the applies-where `Proxy` set (HU-04/05/36B). It inherits the standard `Administrator` `AuthorizationBehaviour`/gateway guard (ADR-0001/0002) - note only, no new `Proxy` gate. `LiveSession` lifecycle `State` is HU-21A's scope (DES-76); HU-16 only reads the initial `Scheduled` - no `State` gate.

---

## Pre-resolved orient (as of 2026-06-30)

> Step 1 has already been run. Paste this section into any agent session that needs context
> before picking up a phase - no need to re-run the orient prompt.

### What predecessors have already landed

DES-75 (HU-16 realign) is **Todo**, a `needs-rebuild` row in the realignment map (phase #4 "Session creation": "Trivia selection as a `Substage` (`TriviaQuizSelection`), not a session"), blocked by DES-22 + DES-24. It **supersedes DES-23** (the old HU-16, Canceled).

- **DES-22 (HU-15) is Done (2026-06-21)** and shipped the immutable `MissionRuntimeSnapshot` **including the full trivia-content copy**: `MissionRuntimeSnapshot` (owned by `LiveSession`) with `StageSnapshots`/`TargetSnapshots`/`TriviaQuestionSnapshots`; the `TriviaQuestionSnapshot`/`TriviaOptionSnapshot`/`SubstageSnapshot` VOs; `CreateSessionFacade.BuildMissionRuntimeSnapshot` copying every resolved question/option in mission order (`CreateSessionFacade.cs:114-130`); `IMissionRuntimeSource` over `GET /api/missions/{id}/runtime-plan`; the `20260621174437_AddMissionRuntimeSnapshot` migration; mission-only `POST /api/sessions`.
- **DES-24 (HU-17) is Done (2026-07-01)** and locked the single-source invariant (`Mission` is the only `SessionSource`; `TriviaQuiz` cannot create a `LiveSession`; no `SessionMode`) with per-layer tests, and finished the two-source teardown. It **kept** the live `TriviaQuestionSnapshot*` types/exceptions (reused inside `MissionRuntimeSnapshot`).

HU-16 is **session-operations-only** (single `svc:` label after the 2026-06-30 correction that dropped `svc:mission-design-service`) - the trivia-substage authoring (`TriviaQuizSelection`) is HU-10A's (DES-15, mission-design, Done); HU-16 consumes it via HU-15's runtime-plan read seam.

Superseded and excluded from the predecessor set: DES-23 (the old HU-16, this HU replaces it), HU-21A (DES-28->DES-76), HU-22 (DES-30->DES-77), HU-33A (DES-44->DES-78) - all Canceled. No same-service In Progress predecessors, so the branch base is `develop`.

### What HU-16 adds on top (per DES-75, the realignment map, and PRD DES-70)

| Concern | New work |
|---|---|
| Verification posture | Lock HU-15's snapshot-content copy as an explicitly tested fidelity invariant; retire the last stale docs. Do not rebuild creation or the snapshot. |
| Whole-quiz snapshot (domain) | Unit tests: a trivia `SubstageSnapshot` freezes the **entire** published quiz - every `TriviaQuestionSnapshot` (prompt, sequenceOrder, scoreValue [1,100], timeLimitSeconds [5,120], options + exactly-one-correct) in strict mission order; trivia substage >=1 question; snapshot immutable after `Create`. |
| No partial selection (domain) | Assert (test + grep) no `TriviaQuestionSelection` type and no partial/ordered-subset path exist - the substage takes the whole quiz. |
| Facade snapshot fidelity (application) | Test that `CreateSessionFacade.BuildMissionRuntimeSnapshot` copies the full resolved quiz into `TriviaQuestionSnapshots` (count + content parity), through the single `Facade`; no dropped questions, no second path. |
| Persistence fidelity (infra) | Repository round-trip test: a session from a trivia-bearing mission reloads with all trivia questions/options/correct-flags intact. No new migration. |
| API | Endpoint test: trivia-bearing mission -> 201; no standalone trivia-session / quiz-as-source route. Reshape nothing. |
| Documentation supersession | Retire the stale quiz-as-source block `faq/workflow-and-sprint-planning.md:167-173`; confirm the superseded banners on the overwritten HU-16 docs (DES-75 AC #4). |
| Frontend | Verification/cleanup: no UI affordance offers a standalone trivia-session / quiz-as-source path. No contract change. |

### Branch state and prerequisite

`feature/hu-16-runtime-snapshot-trivia-substage` should be branched from `develop`. All dependencies are Done; no same-service predecessor is In Progress.

**Before starting:** confirm (grep) that `TriviaQuestionSelection` does not exist, that the `TriviaQuestionSnapshot*` types/exceptions are **live** (reused by `MissionRuntimeSnapshot`), and that `CreateSessionFacade.BuildMissionRuntimeSnapshot` copies the full trivia content. There is **nothing to delete in code** - this is verification + test-locking.

### Linear state (as of 2026-06-30)

- DES-75 (HU-16 realign): **Todo**, labels: `canon-realign`, `needs-rebuild`, `ready-for-agent`, `svc:session-operations-service`, `Feature`
- DES-22 (HU-15): **Done** · DES-24 (HU-17): **Done** (the foundation this slice locks)
- Same-service Canceled (superseded): DES-23 (the old HU-16 this HU replaces), DES-28, DES-30, DES-44

> Linear live state may have changed. Use the Linear MCP to verify DES-75 status and labels if needed, but do not re-fetch PRD scope - read the local file at `@backend/docs/prd/DES-70-primera-implementacion-de-session-operations-service-hu-15-a-hu-36.md` and overlay `@backend/docs/canon-realignment-after-mission-runtime-rewrite.md`.

---

## 1. Orient - read service state, PRD, and realignment overlay

> **Skip this step if you have read the pre-resolved orient section above.** Run it only
> if the service source, README, or Linear state may have changed since 2026-06-30.

```text
Read the following and summarise what is already implemented vs. what HU-16 must lock:
- @backend/docs/hu16-context.md - the pre-resolved HU-16 (DES-75) context (primary)
- @backend/docs/hu15-context.md - what HU-15 already shipped (the snapshot machinery HU-16 verifies)
- @backend/docs/hu17-context.md - what HU-17 already locked (single-source invariant)
- @backend/docs/prd/DES-70-primera-implementacion-de-session-operations-service-hu-15-a-hu-36.md
- @backend/docs/canon-realignment-after-mission-runtime-rewrite.md - realignment overlay (trivia-as-substage canon delta)

Then grep the existing session-operations source to confirm:
- MissionRuntimeSnapshot has TriviaQuestionSnapshots; TriviaQuestionSnapshot copies prompt/sequenceOrder/scoreValue/timeLimitSeconds/options + IsCorrect; SubstageSnapshot.CreateTrivia builds trivia content
- CreateSessionFacade.BuildMissionRuntimeSnapshot copies the full resolved quiz (all questions/options) in mission order
- there is NO TriviaQuestionSelection type and no partial-selection path anywhere
- the live TriviaQuestionSnapshot* exceptions are reused by MissionRuntimeSnapshot (do NOT delete/rename)

Then use the Linear MCP to fetch the current live state and labels of DES-75.

Output: what is already correct (and must NOT be rebuilt), confirmation there is nothing to delete in code, and the per-layer snapshot-fidelity invariants to lock.
Do not start planning or implementing yet.
```

---

## 2. Label DES-75 as ready-for-agent

```text
Use the Linear MCP to confirm DES-75 still carries the label ready-for-agent.
If it is missing, add it.
Output the updated DES-75 ticket state and labels, including canon-realign and needs-rebuild.
Confirm the svc label is svc:session-operations-service only (svc:mission-design-service was dropped 2026-06-30).
```

---

## 3. Confirm slice readiness

```text
Use the Linear MCP to confirm DES-75 carries both svc:session-operations-service
and ready-for-agent labels and output its current status and acceptance criteria.

The PRD scope is already in the local file at
@backend/docs/prd/DES-70-primera-implementacion-de-session-operations-service-hu-15-a-hu-36.md.
The realignment overlay is in
@backend/docs/canon-realignment-after-mission-runtime-rewrite.md.
Do not re-fetch PRD scope from Linear.

Before planning, explicitly confirm the Stop 1 acceptance guard:
- a trivia Substage assigns one whole published TriviaQuiz (TriviaQuizSelection); all its questions are copied into the immutable MissionRuntimeSnapshot at creation - no partial selection
- there is no TriviaQuestionSelection type and no partial-selection path
- the snapshot copy carries every question's prompt/options/correct/score/timer in strict mission order and is immutable
- there is no standalone trivia-session / quiz-as-source creation route
- the immutable copy is orchestrated through the single CreateSessionFacade (Facade)
- HU-15's snapshot machinery and HU-17's single-source lock are verified and locked, NOT rebuilt; DES-23 is superseded and documented

Output the confirmed HU id, title, acceptance criteria, labels, and the guard confirmation before planning the slice.
```

In the remaining steps, `HU-16` and `DES-75` are the resolved values; `DES-70` is the shared session-operations PRD (local file above, overlaid by the realignment document).

---

## 4. Start the slice

```text
Prepare the trivia-substage runtime-snapshot fidelity lock slice on branch feature/hu-16-runtime-snapshot-trivia-substage.
Use the HU id and DES id resolved from Linear in the previous step.
This slice affects backend session-operations-service, two stale docs, and (verification only) frontend.

The pre-resolved orient at the top of this document lists what HU-15/HU-17 already shipped and what
HU-16 locks. Do not re-read the PRD for scoping unless you need a precise implementation detail.

This is a verification + test-locking slice: lock the whole-quiz snapshot-content-fidelity invariant
with tests and retire the last stale quiz-as-source docs. Do NOT re-implement the MissionRuntimeSnapshot,
the TriviaQuestionSnapshot/TriviaOptionSnapshot/SubstageSnapshot VOs, the CreateSessionFacade,
IMissionRuntimeSource, the migration, or the POST /api/sessions contract - they are Done. There is
nothing to delete in code.

Move DES-75 to In Progress and output the exact scope, branch name, base branch, and touched surfaces.
```

---

## 5. Backend phase X.1 - Domain layer

```text
Use @backend/.agents/backend-agent.md.
Implement backend phase X.1 for HU-16 in session-operations-service, per the
**X.1 derivation block in @backend/docs/hu16-context.md** (your spec - do not
re-read the canon or re-inspect the tree; open a cited canon section only to
fill a gap the block leaves open).

Gate:
- Domain build passes; unit tests lock the whole-quiz snapshot fidelity (all questions/options/correct-flag/score/timer copied in strict mission order; trivia substage >=1 question; snapshot immutable after Create)
- assert no TriviaQuestionSelection type and no partial-selection path exist
- the live TriviaQuestionSnapshot* types/exceptions are untouched (do NOT delete or rename)

Do not touch other backend layers or frontend. Do not re-implement any HU-15 type. There is nothing to delete in code.
```

Commit:

```text
feat(session-operations): phase X.1 - domain layer (HU-16)

Ref: HU-16
Ref: DES-75
Ref: DES-70
```

---

## 6. Backend phase X.2 - Application layer

```text
Use @backend/.agents/backend-agent.md.
Implement backend phase X.2 for HU-16 in session-operations-service, per the
**X.2 derivation block in @backend/docs/hu16-context.md** (your spec - do not
re-read the canon or re-inspect the tree; open a cited canon section only to
fill a gap the block leaves open).

Gate:
- Application build passes; facade test proves the immutable copy of the full mission tree (substages, targets, clues, questions - required_patterns_matrix.md:99) into MissionRuntimeSnapshot is orchestrated through the single CreateSessionFacade (Facade) - no second creation/snapshot path, no ad-hoc handler snapshot logic
- DES-75 fidelity depth on the trivia slice: CreateSessionFacade.BuildMissionRuntimeSnapshot copies the full published quiz (all questions/options/correct/score/timer, mission order) into TriviaQuestionSnapshots - no partial copy, no dropped questions
- the command carries no quiz/second-source field; no CreateTriviaSession* / IPublishedTriviaQuizSource path remains (verify absent)

Do not touch Infrastructure, Api, or frontend. Do not rewrite the CreateSessionFacade.
```

Commit:

```text
feat(session-operations): phase X.2 - application layer (HU-16)

Ref: HU-16
Ref: DES-75
Ref: DES-70
```

---

## 7. Backend phase X.3 - Infrastructure layer

```text
Use @backend/.agents/backend-agent.md.
Implement backend phase X.3 for HU-16 in session-operations-service, per the
**X.3 derivation block in @backend/docs/hu16-context.md** (your spec - do not
re-read the canon or re-inspect the tree; grep the model snapshot rather than
full-reading it, as the block instructs).

Gate:
- Infrastructure build passes
- NO new migration - assert (ef migrations add dry-check or model-snapshot grep) the model is unchanged and clean (no source_trivia_quiz_id / quiz-snapshot tables)
- repository integration test round-trips a session from a trivia-bearing mission with all trivia questions/options/correct-flags intact

Do not touch Api or frontend. Do not add an empty migration.
```

Commit:

```text
feat(session-operations): phase X.3 - infrastructure layer (HU-16)

Ref: HU-16
Ref: DES-75
Ref: DES-70
```

---

## 8. Backend phase X.4 - API layer

```text
Use @backend/.agents/backend-agent.md.
Implement backend phase X.4 for HU-16 in session-operations-service, per the
**X.4 derivation block in @backend/docs/hu16-context.md** (your spec - do not
re-read the canon or re-inspect the tree; open a cited canon section only to
fill a gap the block leaves open).

Gate:
- endpoint integration test: a trivia-bearing mission -> 201 with a single mission source; no standalone trivia-session / quiz-as-source creation route; plus the ineligible-mission rejection (reuse HU-15's 409/422 path)
- retire the stale quiz-as-source doc block at backend/docs/faq/workflow-and-sprint-planning.md:167-173 (replace with the canon model: trivia is a substage of the mission; Mission is the only source) and confirm the superseded banners on the overwritten HU-16 docs
- service coverage reaches the repo gate target (ADR-0005)

Do not touch frontend. Do not reshape the POST /api/sessions contract.
```

Commit:

```text
feat(session-operations): phase X.4 - api layer (HU-16)

Ref: HU-16
Ref: DES-75
Ref: DES-70
```

---

## 8.5. Docker rebuild and smoke

```text
From the monorepo root, rebuild and restart the backend stack after the API phase:

docker compose build session-operations-service api-gateway
docker compose up -d session-operations-service api-gateway

Run curl smoke checks through the gateway for:
- POST /api/sessions with an active, runtime-ready mission that has a trivia substage -> expect 201 Created (single mission source)
- GET the created session's runtime snapshot (or the create response) -> confirm the trivia substage carries all of the source quiz's questions/options
- POST /api/sessions with an inactive / not-ready mission -> expect 409/422
- confirm there is no quiz-as-source / standalone trivia-session creation route

Output:
- container status
- smoke command results
- confirmation the snapshot freezes the full trivia quiz and the contract is unchanged from HU-15 (no drift for the frontend)
```

---

## 9. Frontend slice

```text
Generate a multi phase plan in a markdown file - following the **frontend plan concreteness rule** (below), modelled on the exemplar closest to this slice's shape (`@frontend/plans/hu-03-frontend-role-permission-assignment.md` for a small 1-few-endpoint surface; `@frontend/plans/hu-10a-frontend-mission-hierarchy-authoring.md` for a large/multi-endpoint or partially-blocked surface) - save it in `@frontend/plans/` for the following:
Use @frontend/AGENTS.md.
Implement the frontend slice for HU-16 trivia-substage runtime-snapshot realignment.

The backend contract is UNCHANGED from HU-15: POST /api/sessions takes a mission-only
CreateSessionRequest (MissionId, Title, MaximumTimeMinutes, ScheduledAt), Administrator-only,
201 Created, 409/422 when the mission is not eligible. This is a small verification/cleanup
surface - choose the hu-03 exemplar.

Scope:
- confirm the session-creation form selects a Mission as the only source; trivia is authored into a mission substage upstream, never chosen at session creation
- remove or rewrite any residual UI copy/types that offer creating a session from a trivia quiz, a SessionMode, or a standalone trivia session
- ensure no UI affordance offers a quiz-as-source / standalone trivia-session creation path
- keep surfacing backend eligibility/readiness rejection (mission inactive / not ready -> 409/422)

Gate:
- frontend typecheck/build passes
- session-creation flow exercises create-from-mission against the (unchanged) API contract, incl. the not-eligible rejection path
- Gate: no UI type/copy offers a standalone trivia-session / quiz-as-source creation path
- Gate: no UI type/copy introduces or retains SessionMode

Do not modify backend code in this step.
```

**Frontend plan concreteness rule (embed verbatim in the generated plan's altitude choice):**

1. **Proportion concreteness to certainty.** Write code-complete detail - exact DTO/request types, real component skeletons, exact client-fn + server-action bodies, a `data-testid` contract - only for the **fully-knowable near-term increments** (typically the foundation + first authoring increment). Keep later, large, or blocked increments at **contract + gate altitude**: a contract table, scope, and gate, with no invented bodies. Never write code for an increment blocked on an open question.
2. **Verify every code anchor against the real source before writing it.** Open the files the plan names - exported vs. private helpers, exact signatures, the const/env it reads, the line a refactor targets - and write only what the source actually supports. A confident-but-wrong anchor (e.g. "reuse `getIdentityHeaders`" when it is not exported) is worse than an altitude note. If a detail is not verifiable, state the assumption under Open Questions rather than inventing it.
3. **Required sections** (both exemplars carry these; a plan missing one is a defect): Context · Verified Backend Contract (endpoint/shape table) · Architecture Decisions · **Environment** (env vars / config consts reused) · **data-testid contract** · phased Scope + Gate per increment · **Acceptance-criteria -> test mapping** · Open Questions / Dependencies · Out of Scope.
4. **Final forms only, sequential by default.** Write only the final version of each anchor - no "wrong -> revised" trails - and keep increments sequential unless the slice genuinely parallelizes.

Commit:

```text
feat(frontend): trivia-substage session creation realignment - HU-16

Ref: HU-16
Ref: DES-75
Ref: DES-70
```

---

## 9b. Implement the frontend plan

> Run only after the Step 9 plan is written and reviewed. The plan is the source of
> truth and supersedes the Step 9 seed scope - including Step 9's single seed commit:
> commit per the plan's own per-phase Commit Sequence, not the one above.

```text
Use @frontend/AGENTS.md. Implement the Step 9 plan at @frontend/plans/hu-16-frontend-trivia-substage-session-creation.md,
phase by phase per the plan's own Scope / Gate / Commit Sequence.

For each phase: implement only that phase, run its Gate (build + typecheck, plus any e2e the
phase lands), then commit with the exact subject from the plan's Commit Sequence for that phase.

STOP at any increment the plan marks blocked on an Open Question (name it). Do not invent the
blocked behaviour; surface the question and wait.

Do not re-generate the plan. Do not modify backend code in this step.
```

---

## 10. Close-out

```text
Use the Linear MCP to re-check DES-75 acceptance criteria and labels.
Verify the final implementation against the Stop 1 acceptance guard:
- a trivia Substage assigns one whole published TriviaQuiz; all its questions are copied into the immutable MissionRuntimeSnapshot at creation - no partial selection
- there is no TriviaQuestionSelection type and no partial-selection path
- the snapshot copy carries every question's prompt/options/correct/score/timer in strict mission order and is immutable
- there is no standalone trivia-session / quiz-as-source creation route
- the immutable copy is orchestrated through the single CreateSessionFacade (Facade)
- HU-15's snapshot machinery and HU-17's single-source lock are verified and locked, not rebuilt; DES-23 is superseded and documented

Run final backend and frontend verification required by the repo instructions.
Summarise:
- commits created
- the per-layer snapshot-fidelity tests added
- the stale quiz-as-source docs retired (faq block + confirmed superseded banners)
- tests and gates run (incl. ADR-0005 coverage)
- confirmation that DES-78 (HU-33A trivia orchestration) can now build on a fidelity-locked trivia snapshot

Create the PR:

gh pr create \
  --base develop \
  --head feature/hu-16-runtime-snapshot-trivia-substage \
  --title "feat(session-operations): lock trivia-substage runtime-snapshot fidelity (HU-16 realign)" \
  --body "Realigns DES-75/HU-16: trivia is a Substage of the mission that assigns one whole published TriviaQuiz (TriviaQuizSelection), and every one of its questions/options/correct-answers/scores/timers is frozen into the immutable MissionRuntimeSnapshot at LiveSession creation - no partial TriviaQuestionSelection, no standalone trivia-session route. Adds per-layer snapshot-content-fidelity tests over HU-15's snapshot machinery and retires the last stale quiz-as-source docs. No new aggregate, endpoint, or migration - HU-15's snapshot and HU-17's single-source lock are verified and hardened, not rebuilt. Supersedes DES-23."
```

---

## Rationale

DES-75's acceptance criteria (no standalone trivia session; a trivia substage assigns a whole published quiz whose questions are snapshotted immutably; no `TriviaQuestionSelection`; DES-23 superseded) are the **snapshot-content-fidelity invariant** of the mission-wrapper model. After the 2026-06-16 mission-runtime rewrite, HU-15 (DES-22) rebuilt session creation around `Mission` as the only source and built the immutable `MissionRuntimeSnapshot` that already copies the full trivia quiz into `TriviaQuestionSnapshots`; HU-17 (DES-24) locked the single-source half of the invariant. So most of HU-16's acceptance is already an emergent property of HU-15 + HU-17. HU-16 exists to make the **content-fidelity** half explicit, tested, and structurally permanent: the whole quiz is frozen (all questions, immutable, no partial selection), and it converts that emergent guarantee into a locked invariant while retiring the last stale quiz-as-source documentation.

The mandated pattern is `Facade` (`required_patterns_matrix.md` HU-16 row), but its canon note is explicit that the Facade "is realized via `HU-15`/`HU-17`" - the existing `CreateSessionFacade`. HU-16 therefore carries the `Facade` as its X.2-owned pattern in the **verification** sense (the snapshot copy must ride on the single facade), and adds no new pattern class - inventing a second facade would be the inverse of the HU-01/02/03 defect. This slice is deliberately verification-dominant; it is still a gated deliverable because DES-78 (HU-33A trivia orchestration) executes against exactly this frozen trivia snapshot, so its fidelity must be locked before trivia play is rebuilt.

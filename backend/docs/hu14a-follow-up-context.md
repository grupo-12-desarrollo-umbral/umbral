# HU-14A follow-up Context — RemoveTriviaQuestion command + question-removal domain slot

> Paste this section into any agent session that needs context for DES-80.
> Last updated: 2026-07-11 | Branch: `feature/hu-14a-follow-up-remove-trivia-question`

## State

- DES-80 (HU-14A follow-up): **In Progress**, labels: `ready-for-agent`, `svc:mission-design-service`, `Feature`
- **Resolved mode: feature flow** — DES-80 carries neither `canon-realign` nor `needs-rebuild`
- **Superseded handling applied:** none for this slice; the realignment map was checked and no same-service predecessor for this trivia-question-removal seam required drop/substitution
- Same-service build-on predecessors (Done): **DES-17 (HU-11)**, **DES-20 (HU-14A)**, **DES-21 (HU-14B)**, **DES-18 (HU-12)**, **DES-19 (HU-13)**
- Same-service landed but untouched by this HU: **DES-14 (HU-09)**, **DES-15 (HU-10A)**, **DES-22 (HU-15)**, **DES-79 (HU-15 follow-up)**, **DES-86**
- PRD DES id: **DES-62** → `backend/docs/prd/DES-62-mission-design-service-baseline.md`
- Branch: `feature/hu-14a-follow-up-remove-trivia-question`, base **`develop`** (no same-service predecessor In Progress)

## Required design patterns

| Pattern | Owning phase | Why (from patterns matrix) | Concrete obligation |
|---|---|---|---|
| `Template Method` (mandated) | X.1 Domain + X.2 Application | `backend/docs/required_patterns_matrix.md` / `backend/docs/trivia_sprint_required_patterns_matrix.md`: question authoring uses a shared validation flow with question-specific steps | Extend the existing trivia-question authoring workflow to cover remove without forking a separate validation sequence. The remove path must reuse one stable authoring/editability flow with remove-specific steps, not ad-hoc handler/controller conditionals. |

Applies-where note (no new `Proxy` gate): the delete endpoint is an administrator-only mutation, but this HU is not matrix-tagged for `Proxy` and not in the applies-where set. It inherits the standard `[Authorize]` / `AuthorizationBehaviour` guard already used by trivia mutations.

## What predecessors have already landed

**Build-on (read for derivation):**

- **DES-17 (HU-11) — Done.** Introduced `TriviaQuiz` as the trivia authoring aggregate root, the draft editability baseline, and the first `Template Method` validation seam for quiz authoring. This slice extends that same aggregate rather than introducing a parallel remove path.
- **DES-20 (HU-14A) — Done.** Added `TriviaQuestion` / `TriviaOption` authoring (`AddQuestion` / `UpdateQuestion`) and explicitly recorded that `RemoveTriviaQuestion` was deferred because the aggregate had no per-question removal slot. DES-80 is the scheduled follow-up that lands that missing slot.
- **DES-21 (HU-14B) — Done.** Landed the explicit trivia-question validity baseline for option count, correct-answer, score, and timer constraints. Remove must preserve that question model and only delete a target question; it must not re-open or weaken the landed validation rules.
- **DES-18 (HU-12) — Done.** Added trivia quiz lifecycle transitions (`Draft` / `Published` / `Archived`) and source-readiness semantics. DES-80 builds on that lifecycle boundary: published or archived quizzes reject question removal as edit-blocked.
- **DES-19 (HU-13) — Done.** Added duplicate/retire reuse behavior and preserved the distinction between question-level authoring changes and whole-quiz destructive removal/retirement. DES-80 must add per-question removal without disturbing the existing whole-quiz delete/retire semantics.

**Landed, untouched by this HU:**

- DES-14 / DES-15 — mission wrapper, hierarchy, targets, and trivia-substage selection; different aggregate surface
- DES-22 — mission-runtime snapshot creation consumes published trivia content downstream but does not own trivia authoring mutations
- DES-79 — archive-time protection for quizzes referenced by active missions; separate archive seam, not question removal
- DES-86 — per-target treasure-hunt scoring refactor; unrelated bounded-context seam for this HU

**Coverage:** no stable carried-forward aggregate percentage is recorded for `mission-design-service`; verify the real service percentage at X.4 against the repo coverage gate.

## What this HU adds

| Concern | New work |
|---|---|
| Per-question removal slot | Add `TriviaQuiz.RemoveQuestion(...)` to the existing aggregate so an administrator can delete one authored question instead of only deleting the whole quiz |
| Removed-question domain fact | Raise `TriviaQuestionRemoved` when a question is successfully removed |
| Question ordering integrity | Reconcile `sequenceOrder` for the remaining questions after deletion so quiz question order stays coherent |
| Lifecycle edit guard | Reject removal when the quiz is no longer editable (`Published` / `Archived`) with the same clear edit-blocked domain path used by trivia authoring |
| Application use case | Add `RemoveTriviaQuestionCommand` + handler + validator with admin-only authorization and not-found handling for missing quiz/question lookups |
| Persistence + API contract | Reuse the existing trivia persistence model and add `DELETE /api/trivias/{triviaQuizId}/questions/{questionId}` to the authoring surface |
| Frontend flow | Add question-removal UI to the existing trivia authoring experience, consuming the verified delete contract and updated round-tripped question order |

## Touched surfaces

- `backend/services/mission-design-service`
- `frontend/` trivia quiz authoring UI
- `backend/frontend` API contract boundary: trivia question delete endpoint and the post-delete trivia detail/mutation response shape

## Committed phases

| Commit | Phase | Description |
|---|---|---|
| — | — | No commits yet |

## Known quirks / gotchas

- DES-80 is a **follow-up slice**, not a rewrite of HU-14A. Keep the original HU-14A docs intact; this file projects only the deferred remove scope.
- The local PRD file is authoritative. DES-62 already records that `RemoveTriviaQuestion` and `TriviaQuestionRemoved` were intentionally deferred from HU-14A and now belong in a separate slice.
- The service already has two destructive seams: whole-quiz delete (`EnsureCanBeDestructivelyRemoved` / `DeleteTriviaQuiz`) and quiz retirement/archive. DES-80 must add **question-level** removal only; do not blur those seams together.
- The mandated `Template Method` must appear explicitly in phase X.1 and X.2 scope and gate lines. If it drops out of those phase gates, the generated plan is defective.
- Pattern placement matters here: extend the existing trivia authoring template/validator family only where it remains genuine per `backend/docs/adr/0012-design-pattern-placement-convention.md`. Do not restore handler base classes or add a ceremonial new shared validator if the fixed workflow already lives in the aggregate/handler path.
- Infrastructure may be a **no-op migration** slice. The question/option persistence model already exists from HU-14A; add a migration only if the current mapping blocks per-question delete semantics.

## Per-phase derivation — authoritative for implementation

> Primary source for the phase subagent (per `backend-agent.md` "Read first").
> Mode = **feature flow**. Canon precedence: `ddd_solution_model.md` → service `CONTEXT.md` → `structure.md` → `bd_umbral_entity_spec.md` → plan docs.

### Phase X.1 — Domain
**Derive** (`backend/docs/prd/DES-62-mission-design-service-baseline.md` §implementation note for `RemoveTriviaQuestion`; `backend/docs/bd_umbral_entity_spec.md` §`TriviaQuiz`, §`TriviaQuestion`, §`TriviaOption`; `backend/docs/ddd_solution_model.md` §`MissionDesign`; `backend/services/mission-design-service/CONTEXT.md` §Trivia Authoring, §Required Patterns):
- Add `TriviaQuiz.RemoveQuestion(triviaQuestionId, removedAt)` (timestamp/name may mirror the existing aggregate conventions) to delete one authored `TriviaQuestion` from the existing quiz aggregate.
- Raise `TriviaQuestionRemoved` on successful removal.
- Reconcile `sequenceOrder` of the remaining questions after removal so the quiz keeps a unique contiguous authored order.
- Reject removal when the quiz state no longer permits authoring edits (`Published` / `Archived`) through the existing trivia edit-blocked domain path; do not invent a second lifecycle model.
- Reuse the existing trivia-question authoring `Template Method` seam inside `TriviaQuiz.cs`: remove must participate in one stable authoring/editability workflow with remove-specific steps, not a detached ad-hoc branch.
- Preserve the landed question model from HU-14A/HU-14B: deleting a question removes its options with it, and no remaining question/option invariants are weakened.

**Target files** (create | edit — file to mirror):
- edit `backend/services/mission-design-service/src/Domain/Entities/TriviaQuiz.cs` — mirror the existing add/update question workflow and nested template realization already in this file
- create `backend/services/mission-design-service/src/Domain/Events/TriviaQuestionRemovedEvent.cs` — mirror `backend/services/mission-design-service/src/Domain/Events/TriviaQuestionAddedEvent.cs`
- edit `backend/services/mission-design-service/tests/UnitTests/Domain/Entities/TriviaQuizTests.cs` — mirror the existing add/update question domain-test style in the same file

**Pattern this phase owns:** `Template Method` — extend the existing fixed trivia-question authoring workflow inside the aggregate so remove follows the same stable skeleton with remove-specific hooks.
**Gate:** domain build passes; unit tests lock successful remove, `sequenceOrder` reconciliation, question-not-found rejection, and published/archived edit-blocked rejection; **Gate: removal is realized through the existing `Template Method` authoring workflow, not a duplicated remove-only rule sequence or controller/handler conditional chain.**

### Phase X.2 — Application
**Derive** (`backend/docs/prd/DES-62-mission-design-service-baseline.md` §minimum application interfaces; `backend/docs/ddd_solution_model.md` §`MissionDesign`; `backend/services/mission-design-service/CONTEXT.md` §Trivia Authoring, §Required Patterns; `backend/docs/workflow_for_prompts.md` `DELETE /trivia/{id}/questions/{qid} -> RemoveTriviaQuestionCommand`):
- Add the `RemoveTriviaQuestion` command slice with request, handler, and validator.
- Keep the mutation administrator-only, aligned with the existing trivia authoring command surface.
- Load the target quiz, reject missing quiz/question lookups cleanly, call the aggregate removal method, persist, and return the same trivia mutation/detail shape the existing add/update question commands already use.
- Reuse the existing trivia authoring application seam (`Trivias/Common/Authoring/`) where it genuinely carries the fixed workflow. Remove must follow one stable validation/authoring sequence with remove-specific steps; do not fork a one-off handler-local rule order.
- Preserve infrastructure independence: all persistence stays behind `ITriviaQuizRepository`; no direct EF logic enters the slice.

**Target files** (create | edit — file to mirror):
- create `backend/services/mission-design-service/src/Application/Trivias/Commands/RemoveTriviaQuestion/RemoveTriviaQuestionCommand.cs` — mirror `backend/services/mission-design-service/src/Application/Trivias/Commands/UpdateTriviaQuestion/UpdateTriviaQuestionCommand.cs`
- create `backend/services/mission-design-service/src/Application/Trivias/Commands/RemoveTriviaQuestion/RemoveTriviaQuestionCommandHandler.cs` — mirror `backend/services/mission-design-service/src/Application/Trivias/Commands/UpdateTriviaQuestion/UpdateTriviaQuestionCommandHandler.cs`
- create `backend/services/mission-design-service/src/Application/Trivias/Commands/RemoveTriviaQuestion/RemoveTriviaQuestionCommandValidator.cs` — mirror `backend/services/mission-design-service/src/Application/Trivias/Commands/UpdateTriviaQuestion/UpdateTriviaQuestionCommandValidator.cs`
- edit `backend/services/mission-design-service/src/Application/Trivias/Common/Authoring/TriviaQuestionAuthoringCommandValidator.cs` and/or sibling authoring helpers only if needed to keep one genuine shared workflow — mirror the current `Trivias/Common/Authoring/` seam, do not add ceremony
- edit `backend/services/mission-design-service/tests/UnitTests/Application/Trivias/Handlers/` with a new remove handler test file — mirror `AddTriviaQuestionCommandHandlerTests.cs` / `UpdateTriviaQuestionCommandHandlerTests.cs`
- edit `backend/services/mission-design-service/tests/UnitTests/Application/Trivias/Commands/RemoveTriviaQuestion/` with validator tests — mirror the add/update validator test structure

**Pattern this phase owns:** `Template Method` — the remove command must route through the same stable trivia authoring workflow/validator family rather than creating a detached remove-only sequence.
**Gate:** application build passes; handler tests cover valid removal, missing quiz, missing question, and edit-blocked rejection; validator tests cover valid and invalid ids; **Gate: remove validation/authoring is enforced through one stable `Template Method` flow shared with trivia question authoring, not by unrelated handlers each owning their own sequencing.**

### Phase X.3 — Infrastructure
**Derive** (`backend/docs/bd_umbral_entity_spec.md` §`TriviaQuiz`, §`TriviaQuestion`, §`TriviaOption`; `backend/docs/ddd_solution_model.md` §`MissionDesign`; `backend/structure.md` persistence conventions):
- Reuse the existing `TriviaQuiz` persistence model so removing a question deletes its owned options and preserves the remaining question order on round-trip.
- Verify whether EF configuration already supports child removal for the current trivia mapping. If it does, keep this as a no-op schema phase with tests only; if it does not, make the minimal configuration/migration change required.
- Preserve the existing repository seam and read-model contract; the post-delete detail read must return the updated question list in reconciled order.

**Target files** (create | edit — file to mirror):
- inspect/edit `backend/services/mission-design-service/src/Infrastructure/Persistence/Configurations/TriviaQuizConfiguration.cs` only if the current mapping blocks per-question deletion — mirror the existing question/option owned-entity configuration
- edit `backend/services/mission-design-service/src/Infrastructure/Persistence/Repositories/TriviaQuizRepository.cs` only if repository loading/tracking needs adjustment for delete semantics — mirror the current add/update mutation persistence path
- edit `backend/services/mission-design-service/tests/IntegrationTests/Persistence/MissionInfrastructureIntegrationTests.cs` — mirror the existing trivia add/update persistence coverage in that suite
- create a new migration under `backend/services/mission-design-service/src/Infrastructure/Persistence/Migrations/` **only if required** by the verified mapping change; otherwise keep migration no-op and say so

**Pattern this phase owns:** none
**Gate:** infrastructure build passes; the trivia repository integration test proves remove-question persistence and round-tripped reconciled order; model snapshot/migration remain consistent; if no schema change is needed, explicitly prove the no-op by verification rather than inventing a migration.

### Phase X.4 — Api
**Derive** (`backend/docs/workflow_for_prompts.md` `DELETE /trivia/{id}/questions/{qid}`; `backend/docs/ddd_solution_model.md` §`MissionDesign`; `backend/services/mission-design-service/CONTEXT.md` §Trivia Authoring, §Boundary Rules):
- Expose `DELETE /api/trivias/{triviaQuizId}/questions/{questionId}` on the existing trivia controller surface.
- Bind route params into `RemoveTriviaQuestionCommand`, keep the endpoint administrator-only, and let not-found / edit-blocked failures flow through the global ProblemDetails handler.
- Return the verified trivia mutation/detail response shape already used by the add/update question endpoints, now reflecting the removed question and reconciled order.
- Do not change whole-quiz delete, publish/archive, or mission-selection endpoints in this slice.

**Target files** (create | edit — file to mirror):
- edit `backend/services/mission-design-service/src/Api/Controllers/TriviasController.cs` — mirror the existing add/update question endpoints and the whole-quiz delete route style
- edit `backend/services/mission-design-service/tests/IntegrationTests/Api/TriviaEndpointsTests.cs` — mirror the existing add/update question endpoint tests

**Pattern this phase owns:** none (standard `[Authorize]` / `AuthorizationBehaviour`; no new `Proxy`)
**Gate:** endpoint tests pass for successful delete, non-admin rejection, missing quiz/question not-found, and edit-blocked rejection; trivia detail/mutation response shows the updated question list and order; repo coverage gate passes for `mission-design-service`.

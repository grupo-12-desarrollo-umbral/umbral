# HU-14A Context — Gestión de preguntas y opciones de trivia

> Paste this section into any agent session that needs context for HU-14A.
> Last updated: 2026-06-01 | Branch: `feature/hu-14a-trivia-question-and-options-management`

## State

- DES-20 (HU-14A): **Backlog**, labels: `ready-for-agent`, `svc:mission-design-service`, `Feature`
- DES-17 (HU-11): **Done**
- DES-14 (HU-09): **Done**
- DES-62 (PRD): **Backlog**, labels: `ready-for-agent`
- Branch: `feature/hu-14a-trivia-question-and-options-management` (branch from `develop`)

## Required design patterns

- `Template Method`
  - Why: question authoring uses a shared validation flow with question-specific steps.
  - Phase owner: X.1 Domain and X.2 Application
  - Concrete obligation: model one stable trivia-question authoring validation workflow for add/update operations, with overridable steps for option-cardinality, correct-answer, and editability checks; do not duplicate rule sequencing across handlers or scatter it into ad-hoc conditionals.

## What predecessors have already landed

`mission-design-service` now has two same-service predecessors in Linear as **Done**: HU-09 (`DES-14`) and HU-11 (`DES-17`). Only HU-11 has a dedicated context file; HU-09 is reconstructed conservatively from the service README and the established service baseline.

**Domain layer**
- HU-09 established the `Mission` authoring baseline and confirmed `MissionActivation` remains a source-readiness concept, not a runtime lifecycle
- HU-09 already introduced mission-side value objects such as `Difficulty` and `MaximumTime`
- HU-11 introduced the trivia-side aggregate root baseline: `TriviaQuiz`
- HU-11 established the minimum quiz lifecycle boundary for authoring/editability and the aggregate/detail expectation that quiz basics and associated-question shape stay together
- HU-11 already carried the mandated `Template Method` for stable quiz authoring validation

**Application layer**
- HU-09 established the service application baseline around create, update, deactivate, catalog, and detail flows for missions
- Authorization and validation behaviours are already part of the service pipeline
- HU-11 added trivia-side create/update/query use cases and the repository/read-model abstractions needed for `TriviaQuiz`

**Infrastructure / API**
- EF Core persistence baseline, repositories, and `/api/missions` endpoints already exist from HU-09
- HU-11 added the trivia-side persistence/API baseline for quiz authoring and draft consultation
- `mission-design-service` is already wired into local Docker and the API gateway

**Frontend**
- Mission-management UI is the original predecessor frontend slice
- HU-11 added trivia-quiz create/edit/detail UI, including the associated-question section shape without full question-management behavior yet

**Coverage:** no predecessor context file records a verified aggregate coverage percentage for `mission-design-service`; verify the real service percentage during HU-14A phase X.4.

## What HU-14A adds on top (per PRD DES-62 and DES-20)

| Concern | New work |
|---|---|
| `TriviaQuestion` authoring | Add question-management behavior inside the existing `TriviaQuiz` aggregate so an administrator can create and edit questions associated with a quiz |
| `TriviaOption` authoring | Persist and edit between 2 and 4 options per question as part of the same question workflow |
| Correct-answer authority | Ensure each question records exactly one correct option in the authoring model |
| Optional explanation | Persist an optional explanation together with the question content |
| Score and timer capture | Carry question score and timer fields as part of question authoring; if numeric ranges remain unresolved, record the ambiguity for HU-14B rather than inventing extra validation scope here |
| Backend contract | Extend the trivia API contract with question-management operations nested under the existing quiz authoring surface |
| Frontend flow | Extend trivia quiz detail/edit UI so administrators can add and edit questions and their options against the verified backend contract |

## Touched surfaces

- `backend/services/mission-design-service`
- `frontend/` trivia question authoring UI within the existing quiz-management flow
- `backend/frontend` API contract boundary: trivia question/option authoring endpoints and payloads under the trivia quiz contract

## Committed phases

| Commit | Phase | Description |
|---|---|---|
| — | — | No commits yet |

## Known quirks / gotchas

- `DES-62` is the authoritative PRD and has the local file `backend/docs/prd/DES-62-mission-design-service-baseline.md`, but the Linear PRD ticket still does not carry `svc:mission-design-service`. Use the local PRD file and the resolved DES-20 relation as authority for this slice.
- The service README is intentionally sparse. For predecessor reuse, treat `backend/docs/hu11-context.md` as the authoritative fast path and fall back to the README only for HU-09-level baseline context.
- HU-11 already established the associated-question shape inside trivia detail reads. HU-14A must extend that baseline into actual question/option authoring, not recreate the trivia aggregate from scratch.
- HU-14B is the dedicated follow-on slice for explicit trivia-question validation rules. HU-14A should author and persist question content cleanly, but if score/timer numeric ranges or deeper invalid-rule matrices are still ambiguous, record that ambiguity instead of smuggling the full HU-14B rule set into this slice.
- The mandated `Template Method` must appear explicitly in phase X.1 and X.2 scope and gate lines. If it drops out of those phase gates, the generated plan is defective.

## Deferred scope — `RemoveTriviaQuestion`

`RemoveTriviaQuestion` is listed in the PRD's minimum application-interface set (`DES-62`, and the priority domain event `TriviaQuestionRemoved`), but it is **deliberately deferred out of HU-14A**, not an oversight.

- **Trigger condition not met.** The HU-14A prompt (`prompt_example_feature_hu14a.md`, scope bullet) authorizes adding `RemoveTriviaQuestion` *only if the existing trivia editing baseline already has a clear removal slot; otherwise keep the scope to add/update and note the deferral explicitly*. The `TriviaQuiz` aggregate exposes only `AddQuestion` and `UpdateQuestion` (`Domain/Entities/TriviaQuiz.cs`) — no per-question removal slot. The only destructive path is whole-quiz `EnsureCanBeDestructivelyRemoved` / `DeleteTriviaQuiz`.
- **What it would require (own slice).** A `TriviaQuiz.RemoveQuestion` aggregate method, a `TriviaQuestionRemoved` domain event, `sequenceOrder` reconciliation after removal, and published-state edit guards — i.e. net-new domain behavior beyond HU-14A's add/update authoring scope and its "do not touch Infrastructure/Api" gate.
- **Pattern matrix is unaffected.** The required-pattern obligation for HU-14A is `Template Method` over add/update question validation (`trivia_sprint_required_patterns_matrix.md`), which is satisfied. Removing questions is not pattern-mandated and does not restore any handler base-class requirement.
- **Reviewer note.** A reviewer using the HU-14A closing checklist is aligned; a reviewer reading the PRD's interface list literally should treat this entry as the recorded deferral, not a gap. Schedule as a follow-up slice when question removal is actually required.

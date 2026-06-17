# HU-13 Context — Duplicación y retiro de quizzes usados

> Updated on 2026-06-16 for the mission-runtime restructure. A used quiz is one
> referenced by a mission trivia `Substage` or frozen in a
> `MissionRuntimeSnapshot`, not one that directly created a `LiveSession`.

> Paste this section into any agent session that needs context for HU-13.
> Last updated: 2026-06-03 | Branch: `feature/hu-13-trivia-quiz-duplication-and-retirement`

## State

- DES-19 (HU-13): **Backlog**, labels: `ready-for-agent`, `svc:mission-design-service`, `Feature`
- Predecessors in same service already landed: DES-14 (HU-09) **Done**, DES-17 (HU-11) **Done**, DES-20 (HU-14A) **Done**, DES-21 (HU-14B) **Done**, DES-18 (HU-12) **Done**
- DES-62 (PRD): **Backlog**, labels: `ready-for-agent`
- Branch: `feature/hu-13-trivia-quiz-duplication-and-retirement` (branch from `develop`)

## Required design patterns

- `Template Method`
  - Why: duplication and retirement need one stable validation workflow over an existing `TriviaQuiz`, with operation-specific steps for copy creation vs withdrawal-from-use.
  - Phase owner: X.1 Domain and X.2 Application
  - Concrete obligation: model one stable trivia duplication/retirement validation workflow with overridable duplicate-vs-retire steps; do not duplicate used-quiz checks, editability checks, or retirement preconditions across handlers or scatter them into ad-hoc conditionals.

## What predecessors have already landed

`mission-design-service` now has five same-service predecessors in Linear as **Done**: HU-09 (`DES-14`), HU-11 (`DES-17`), HU-14A (`DES-20`), HU-14B (`DES-21`), and HU-12 (`DES-18`). The strongest documented reuse baseline for HU-13 is HU-11, HU-14A, and HU-12. HU-09 and HU-14B still do not have dedicated context files in `backend/docs/`, and `backend/services/mission-design-service/README.md` remains intentionally sparse, so those predecessor details are recorded conservatively.

**Domain layer**
- HU-09 established the service baseline around `Mission` authoring and source-readiness as a design-time concern, not runtime session state
- HU-11 introduced `TriviaQuiz` as the trivia-side aggregate root and established the initial authoring/editability boundary
- HU-11 already carried the mandated `Template Method` for stable trivia authoring validation
- HU-14A extended the existing `TriviaQuiz` aggregate with `TriviaQuestion` and `TriviaOption` authoring behavior, preserving quiz basics plus associated-question shape in one authoring model
- DES-21 (HU-14B) is **Done**, so the quiz already has a landed question-validation baseline for option cardinality, correct-answer rules, score, and timer constraints, but there is no dedicated `backend/docs/hu14b-context.md`; verify exact landed names in source during implementation
- HU-12 moved `TriviaQuiz` through draft/published/archived lifecycle transitions and established that published quizzes are source-ready while draft and archived quizzes are not
- HU-12 already carried the mandated `Template Method` for stable lifecycle validation

**Application layer**
- Mission-side application flows remain the service baseline from HU-09
- `AuthorizationBehaviour` and `ValidationBehaviour` already exist in the service pipeline
- HU-11 introduced trivia-side create/update/query use cases and the repository/read-model abstractions needed for `TriviaQuiz`
- HU-14A extended the application layer with question/option authoring use cases
- HU-12 added publish/archive commands, handlers, validators, and state-aware read projections for trivia lifecycle

**Infrastructure / API**
- EF Core persistence baseline, repositories, and `/api/missions` endpoints already exist
- Trivia-side persistence and draft consultation endpoints were established in HU-11
- Trivia-side question/option persistence and API surface were extended in HU-14A
- HU-12 extended persistence and API surfaces for lifecycle transitions and coherent quiz-state projection
- `mission-design-service` is already wired into local Docker and the API gateway

**Frontend**
- Mission-management UI exists from the earlier service baseline
- Trivia quiz create/edit/detail UI exists from HU-11
- Question and option authoring UI exists from HU-14A
- Lifecycle publish/archive UI and state feedback exist from HU-12 and should be reused rather than reimplemented for retirement semantics

**Coverage:** no stable aggregate percentage is captured in predecessor docs for `mission-design-service`; verify real service coverage during phase X.4.

## What HU-13 adds on top (per PRD DES-62 and DES-19)

| Concern | New work |
|---|---|
| Quiz duplication | Duplicate an existing `TriviaQuiz` so its reusable structure becomes a new authoring copy rather than mutating the historical source quiz |
| Historical lineage | Preserve traceability between the duplicated quiz and the original quiz so historical use of the original remains intact |
| Used-quiz retirement | Prevent destructive removal of quizzes already referenced by missions or runtime snapshots; route withdrawal-from-use through archive or equivalent deactivation semantics instead |
| State-aware reuse | Ensure a duplicated quiz can continue through the existing authoring/publication flow as a separate quiz while the original keeps its historical identity |
| Backend contract | Extend the trivia API contract with duplication and used-quiz retirement/rejection behavior needed by verified consumers; do not invent unrelated hard-delete scope |
| Frontend flow | Extend trivia administration UI with duplicate action, retirement messaging, and clear historical-vs-copy cues aligned to the verified backend contract |

## Touched surfaces

- `backend/services/mission-design-service`
- `frontend/` trivia quiz administration UI
- `backend/frontend` API contract boundary: trivia duplication operation plus used-quiz retirement or delete-rejection semantics and any lineage/read-model payload needed by the verified UI

## Committed phases

| Commit | Phase | Description |
|---|---|---|
| — | — | No commits yet |

## Known quirks / gotchas

- `DES-19` is the HU ticket for this slice. The service PRD reference is still `DES-62`, resolved separately by the generator workflow. Both must appear in the generated phase commits.
- `DES-62` is the authoritative PRD and has the local file `backend/docs/prd/DES-62-mission-design-service-baseline.md`, but the Linear PRD ticket still does not carry `svc:mission-design-service`. Use the local PRD file and the resolved DES-19 relation as authority for this slice.
- The predecessor fast path remains incomplete for this service: there is no dedicated `backend/docs/hu09-context.md` or `backend/docs/hu14b-context.md`, and the service README is sparse. Treat HU-11, HU-14A, and HU-12 context files as the strongest documented reuse baseline and verify exact HU-14B landed naming in source before extending it.
- HU-12 already introduced archive semantics for quizzes. HU-13 must add duplication and the used-quiz retirement rule without re-implementing the whole lifecycle slice or inventing runtime `SessionOperations` behavior.
- The acceptance criteria say a used quiz cannot be deleted and may be retired via archive or deactivation, but they do not fully specify whether duplication is allowed from every quiz state or only from specific states. Record that ambiguity in the implementation debrief/rationale instead of guessing extra state rules.
- The mandated `Template Method` must appear explicitly in phase X.1 and X.2 scope and gate lines. If it drops out of those phase gates, the generated plan is defective.

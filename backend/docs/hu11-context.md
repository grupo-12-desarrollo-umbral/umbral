# HU-11 Context — Creación y edición de quizzes de trivia

> Paste this section into any agent session that needs context for HU-11.
> Last updated: 2026-06-01 | Branch: `feature/hu-11-trivia-quiz-management`

## State

- DES-17 (HU-11): **Backlog**, labels: `ready-for-agent`, `svc:mission-design-service`, `Feature`
- DES-14 (HU-09): **Done**
- DES-62 (PRD): **Backlog**, labels: `ready-for-agent`
- Branch: `feature/hu-11-trivia-quiz-management` (branch from `develop`)

## Required design patterns

- `Template Method`
  - Why: Quiz creation/edit validation keeps one invariant workflow.
  - Phase owner: X.1 Domain and X.2 Application
  - Concrete obligation: model one stable trivia-quiz authoring validation workflow with overridable state-specific and operation-specific steps; do not duplicate create-vs-update rule sequencing across handlers or scatter editability checks into ad-hoc conditionals.

## What predecessors have already landed

Predecessor documentation is thin for `mission-design-service`: there is no `backend/docs/hu09-context.md`, and `backend/services/mission-design-service/README.md` only confirms the service scaffold and migrated code. The live repo and DES-14 state show that HU-09 is the landed same-service predecessor, but its detailed context was not captured in a dedicated HU context file.

**Domain layer**
- `Mission` authoring baseline is the established predecessor slice for this service; `MissionActivation` remains a source-readiness concept, not a runtime state
- `Difficulty` and `MaximumTime` already exist as mission-side value objects in the bounded context

**Application layer**
- Mission baseline use cases are already in place around create, update, deactivate, catalog, and detail flows
- Authorization and validation behaviours are already part of the service application pipeline

**Infrastructure / API**
- EF Core persistence, repositories, and `/api/missions` endpoints form the current backend baseline for the service
- `mission-design-service` is already wired into local Docker and the API gateway

**Frontend**
- Mission-management UI is the documented predecessor frontend slice; no trivia-authoring UI is documented yet

**Coverage:** no predecessor context file records a verified coverage percentage for `mission-design-service`; verify the real aggregate percentage during HU-11 phase X.4.

## What HU-11 adds on top (per PRD DES-62 and DES-17)

| Concern | New work |
|---|---|
| `TriviaQuiz` authoring baseline | Introduce the trivia-side aggregate root for creating a quiz in draft authoring state |
| Quiz editability by state | Allow quiz edits only while its lifecycle state permits modification |
| Unified quiz detail | Preserve base quiz information together with associated `TriviaQuestion` data in one authoring model / detail projection |
| Draft consultability | Make saved changes queryable before publication or archival slices land |
| Backend contract | Add trivia quiz create, update, and read surface under a dedicated trivia API contract |
| Frontend flow | Add administrator trivia-quiz create/edit/detail UI aligned to the new backend contract |

## Touched surfaces

- `backend/services/mission-design-service`
- `frontend/` trivia quiz authoring UI
- `backend/frontend` API contract boundary: trivia quiz create/update/detail endpoints and payloads

## Committed phases

| Commit | Phase | Description |
|---|---|---|
| — | — | No commits yet |

## Known quirks / gotchas

- `DES-62` is the authoritative PRD and has the local file `backend/docs/prd/DES-62-mission-design-service-baseline.md`, but the Linear PRD ticket currently carries `ready-for-agent` without `svc:mission-design-service`. Use the local PRD file and the explicit DES-17 relation as the authority for this slice.
- `mission-design-service` currently contains the mission baseline only. `TriviaQuiz`, `TriviaQuestion`, `TriviaOption`, `QuestionTimer`, and trivia repositories/read models are not in the current service tree yet.
- HU-11 precedes HU-14A/HU-14B, HU-12, and HU-13. Do not smuggle full question CRUD, publication/archive workflows, or duplication/retirement into this slice.
- The PRD says HU-11 must keep quiz basic information and associated questions together, but detailed question authoring lands in HU-14A/HU-14B. Treat that as an aggregate/detail-shape obligation for HU-11, not permission to invent the later question-management slice early.
- The mandated `Template Method` must appear explicitly in phase X.1 and X.2 scope and gate lines. If it drops out of those phase gates, the generated plan is defective.

# HU-12 Context — Publicación y archivado de quizzes de trivia

> Paste this section into any agent session that needs context for HU-12.
> Last updated: 2026-06-01 | Branch: `feature/hu-12-trivia-quiz-publication-and-archive`

## State

- DES-18 (HU-12): **Backlog**, labels: `ready-for-agent`, `svc:mission-design-service`, `Feature`
- DES-21 (HU-14B): **Done**
- DES-20 (HU-14A): **Done**
- DES-17 (HU-11): **Done**
- DES-14 (HU-09): **Done**
- DES-62 (PRD): **Backlog**, labels: `ready-for-agent`
- Branch: `feature/hu-12-trivia-quiz-publication-and-archive` (branch from `develop`)

## Required design patterns

- `Template Method`
  - Why: publication/archival requires a stable readiness validation pipeline.
  - Phase owner: X.1 Domain and X.2 Application
  - Concrete obligation: model one stable trivia-quiz lifecycle validation workflow with overridable publish-vs-archive steps; do not duplicate readiness rule sequencing across handlers or scatter publishability checks into ad-hoc conditionals.

## What predecessors have already landed

`mission-design-service` now has four same-service predecessors in Linear as **Done**: HU-09 (`DES-14`), HU-11 (`DES-17`), HU-14A (`DES-20`), and HU-14B (`DES-21`). HU-11 and HU-14A have dedicated context files and are the main reuse sources here. HU-09 and HU-14B do not have dedicated context files in `backend/docs/`, and `backend/services/mission-design-service/README.md` is intentionally sparse, so those predecessor details are recorded conservatively.

**Domain layer**
- HU-09 established the service baseline around `Mission` authoring and source-readiness concepts that remain separate from runtime session state
- HU-11 introduced `TriviaQuiz` as the trivia-side aggregate root baseline and established the quiz lifecycle/editability boundary for authoring
- HU-11 already carried the mandated `Template Method` for stable quiz authoring validation
- HU-14A extended the existing `TriviaQuiz` aggregate with `TriviaQuestion` and `TriviaOption` authoring behavior, including associated-question shape inside trivia detail reads
- HU-14A explicitly deferred the deeper invalid-rule matrix to HU-14B; DES-21 is now **Done**, but because there is no `backend/docs/hu14b-context.md`, verify the exact landed validation baseline in source during implementation rather than assuming undocumented details

**Application layer**
- Mission-side create/update/deactivate/catalog/detail flows are already the service baseline
- `AuthorizationBehaviour` and `ValidationBehaviour` already exist in the service pipeline
- HU-11 added trivia-side create/update/query use cases and repository/read-model abstractions for `TriviaQuiz`
- HU-14A extended the trivia application surface for question and option authoring

**Infrastructure / API**
- EF Core persistence baseline, repositories, and `/api/missions` endpoints already exist from HU-09
- HU-11 added the trivia-side persistence/API baseline for quiz authoring and draft consultation
- HU-14A extended the trivia contract and persistence model for question/option authoring
- `mission-design-service` is already wired into local Docker and the API gateway

**Frontend**
- Mission-management UI is the original predecessor frontend slice
- HU-11 added trivia-quiz create/edit/detail UI
- HU-14A extended that flow with question and option authoring

**Coverage:** no predecessor context file records a verified aggregate coverage percentage for `mission-design-service`; verify the real service percentage during HU-12 phase X.4.

## What HU-12 adds on top (per PRD DES-62 and DES-18)

| Concern | New work |
|---|---|
| Quiz lifecycle transitions | Add the explicit publish/archive behavior on top of the existing trivia authoring baseline |
| Publication readiness | Allow publication only when a quiz satisfies the established validity rules and state preconditions |
| Source readiness for sessions | Ensure only published quizzes are considered usable for trivia session creation; draft or archived quizzes are not source-ready |
| Historical retention | Archive a quiz to withdraw it from future sessions without destructive removal |
| State projection | Reflect draft/published/archived state coherently in backend reads used by administration and operation views |
| Backend contract | Extend the trivia API contract with publish/archive operations and any state/readiness projection needed by the verified consumers |
| Frontend flow | Extend the trivia administration UI with publish/archive actions and visible state feedback aligned to the backend contract |

## Touched surfaces

- `backend/services/mission-design-service`
- `frontend/` trivia quiz administration UI
- `backend/frontend` API contract boundary: trivia publish/archive endpoints and quiz state/readiness payloads

## Committed phases

| Commit | Phase | Description |
|---|---|---|
| — | — | No commits yet |

## Known quirks / gotchas

- `DES-62` is the authoritative PRD and has the local file `backend/docs/prd/DES-62-mission-design-service-baseline.md`, but the Linear PRD ticket does not currently carry `svc:mission-design-service`. Use the local PRD file and the resolved DES-18 relation as authority for this slice.
- The predecessor fast path is incomplete for this service: there is no dedicated `backend/docs/hu09-context.md` or `backend/docs/hu14b-context.md`, and the service README is sparse. Treat HU-11 and HU-14A context files as the authoritative documented reuse baseline, and verify the exact HU-14B landed state in source before extending it.
- `DES-62` makes `SessionOperations` a consumer of source-readiness facts rather than an owner of mission-design content. HU-12 must expose or preserve readiness facts for published quizzes, but it must not introduce runtime session orchestration or cross-context mutations.
- HU-13 is the dedicated follow-on slice for duplication and retirement of used quizzes. HU-12 should publish and archive quizzes cleanly, but it must not smuggle duplication flow or destructive-delete rules into this slice.
- The mandated `Template Method` must appear explicitly in phase X.1 and X.2 scope and gate lines. If it drops out of those phase gates, the generated plan is defective.

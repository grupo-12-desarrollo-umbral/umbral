# Workflow Prompts — Feature Slice Branches With Backend Phase Gates

This document shows how to drive implementation prompts when the branch and PR represent a feature slice rather than a whole backend service.

The examples below keep the backend phase gates intact while allowing one slice to include both:

- `backend/services/...`
- `frontend/...`

---

## Core rule

The branch is **feature-scoped**.

The backend prompts are still **phase-scoped**.

That means:

- one branch can contain frontend work plus backend work
- one backend prompt still covers exactly one phase in exactly one backend service
- one draft PR represents the user-visible slice

---

## Naming

Use branch names like:

- `feature/trivia-quiz-authoring`
- `feature/trivia-quiz-publish`
- `feature/participant-rejoin-session`

Avoid:

- `feature/mission-design-service`
- `feature/session-operations-service`

---

## Recommended slice shape

Prefer this shape:

1. One HU or tightly-coupled HU bundle
2. One frontend flow
3. One backend service where possible

If a slice requires multiple backend services, keep the same branch but finish one backend service at a time.

---

## Example slice — trivia quiz authoring

Scope:

- HUs: `HU-11`, `HU-14A`, `HU-14B`, `HU-12`, `HU-13`
- Frontend area: `frontend/` admin trivia authoring flow
- Backend service: `backend/services/mission-design-service`
- PRD: `backend/docs/prd/DES-62-mission-design-service-baseline.md`
- Branch: `feature/trivia-quiz-authoring`

This is still a single backend service, but the branch name reflects the feature the user experiences.

---

## Pre-flight

Create the slice branch:

```text
git checkout develop
git checkout -b feature/trivia-quiz-authoring
```

Move the relevant HUs to **In Progress** in Linear:

```text
Move HU-11, HU-14A, HU-14B, HU-12, and HU-13 to In Progress in Linear.
```

If a GitHub issue exists for the slice, keep its number available for commit `Ref:` lines and the final PR description.

---

## Backend Phase 2.1 — Domain layer

Type this prompt:

```text
Read backend/docs/prd/DES-62-mission-design-service-baseline.md,
backend/docs/ddd_solution_model.md,
backend/docs/bd_umbral_entity_spec.md, and
backend/plans/multi-phase-service-implementation.md.

Implement Phase 2.1 — TriviaQuiz domain layer in
backend/services/mission-design-service/src/Domain/:

- TriviaQuiz aggregate root (Title, Description, Status, Questions collection)
- TriviaQuestion child entity (QuestionText, Timer, Score, Explanation, Options)
- TriviaOption child entity (Text, IsCorrect)
- TriviaQuizStatus enum: Draft, Published, Archived
- QuestionTimer value object (range-validated; fix any missing ranges as explicit domain rules)
- TriviaPublicationPolicy domain service
- Domain events: TriviaQuizCreated, TriviaQuizDetailsUpdated, TriviaQuizPublished,
  TriviaQuizArchived, TriviaQuestionAdded, TriviaQuestionUpdated, TriviaQuestionRemoved
- Domain exceptions: one per invariant (option count, single correct option,
  score range, timer range, not publishable, already used)

Gate: dotnet build exits 0, all types resolve.
Do not touch any other backend layer or the frontend.
```

After the gate passes, commit:

```text
feat(mission-design): phase 2.1 — domain layer

Ref: HU-11, HU-14A, HU-14B, HU-12, HU-13
Ref: #<slice-issue>
```

Then run: `/debrief`

---

## Backend Phase 2.2 — Application layer

Type this prompt:

```text
Read backend/docs/prd/DES-62-mission-design-service-baseline.md,
backend/docs/ddd_solution_model.md, and
backend/plans/multi-phase-service-implementation.md.

Implement Phase 2.2 — TriviaQuiz application layer in
backend/services/mission-design-service/src/Application/TriviaQuizzes/:

Repository interfaces (Application/Common/Interfaces/):
- ITriviaQuizRepository
- ITriviaQuizReadModelRepository

Commands + validators + handlers:
- CreateTriviaQuizCommand
- UpdateTriviaQuizCommand
- AddTriviaQuestionCommand
- UpdateTriviaQuestionCommand
- RemoveTriviaQuestionCommand
- PublishTriviaQuizCommand
- ArchiveTriviaQuizCommand
- DuplicateTriviaQuizCommand

Queries + handlers:
- GetTriviaCatalogQuery
- GetTriviaDetailQuery

DTOs:
- TriviaQuizDto
- TriviaQuizSummaryDto
- TriviaQuestionDto
- TriviaOptionDto

Gate: dotnet build clean + at least one handler unit test green for
CreateTriviaQuizCommandHandler and PublishTriviaQuizCommand
(valid + invalid paths).
Do not touch Domain, Infrastructure, Api, or the frontend.
```

Commit:

```text
feat(mission-design): phase 2.2 — application layer

Ref: HU-11, HU-14A, HU-14B, HU-12, HU-13
Ref: #<slice-issue>
```

Then run: `/debrief`

---

## Backend Phase 2.3 — Infrastructure layer

Type this prompt:

```text
Read backend/docs/prd/DES-62-mission-design-service-baseline.md,
backend/docs/bd_umbral_entity_spec.md, and
backend/plans/multi-phase-service-implementation.md.

Implement Phase 2.3 — TriviaQuiz infrastructure layer in
backend/services/mission-design-service/src/Infrastructure/:

- TriviaQuizConfiguration.cs EF Core config
- TriviaQuestionConfiguration.cs
- TriviaOptionConfiguration.cs
- Add DbSet<TriviaQuiz> TriviaQuizzes to ApplicationDbContext
- TriviaQuizRepository implementing ITriviaQuizRepository
- TriviaQuizReadModelRepository implementing ITriviaQuizReadModelRepository
- Register both repositories in Infrastructure/DependencyInjection.cs
- Run: dotnet ef migrations add AddTriviaQuiz -p Infrastructure -s Api

Gate: migration succeeds + at least one repository integration test green
(persist a TriviaQuiz, retrieve it, assert fields match).
Do not touch Domain, Application, Api, or the frontend.
```

Commit:

```text
feat(mission-design): phase 2.3 — infrastructure layer

Ref: HU-11, HU-14A, HU-14B, HU-12, HU-13
Ref: #<slice-issue>
```

Then run: `/debrief`

---

## Backend Phase 2.4 — API layer plus coverage gate

Type this prompt:

```text
Read backend/docs/prd/DES-62-mission-design-service-baseline.md and
backend/plans/multi-phase-service-implementation.md.

Implement Phase 2.4 — TriviaQuiz API layer in
backend/services/mission-design-service/src/Api/Endpoints/TriviaEndpoints.cs:

POST   /trivia                        CreateTriviaQuizCommand
GET    /trivia                        GetTriviaCatalogQuery
GET    /trivia/{id}                   GetTriviaDetailQuery
PUT    /trivia/{id}                   UpdateTriviaQuizCommand
POST   /trivia/{id}/questions         AddTriviaQuestionCommand
PUT    /trivia/{id}/questions/{qid}   UpdateTriviaQuestionCommand
DELETE /trivia/{id}/questions/{qid}   RemoveTriviaQuestionCommand
POST   /trivia/{id}/publish           PublishTriviaQuizCommand
POST   /trivia/{id}/archive           ArchiveTriviaQuizCommand
POST   /trivia/{id}/duplicate         DuplicateTriviaQuizCommand

Register TriviaEndpoints in Api/DependencyInjection.cs.

Gate: POST /trivia returns 201 and GET /trivia returns 200 via curl or HTTP test.

Then run the coverage gate:
dotnet test --coverage --coverage-output-format cobertura
python .agents/skills/aspnet-backend-testing/scripts/check_cobertura_threshold.py merged.cobertura.xml 95

If below 95%, add the missing tests before committing.
Do not touch the frontend in this phase prompt.
```

Commit:

```text
feat(mission-design): phase 2.4 — api layer

Ref: HU-11, HU-14A, HU-14B, HU-12, HU-13
Ref: #<slice-issue>
```

Then run: `/debrief`

---

## Frontend prompt after backend contracts are green

Once backend phase 2.4 is complete, you can implement or finish the user-facing flow in `frontend/`.

Type a frontend prompt shaped like this:

```text
Read frontend/docs/umbral_user_stories.md and the backend API contract implemented for trivia quiz authoring.

Implement the trivia quiz authoring flow in frontend/ for:

- create quiz
- edit quiz
- manage questions and options
- publish and archive actions
- duplicate action

Use the endpoints already implemented in the backend.

Gate: the admin flow can create a quiz, list quizzes, edit one, add a question,
and publish it successfully against the local backend.
```

Frontend commits should be feature-scoped:

```text
feat(frontend): trivia quiz authoring flow
```

---

## Close out

Open or update the draft PR from the slice branch:

```text
gh pr create --draft --base develop --title "feat: trivia quiz authoring"
```

The PR description should include:

- the HUs covered
- the touched paths: `backend/services/mission-design-service` and `frontend/`
- `Closes #<slice-issue>` where applicable

Then verify each HU acceptance criterion end-to-end and move only the satisfied HUs to **Done**.

---

## Multi-service slice template

If a slice touches multiple backend services, keep this structure:

| Order | Area |
|---|---|
| 1 | backend service A phase X.1 |
| 2 | backend service A phase X.2 |
| 3 | backend service A phase X.3 |
| 4 | backend service A phase X.4 |
| 5 | backend service B phase Y.1 |
| 6 | backend service B phase Y.2 |
| 7 | backend service B phase Y.3 |
| 8 | backend service B phase Y.4 |
| 9 | frontend integration and end-to-end verification |

Do not create a prompt that spans backend services or backend phases, even when the branch spans the full feature.

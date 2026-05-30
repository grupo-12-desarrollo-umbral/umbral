# Workflow — mission-design-service trivia prompts

Covers HU-11, HU-14A, HU-14B, HU-12, HU-13.
PRD: DES-62 (already exists). Branch: `feature/mission-design-service` (already exists).

---

## Pre-flight — move tickets via Linear MCP

Type this prompt:

```
Move HU-11, HU-14A, HU-14B, HU-12, and HU-13 to In Progress in Linear.
```
---

## Phase 2.1 — Domain layer

Type this prompt:

```
Read docs/prd/DES-62-mission-design-service-baseline.md,
docs/ddd_solution_model.md, docs/bd_umbral_entity_spec.md, and
plans/multi-phase-service-implementation.md.

Implement Phase 2.1 — TriviaQuiz domain layer in
services/mission-design-service/src/Domain/:

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
Do not touch Mission or any other layer.
```

After gate passes, commit:

```
feat(mission-design): TriviaQuiz + TriviaQuestion + TriviaOption domain layer

Ref: HU-11, HU-14A, HU-14B, HU-12, HU-13
```

Then run: `/debrief`

---

## Phase 2.2 — Application layer

Type this prompt:

```
Read docs/prd/DES-62-mission-design-service-baseline.md,
docs/ddd_solution_model.md, and plans/multi-phase-service-implementation.md.

Implement Phase 2.2 — TriviaQuiz application layer in
services/mission-design-service/src/Application/TriviaQuizzes/:

Repository interfaces (Application/Common/Interfaces/):
- ITriviaQuizRepository
- ITriviaQuizReadModelRepository

Commands + validators + handlers:
- CreateTriviaQuizCommand       (HU-11)
- UpdateTriviaQuizCommand       (HU-11)
- AddTriviaQuestionCommand      (HU-14A)
- UpdateTriviaQuestionCommand   (HU-14A)
- RemoveTriviaQuestionCommand   (HU-14A)
- PublishTriviaQuizCommand      (HU-12)
- ArchiveTriviaQuizCommand      (HU-12)
- DuplicateTriviaQuizCommand    (HU-13)

Queries + handlers:
- GetTriviaCatalogQuery         (HU-11)
- GetTriviaDetailQuery          (HU-11)

DTOs: TriviaQuizDto, TriviaQuizSummaryDto, TriviaQuestionDto, TriviaOptionDto

Gate: dotnet build clean + at least one handler unit test green for
CreateTriviaQuizCommandHandler and PublishTriviaQuizCommand (valid + invalid paths).
Do not touch Mission or Infrastructure.
```

Commit one per HU:

```
feat(mission-design): CreateTriviaQuiz command + query handlers    Ref: HU-11
feat(mission-design): AddTriviaQuestion command handlers           Ref: HU-14A, HU-14B
feat(mission-design): PublishTriviaQuiz + ArchiveTriviaQuiz        Ref: HU-12
feat(mission-design): DuplicateTriviaQuiz command                  Ref: HU-13
```

Then run: `/debrief`

---

## Phase 2.3 — Infrastructure layer

Type this prompt:

```
Read docs/prd/DES-62-mission-design-service-baseline.md,
docs/bd_umbral_entity_spec.md, and plans/multi-phase-service-implementation.md.

Implement Phase 2.3 — TriviaQuiz infrastructure layer in
services/mission-design-service/src/Infrastructure/:

- TriviaQuizConfiguration.cs EF Core config (owned collections for questions/options)
- TriviaQuestionConfiguration.cs
- TriviaOptionConfiguration.cs
- Add DbSet<TriviaQuiz> TriviaQuizzes to ApplicationDbContext
- TriviaQuizRepository implementing ITriviaQuizRepository
- TriviaQuizReadModelRepository implementing ITriviaQuizReadModelRepository
- Register both repositories in Infrastructure/DependencyInjection.cs
- Run: dotnet ef migrations add AddTriviaQuiz -p Infrastructure -s Api

Gate: migration succeeds + at least one repository integration test green
(persist a TriviaQuiz, retrieve it, assert fields match).
Do not touch Mission config or Application layer.
```

Commit:

```
feat(mission-design): TriviaQuiz EF config, repositories, migration

Ref: HU-11, HU-14A, HU-14B, HU-12, HU-13
```

Then run: `/debrief`

---

## Phase 2.4 — API layer + coverage gate

Type this prompt:

```
Read docs/prd/DES-62-mission-design-service-baseline.md and
plans/multi-phase-service-implementation.md.

Implement Phase 2.4 — TriviaQuiz API layer in
services/mission-design-service/src/Api/Endpoints/TriviaEndpoints.cs:

POST   /trivia                        CreateTriviaQuizCommand       HU-11
GET    /trivia                        GetTriviaCatalogQuery          HU-11
GET    /trivia/{id}                   GetTriviaDetailQuery           HU-11
PUT    /trivia/{id}                   UpdateTriviaQuizCommand        HU-11
POST   /trivia/{id}/questions         AddTriviaQuestionCommand       HU-14A
PUT    /trivia/{id}/questions/{qid}   UpdateTriviaQuestionCommand    HU-14A
DELETE /trivia/{id}/questions/{qid}   RemoveTriviaQuestionCommand    HU-14A
POST   /trivia/{id}/publish           PublishTriviaQuizCommand       HU-12
POST   /trivia/{id}/archive           ArchiveTriviaQuizCommand       HU-12
POST   /trivia/{id}/duplicate         DuplicateTriviaQuizCommand     HU-13

Register TriviaEndpoints in Api/DependencyInjection.cs.

Gate: POST /trivia returns 201 and GET /trivia returns 200 via curl or HTTP test.

Then run the coverage gate:
dotnet test --coverage --coverage-output-format cobertura
python .agents/skills/aspnet-backend-testing/scripts/check_cobertura_threshold.py merged.cobertura.xml 95

If below 95%, add the missing tests before committing.
```

Commit one per HU:

```
feat(mission-design): TriviaQuiz CRUD endpoints                    Ref: HU-11
feat(mission-design): question management endpoints                Ref: HU-14A, HU-14B
feat(mission-design): publish + archive + duplicate endpoints      Ref: HU-12, HU-13
```

Then run: `/debrief`

---

## Close out

Open the draft PR:

```
gh pr create --draft --base develop --title "feat(mission-design): TriviaQuiz authoring — HU-11, HU-12, HU-13, HU-14A, HU-14B"
```

Then in Linear verify each HU's acceptance criteria against the running service, and move HU-11, HU-14A, HU-14B, HU-12, HU-13 → **Done**.

---

## Cheatsheet

| Step | What to type |
|---|---|
| Pre-flight | `Move HU-11, HU-14A, HU-14B, HU-12, HU-13 to In Progress in Linear.` |
| Phase 2.1 | Prompt above → `dotnet build` → commit → `/debrief` |
| Phase 2.2 | Prompt above → build + unit tests → commit per HU → `/debrief` |
| Phase 2.3 | Prompt above → migration + integration test → commit → `/debrief` |
| Phase 2.4 | Prompt above → HTTP test + coverage gate → commit per HU → `/debrief` |
| Close | `gh pr create --draft` → verify HUs in Linear → Done |

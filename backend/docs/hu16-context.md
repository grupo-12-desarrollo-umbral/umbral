# HU-16 Context - Realineacion: la creacion de sesiones de trivia se subsume en el modelo mision-wrapper (runtime-snapshot fidelity lock)

> Paste this section into any agent session that needs context for HU-16 (DES-75).
> Last updated: 2026-06-30 | Branch: `feature/hu-16-runtime-snapshot-trivia-substage`
> Supersedes the old DES-23 HU-16 ("crear sesion de trivia desde un quiz publicado"); that model is retired.

## State

- DES-75 (HU-16 realign): **Todo**, labels: `canon-realign`, `needs-rebuild`, `ready-for-agent`, `svc:session-operations-service`, `Feature`. Both required labels present. (The original `svc:mission-design-service` label was dropped 2026-06-30 - see ownership note below.)
- **Resolved mode: realignment-rebuild** (`needs-rebuild` present) - but **verification-dominant**, even more so than HU-17. DES-75 is the realignment map's phase #4 "Session creation" row ("Trivia selection as a `Substage` (`TriviaQuizSelection`), not a session", `canon-realignment-after-mission-runtime-rewrite.md:99`), blocked by DES-22 + DES-24. It is the **rebuild ticket** that supersedes DES-23 (HU-16, Canceled); it is **not** itself in the superseded column. The immutable `MissionRuntimeSnapshot` - including the full trivia-question copy - was **already shipped by HU-15 (DES-22, Done 2026-06-21)** and the single-source invariant was **locked by HU-17 (DES-24, Done 2026-07-01)**. HU-16 does **not** rebuild either - it **locks the snapshot-content-fidelity invariant with explicit tests across all four layers** and retires the last stale quiz-as-source documentation.
- **Owning service: `session-operations-service`** (single `svc:` label after the 2026-06-30 correction). Resolution: the PRD partition puts HU-15-36 in session-operations (`DES-70`), HU-09-14 in mission-design (`DES-62`) - HU-16 is in the session-operations range; `required_patterns_matrix.md` files HU-16 under "SessionOperations - preparation"; the realignment map orders DES-75 in the "Session creation" phase, blocked by DES-22/DES-24 (both session-operations). The trivia `Substage` **authoring** (assigning a whole published quiz via `TriviaQuizSelection`) is **HU-10A's** job (DES-15, mission-design, Done) - HU-16 **consumes** that contract via HU-15's runtime-plan read seam; it does not author it. Mission-design is a landed dependency, not a co-owner.
- Predecessor DES ids: **DES-22 (HU-15, Done 2026-06-21)** and **DES-24 (HU-17, Done 2026-07-01)** - the build-on foundation this HU verifies and locks. Landed-untouched same-service: DES-25 (HU-18), DES-26 (HU-19), DES-27 (HU-20), DES-11 (HU-07A), DES-12 (HU-07B).
- PRD DES id: **DES-70** -> `backend/docs/prd/DES-70-primera-implementacion-de-session-operations-service-hu-15-a-hu-36.md` (authoritative; never re-fetch PRD scope from Linear).
- Branch: `feature/hu-16-runtime-snapshot-trivia-substage`, base `develop` (all dependencies Done/merged; no same-service predecessor In Progress).
- Superseded handling applied: **this HU supersedes DES-23** (HU-16, Canceled). Sibling rebuilds HU-21A (DES-28->DES-76), HU-22 (DES-30->DES-77), HU-33A (DES-44->DES-78) are Canceled and dropped from the predecessor set - never anchor on them.

## Required design patterns

- `Facade` (mandated by `required_patterns_matrix.md` HU-16 row: "Runtime snapshot orchestrates the immutable copy of the mission Composite tree (substages, targets, clues, questions) into `MissionRuntimeSnapshot`").
  - **Realized via HU-15's already-built `CreateSessionFacade`** - the same matrix row's canon note states the Linear HU-16 ticket (quiz-sourced trivia-session creation) is retired and "this Facade is realized via `HU-15`/`HU-17`." HU-16 adds **no new facade** and **no new pattern gate class** - it verifies that the immutable snapshot copy rides on the single `CreateSessionFacade` (`Facade`), and locks the snapshot-fidelity obligation with tests.
  - Phase owner: **X.2 Application** (where `CreateSessionFacade.BuildMissionRuntimeSnapshot` lives).
  - Concrete obligation (verify, do not rebuild): the full published trivia quiz is copied into `MissionRuntimeSnapshot.TriviaQuestionSnapshots` through the single `CreateSessionFacade` orchestration - no ad-hoc handler snapshot logic, no second creation/snapshot path. Canon home per ADR-0012: `Facade` lives in the Application layer, in the slice it orchestrates (`adr/0012-design-pattern-placement-convention.md` §canonical-home table); the existing `Application/Sessions/Commands/CreateSession/` placement is correct.
- **Applies-where note (no new gate):** `POST /api/sessions` is a protected mutation, but HU-16 is **not** in the applies-where `Proxy` set (HU-04/05/36B). The endpoint inherits the standard `Administrator` `AuthorizationBehaviour`/gateway guard (ADR-0001/0002) - note only, **no** new `Proxy` gate. `LiveSession` lifecycle `State` is HU-21A's scope (DES-76); HU-16 only reads the initial `Scheduled` set at creation - no `State` gate.

## What predecessors have already landed

**Build-on (read for derivation):**

- **DES-22 (HU-15) - session-operations-service, Done 2026-06-21.** Rebuilt session creation around `Mission` as the only `SessionSource` and shipped the immutable `MissionRuntimeSnapshot` **including the full trivia-content copy**: `MissionRuntimeSnapshot` (owned by `LiveSession`) with `StageSnapshots` / `TargetSnapshots` / `TriviaQuestionSnapshots`; the `TriviaQuestionSnapshot` / `TriviaOptionSnapshot` VOs (prompt, sequenceOrder, scoreValue, timeLimitSeconds, explanation, options + correct flag); `SubstageSnapshot.CreateTrivia`; the `CreateSessionFacade` whose `BuildMissionRuntimeSnapshot` copies every resolved question/option in mission order (`CreateSessionFacade.cs:114-130`); `IMissionRuntimeSource` read port + HTTP adapter over `GET /api/missions/{id}/runtime-plan`; the `20260621174437_AddMissionRuntimeSnapshot` migration. **This is the snapshot machinery HU-16 verifies and locks** - see `hu15-context.md`, `hu15-brief.md`. HU-16 does not rebuild it.
- **DES-24 (HU-17) - session-operations-service, Done 2026-07-01.** Locked the single-source invariant (`Mission` is the only `SessionSource`; `TriviaQuiz` cannot create a `LiveSession`; no session-level `SessionMode`) with per-layer tests and finished the residual two-source teardown (deleted `SessionMode`, `SessionSourceDoesNotMatchModeException`, the `TriviaSessionSnapshot*` exception pair). **Explicitly kept the live `TriviaQuestionSnapshot*` exceptions** (reused inside `MissionRuntimeSnapshot`). HU-16 builds on HU-17's lock: single-source is a solved invariant; HU-16 adds the orthogonal **snapshot-content-fidelity** invariant on top.

**Landed, untouched by this HU:** HU-18 (DES-25, team association), HU-19 (DES-26, operator assignment), HU-20 (DES-27, assigned-session reads), HU-07A/07B (DES-11/12, membership + reconnect) build on/around the session aggregate but do not inform the snapshot-fidelity invariant - do not anchor on them.

**Superseded / not predecessors (do NOT anchor on):** DES-23 (the old HU-16, Canceled - this HU replaces it), HU-21A (DES-28->DES-76), HU-22 (DES-30->DES-77), HU-33A (DES-44->DES-78).

**Coverage:** session-operations-service carries the HU-07A/07B/HU-15/HU-17 baseline; verify the real service percentage against the ADR-0005 repo gate at phase X.4.

## What this HU adds

| Concern | New work |
|---|---|
| Verification posture | HU-15 already copies the full trivia quiz into the immutable snapshot; HU-17 already locked single-source. HU-16 **locks the snapshot-content-fidelity invariant** as explicit tests and retires the last stale docs. It does **not** rebuild creation or the snapshot. |
| Whole-quiz snapshot (domain) | Unit tests that a trivia `SubstageSnapshot` freezes the **entire** published quiz: every `TriviaQuestionSnapshot` (prompt, sequenceOrder, scoreValue [1,100], timeLimitSeconds [5,120], options + exactly-one-correct) in strict mission order; a trivia substage requires >=1 question; the snapshot is immutable after `LiveSession.Create`. |
| No partial selection (domain) | Assert (test + grep) there is **no** `TriviaQuestionSelection` type anywhere and no partial/ordered-subset selection path - the substage takes the whole quiz (`TriviaQuizSelection`), all questions copied. |
| Facade snapshot fidelity (application) | Test that `CreateSessionFacade.BuildMissionRuntimeSnapshot` copies the full resolved quiz from `IMissionRuntimeSource` into `TriviaQuestionSnapshots` (count + content parity with the source), through the single `Facade` - no second orchestration path, no dropped questions. |
| Persistence fidelity (infrastructure) | Repository round-trip test: a session created from a mission with a trivia substage reloads with **all** trivia questions/options and their correct-answer flags intact. **No new migration** - HU-15's owned-graph mapping already persists them. |
| API (no standalone trivia route) | Endpoint test: `POST /api/sessions` with a trivia-bearing mission -> 201; there is **no** standalone trivia-session / quiz-as-source creation route. |
| Documentation supersession | Retire the last stale quiz-as-source docs: confirm the superseded banners on the old `hu16-context.md`/`prompt_example_feature_hu16.md` (this rebuild overwrites both), and fix the un-bannered stale block `faq/workflow-and-sprint-planning.md:167-173` ("`TriviaQuiz` -> originates Trivia sessions (sessionMode = Trivia)"). Satisfies DES-75 AC #4 (DES-23 superseded + documented). |
| Frontend flow | Verification/cleanup only: confirm no UI affordance offers a standalone trivia-session / quiz-as-source path; trivia is authored into a mission substage upstream. No API contract change. |

## Touched surfaces

- `backend/services/session-operations-service` (owner - snapshot-fidelity invariant tests across all four layers; no production-code change beyond tests)
- `backend/docs/faq/workflow-and-sprint-planning.md` (retire stale quiz-as-source block) + the two overwritten HU-16 docs
- `frontend/` session-creation UI (verification only; no contract change)
- API contract boundary: **unchanged** - `POST /api/sessions` mission-only `CreateSessionRequest` (landed by HU-15); HU-16 only proves the snapshot content is faithfully frozen

## Committed phases

| Commit | Phase | Description |
|---|---|---|
| | | |

## Known quirks / gotchas

- **Do NOT re-implement HU-15 or HU-17.** `MissionRuntimeSnapshot`, the `TriviaQuestionSnapshot`/`TriviaOptionSnapshot`/`SubstageSnapshot` VOs, `CreateSessionFacade`, `IMissionRuntimeSource`, the migration, and the mission-only `POST /api/sessions` contract are Done. Reject any plan that re-adds a snapshot type, a creation facade, a runtime-plan endpoint, or a migration - HU-16 verifies and locks, it does not duplicate.
- **There is nothing to delete in code (confirmed by grep, 2026-06-30):** `TriviaQuestionSelection` does not exist anywhere; `TriviaSessionSnapshot*`, `SessionMode`, `CreateTriviaSession*`, `IPublishedTriviaQuizSource`, `PublishedTriviaQuizDto`, `source_trivia_quiz_id` were already removed by HU-15/HU-17. HU-16 **verifies their absence** (some via the existing `CreateSessionSingleSourceInvariantTests`), it does not delete them.
- **Do NOT delete or rename the `TriviaQuestionSnapshot*` types or exceptions.** `TriviaQuestionSnapshot` / `TriviaOptionSnapshot` and `TriviaQuestionSnapshotRequiresAtLeastTwoOptionsException` / `TriviaQuestionSnapshotRequiresCorrectOptionException` are the **live** trivia-substage content inside `MissionRuntimeSnapshot`. They are exactly what this HU asserts fidelity over.
- **Test-helper naming is not residue.** `CreateTriviaSession(...)` / `CreateTriviaSnapshot(...)` factories in `tests/` build a **mission** snapshot with a trivia substage - functionally canon-aligned. Renaming is optional and out of scope; do not treat them as the retired concept.
- **Migration history is immutable.** `SessionMode`/`TriviaSessionSnapshot` still appear in historical `2026060*_*.Designer.cs` snapshots. The **current** `ApplicationDbContextModelSnapshot.cs` is clean (grep = 0). Do not rewrite migration history and do not add an empty migration.
- **Namespace is `umbral_backend.*`** across all session-ops layers; `src/` is the source root. Match existing files.
- **The whole-quiz rule is the headline invariant.** Canon (`grilling-session-mission-restructure.md:14-17`, `bd_umbral_entity_spec.md:267`): the trivia substage selects the **entire** published quiz in authored order - "there is no partial or ordered-subset selection." HU-16's tests must assert the snapshot copies **all** questions, not a subset.
- **DES-78 (HU-33A trivia orchestration) is blocked on DES-75.** The frozen trivia snapshot is the content trivia play executes against; locking snapshot fidelity here is why a verification-dominant slice is still a gated deliverable.

## Per-phase derivation - authoritative for implementation

> Primary source for the phase subagent (per `backend-agent.md` "Read first").
> Mode = **realignment-rebuild (verification-dominant)**: authority chain **canon docs > tracker AC > existing code**
> (`canon-realignment-workflow.md:19-34`); keep/delete/decide per `:72-84`. Mirror-anchors point **only** at code classified `keep`.
> Canon-delta (`canon-realignment-after-mission-runtime-rewrite.md:22-29`): `Mission` is the only `SessionSource`; there is no session-level `SessionMode`; a trivia `Substage` references one **whole** published `TriviaQuiz` through a `TriviaQuizSelection`; the `MissionRuntimeSnapshot` is immutable and snapshots the full runtime plan at creation.
> Whole-quiz + fidelity canon: `grilling-session-mission-restructure.md:14-17` ("the entire quiz is selected in authored question order; there is no partial or ordered-subset selection"; snapshot immutable), `:55-71` (trivia execution reads the question snapshot; `ScoreValue` snapshotted `:66-68`); `bd_umbral_entity_spec.md:246-270` (`TriviaQuizSelection`, whole quiz `:267`), `:191-244` (`TriviaQuestion`/`TriviaOption`: 2-4 options `:216`, exactly one correct `:216,244`, `scoreValue` [1,100] `:217`, `timeLimit` [5,120] `:218`), `:326-360` (`MissionRuntimeSnapshot`: `triviaQuestionSnapshots` `:343`, immutable `:355`, trivia substage >=1 question with timer/score/options/correct `:359`), `:189` (`TriviaQuiz` is not a `SessionSource`), `:273-325` (`LiveSession`, `Scheduled` at creation `:317`). PRD DES-70:74-76 (snapshot the full plan incl. trivia questions/timers/scores), :181 (immutable snapshot), :183-184 (HU-15/16/17 family), :195-197 (Facade for session creation), :289 (HU-16 rebuilt/superseded around `MissionRuntimeSnapshot`). Pattern: `required_patterns_matrix.md` HU-16 row (`Facade`, realized via HU-15/17); ADR-0004:3; `adr/0012-design-pattern-placement-convention.md` (Facade home = Application slice); `ddd_solution_model.md:434-435,453-454`.

### Phase X.1 - Domain
**Derive** (`bd_umbral_entity_spec.md:246-270,191-244,326-360`; `grilling-session-mission-restructure.md:14-17`; `canon-realignment-after-mission-runtime-rewrite.md:22-29`):
- Lock the **whole-quiz snapshot-content-fidelity** invariant as tests over the as-built domain: a trivia `SubstageSnapshot` (built via `SubstageSnapshot.CreateTrivia`) freezes the **entire** published quiz - every `TriviaQuestionSnapshot` carries `Prompt`, `SequenceOrder`, `ScoreValue`, `TimeLimitSeconds`, `Explanation`, and its owned `TriviaOptionSnapshot`s (each with `OptionText`, `SequenceOrder`, `IsCorrect`); invariants: >=2 options and >=1 correct per question (`TriviaQuestionSnapshotRequires*Exception`), trivia substage >=1 question, strict mission/sequence order, and the snapshot is immutable after `LiveSession.Create`.
- Assert **no partial selection**: there is no `TriviaQuestionSelection` type and no ordered-subset path; the substage takes the whole quiz (`TriviaQuizSelection`).

**Target files** (create | edit - file to mirror):
- create/extend `tests/UnitTests/Domain/.../MissionRuntimeSnapshotTriviaFidelityTests.cs` (or extend the existing snapshot/VO domain tests) - mirror the existing `MissionRuntimeSnapshot`/`TriviaQuestionSnapshot` domain tests and the `MissionRuntimeSnapshotFactory` test helper
- keep (verify, do not rewrite) `src/Domain/Entities/MissionRuntimeSnapshot.cs`, `src/Domain/ValueObjects/{SubstageSnapshot,TriviaQuestionSnapshot,TriviaOptionSnapshot,StageSnapshot,TargetSnapshot}.cs`, `src/Domain/Entities/LiveSession.cs` (`Create` :118), `src/Domain/Exceptions/TriviaQuestionSnapshotRequires*Exception.cs`

**Pattern this phase owns:** none (`Facade` is X.2).
**Gate:** Domain build passes; unit tests lock the whole-quiz snapshot fidelity (all questions/options/correct-flag/score/timer copied in strict order; trivia substage >=1 question; snapshot immutable after creation); assert no `TriviaQuestionSelection` type and no partial-selection path exist; the live `TriviaQuestionSnapshot*` types/exceptions are untouched.

**Existing code (keep / delete / decide):**
- keep `src/Domain/Entities/MissionRuntimeSnapshot.cs`, `src/Domain/Entities/LiveSession.cs`, `src/Domain/ValueObjects/{SubstageSnapshot,TriviaQuestionSnapshot,TriviaOptionSnapshot,StageSnapshot,TargetSnapshot}.cs`, `src/Domain/Exceptions/{TriviaQuestionSnapshotRequires*,TriviaSubstage*,SessionSourceEntityRequiredException}.cs` - canon-aligned; verify + mirror, do not rewrite
- delete none in code (HU-15/HU-17 already removed all quiz-as-source / `SessionMode` / `TriviaSessionSnapshot*` debris; `TriviaQuestionSelection` never existed) - **verify absent**, do not recreate
- decide none - the `TriviaQuestionSnapshot*` family is live; do not touch

### Phase X.2 - Application - **owns `Facade` (verify, realized via HU-15's `CreateSessionFacade`)**
**Derive** (`required_patterns_matrix.md` HU-16 row; `adr/0012-design-pattern-placement-convention.md`; PRD DES-70:195-197; `ddd_solution_model.md:453-454`):
- Verify the matrix's HU-16 `Facade` obligation: the immutable copy of the **full mission Composite tree** - substages, targets, clues, **and** trivia questions (`required_patterns_matrix.md:99`) - into `MissionRuntimeSnapshot` is orchestrated through the **single** `CreateSessionFacade` (`Facade`), via `BuildMissionRuntimeSnapshot` (`CreateSessionFacade.cs:114-130`) mapping the resolved plan from `IMissionRuntimeSource.GetByIdAsync(command.MissionId)` - no second orchestration path, no ad-hoc handler snapshot logic.
- DES-75 fidelity depth on the trivia slice of that copy: `BuildMissionRuntimeSnapshot` copies **every** question and option of the whole published quiz into `MissionRuntimeSnapshot.TriviaQuestionSnapshots` (count + content parity with the source), in mission order - no dropped questions, no partial selection.
- HU-16 adds the **test** that locks this; it does not change the facade.

**Target files** (create | edit - file to mirror):
- create/extend a facade test under `tests/Application.UnitTests/Sessions/Commands/CreateSession/CreateSessionSnapshotFidelityTests.cs` - mirror the existing `CreateSessionFacade`/`CreateSessionSingleSourceInvariantTests` (assert full trivia copy: source quiz of N questions -> snapshot with N `TriviaQuestionSnapshots` with matching options/correct/score/timer)
- keep (verify, do not rewrite) `src/Application/Sessions/Commands/CreateSession/{CreateSessionFacade,ICreateSessionFacade,CreateSessionCommand,CreateSessionCommandHandler,CreateSessionCommandValidator,CreateSessionResultDto}.cs`, `src/Application/Common/Interfaces/IMissionRuntimeSource.cs`, `src/Application/Sessions/Common/MissionRuntimeDto.cs`

**Pattern this phase owns:** `Facade` - the immutable snapshot copy rides on HU-15's single `CreateSessionFacade`; HU-16 verifies fidelity, adds **no new facade/pattern class**.
**Gate:** Application build passes; facade test proves the immutable copy of the **full mission tree** (substages, targets, clues, questions - `required_patterns_matrix.md:99`) into `MissionRuntimeSnapshot` is orchestrated through the single `CreateSessionFacade` (`Facade`) - no second creation/snapshot path, no ad-hoc handler snapshot logic; the DES-75 fidelity depth is asserted on the trivia slice: the **full** published quiz (all questions/options/correct/score/timer, mission order) is copied into `TriviaQuestionSnapshots`, no partial copy; the command carries no quiz/second-source field; no `CreateTriviaSession*`/`IPublishedTriviaQuizSource` path remains (verify absent).

**Existing code (keep / delete / decide):**
- keep `src/Application/Sessions/Commands/CreateSession/*`, `src/Application/Common/Interfaces/{IMissionRuntimeSource,IMissionReadinessSource,ILiveSessionRepository}.cs`, `src/Application/Sessions/Common/{MissionRuntimeDto,MissionReadinessDto}.cs`
- delete none (HU-15 already removed `CreateTriviaSession*`, `IPublishedTriviaQuizSource`, `PublishedTriviaQuizDto`) - **verify absent**
- decide none

### Phase X.3 - Infrastructure
**Derive** (`canon-realignment-after-mission-runtime-rewrite.md:25` immutable snapshot; `structure.md` §session-ops persistence):
- Verify the persisted `LiveSession` + owned `MissionRuntimeSnapshot` graph round-trips **all** trivia content: a session created from a mission with a trivia substage reloads with every `TriviaQuestionSnapshot`/`TriviaOptionSnapshot` and its `IsCorrect` flag intact. HU-15's `LiveSessionConfiguration` owned-graph mapping + `20260621174437_AddMissionRuntimeSnapshot` migration already persist them. **No new migration** (current `ApplicationDbContextModelSnapshot.cs` is clean, grep-verified 2026-06-30).
- HU-16 adds the **test** that locks persistence fidelity.

**Target files** (create | edit - file to mirror):
- create/extend a repository integration test under `tests/IntegrationTests/Persistence/` - mirror the existing `LiveSessionRepositoryIntegrationTests`: round-trip a session whose mission has a trivia substage; assert all trivia questions/options/correct-flags persisted
- keep (verify, do not rewrite) `src/Infrastructure/Persistence/Configurations/LiveSessionConfiguration.cs`, `src/Infrastructure/Persistence/Repositories/LiveSessionRepository.cs`, `src/Infrastructure/Integrations/MissionDesign/{MissionRuntimeSource,MissionReadinessSource}*.cs`

**Pattern this phase owns:** none.
**Gate:** Infrastructure build passes; **no new migration** - assert (`ef migrations add` dry-check or model-snapshot grep) the model is unchanged and clean; repository integration test round-trips a session with a trivia substage and all trivia questions/options/correct-flags intact; no `source_trivia_quiz_id`/quiz-snapshot tables persist.

**Existing code (keep / delete / decide):**
- keep `LiveSessionConfiguration.cs`, `LiveSessionRepository.cs`, `MissionRuntimeSource.cs`, `MissionReadinessSource.cs`, migration `20260621174437_AddMissionRuntimeSnapshot.cs`
- delete none (HU-15 dropped `PublishedTriviaQuizSource*` + the trivia-session tables) - **verify absent**
- decide historical `2026060*_*.Designer.cs` snapshots carry `SessionMode`/`TriviaSessionSnapshot` - **leave as-is** (migration history is immutable; current model snapshot is clean)

### Phase X.4 - Api
**Derive** (`src/Api/Controllers/SessionsController.cs:22-37,182`; ADR-0001/0002 gateway auth; ADR-0005 coverage):
- Verify `POST /api/sessions` accepts a mission carrying a trivia substage and returns `201 Created` (mission-only `CreateSessionRequest(MissionId, Title, MaximumTimeMinutes, ScheduledAt)`, `Administrator`-guarded), and that there is **no** standalone trivia-session / quiz-as-source creation route. HU-16 verifies + covers; it reshapes nothing.
- Retire the last stale quiz-as-source doc block (`faq/workflow-and-sprint-planning.md:167-173`) and confirm the superseded banners on the overwritten HU-16 docs (DES-75 AC #4).

**Target files** (create | edit - file to mirror):
- create/extend an endpoint integration test under `tests/IntegrationTests/Api/` - mirror the existing `CreateSessionEndpointTests`: trivia-bearing mission -> 201; no alternate/quiz-as-source creation route
- edit `backend/docs/faq/workflow-and-sprint-planning.md` (lines 167-173) - replace the stale "`TriviaQuiz` -> originates Trivia sessions (sessionMode = Trivia)" block with the canon model (trivia is a substage of the mission; `Mission` is the only source)
- keep (verify, do not reshape) `src/Api/Controllers/SessionsController.cs`

**Pattern this phase owns:** none - the endpoint inherits the standard `Administrator` `AuthorizationBehaviour`/gateway guard (ADR-0001/0002); HU-16 is not in the applies-where `Proxy` set -> no new gate.
**Gate:** endpoint integration test (trivia-bearing mission -> 201; no standalone trivia-session / quiz-as-source route) + the ineligible-mission rejection (reuse HU-15's 409/422 path); stale quiz-as-source doc block retired + supersession documented; **ADR-0005 coverage** (service >= repo gate).

**Existing code (keep / decide):**
- keep `src/Api/Controllers/SessionsController.cs` (route/auth/201, mission-only `CreateSessionRequest`) - verify, do not reshape
- decide none

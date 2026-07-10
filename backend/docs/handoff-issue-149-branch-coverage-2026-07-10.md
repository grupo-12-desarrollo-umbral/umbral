# Handoff — Issue #149: enforce ≥ branch coverage per service

**Date:** 2026-07-10
**Branch:** `develop` (no feature branch cut yet — do that before committing)
**Goal (as instructed):** run every service's tests via the Makefile and reach **≥95% branch coverage per service** (the issue text itself says ≥93%; the working target for this task is 95%).

## Issue summary

`gh issue view 149`. Today `backend/scripts/cover-gate.sh` gates **line** coverage only
(`/p:ThresholdType=line`). #149 asks to also gate **branch** coverage per service, amend
ADR-0005, and update the ~8 doc locations that state the "93% line" policy. Full acceptance
criteria + doc-location list are in the issue body — do not duplicate here.

Gated services (auto-discovered in `backend/Makefile:58` from `services/*/tests/IntegrationTests/*.csproj`):
`identity-access-service`, `mission-design-service`, `session-operations-service`.
Out of scope (state in ADR): `api-gateway` (E2E-only, coverlet sees ~0%), `scoring-monitoring-service` (no tests/source).

## Progress so far

Measured with `make gate SVC=<svc>` (reads `services/<svc>/coverage/gate/Summary.txt`).

| Service | Baseline branch | Current branch | Line | Status |
|---|---|---|---|---|
| identity-access-service | 84.8% (246/290) | **96.2% (279/290)** | 94.2% | ✅ ≥95% |
| mission-design-service | 74.5% (164/220) | **95.0% (209/220)** | 94.9% | ✅ ≥95% |
| session-operations-service | 79.2% (636/803) | **88.2% (709/803)** | 96.0% | ⏳ need 763/803 (+54) |

All existing tests still pass; every new test passes. `make gate` for identity and mission is green.

### Key design finding (resolves the issue's open question)

The goal is reached by **writing real tests**, not ratcheting — so the `cover-gate.sh` change is a
simple flip to `/p:ThresholdType="line,branch"` with a single `/p:Threshold` applied to **both**
types (coverlet 6.0.4 applies one Threshold value to every listed ThresholdType; it does **not**
support positional per-type values). No cobertura-XML parsing needed. **This edit is NOT done yet**
(task 2 below) — do it only once session is ≥ the bar, or the gate goes red mid-work.

## What's left (do these in order)

### 1. Finish session-operations branch coverage (+54 branches → 763/803)

Analysis tool (writes doubled counts — the XML lists each class twice, so **real uncovered = shown/2**):
```
python3 /tmp/claude-1000/-home-samu-Desktop-umbral/edc11161-78d2-42d9-b70a-89c78b0c0fdb/scratchpad/branchgaps.py \
  services/session-operations-service/coverage/gate/merged.cobertura.xml
```
(If that scratchpad path is gone, the script is short — re-create it: it parses `condition-coverage="x% (a/b)"`
on `<line branch="True">` and ranks classes by uncovered branches. `branch="True"` is capital-T.)

Top remaining session targets (real uncovered, biggest first):
- **LiveSession** (~17): domain guards at `src/Domain/Entities/LiveSession.cs` L212, L388/392,
  L493 (NoActiveSubstage), L633/637/663 (question-timer expiry), L696/701 (empty questions),
  L589 (GetAdvancingQuestionTimerSnapshot). Drive via `LiveSessionTestFactory` (see below). Some
  need building a session into Active with an expired/closed question window.
- **DomainExceptionHubFilter** L94 (~17 *shown as uncoverable*): this is the `MessageFor` **string
  switch** — coverlet over-counts compiler hash-bucket branches that are unreachable. **Do not chase
  this**; treat it as a coverlet artifact. (Its MapCode/CodeFor arms are already covered.)
- **CreateSessionCommandHandler** (4): `src/Application/Sessions/Commands/CreateSession` L83/101/147.
- **Team** (3): `Domain/Entities/Team.cs` L132/141/144.
- **DuplicateTriviaAnswerLink** (3 left): L18 (match by `ReferenceTeamId` — build a session, capture
  the referenceId passed to `AssociateTeam`, set `context.TeamId` = that), L28.
- **RabbitMqIntegrationEventPublisher** (~4), **ConnectionTracker** (Api/Services) L15/L51 (4),
  **SequentialQuestionActivationStrategy** (2), **TriviaAnswerValidationChain** L37 (2, empty-links
  chain → `ValidateAsync` returns CompletedTask), **ListAssignableSessionsQueryHandler** (2),
  **TriviaRoundStartedNotificationHandler** (3), **SelectTeamCommandHandler** (2),
  **EntityEntryExtensions** + **AuditableEntityInterceptor** (3 — same as the identity fix, see below),
  **JoinPolicy**/**SessionParticipant**/**OpenTeamSelectionPolicy**/**JoinContext**/**DomainException**
  L48/**TargetSnapshot** L26/**StageSnapshot** L22 (1 each; several are unreachable `Guid.Empty` guards
  behind `Create()` that always passes `Guid.NewGuid()` — skip those, they can't be hit via the public API).

Note several "50%" items in async `<Handle>d__` state machines are the compiler's fault/no-fault
branch and are effectively uncoverable — prefer real-logic branches.

### 2. Flip `cover-gate.sh` to gate branch coverage (task not started)

`backend/scripts/cover-gate.sh` ~line 118: change `/p:ThresholdType=line` →
`/p:ThresholdType="line,branch"`. Keep the single `/p:Threshold="$THRESHOLD"` (default 93 — the
issue's floor; applies to both line and branch). Update the script's header/usage comment to state
`THRESHOLD` now applies to both types. Then `make gate-all` must pass on develop.
**Only do this after all three services clear the threshold**, else the gate blocks iteration.

### 3. Docs (task not started) — issue lists all 8 locations

Amend `backend/docs/adr/0005-coverlet-msbuild-for-aggregate-coverage.md` (line/branch + the
api-gateway / scoring-monitoring scope note), plus the skill/agent/HANDOFF files the issue enumerates.
While in `docs/current_workflow.md:178`, remove the stale `check_cobertura_threshold.py` reference.

## Conventions learned (reuse these)

- **Run tests only via the Makefile**, sandbox-disabled: `make gate SVC=<svc>`, `make build SVC=<svc>`,
  `make gate-all`. Bash needs `dangerouslyDisableSandbox: true` (nested-userns seccomp blocks the
  toolchain otherwise). A `dotnet watch` dev stack + postgres are running in Docker — integration
  tests use Testcontainers and take ~1 min for session.
- Fast iteration: `dotnet build services/<svc>/tests/<proj>.csproj` to catch compile errors before the
  full gate.
- **Moq** needs an explicit `using Moq;` in mission tests (not in GlobalUsings); it IS global in
  session `Application.UnitTests`.
- To unit-test `internal` classes, add `<InternalsVisibleTo Include="<test-asm>"/>` to the src `.csproj`.
  Already added: identity `Infrastructure.csproj` → `umbral_backend.Application.UnitTests`; mission
  `Application.csproj` → `umbral_backend.Application.UnitTests`. Session Infrastructure already exposes
  internals to `umbral_backend.Infrastructure.IntegrationTests`.
- **Target scores are derived, valid values are 50/100/150** (`50 × Difficulty.ScoreFactor`; Advanced=150).
  `ScoreValue.Create` also requires multiple-of-10 ≤150. Don't invent arbitrary scores.
- Session `LiveSessionTestFactory` (`tests/Application.UnitTests/TestData/`) builds scheduled/active
  trivia & treasure sessions and exposes `TriviaQuestionActivatedAt` (30s window). Use it to drive
  `LiveSession` guards. `session.RegisterTriviaAnswer(teamId, optionOrder, participantId, submittedAt)`.
- The `ProblemDetailsExceptionHandler` / `DomainExceptionHubFilter` switch arms are covered with a
  `FakeMetadataException : Exception, IErrorMetadata` (null `PublicDetail`) iterated over every
  `ErrorCategory` + an out-of-range `(ErrorCategory)999` for the default arm — see the new tests.
- `EntityEntryExtensions.HasChangedOwnedEntities` (in `AuditableEntityInterceptor.cs`) has no owned
  entities in the real model; cover it with a throwaway EF **InMemory** DbContext defining an owned
  navigation (see identity `EntityEntryExtensionsTests.cs`; needs the
  `Microsoft.EntityFrameworkCore.InMemory` package — added to identity UnitTests, add to session
  Domain/Infra test proj if you take this route).

## New test files added this session

Identity (`services/identity-access-service/tests/UnitTests/`): `Infrastructure/Identity/CurrentUserTests.cs`,
`Api/Services/ProblemDetailsExceptionHandlerSwitchArmsTests.cs`, `Api/Services/ApiCurrentUserTests.cs`,
`Domain/Common/ValueObjectEqualsBranchesTests.cs`, `Domain/Exceptions/DeriveErrorCodeTests.cs`,
`Application/Users/Handlers/UserCommandHandlerNotFoundTests.cs`,
`Infrastructure/Identity/Keycloak/KeycloakOptionsValidatorBlankFieldTests.cs`,
`Infrastructure/Persistence/Interceptors/EntityEntryExtensionsTests.cs`, plus a case appended to
`ValidateParticipantMembershipAccessQueryHandlerTests.cs`. Also `src/Infrastructure/Infrastructure.csproj`
(InternalsVisibleTo) and `tests/UnitTests/Application.UnitTests.csproj` (EFCore.InMemory).

Mission (`services/mission-design-service/tests/UnitTests/`): `Application/Missions/Common/MissionStructureEditorTests.cs`,
`Application/Missions/Common/MissionTriviaPublicationCheckerTests.cs`, `Application/Missions/Common/TriviaQuizSelectionGuardTests.cs`,
`Common/Behaviours/PipelineTelemetryBehaviourTests.cs`, `Application/Missions/Queries/GetMissionRuntimePlanQueryTests.cs`,
`Application/Missions/Handlers/MissionCommandHandlerNotFoundTests.cs`, `Application/Missions/Handlers/AddMissionNodeClueVisibilityTests.cs`,
`Application/Missions/Commands/AddMissionNodeCommandValidatorBranchTests.cs`. Also `src/Application/Application.csproj` (InternalsVisibleTo).

Session (`services/session-operations-service/`): extended `tests/IntegrationTests/Api/DomainExceptionHubFilterTests.cs`;
new `tests/IntegrationTests/Api/ProblemDetailsExceptionHandlerSwitchArmsTests.cs`,
`tests/Application.UnitTests/Common/Behaviours/PipelineBehaviourBranchTests.cs`,
`tests/UnitTests/Common/ValueObjectBranchTests.cs`, `tests/UnitTests/ValueObjects/SnapshotGuardTests.cs`,
`tests/Application.UnitTests/Sessions/Common/TriviaAnswerValidation/TriviaAnswerLinkBranchTests.cs`,
`tests/Application.UnitTests/Sessions/LiveSessionGuardBranchTests.cs`.

## Suggested skills

- `aspnet-backend-testing` (`.claude/skills/` and `.agents/skills/`) — the project's testing playbook.
- `code-review` before opening the PR.
- Re-invoke `handoff` if you compact again.

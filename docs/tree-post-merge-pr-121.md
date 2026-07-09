# Backend tree after merging PR #121

`refactor(backend): pipeline-pure Application slices + central Dtos root`
`refactor/app-layer-slice-shape` → `develop` @ `66f67f0` (merge base == develop HEAD, so the merged tree is exactly the branch tree)

Backend tracked files: **1253 → 1248** (−5).

## What changes shape

1. **Central `Application/Dtos/<Aggregate>/` root** per service. Every DTO leaves its slice folder and moves there.
2. **Facades deleted** in session-operations command slices — logic inlined into the handlers (`CreateSession`, `AssignOperatorToSession`, `TransitionSessionState`). Slice-shared facades under `Sessions/Common/` stay.
3. **Cross-cutting pieces get sub-folders**: `<Aggregate>/Common/Authorization/` for proxies, `Sessions/Common/Notifications/` for notification DTOs.
4. New ADR `0013` + findings doc; `structure-guard.sh` updated to enforce the new shape.

---

## identity-access-service — `src/Application/`

```
Application/
├── Common/                    (unchanged: Behaviours, Exceptions, Identity, Interfaces, Models, Security)
├── Dtos/                                      ← NEW
│   ├── Permissions/
│   │   ├── ParticipantEligibleTeamsDto.cs             ← from Permissions/Queries/GetParticipantEligibleTeams/
│   │   ├── ParticipantMembershipAccessDecisionDto.cs  ← from Permissions/Queries/ValidateParticipantMembershipAccess/
│   │   └── ProtectedAccessDecisionDto.cs              ← from Permissions/Queries/CheckProtectedCapabilityAccess/
│   ├── Teams/
│   │   ├── RegisteredTeamMembershipDto.cs             ← from Teams/Queries/GetTeamParticipants/
│   │   └── TeamDto.cs                                 ← from Teams/Common/
│   └── Users/
│       ├── AuthenticateUserResultDto.cs               ← from Users/Commands/AuthenticateUser/
│       ├── AuthenticatedActorProfileDto.cs            ← from Users/Common/
│       └── UserAccessCatalogItemDto.cs                ← from Users/Queries/GetUsers/
├── Permissions/
│   ├── Common/
│   │   ├── Authorization/
│   │   │   └── ParticipantMembershipAccessAuthorizationProxy.cs  ← from Queries/ValidateParticipantMembershipAccess/
│   │   └── ParticipantMembershipAccessReasonCodes.cs             ← from Queries/ValidateParticipantMembershipAccess/
│   └── Queries/{CheckProtectedCapabilityAccess,GetParticipantEligibleTeams,ValidateParticipantMembershipAccess}/
│       └── Query + Handler + Validator only
├── Teams/
│   ├── Commands/{AuthorizeParticipantForTeam,DeactivateTeam,RegisterTeam,UpdateTeam}/
│   └── Queries/{GetTeamById,GetTeamParticipants,GetTeams}/
│   (Teams/Common/ disappears — TeamDto.cs was its only file)
├── Users/
│   ├── Commands/{AssignUserRole,AuthenticateUser,DeactivateUser,ReactivateUser}/
│   ├── Common/
│   │   └── Authorization/
│   │       └── UserRoleAssignmentAuthorizationProxy.cs  ← from Commands/AssignUserRole/
│   └── Queries/{GetAuthenticatedActorProfile,GetUsers}/
├── Application.csproj
├── DependencyInjection.cs
└── GlobalUsings.cs
```

## mission-design-service — `src/Application/`

```
Application/
├── Common/                    (unchanged)
├── Dtos/                                      ← NEW
│   ├── Missions/
│   │   ├── DifficultyDto.cs         ← from Missions/Queries/GetDifficultyCatalog/
│   │   ├── MissionDto.cs            ← from Missions/Common/
│   │   ├── MissionReadinessDto.cs   ← from Missions/Queries/GetMissionReadiness/
│   │   ├── MissionRuntimePlanDto.cs ← from Missions/Queries/GetMissionRuntimePlan/
│   │   └── MissionSummaryDto.cs     ← from Missions/Queries/GetMissionCatalog/
│   └── Trivias/
│       ├── TriviaOptionDto.cs       ← from Trivias/Common/
│       ├── TriviaQuestionDto.cs     ← from Trivias/Common/
│       ├── TriviaQuizDto.cs         ← from Trivias/Common/
│       └── TriviaQuizSummaryDto.cs  ← from Trivias/Queries/GetTriviaCatalog/
├── Missions/
│   ├── Commands/         (13 slices, unchanged)
│   ├── Common/           ActiveMissionTriviaReferenceGuard, MissionDtoMapper,
│   │                     MissionStructureEditor, MissionTriviaPublicationChecker,
│   │                     TriviaQuizSelectionGuard
│   └── Queries/{GetDifficultyCatalog,GetMissionCatalog,GetMissionDetail,
│                GetMissionReadiness,GetMissionRuntimePlan}/
├── Trivias/
│   ├── Commands/         (8 slices, unchanged)
│   ├── Common/Authoring/ (unchanged)
│   └── Queries/{GetTriviaCatalog,GetTriviaDetail}/
├── Application.csproj
├── DependencyInjection.cs
└── GlobalUsings.cs        ← + global using of Dtos namespaces (also in Api/, Infrastructure/, all 4 test projects)
```

## session-operations-service — `src/Application/`

```
Application/
├── Common/                    (unchanged: Behaviours, Exceptions, Interfaces, Security)
├── Dtos/                                      ← NEW (18 DTOs, all from Sessions/Common/ or command slices)
│   └── Sessions/
│       ├── AssignOperatorToSessionResultDto.cs
│       ├── AssociateTeamToSessionResultDto.cs
│       ├── AssociatedSessionTeamDto.cs
│       ├── AuthenticatedActorProfileLookupDto.cs
│       ├── CreateSessionResultDto.cs
│       ├── MissionReadinessDto.cs
│       ├── MissionRuntimeDto.cs
│       ├── ParticipantEligibleTeamsDto.cs
│       ├── ParticipantMembershipAccessDecisionDto.cs
│       ├── ReconnectParticipantResultDto.cs
│       ├── SelectTeamResultDto.cs
│       ├── SessionAssociatedTeamsDto.cs
│       ├── SessionOperatorEligibilityDecisionDto.cs
│       ├── SessionOperatorSummaryDto.cs
│       ├── SessionTeamLobbyDto.cs
│       ├── SessionTimerSnapshotDto.cs
│       ├── TeamReferenceDto.cs
│       └── TransitionSessionStateResultDto.cs
├── Sessions/
│   ├── Commands/
│   │   ├── AssignOperatorToSession/   Command + Handler + Validator
│   │   │     ✗ AssignOperatorToSessionFacade.cs, IAssignOperatorToSessionFacade.cs   DELETED
│   │   │     ✗ SessionAdministrationAuthorizationProxy.cs → Sessions/Common/Authorization/
│   │   ├── AssociateTeamToSession/    (ByCode + plain: Command/Handler/Validator ×2)
│   │   ├── CreateSession/             Command + Handler + Validator
│   │   │     ✗ CreateSessionFacade.cs, ICreateSessionFacade.cs                       DELETED
│   │   ├── DisconnectParticipant/
│   │   ├── ReconnectAuthenticatedParticipant/
│   │   ├── SelectTeam/
│   │   └── TransitionSessionState/    Command + Handler + Validator
│   │         ✗ TransitionSessionStateFacade.cs, ITransitionSessionStateFacade.cs     DELETED
│   ├── Common/
│   │   ├── Authorization/
│   │   │   └── SessionAdministrationAuthorizationProxy.cs   ← from Commands/AssignOperatorToSession/
│   │   ├── Notifications/                                   ← NEW sub-folder
│   │   │   ├── QuestionActivatedNotificationDto.cs
│   │   │   ├── QuestionClosedNotificationDto.cs
│   │   │   ├── SessionStateChangedNotificationDto.cs
│   │   │   ├── SessionTimerUpdatedNotificationDto.cs
│   │   │   └── SubstageAdvancedNotificationDto.cs
│   │   ├── ISessionTeamAssociationFacade.cs      (kept — shared across slices)
│   │   ├── SessionTeamAssociationFacade.cs
│   │   ├── ITriviaRoundOrchestratorFacade.cs     (kept — shared across slices)
│   │   ├── TriviaRoundOrchestratorFacade.cs
│   │   ├── QuestionClosedIntegrationEvent.cs
│   │   ├── SessionResultsFinalizedIntegrationEvent.cs
│   │   ├── RuntimeParticipationGuard.cs
│   │   ├── SessionTimerSnapshotDtoFactory.cs
│   │   └── TriviaQuestionSnapshotSelector.cs
│   ├── EventHandlers/         (unchanged, 4 handlers)
│   ├── Queries/               (unchanged, 6 slices)
│   └── StateTransitions/      (unchanged: Chain, Context, Validator, Validators/×3)
├── Application.csproj
├── DependencyInjection.cs     (−3 lines: facade registrations gone)
└── GlobalUsings.cs
```

`tests/Application.UnitTests/Sessions/Commands/CreateSession/CreateSessionFacadeTests.cs` is **deleted**; the remaining `CreateSession*` tests stay.

`scoring-monitoring-service` is untouched (still only `.gitkeep` placeholders).

## Docs & scripts

```
backend/docs/
├── adr/
│   ├── 0011-application-layer-vertical-slice-organization.md   (M)
│   ├── 0012-design-pattern-placement-convention.md             (M)
│   └── 0013-facades-in-application-command-slices.md           (A, +116)
├── findings/
│   └── adr-0013-facade-command-slice-review-findings.md        (A, +222)
├── adr-0012-pattern-realizations-by-layer.md                   (M)
└── refactors/application-layer-overengineering-checklist.md    (M)

backend/plans/application-layer-cqrs-refactor.md                (M)
backend/scripts/structure-guard.sh                              (M, +44/−17)
```

## Net structural delta

| | |
|---|---|
| New dirs | `Application/Dtos/**` (3 services), `Permissions/Common/Authorization/`, `Users/Common/Authorization/`, `Sessions/Common/Authorization/`, `Sessions/Common/Notifications/`, `backend/docs/findings/` |
| Removed dirs | `identity/Teams/Common/`, `mission/Missions/Queries/GetDifficultyCatalog/` keeps only Query+Handler |
| Deleted files | 6 facade files + `CreateSessionFacadeTests.cs` |
| Renamed files | 40 (DTOs, proxies, notification DTOs) |
| Added files | 4 (2 docs, `Dtos/Missions/DifficultyDto.cs`, `Dtos/Users/AuthenticateUserResultDto.cs` — both recreated at new path with namespace change) |

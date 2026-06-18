# UMBRAL DDD Solution Model

This document is the canonical DDD-oriented view of the solution.

It consolidates the material that was previously split across strategic mapping, service-contract notes, and part of the architecture discussion.

Read it together with:

- `docs/requirements_traceability.md`
- `docs/condensed_roadmap_umbral.md`
- `docs/umbral_user_stories.md`
- `docs/bd_umbral_entity_spec.md`
- `docs/adr/001_platform_shape_adr.md`
- `docs/adr/0001-gateway-central-jwt-validation.md`
- `docs/adr/0002-websocket-token-extraction-at-gateway.md`
- `docs/adr/0004-required-domain-patterns.md`
- `CONTEXT-MAP.md`
- `mission-design-service/CONTEXT.md`
- `session-operations-service/CONTEXT.md`
- `scoring-monitoring-service/CONTEXT.md`
- `identity-access-service/CONTEXT.md`

This document owns:

- subdomains
- bounded contexts
- aggregates and ownership boundaries
- domain events
- repository interfaces
- domain services and policies
- application services derived from the backlog
- cross-context contracts at DDD level

This document does not replace:

- the academic baseline
- the roadmap and product decisions
- the field-level logical entity specification
- the bounded-context-local terminology owned by each service `CONTEXT.md`

## 1. Strategic inputs

The model is constrained by five stable inputs:

- the committed scope and delivery decisions in `condensed_roadmap_umbral.md`
- the backlog and acceptance criteria in `umbral_user_stories.md`
- the context mapping in `CONTEXT-MAP.md`
- the bounded-context-local language in each service `CONTEXT.md`
- the logical entity detail in `bd_umbral_entity_spec.md`

The current committed scope is:

- `Administrador` and `Operador` use the web client
- participant teams use the mobile client
- the platform supports `TreasureHunt` and `Trivia`
- every `LiveSession` is created from exactly one active `Mission`
- each mission `Substage` carries exactly one play mode: `TreasureHunt` or `Trivia`
- scoring, ranking, monitoring, and audit remain shared platform concerns

## 2. Subdomains

| Subdomain                          | Type                      | Main capability                                                                                | Why it exists                                                |
| ---------------------------------- | ------------------------- | ---------------------------------------------------------------------------------------------- | ------------------------------------------------------------ |
| `Live Gameplay Operations`         | Core                      | Run live sessions, manage teams, release clues, receive submissions, and enforce runtime rules | This is the operational heart of UMBRAL                      |
| `Mission and Content Authoring`    | Supporting                | Create missions and trivia content that can later be used in sessions                          | Content preparation changes differently from live operations |
| `Scoring, Ranking, and Monitoring` | Supporting                | Evaluate performance, apply penalties, refresh ranking, and expose supervision views           | Score traceability and monitoring need explicit ownership    |
| `Access and Identity`              | Supporting / externalized | Authenticate actors and enforce access permissions                                             | Identity is necessary but not the primary domain core        |

## 3. Bounded contexts

### `MissionDesign`

Owns:

- `Mission`
- `MissionNode`
- `Target`
- `Clue`
- `TriviaQuiz`
- `TriviaQuestion`
- `TriviaOption`
- `TriviaQuizSelection`
- `MissionActivation`
- source-content readiness for live use

Main use-case focus:

- mission authoring
- mission structure updates
- trivia authoring
- mission readiness and activation

### `SessionOperations`

> **Note:** The `Team` here is the **runtime** entity that exists inside a `LiveSession`
> (carries `currentScore`, `joinStatus`, `progressNodeId`, etc.). HU-04 creates a separate
> **reference-data** Team in the Identity bounded context for the pre-session team catalog.
> See [`Identity`](#identity) below.

Owns:

- `LiveSession`
- `MissionRuntimeSnapshot`
- `Team` (runtime — see note above)
- `SessionParticipant`
- `TeamMember`
- `JoinContext`
- `EvidenceSubmission`
- `TreasureEvidenceSubmission`
- `TargetResolution`
- `TriviaAnswerSubmission`
- `ClueReleaseRecord`
- `SessionEvent`
- `ScoreEntry` references needed to explain runtime scoring facts

Main use-case focus:

- mission-based session creation and lifecycle
- team assignment to live sessions (runtime)
- participant join and reconnection
- clue release and runtime progression
- evidence intake and runtime control

### `ScoringMonitoring`

Owns:

- `ScoreEntry`
- `Penalty`
- `Ranking`
- `AuditHistory`
- monitoring projections
- score-policy execution

Main use-case focus:

- score traceability
- penalty registration
- ranking refresh
- audit and monitoring views

### `Identity`

This is modeled as a supporting bounded context in the current version.

Rules:

- authentication is externalized to `Keycloak`
- authorization decisions remain explicit through the `Identity` contracts and the owning application services
- this context does not replace `SessionOperations` ownership of live-session participation rules, but it does own identity and access language
- HU-04 introduces a **reference-data `Team`** in Identity (id, name, code, active status) — distinct from `SessionOperations`' runtime `Team` (score, progress, live state)

Owns:

- `User`
- `Role`
- `IdentityProviderSession`
- `JoinToken`
- `Team` reference data (HU-04)
- `TeamMembership` record (HU-05)
- actor-access validation facts

Main use-case focus:

- authenticate administrators, operators, and participants
- manage identity-provider session state relevant to the platform
- enforce role/access policies
- validate whether an authenticated actor may enter a requested team/session context
- maintain team reference data and membership records (HU-04/HU-05)

## 4. Context-to-deployable map

| Bounded Context     | Primary deployable           | Notes                                                                           |
| ------------------- | ---------------------------- | ------------------------------------------------------------------------------- |
| `MissionDesign`     | `mission-design-service`     | Owns authoring and source readiness                                             |
| `SessionOperations` | `session-operations-service` | Owns live runtime authority                                                     |
| `ScoringMonitoring` | `scoring-monitoring-service` | Owns scoring, ranking, and monitoring views                                     |
| `Identity`          | `identity-access-service`    | Owns identity and access language while delegating authentication to `Keycloak` |

Supporting deployables:

- `api-gateway`
- `umbral-web`
- `umbral-mobile`
- `rabbitmq`
- `postgres`
- `keycloak`
- optional workers for non-blocking secondary flows

Microservice rules:

- microservices follow business capability cohesion
- aggregates are never split into separate services
- infrastructure components are not bounded contexts
- background workers are execution components, not domain services

## 5. Aggregate map

### `MissionDesign`

Aggregate roots:

- `Mission`
- `TriviaQuiz`

Internal entities and value objects:

- `MissionNode`
- `Target`
- `Clue`
- `SubstagePlayMode`
- `ClueVisibilityPolicy`
- `TriviaQuestion`
- `TriviaOption`
- `TriviaQuizSelection`
- `Difficulty`
- `MaximumTime`
- `MissionActivation`

### `SessionOperations`

Aggregate roots:

- `LiveSession`

Internal entities and value objects:

- `Team`
- `MissionRuntimeSnapshot`
- `SessionParticipant`
- `TeamMember`
- `JoinContext`
- `EvidenceSubmission`
- `TreasureEvidenceSubmission`
- `TargetResolution`
- `TriviaAnswerSubmission`
- `ClueReleaseRecord`
- `SessionEvent`
- `SessionState`
- `SessionSource`
- `SubstageAdvancement`
- `ResolutionTime`
- `TeamCode`

> *Evidence* is the generic umbrella term for a team submission that proves or
> resolves progress in the active mission substage. `EvidenceSubmission` is the
> umbrella base, specialized into exactly two forms:
> `TreasureEvidenceSubmission` (the QR/token scan in treasure-hunt substages)
> and `TriviaAnswerSubmission` (the team answer in trivia substages). Each form
> keeps its own concrete validation and events.

### `ScoringMonitoring`

Aggregate roots:

- `ScoreEntry`
- `Penalty`

Projection-oriented owned models:

- `Ranking`
- `AuditHistory`
- monitoring dashboards

Supporting value objects and policies:

- `ScoreValue`
- `PenaltyReason`
- `ResolutionTime`
- `ScorePolicy`

### `Identity`

Aggregate roots:

- `User`
- `IdentityProviderSession`

Internal entities and value objects:

- `Role`
- `JoinToken`
- access-policy validation facts

Field-level logical detail remains in `docs/bd_umbral_entity_spec.md`.

## 6. Domain events

### `MissionDesign`

- `MissionCreated`
- `MissionDetailsUpdated`
- `MissionStructureChanged`
- `MissionNodeAdded`
- `MissionNodeUpdated`
- `MissionNodeRemoved`
- `SubstagePlayModeAssigned`
- `TargetAddedToSubstage`
- `TargetUpdated`
- `TargetRemovedFromSubstage`
- `ClueAssociatedWithTarget`
- `ClueVisibilityPolicyChanged`
- `MissionActivated`
- `MissionDeactivated`
- `TriviaQuizCreated`
- `TriviaQuizDetailsUpdated`
- `TriviaQuestionAdded`
- `TriviaQuestionUpdated`
- `TriviaQuestionRemoved`
- `TriviaQuizPublished`
- `TriviaQuizArchived`

### `SessionOperations`

- `LiveSessionCreated`
- `MissionRuntimeSnapshotCreated`
- `LiveSessionStarted`
- `LiveSessionPaused`
- `LiveSessionResumed`
- `LiveSessionFinished`
- `LiveSessionCancelled`
- `TeamRegisteredInSession`
- `ParticipantJoinedSession`
- `ParticipantAssignedToTeam`
- `ClueReleasedToTeam`
- `EvidenceSubmissionRegistered`
- `EvidenceSubmissionAccepted`
- `EvidenceSubmissionRejected`
- `TargetResolved`
- `TreasureHuntSubstageWon`
- `SubstageAdvanced`
- `TriviaAnswerSubmitted`
- `TriviaQuestionActivated`
- `TriviaQuestionClosed`
- `TriviaSubstageCompleted`
- `SessionStateChanged`
- `SessionEventRecorded`

### `ScoringMonitoring`

- `ScoreEntryRecorded`
- `ScoreEntryAdjusted`
- `PenaltyApplied`
- `PenaltyReverted`
- `RankingRecalculated`
- `MonitoringProjectionUpdated`
- `AuditHistoryUpdated`
- `ScorePolicyEvaluated`

### `Identity`

- `UserProvisioned`
- `UserAccessDeactivated`
- `UserRoleAssigned`
- `UserRoleRevoked`
- `IdentityProviderSessionStarted`
- `IdentityProviderSessionEnded`
- `JoinTokenIssued`
- `JoinTokenConsumed`
- `AccessDecisionRecorded`

Only completed business facts that another deployable genuinely needs should be promoted to public integration events.

## 7. Repository interfaces

Repository interfaces are defined around aggregate roots and clearly owned projections, not around every entity.

### `MissionDesign`

- `IMissionRepository`
- `ITriviaQuizRepository`
- `IMissionReadModelRepository`
- `ITriviaQuizReadModelRepository`

### `SessionOperations`

- `ILiveSessionRepository`
- `IJoinContextRepository` only if persisted outside the aggregate strategy
- `ISessionReadModelRepository`
- `ITeamBoardReadModelRepository`
- `ISessionEventReadRepository`

### `ScoringMonitoring`

- `IScoreEntryRepository`
- `IPenaltyRepository`
- `IRankingReadModelRepository`
- `IMonitoringProjectionRepository`
- `IAuditHistoryRepository`

### `Identity`

- `IUserRepository`
- `IIdentityProviderSessionRepository`
- `IJoinTokenRepository`
- `IAccessReadModelRepository`

## 8. Domain services and policies

These are domain-level rules that should not be diluted into controllers or infrastructure adapters.

Required implementation patterns from ADR-0004 apply here:

- `Strategy` for difficulty-based and mode-specific scoring policies instead of handler branching
- `Composite` for hierarchical mission authoring structures
- `Facade` for a narrow application-level coordination service that orchestrates session operations and outbound event publication
- `Proxy` for service-side and presentation-side access guards
- `Template Method` for stable validation flows with mode-specific extension points
- `State` for `LiveSession` lifecycle enforcement
- `Chain of Responsibility` for composed validation pipelines around submissions and transitions

`Facade` and `Proxy` are not domain services by themselves. They are required implementation patterns around application coordination and access control that must preserve these domain rules without moving orchestration or authorization branching into handlers and endpoints.

### `MissionDesign`

- `MissionActivationPolicy`
  - decides whether a mission is ready for live use
  - requires at least one stage, at least one substage per stage, exactly one play mode per substage, target and winner-score readiness for treasure hunt, and published question selections for trivia substages
- `MissionStructurePolicy`
  - protects mission hierarchy invariants
- `TriviaPublicationPolicy`
  - decides whether a trivia quiz is publishable
  - validates reusable trivia content for selection into trivia substages; it does not make a quiz a session source

Pattern mapping:

- `Composite`
  - `Mission` owns a tree of `MissionNode` elements representing `Stage`, `Substage`, and `Clue`; substages may own optional clue guidance, and treasure-hunt substages additionally own target objectives
- `Template Method`
  - structural and publication validation should keep one stable flow while allowing specialized checks

### `SessionOperations`

- `SessionCreationPolicy`
  - validates whether a `LiveSession` may be created from an active, runtime-ready `Mission`
- `SessionStateTransitionPolicy`
  - governs valid lifecycle transitions
- `ClueReleasePolicy`
  - prevents invalid or duplicate clue visibility changes
- `JoinPolicy`
  - validates participant access, team membership, late join, and reconnection rules
- `EvidenceValidationPolicy`
  - validates whether an evidence submission can be accepted or rejected
- `TargetResolutionPolicy`
  - prevents duplicate or invalid target resolution
- `SubstageAdvancementPolicy`
  - advances by strict mission order; treasure-hunt substages advance when the first team resolves all targets, and trivia substages advance when the final question timer expires
- `TriviaQuestionTimerPolicy`
  - controls synchronized question activation, pause/resume behavior, close, and duplicate or late answer rejection

Pattern mapping:

- `Facade`
  - application-facing orchestration should be exposed through a narrow coordination service that executes session operations and triggers outbound event publication without leaking that coordination into handlers or endpoints
- `State`
  - `LiveSession` lifecycle transitions must be modeled explicitly through state-aware behavior
- `Chain of Responsibility`
  - QR submissions, trivia answers, and transition guards should compose validators instead of centralizing all branching in one handler
- `Template Method`
  - validation flows may keep one invariant sequence while delegating mode-specific checks
- `Proxy`
  - access to restricted clues, operator dashboards, protected panels, and other sensitive runtime operations should be enforced through guards before mutation paths execute or protected data is exposed

### `ScoringMonitoring`

- `ScorePolicy`
  - decides how points are granted, deducted, or normalized
- `RankingPolicy`
  - orders teams and applies tie-break rules
- `PenaltyPolicy`
  - validates penalty eligibility and justification requirements

Pattern mapping:

- `Strategy`
  - score calculation, difficulty weighting, and normalization rules should be implemented as swappable strategies instead of proliferating conditional branches

### `Identity`

- `AccessPolicy`
  - validates whether an authenticated actor may access a protected capability or team/session context
- `JoinTokenPolicy`
  - validates issuance, expiration, consumption, and replay constraints for join tokens
- `IdentityProvisioningPolicy`
  - keeps application-side user/role state aligned with externalized authentication

Pattern mapping:

- `Proxy`
  - service and presentation layer guards should restrict protected capabilities, restricted panels, and other sensitive resources based on role and policy facts before downstream execution

## 9. Application services derived from the backlog

These application services are derived from `docs/umbral_user_stories.md`. They coordinate use cases, invoke domain rules, and persist changes through repositories.

### `MissionDesign`

- `CreateMission`
- `UpdateMission`
- `AddMissionNode`
- `UpdateMissionNode`
- `ActivateMission`
- `DeactivateMission`
- `CreateTriviaQuiz`
- `AddTriviaQuestion`
- `PublishTriviaQuiz`
- `ArchiveTriviaQuiz`
- `GetMissionCatalog`
- `GetMissionDetail`
- `GetTriviaCatalog`
- `GetTriviaDetail`

Backlog alignment:

- `HU-09` to `HU-14`

### `SessionOperations`

- `CreateLiveSession`
- `CreateMissionRuntimeSnapshot`
- `AssignOperatorToSession`
- `RegisterTeamInSession`
- `JoinParticipantToSession`
- `ReconnectParticipantToSession`
- `StartSession`
- `PauseSession`
- `ResumeSession`
- `CancelSession`
- `ReleaseClue`
- `RegisterEvidenceSubmission`
- `AcceptEvidenceSubmission`
- `RejectEvidenceSubmission`
- `ResolveTarget`
- `AdvanceSubstage`
- `ActivateTriviaQuestion`
- `CloseTriviaQuestion`
- `SubmitTriviaAnswer`
- `GetSessionBoard`
- `GetTeamBoard`
- `GetSessionHistory`

Backlog alignment:

- `HU-15` to `HU-36`

### `ScoringMonitoring`

- `RecordScoreEntry`
- `ApplyPenalty`
- `RevertPenalty`
- `RecalculateRanking`
- `RefreshMonitoringProjection`
- `RefreshAuditHistory`
- `GetRankingSnapshot`
- `GetScoreHistory`
- `GetMonitoringDashboard`
- `GetAuditHistory`

Backlog alignment:

- `HU-37` to `HU-40`

### `Identity`

- `AuthenticateUser`
- `DeactivateUserAccess`
- `AssignUserRole`
- `IssueJoinToken`
- `ValidateParticipantMembershipAccess`
- `ReconnectAuthenticatedParticipant`
- `GetUserAccessCatalog`
- `GetAuthenticatedActorProfile`

Backlog alignment:

- `HU-01` to `HU-08`

## 10. Cross-context contracts

The DDD rule is ownership first.

`MissionDesign` exposes:

- mission summary and mission snapshot contracts
- mission activation status
- published trivia quiz question contracts for mission readiness and trivia-substage selections
- mission readiness facts for session creation

`SessionOperations` exposes:

- live-session summary and `MissionRuntimeSnapshot` contracts
- team progress and team board contracts
- join-context views
- runtime completion facts that may affect scoring or monitoring

`ScoringMonitoring` exposes:

- score-entry history
- penalty facts
- ranking snapshots
- monitoring dashboards
- audit-history projections

`Identity` exposes:

- authenticated actor views
- actor role snapshots
- join-token validation facts
- access decisions and access-validation results

Communication rules:

- synchronous calls are narrow validation or lookup requests
- asynchronous events describe completed business facts
- the minimum explicit RabbitMQ workflow is `EvidenceSubmissionRegistered` published after successful evidence registration, then consumed by audit/history, notification, and secondary recalculation or projection-support flows
- no service can read another service's persistence directly
- no shared domain library can collapse the bounded contexts

## 11. What this document makes unnecessary as separate canonicals

This document absorbs the purpose of:

- strategic subdomain to bounded-context mapping
- service contract and repository catalog material
- DDD-level microservice split heuristics

Those materials may still exist in `docs/archive/`, but they should no longer be treated as active canonical references.

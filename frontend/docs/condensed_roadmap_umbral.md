# UMBRAL Roadmap Delta

This document keeps only the parts of `docs/archive/roadmap_umbral.md` that are not already owned in more appropriate detail by other documents in `docs/`.

Use it as:

- the short list of roadmap-specific decisions and delivery guidance that remain unique
- the index of what was intentionally delegated out of the roadmap

Do not use it as the owner for architecture detail, glossary, entity detail, or full requirements traceability.

## 1. What Is Delegated Elsewhere

These areas were present in `docs/archive/roadmap_umbral.md`, but they are already covered elsewhere and should not be repeated in detail here.


| Roadmap area                                                          | Owner document                         |
| --------------------------------------------------------------------- | -------------------------------------- |
| Academic RF, RNF, RB baseline                                         | `docs/academic_requirements_umbral.md` |
| Active requirement-to-owner traceability                              | `docs/requirements_traceability.md`    |
| User-visible flows, acceptance criteria, technical enablers           | `docs/umbral_user_stories.md`          |
| Canonical requirement-to-owner traceability, including explicit RNF proof | `docs/requirements_traceability.md` |
| Bounded contexts, aggregates, contracts, repositories, domain events  | `docs/ddd_solution_model.md`           |
| Logical entity shape, fields, constraints                             | `docs/bd_umbral_entity_spec.md`        |
| Platform shape, deployables, communication rules, data ownership      | `docs/adr/001_platform_shape_adr.md`   |
| Architecture defaults                                                 | `docs/references/architecture.md`      |
| Repository and service structure guidance                             | `docs/references/structure.md`         |
| Engineering conventions                                               | `docs/references/conventions.md`       |
| Testing guidance                                                      | `docs/references/testing.md`           |
| Logical requirement-to-model coverage and partial/full coverage notes | `docs/bd_umbral_entity_spec.md`        |

Delegation clarification:

- `docs/references/structure.md` is the owner for backend solution structure only
- the old multi-repo illustration from `docs/archive/roadmap_umbral.md` is superseded and should no longer be treated as the active recommendation
- the active stance is the one aligned with `docs/adr/001_platform_shape_adr.md`: one backend repository with explicit service boundaries; web and mobile repository placement is a delivery choice, not a canonical architecture rule


For those topics, this delta document only keeps what was still unique to the roadmap.

Traceability note:

- this document is not the owner for the full RF/RNF/RB walkthrough
- the academic requirement baseline lives in `docs/academic_requirements_umbral.md`
- the active cross-document traceability map, including explicit RNF proof, lives in `docs/requirements_traceability.md`
- the logical requirement-to-model coverage matrix lives in `docs/bd_umbral_entity_spec.md`
- the roadmap-specific architectural closure that turns the remaining `Parcial` areas into an implementable delivery stance is kept here in Section 4.1

## 2. Unique Positioning Kept From the Roadmap

### 2.1 Defense Framing

UMBRAL must be defended at two explicit levels:

- the academic baseline required by the project brief
- the additional committed scope chosen by the team

The academic baseline remains centered on:

- `Mission`
- `MissionNode`
- `LiveSession`
- `Team`
- `EvidenceSubmission`
- `ScoreEntry`
- `SessionEvent`

The committed team extensions are:

- `Trivia` as a second supported session mode
- React Native Expo as the participant client
- QR-supported mission refinement through `Target`, `TreasureEvidenceSubmission`, and `TargetResolution`
- individually authenticated participants with multi-device team participation

The key defense rule is:

- explain the academic baseline first
- present the extensions as refinements over the same session, evidence, scoring, ranking, and audit backbone
- avoid presenting `Trivia` or QR-specific language as a replacement for the academic vocabulary

### 2.2 Scope Position That Must Stay Explicit

Committed scope restrictions that should remain easy to find:

- the platform supports exactly two session modes: `TreasureHunt` and `Trivia`
- every `LiveSession` belongs to exactly one mode
- hybrid sessions are out of scope
- one participant app serves both committed flows
- team participation is multi-device and individually authenticated

### 2.3 Decision Rationale That Must Stay Explicit

The roadmap still needs a compact record of the main decisions and rejected directions, because the delegated docs mostly preserve the target state, not the product/architecture argument.

Keep the following rationale active:

- microservices were chosen to preserve bounded-context ownership, persistence boundaries, and cross-service integration instead of collapsing the model into one backend process
- one web app was chosen for `Administrador` and `Operador` because separate role-specific web apps would add coordination cost without business value
- `Next.js` remains a presentation layer; business logic ownership stays in the .NET backend rather than moving into web server actions
- one React Native Expo participant app was chosen for both modes; participant-web fallback, anonymous team-only access, and single-device-per-team restrictions remain rejected
- `Keycloak` and `OIDC` were chosen to externalize identity across web and mobile clients; ASP.NET Identity as the baseline and ad hoc JWT issuance remain rejected
- `Identity` is restored as a supporting bounded context so access/authentication rules keep explicit ownership instead of remaining only as a transversal note
- `SignalR` remains the primary real-time channel; raw WebSockets are not the preferred implementation path
- live sessions snapshot immutable source content at runtime; direct live references to editable missions or quizzes remain rejected
- scoring remains an immutable ledger with derived ranking projections; mutable total-score state as the source of truth remains rejected
- core gameplay validation, answer locking, clue progression, and timer enforcement remain backend-authoritative; client-authoritative timing or validation remains rejected
- `RabbitMQ` remains reserved for secondary asynchronous reactions and must stay off the critical synchronous gameplay path
- the mission hierarchy remains bounded to `Stage`, `Substage`, and `Clue`; graph-style progression and a generic workflow engine remain out of scope

## 3. Trivia Details Still Unique to the Roadmap

These were the main sections from `docs/archive/roadmap_umbral.md` that were not fully relocated elsewhere yet.

### 3.1 Trivia Content Model

- a `TriviaQuiz` contains an ordered list of `TriviaQuestion` items
- each `TriviaQuestion` has:
  - a prompt
  - `2-4` answer options
  - exactly one correct option
  - configurable points
  - a required per-question time limit
  - an optional explanation shown after reveal
- each answer option is plain text only and is rendered as a button label in the participant app
- trivia is text-only in v1
- no images
- no audio
- no video

### 3.2 Trivia Authoring Constraints

Agreed v1 constraints:

- quiz title max `80` characters
- question prompt max `150` characters
- answer option text max `50` characters
- explanation max `200` characters
- answer options per question: `2-4`
- exactly one correct option per question
- points default: `10`
- points allowed range: `10-25`
- per-question timer default: `10s`
- per-question timer allowed range: `10-30s`
- pre-game countdown fixed system default: `5s`
- between-question result interval fixed system default: `5s`

### 3.3 Quiz Lifecycle

Quiz states:

- `Draft`
- `Published`
- `Archived`

Rules:

- only `Published` quizzes can be used to create sessions
- operators should only see published quizzes during session creation
- a quiz must contain at least one valid question before publish
- every published question must have:
  - prompt
  - `2-4` answer options
  - exactly one correct option
  - points within allowed range
  - timer within allowed range
- published quizzes may be edited directly
- live sessions always snapshot immutable quiz content at session start
- unused quizzes may be deleted
- used quizzes may not be hard-deleted and should instead be archived or deactivated
- admins may duplicate quizzes
- admins may duplicate questions
- admins may delete questions
- question order in v1 follows authored creation order

### 3.4 Trivia Session Lifecycle

Trivia runtime phases:

- `Lobby`
- `PreGameCountdown`
- `ActiveQuestion`
- `QuestionResult`
- `FinalResults`

Academic `SessionState` remains canonical:

- `Scheduled`
- `Preparing`
- `Active`
- `Paused`
- `Finished`
- `Cancelled`

Main flow:

- operator starts the session
- backend runs a `5s` pre-game countdown
- backend activates Question 1
- teams answer within the configured time window
- backend closes the question when the timer ends
- the system computes score and ranking
- the system shows a `5s` result interval
- backend activates the next question automatically
- after the final question result interval, the session moves to `FinalResults`

### 3.5 Trivia Participation and Visibility Rules

Participant rules:

- any participant may submit the team answer
- the first valid answer from the team locks that question for the team
- only one final answer per team per question is accepted
- second submissions for the same team/question are rejected
- team score and progress are shared across connected teammates
- no internal voting or teammate presence mechanics exist in v1
- no late join is allowed after session start
- reconnect is allowed for already-authorized participants

Operator rules:

- during an active question, the operator sees only `answered / not answered` per team
- before question close, the operator does not see the selected option
- after question close, the operator can see:
  - each team's submitted option
  - whether it was correct
  - points awarded
- the operator can pause, resume, cancel, or force finish the session

Scoring and reveal rules:

- every correct team within the answer window earns the configured points
- unanswered or incorrect responses yield `0` points unless a penalty is applied separately
- ranking updates only after question close
- participants do not see which teams have answered during the active window
- after question close, participants see:
  - the correct answer
  - optional explanation text
  - whether their team answered correctly
  - updated shared ranking
- final participant view shows ranking and total points only
- per-question breakdown is operator-facing in v1
- trivia tie-breaker is earliest cumulative correct-answer submission time

### 3.6 Informational Quiz Timing

- an optional total quiz duration may exist as planning metadata
- gameplay still remains driven by per-question timers
- total quiz time does not replace per-question timing
- question closing conditions remain explicit
- backend advancement stays per-question, not one global countdown

## 4. Roadmap-Only Architectural Closure

The referenced docs already cover the logical model and the baseline requirements, but the following closure is still needed to show how the remaining partial areas are made complete in delivery terms.

### 4.1 Closure for Remaining Partial RF

For `RF-12`, `RF-13`, `RF-14`, and `RF-17`:

- `docs/bd_umbral_entity_spec.md` owns the logical basis and currently marks those areas as `Parcial`
- `docs/adr/001_platform_shape_adr.md` and `docs/references/architecture.md` own the general platform and communication rules
- this roadmap delta keeps the implementation closure that explains how those parts become complete in the actual solution

Required closure:

- real-time ranking and dashboard refresh are delivered through `SignalR` over stable read models after authoritative backend outcomes
- `RabbitMQ` publishes integration events only after successful business completion and stays off the critical synchronous gameplay path
- `CQRS` is completed by explicit separation of command handlers, query handlers, and projection refresh strategy
- reconnect behavior must recover authoritative participant and operator state from the backend query side

Implementation result expected:

- ranking and dashboard views are pushed in near real time and are recoverable after reconnect
- event publication is traceable from completed domain fact to broker message
- write-side invariants remain enforced synchronously in the owning service
- read-side projections remain the source of participant and operator views

### 4.2 Design-Pattern Delegation

The academic defense may still require a concise pattern-mapping explanation. The active owner set does not currently keep that summary elsewhere, so it remains here.

Required pattern mapping:

- Factory or Abstract Factory for mode-specific session handlers
- Strategy for mode-specific scoring or progression behavior
- State for trivia runtime phases and treasure progression states
- Observer for SignalR-driven client updates and ranking refresh
- Command through CQRS application flows
- Composite through hierarchical `Mission` and `MissionNode`
- Facade through session orchestration in `SessionOperations`
- Proxy through role and policy-based access guards

### 4.3 Runtime Communication Appendix Retained

The detailed runtime appendix from the full roadmap is intentionally kept here in compact form so the condensed set does not lose operational context.

Command examples:

- `CreateMission`
- `ActivateMission`
- `CreateTriviaQuiz`
- `PublishTriviaQuiz`
- `CreateLiveSession`
- `AssignOperatorToSession`
- `RegisterTeamInSession`
- `IssueJoinToken`
- `JoinParticipantToSession`
- `ReleaseClueToTeam`
- `RegisterEvidenceSubmission`
- `SubmitTriviaAnswer`
- `ApplyPenalty`
- `ChangeSessionState`

Query examples:

- `GetMissionDetail`
- `GetTriviaDetail`
- `GetMissionCatalog`
- `GetTriviaCatalog`
- `GetSessionBoard`
- `GetTeamBoard`
- `GetRankingSnapshot`
- `GetMonitoringDashboard`
- `GetSessionHistory`
- `GetAuditHistory`

SignalR responsibilities:

- ranking updates
- session state changes
- clue release and mission-clue progression updates
- trivia countdown, question activation, answer lock, and result reveal
- timer snapshots
- penalties and relevant session events
- synchronized team progress across active participant devices

Suggested SignalR group strategy:

- `session:{sessionId}`
- `session:{sessionId}:team:{teamId}`
- `operator:{operatorId}`
- `user:{userId}`

RabbitMQ event examples:

- `SessionCreated`
- `EvidenceSubmissionRegistered`
- `TreasureScanValidated`
- `TargetResolved`
- `TriviaAnswerLocked`
- `TriviaQuestionClosed`
- `PenaltyApplied`
- `SessionStateChanged`
- `ParticipantJoinedSession`

RabbitMQ consumer examples:

- audit/history consumer
- notification/alert consumer
- projection refresh or secondary recalculation consumer
- historical consolidation consumer

Internal communication rule:

- use direct synchronous calls only for narrow validations or lookups that require immediate consistency
- use internal or integration events for completed business facts and downstream reactions
- publish RabbitMQ messages only after successful business completion

## 5. Roadmap-Specific Delivery Plan

This is the main part that should remain in a roadmap even after other details are delegated out.

### Phase 0. Validation and Setup

- confirm scope boundaries
- confirm teacher acceptance of Expo as the React Native implementation path
- establish the canonical documentation set and repository baseline

### Phase 1. Foundational Architecture

- establish bounded-context boundaries and service skeletons
- set up web, mobile, gateway, PostgreSQL, RabbitMQ, Keycloak, and SignalR skeletons
- establish logging, validation, and baseline operational conventions

### Phase 2. Access and Authentication

- integrate Keycloak for web and mobile access
- implement role-aware access for `Administrador`, `Operador`, and participants
- validate participant membership and team/session access rules

### Phase 3. Shared Session Backbone

- implement session lifecycle backbone
- assign teams and operators
- create immutable session snapshots
- establish backend-authoritative timer and monitoring shell

### Phase 4. Mission-Clue QR Flow

- implement mission authoring and activation
- support bounded mission-node hierarchy
- implement clue release and QR-supported target validation
- preserve the edge case where a team may resolve a `Target` before formal clue release when the refinement allows it
- if that happens, complete the clue automatically and preserve audit evidence that it was completed without release
- prevent later release of an already completed clue
- complete the mission live flow

### Phase 5. Trivia Flow

- implement trivia authoring and publish/archive flow
- support timed question lifecycle
- implement answer locking, scoring, reveal, and ranking updates

### Phase 6. Scoring and Monitoring

- implement score-entry traceability
- implement penalties and ranking projection
- implement monitoring and audit views

### Phase 7. Real-Time and Async Integration

- wire SignalR updates for live session state
- synchronize team state across active devices
- publish and consume RabbitMQ integration events for secondary flows

### Phase 8. Testing and Hardening

- expand unit, integration, workflow, and E2E coverage where justified
- validate auth rules, timer behavior, scoring, session transitions, and QR/trivia edge cases

### Phase 9. Delivery and Defense Preparation

- validate local reproducibility
- prepare demo and defense narrative
- clean final documentation and traceability material

## 6. Runtime and Delivery Rules That Still Matter

### 6.1 Connectivity Degradation

- if the mobile client loses connection, it becomes read-only
- no offline answer queue exists in v1
- no offline scan queue exists in v1
- on reconnect, the client resynchronizes authoritative state from the backend

### 6.2 Delivery and Persistence Notes

- timers remain backend-authoritative
- when a live session is paused, the active countdown freezes and must resume from remaining time
- one PostgreSQL cluster may host isolated service databases or schema namespaces
- no service may read or write another service's tables directly
- the current treasure scope does not require binary file uploads or external object storage
- Docker Compose covers the core backend and infrastructure stack
- the React Native participant runtime may run separately during development and demo
- the minimum CI path remains: restore dependencies, build backend, run backend tests, build web, run web checks/tests, publish status

## 7. Priority and Scope Reduction

### 7.1 Priority Order

1. platform skeleton and canonical boundaries
2. authentication and authorization
3. shared session backbone
4. mission baseline flow
5. scoring, ranking, and monitoring
6. real-time and async integration
7. trivia extension
8. hardening and defense preparation

### 7.2 Scope Reduction Order If Time Tightens

Reduce scope in this order:

1. mobile E2E automation before core backend/web verification
2. non-essential operator/reporting polish
3. optional trivia presentation refinements
4. optional QR/geolocation refinements that do not affect the academic baseline

Do not reduce:

- academic RF/RNF/RB compliance
- score traceability
- audit history
- role separation
- valid session-state enforcement
- evidence-validation rules

## 8. Risks and Teacher Validation

Main risks:

- the roadmap drifts back into owning content that belongs in other documents
- the team over-invests in extensions before the academic baseline is demonstrable
- cross-service integration adds implementation overhead
- real-time and async concerns create accidental coupling if ownership boundaries are not respected
- drift between delegated references can silently break the condensed-document strategy
- identity setup complexity introduced by Keycloak/OIDC
- scope expansion if both committed modes are implemented too deeply too early
- confusion between shared platform rules and mode-specific rules
- live synchronization bugs across multiple participant devices

Teacher validation items:

- confirm that React Native Expo is acceptable as the React Native implementation path
- confirm that the committed refinements are defended as extensions over the academic baseline, not as replacements
- confirm that the microservice decomposition remains acceptable as a compatible implementation shape for the academic brief

Recommended teacher-check questions:

- Is React Native-only participant access acceptable for the project defense?
- Are `Trivia` and QR-target refinements being defended as extensions over the canonical academic model rather than replacements?
- Is the chosen microservice decomposition acceptable as a compatible implementation shape for the brief?

## 9. Next Cleanup Recommended

The remaining unique product-spec content here is mostly the trivia lifecycle section.

If that lifecycle is promoted into a better owner document, this delta roadmap can shrink further.

Recommended future move:

- move Section 3 into `docs/umbral_user_stories.md` if the goal is product behavior plus acceptance criteria
- or move it into a dedicated `docs/trivia_spec.md` if the goal is a stable feature specification

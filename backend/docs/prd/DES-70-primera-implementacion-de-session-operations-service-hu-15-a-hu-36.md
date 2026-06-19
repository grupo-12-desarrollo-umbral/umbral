# PRD - Primera implementacion de session-operations-service (HU-15 a HU-36)

> Linear: DES-70 (this PRD) · team `umbral-equipo-12`
> Bounded context: `SessionOperations` (`session-operations-service`)
> Local copy created: 2026-06-03

## Problem Statement

The backlog already defines `HU-15` to `HU-36` for the `SessionOperations`
bounded context, and the repository already contains part of the technical
baseline plus early slices such as `HU-07B` and `HU-16`. What is still missing
is one local, service-scoped PRD that turns that backlog into a coherent first
implementation plan for `session-operations-service`.

Without an explicit local PRD, there is a real risk of:

- treating `LiveSession` as a generic container instead of the aggregate root
  with runtime authority
- duplicating rules between treasure-hunt and trivia substages instead of
  consolidating them under `SessionState`, `MissionRuntimeSnapshot`,
  `JoinContext`, `SessionParticipant`, and runtime `Team`
- blurring what is persisted as a frozen source snapshot versus what remains a
  cross-context dependency
- leaking responsibilities from `Identity` or `ScoringMonitoring` into
  `SessionOperations`
- building endpoints, hubs, and read models before the runtime backbone is
  stable

## Solution

Implement the first delivery of `session-operations-service` as the realization
of the `SessionOperations` bounded context around the `LiveSession` aggregate
root, covering the baseline from session creation through live operation.

The service should provide one common runtime backbone for both session modes
(`TreasureHunt` and `Trivia`) at the substage level and specialize only where
the domain requires it.
The first implementation covers:

1. Session preparation
   - create sessions from exactly one active, runtime-ready `Mission`
   - persist a frozen `MissionRuntimeSnapshot` at creation time
   - associate teams and a responsible operator before start
2. Common runtime control
   - govern `SessionState` through valid transitions
   - keep the authoritative timer
   - expose live operational and participant views
3. Mission runtime
   - release optional clue guidance manually or by visibility policy
   - accept, validate, and trace `EvidenceSubmission`
   - validate `Target` resolution in treasure-hunt substages
4. Trivia runtime
   - automate trivia-substage question activation and closure
   - accept only the first valid answer per team
   - reject late or repeated answers
   - reveal correctness and explanation after close
   - restrict operator visibility before question close

This implementation is organized around the deep modules already defined for the
context:

- `LiveSession` as the authoritative runtime aggregate root
- `MissionRuntimeSnapshot` as the explicit immutable boundary with `MissionDesign`
- `JoinContext` and `SessionParticipant` as participation and entry state
- runtime `Team` and `TeamMember` as session-owned team state
- immutable mission snapshots with treasure-hunt and trivia-substage content
- an Application-layer `Facade` for orchestration and side effects without
  leaking that coordination into endpoints or ad-hoc handlers

## User Stories

1. As an Operator, I want to create a `LiveSession` only from an active,
   runtime-ready `Mission`, so the runtime starts from valid content.
2. As an Operator, I want the session to snapshot the full mission runtime plan,
   including substages, targets, clues, trivia questions, timers, and scores, so
   later authoring changes do not mutate live play.
3. As an Operator, I want every session to originate from exactly one mission
   source, so inconsistent configurations are rejected.
4. As an Operator, I want trivia to enter runtime only through a trivia substage
   question selection, so a `TriviaQuiz` is reusable authoring content rather
   than a session source.
5. As an Operator, I want the session to preserve which mission originated it,
   so authoring and runtime remain traceable.
6. As an Operator, I want to assign active teams to a preparing session, so the
   competition is prepared before it starts.
7. As an Operator, I want to query which teams are associated with a session,
   so I can verify setup completeness.
8. As an Operator, I want the session to reject start without associated teams,
   so empty sessions never go live.
9. As an Administrator, I want to assign or change the responsible operator for
   a session, so ownership of session management is explicit.
10. As an Operator, I want to see my assigned sessions with state, source, and
    teams, so I know what I may operate.
11. As an Operator, I want unauthorized actions against sessions blocked, so
    operational policies are respected.
12. As an Operator, I want to move a session through `Scheduled`, `Preparing`,
    `Active`, `Paused`, `Finished`, and `Cancelled`, so the runtime matches the
    real stage of operation.
13. As an Operator, I want invalid transitions rejected with a reason, so the
    runtime state is not corrupted.
14. As an Operator, I want every state change recorded with actor, time, and
    context, so later audit is possible.
15. As a Participant, I want to see the remaining time for the session or
    active question, so I can organize my play.
16. As a Participant, I want the timer to freeze on pause and recover on resume
    or reconnect, so the displayed time is trustworthy.
17. As a Participant, I want a live team board with score, time, and visible
    progress context, so I know how we are doing.
18. As a Participant, I want that board updated without manual reload, so the
    experience is truly live.
19. As an Operator, I want to monitor session state and progress in real time,
    so I can react during operation.
20. As an Operator, I want a central live view of relevant events, evidence, and
    visible ranking, so I can supervise the session.
21. As an Administrator, I want read-only access to sessions and operational
    data, so I can review operation without mutating runtime.
22. As a Participant, I want read access only to the board and ranking that
    correspond to me, so I cannot access unrelated data.
23. As an Operator, I want to release optional `Clue` guidance manually to one
    team or all teams, so I can guide participants without resolving targets for
    them.
24. As an Operator, I want a clue released to one team not to leak to others, so
    team visibility remains independent.
25. As an Operator, I want duplicate release of the same clue to the same team
    rejected, so the runtime stays consistent.
26. As the system, I want clues configured as visible-at-start to become visible
    when a treasure-hunt substage starts, so manual intervention is reduced.
27. As a Participant, I want all targets in the active treasure-hunt substage to
    be immediately resolvable in any order, so progression depends on target
    resolution rather than clue release.
28. As a Participant, I want to submit `EvidenceSubmission` tied to the active
    substage, so my team's progress is recorded.
29. As a Participant, I want submissions blocked when the session does not admit
    them, so out-of-context evidence is rejected.
30. As an Operator, I want session, team, and node validation before evidence is
    accepted, so inconsistent submissions are rejected.
31. As an Operator, I want evidence rejection to include the reason, so the
    client and operator can understand what failed.
32. As a Participant, I want QR/token target scans validated by the server, so
    target resolution is authoritative.
33. As an Operator, I want every evidence item to retain validation state,
    origin, and rejection reason, so runtime audit is preserved.
34. As an Operator, I want detailed evidence review by team and session, so I
    can inspect what happened.
35. As an Operator, I want trivia substages to advance automatically by
    synchronized question timers, so I do not have to drive every question
    manually.
36. As a Participant, I want any authenticated team member to answer the active
    question, so execution is shared across the team.
37. As a Participant, I want only the first valid in-time answer recorded per
    team, so competition rules are clear.
38. As a Participant, I want late or repeated answers rejected, so trivia stays
    fair.
39. As a Participant, I want to see whether we were correct, the right answer,
    and its explanation after close, so I understand the result.
40. As an Operator, I want to see only who answered and who did not during an
    active question, so supervision does not break fairness.
41. As an Operator, I want post-close answer, correction, and points review, so
    round audit is possible.
42. As `Identity`, I want to deliver access facts while leaving final runtime
    admission to `SessionOperations`, so ownership remains correct.
43. As `MissionDesign`, I want `SessionOperations` to consume readiness facts and
    snapshots rather than mutate authoring data, so the boundary is preserved.
44. As `ScoringMonitoring`, I want runtime facts published by
    `SessionOperations`, so scoring and monitoring can project data without
    taking control of progression.
45. As the development team, I want the first implementation organized as
    verifiable slices with high-value seams, so the context can evolve without
    structural ambiguity.

## Implementation Decisions

- `SessionOperations` is implemented around one main aggregate root:
  `LiveSession`. Runtime authority is not distributed across endpoints, hubs, or
  read models.
- `LiveSession` preserves `SessionState`, `SessionSource` pointing to one
  mission, `MissionRuntimeSnapshot`, snapshot title, time restrictions, assigned
  operator, and runtime collections for `Team`, `SessionParticipant`, and
  `JoinContext`.
- Session creation always uses one active, runtime-ready `Mission` and persists
  an immutable `MissionRuntimeSnapshot` in this bounded context.
- `HU-15`, `HU-16`, and `HU-17` form one coherent family: mission-only creation,
  immutable snapshot creation, and rejection of direct `TriviaQuiz` session
  sources.
- `MissionDesign` is consumed only through read/validation ports for source
  readiness and snapshots. `SessionOperations` never edits missions or trivia
  quizzes.
- `Identity` supplies authenticated identity, role, and access facts; late join,
  reconnect, admission, and participation restrictions remain session-owned.
- Runtime `Team` in `SessionOperations` correlates with the reference-data
  `TeamId` from `Identity`, but it is not the same aggregate and does not share
  ownership.
- `SessionState` transitions must use the required `State` pattern where mapped,
  not free-form mutation.
- Complex orchestration belongs in an Application-layer `Facade`, especially for
  session creation, team/operator association, trivia round orchestration, and
  publication boundaries.
- Protected views and restricted actions must pass through `Proxy`-style guards
  where the matrix requires them; no ad-hoc authorization checks in handlers or
  endpoints.
- The authoritative timer belongs to `SessionOperations`, not to any client.
- Treasure-hunt progression is target-based. All targets in the active
  treasure-hunt substage are active immediately, teams may resolve them in any
  order, and the first team to resolve all targets wins that substage.
- Clue release controls optional guidance visibility only; it does not advance
  the substage or resolve a target.
- Trivia progression keeps synchronized question activation, closure,
  first-answer acceptance, late rejection, reveal, and restricted operator
  monitoring separate but coordinated through the same runtime backbone.
- `ScoringMonitoring` never decides what can happen in a session or when.
  `SessionOperations` publishes facts but retains ownership of progression,
  admission, and state.
- Cross-context contracts in this first implementation:
  - inbound from `MissionDesign`: mission detail, mission runtime snapshot, and
    mission-ready facts
  - inbound from `Identity`: authenticated identity, role, membership/access
    facts, reference-data team identity
  - outbound to `ScoringMonitoring`: runtime events and outcomes for scoring,
    ranking, and audit projections
- Endpoints and hubs stay thin. They translate transport, delegate to
  MediatR/facades, and return or push results derived from authorized state.
- Operational reads (`HU-20`, `HU-23`, `HU-24`, `HU-25`, `HU-32`, `HU-36`) are
  separate read surfaces aligned with CQRS.
- The first service delivery follows one functional backbone:
  1. creation from mission and snapshot (`HU-15` to `HU-17`)
  2. team and operator association (`HU-18` to `HU-20`)
  3. lifecycle and timer (`HU-21` to `HU-22`)
  4. live read models (`HU-23` to `HU-25`)
  5. treasure-hunt target runtime and clue visibility (`HU-26` to `HU-32`)
  6. trivia-substage runtime (`HU-33` to `HU-36`)

## Testing Decisions

- Good tests verify observable service behavior: admission/runtime decisions,
  allowed transitions, persisted snapshots, HTTP or ProblemDetails responses,
  published events, and live views visible to the correct actor.
- Preferred seams are the highest useful seams:
  - domain tests for `LiveSession`, runtime `Team`, `JoinContext`, timer rules,
    and submission acceptance/rejection
  - application tests for handlers/facades that coordinate repositories,
    policies, ports, and publication boundaries
  - integration tests for snapshots, repositories, and internal HTTP contracts
  - API and hub tests for operator and participant live surfaces
- Dedicated coverage is expected for:
  - creation from an active, runtime-ready mission
  - immutable `MissionRuntimeSnapshot` creation
  - team and operator association
  - `SessionState` transitions
  - authoritative timer behavior
  - clue visibility and target-based treasure-hunt progression rules
  - `EvidenceSubmission` registration, validation, and rejection
  - QR/target validation
  - synchronized trivia-substage orchestration, first valid answer, and late/repeated rejection
  - post-close reveal and restricted monitoring
- Reuse prior art already present in the repo:
  - `LiveSession` and related domain tests in `session-operations-service`
  - integration-test style under `tests/IntegrationTests`
  - internal HTTP-port patterns such as `ParticipantMembershipAccessClient`
  - snapshot and validator precedents from `mission-design-service` and
    `identity-access-service`
- For live updates, tests should assert which actor receives which update and in
  what runtime state, not the private mechanics of the hub.
- For cross-service contracts, tests should prove `SessionOperations` degrades
  correctly when a source is unavailable or not ready, and that incompatible
  foreign facts do not override session-owned rules.
- The final gate must respect ADR-0005 coverage requirements without artificial
  exclusions in Domain or Application.

## Out of Scope

- Score calculation, ranking ownership, penalties, and derived projections as
  primary responsibilities of `ScoringMonitoring` (`HU-37` onward)
- user management, authentication, credentials, and membership ownership as
  responsibilities of `Identity`
- authoring, publication, archival, duplication, or editing of `Mission` and
  `TriviaQuiz` as responsibilities of `MissionDesign`
- direct trivia sessions created from `TriviaQuiz`
- hybrid substages that combine `TreasureHunt` and `Trivia`
- a generic configurable workflow engine beyond `TreasureHunt` and `Trivia`
- backlog outside `HU-15` to `HU-36`, even when adjacent HUs are related

## Further Notes

- This PRD is grounded in the local canonical docs for `SessionOperations`, the
  trivia backlog, and the explicit separation between runtime authority and
  derived scoring.
- `HU-16` was previously marked `Done` in Linear on 2026-06-03 under the old
  direct-trivia-session model. It should be rebuilt or superseded around
  `MissionRuntimeSnapshot` creation.
- The most sensitive boundary for this service is ownership: `SessionOperations`
  decides what may happen and when inside a live session. Integrations with
  other contexts must preserve that authority.
- 2026-06-16 decision update (ADR-0010): `EvidenceSubmission` is the umbrella
  term for exactly two committed forms — `TreasureEvidenceSubmission`
  (QR/token scan in treasure-hunt substages) and `TriviaAnswerSubmission`
  (team answer in trivia substages). The QR flow remains two ordered facts:
  `EvidenceSubmissionRegistered` then `TargetResolved`. Trivia answers are
  evidence too, but keep their own concrete validation and scoring behavior.
  Text/photo evidence modes are not part of the current canonical model, and
  operator-mediated human review is out of scope for both current forms.
- This local file is the authoritative PRD copy for generator and driver flows.

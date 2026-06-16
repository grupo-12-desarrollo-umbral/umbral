# UMBRAL Logical Entity Specification

This document defines the logical domain shape of the recommended UMBRAL model before translating it into a physical database design.

It is aligned with:

- `docs/condensed_roadmap_umbral.md`
- `docs/requirements_traceability.md`
- `docs/ddd_solution_model.md`

Purpose:

- make entity responsibilities explicit
- define the minimum fields each entity should expose at domain level
- identify key relationships
- state the main business constraints that later support RF, RNF, and RB validation

Modeling note:

- this is a logical entity specification, not a SQL schema
- field names are intentionally technology-agnostic
- value objects and policies are included when they are required to justify business behavior, even if they are not standalone entities in the main diagram
- the roadmap uses `TeamMembership` in one of its legacy sections, but this specification uses `TeamMember` consistently across the active docs.

## MissionDesign

### `Mission`

- Type: aggregate root
- Scope: academic core
- Why it exists: owns mission authoring, mission metadata, hierarchical structure, and readiness for live execution

Suggested fields:

| Field             | Purpose                                                     |
| ----------------- | ----------------------------------------------------------- |
| `missionId`       | Stable mission identity                                     |
| `title`           | Business name shown to operators and administrators         |
| `description`     | Mission summary or briefing                                 |
| `difficulty`      | Academic difficulty value object                            |
| `maximumTime`     | Allowed execution time value object                         |
| `isActive`        | Indicates whether the mission can be used operationally     |
| `activationState` | Captures mission readiness or `MissionActivation` condition |
| `createdAt`       | Audit creation timestamp                                    |
| `updatedAt`       | Audit last modification timestamp                           |
| `archivedAt`      | Optional archival/deactivation timestamp                    |

Relationships:

- one `Mission` contains one or more `MissionNode`
- one `Mission` can originate many `LiveSession`

Key constraints:

- a `Mission` must have at least one `MissionNode`
- a `Mission` can create a `LiveSession` only when `activationState` satisfies `MissionActivation`
- `maximumTime` must be positive

### `MissionNode`

- Type: child entity of `Mission`
- Scope: academic core
- Why it exists: represents the academic progression structure through `Stage`, `Substage`, and `Clue`

Suggested fields:

| Field           | Purpose                                                     |
| --------------- | ----------------------------------------------------------- |
| `missionNodeId` | Stable node identity inside the mission                     |
| `missionId`     | Parent mission reference                                    |
| `parentNodeId`  | Optional parent for hierarchical structure                  |
| `nodeType`      | Distinguishes `Stage`, `Substage`, or `Clue`                |
| `title`         | Node name or label                                          |
| `description`   | Node instructions or narrative                              |
| `sequenceOrder` | Explicit order among siblings                               |
| `isRequired`    | Indicates whether the node is required for completion rules |
| `createdAt`     | Audit creation timestamp                                    |
| `updatedAt`     | Audit last modification timestamp                           |

Relationships:

- one `MissionNode` belongs to exactly one `Mission`
- one `MissionNode` may have many child `MissionNode`
- one `Clue` node may define zero or one `Target`

Key constraints:

- `nodeType` must be one of `Stage`, `Substage`, or `Clue`
- the parent-child hierarchy must be acyclic
- the mission hierarchy is bounded and domain-specific, not a configurable workflow engine
- a `Stage` node must contain one or more `Substage` nodes
- a `Substage` node may contain `Clue` nodes
- only one `Substage` level is allowed
- a `Clue` node is always a leaf node
- only a `Clue` node may define zero or one `Target`

### `Target`

- Type: child entity of `MissionNode`
- Scope: committed refinement
- Why it exists: defines an optional validation objective attached to some clues in the mission-clue refinement

Suggested fields:

| Field            | Purpose                                                  |
| ---------------- | -------------------------------------------------------- |
| `targetId`       | Stable target identity                                   |
| `missionNodeId`  | Owning clue reference                                    |
| `targetCode`     | Business identifier used for validation                  |
| `validationType` | Declares how the target is resolved, for example QR scan |
| `expectedValue`  | Expected comparison or match value                       |
| `scoreValue`     | Score effect granted on valid resolution                 |
| `isActive`       | Allows deactivation without deleting the target          |

Relationships:

- one `Target` belongs to exactly one `Clue`-typed `MissionNode`

Key constraints:

- a `Clue` may define zero or one `Target` in the mission-clue refinement
- not every `Clue` needs a `Target`
- `Target` is only used when a clue requires machine/server validation such as QR, token, checkpoint, or location confirmation
- `targetCode` must be unique within the owning mission

### `TriviaQuiz`

- Type: aggregate root
- Scope: committed refinement
- Why it exists: owns trivia authoring as a publishable source for trivia-based sessions

Suggested fields:

| Field                      | Purpose                                                       |
| -------------------------- | ------------------------------------------------------------- |
| `triviaQuizId`             | Stable quiz identity                                          |
| `title`                    | Quiz title                                                    |
| `description`              | Quiz summary                                                  |
| `status`                   | Draft, published, archived, or equivalent authoring lifecycle |
| `defaultQuestionTimeLimit` | Default answer window per question                            |
| `createdAt`                | Audit creation timestamp                                      |
| `updatedAt`                | Audit last modification timestamp                             |
| `publishedAt`              | Optional publication timestamp                                |

Relationships:

- one `TriviaQuiz` contains one or more `TriviaQuestion`
- one `TriviaQuiz` can originate many `LiveSession` when session source is trivia

Key constraints:

- a published `TriviaQuiz` must contain at least one `TriviaQuestion`

### `TriviaQuestion`

- Type: child entity of `TriviaQuiz`
- Scope: committed refinement
- Why it exists: represents one timed multiple-choice question in trivia mode

Suggested fields:

| Field              | Purpose                                   |
| ------------------ | ----------------------------------------- |
| `triviaQuestionId` | Stable question identity                  |
| `triviaQuizId`     | Parent quiz reference                     |
| `prompt`           | Question text                             |
| `sequenceOrder`    | Position in the quiz                      |
| `timeLimit`        | Effective answer window for this question |
| `scoreValue`       | Points granted for correct answer         |
| `isActive`         | Allows question deactivation              |

Relationships:

- one `TriviaQuestion` belongs to exactly one `TriviaQuiz`
- one `TriviaQuestion` contains two or more `TriviaOption`

Key constraints:

- a `TriviaQuestion` must define at least two `TriviaOption`
- `sequenceOrder` must be unique within the quiz

### `TriviaOption`

- Type: child entity of `TriviaQuestion`
- Scope: committed refinement
- Why it exists: represents a selectable option for one trivia question

Suggested fields:

| Field              | Purpose                            |
| ------------------ | ---------------------------------- |
| `triviaOptionId`   | Stable option identity             |
| `triviaQuestionId` | Parent question reference          |
| `optionText`       | Visible option content             |
| `sequenceOrder`    | Position in the option list        |
| `isCorrect`        | Marks the canonical correct answer |

Relationships:

- one `TriviaOption` belongs to exactly one `TriviaQuestion`

Key constraints:

- exactly one option should be marked correct unless the quiz mode explicitly allows multiple correct answers

## SessionOperations

### `LiveSession`

- Type: aggregate root
- Scope: academic core
- Why it exists: owns live execution, timing, transitions, participation, and session-level invariants

Suggested fields:

| Field                    | Purpose                                                                       |
| ------------------------ | ----------------------------------------------------------------------------- |
| `liveSessionId`          | Stable session identity                                                       |
| `sessionMode`            | Declares whether the session runs in `TreasureHunt` or `Trivia` mode          |
| `sourceEntityType`       | `SessionSource` value object indicating mission or trivia origin              |
| `sourceEntityId`         | Reference to `Mission` or `TriviaQuiz`                                        |
| `sessionCode`            | Human-usable operational identifier                                           |
| `titleSnapshot`          | Session title copied from source at creation time                             |
| `state`                  | `SessionState` enum                                                           |
| `scheduledAt`            | Planned execution moment                                                      |
| `startedAt`              | Actual start timestamp                                                        |
| `pausedAt`               | Optional pause timestamp                                                      |
| `endedAt`                | Normal completion timestamp                                                   |
| `cancelledAt`            | Optional cancellation timestamp                                               |
| `lastStateChangedAt`     | Timestamp of the most recent lifecycle transition                             |
| `stateReason`            | Optional business explanation for pause, cancellation, or manual intervention |
| `maximumTime`            | Effective time limit snapshot for this session                                |
| `assignedOperatorUserId` | Optional operator responsible for supervision                                 |
| `createdAt`              | Audit creation timestamp                                                      |
| `updatedAt`              | Audit last modification timestamp                                             |

Relationships:

- one `LiveSession` originates from exactly one `Mission` or one `TriviaQuiz`
- one `LiveSession` contains one or more `Team`
- one `LiveSession` contains many `EvidenceSubmission`
- one `LiveSession` contains many `SessionEvent`
- one `LiveSession` may contain many `SessionParticipant`
- one `LiveSession` may contain many `JoinContext`
- one `LiveSession` may contain many `TargetResolution`
- one `LiveSession` may contain many `TriviaAnswerSubmission`

Key constraints:

- a `LiveSession` cannot enter `Active` without at least one `Team`
- valid state changes must follow the `SessionStateTransitionPolicy`
- `sessionMode` must be either `TreasureHunt` or `Trivia`
- hybrid sessions are invalid
- if `sessionMode` is `TreasureHunt`, the source must be a `Mission`
- if `sessionMode` is `Trivia`, the source must be a `TriviaQuiz`

### `Team`

- Type: child entity of `LiveSession`
- Scope: academic core
- Why it exists: represents the competing unit with shared progress, score, and participation state

Suggested fields:

| Field                   | Purpose                                                |
| ----------------------- | ------------------------------------------------------ |
| `teamId`                | Stable team identity                                   |
| `liveSessionId`         | Parent session reference                               |
| `teamCode`              | `TeamCode` business identifier                         |
| `displayName`           | Visible team name                                      |
| `capacity`              | Maximum number of participants the runtime team admits |
| `currentScore`          | Optional cached score view derived from `ScoreEntry`   |
| `currentProgressNodeId` | Optional reference to current progression point        |
| `currentClueNodeId`     | Optional currently available clue for the team board   |
| `releasedClueCount`     | Cached count of clues already made visible to the team |
| `lastScoreCalculatedAt` | Timestamp of the latest score projection refresh       |
| `joinStatus`            | Operational state for participant join flow            |
| `createdAt`             | Audit creation timestamp                               |
| `updatedAt`             | Audit last modification timestamp                      |

Relationships:

- one `Team` belongs to exactly one `LiveSession`
- one `Team` may contain many `TeamMember`
- one `Team` may originate many `EvidenceSubmission`
- one `Team` may originate many `TargetResolution`
- one `Team` may originate many `TriviaAnswerSubmission`
- one `Team` may receive many `ScoreEntry`

Key constraints:

- `teamCode` must be unique within the `LiveSession`
- `capacity` must be greater than zero
- score traceability must come from `ScoreEntry`, even if `currentScore` is cached

### `EvidenceSubmission`

- Type: child entity of `LiveSession`
- Scope: academic core
- Why it exists: preserves the canonical academic record of evidence submitted by a team for a mission node
- Delivery scope: this generic model (text, photo, QR, answer) is canon, but the first delivery implements only the QR mode (`TreasureEvidenceSubmission` + `Target` + `TargetResolution`); non-QR modes and the operator-mediated review path are deferred — see `docs/adr/0010-evidence-qr-only-first-delivery.md`

Suggested fields:

| Field                      | Purpose                                            |
| -------------------------- | -------------------------------------------------- |
| `evidenceSubmissionId`     | Stable submission identity                         |
| `liveSessionId`            | Session reference                                  |
| `teamId`                   | Team reference                                     |
| `missionNodeId`            | Target mission node reference                      |
| `submittedByParticipantId` | Optional submitting participant reference          |
| `submissionType`           | Evidence mode, for example text, photo, QR, answer |
| `payloadReference`         | Logical pointer to evidence content                |
| `submittedAt`              | Submission timestamp                               |
| `validationState`          | `EvidenceValidationState` enum                     |
| `reviewedByUserId`         | Optional operator reviewer reference               |
| `reviewedAt`               | Optional decision timestamp                        |
| `rejectionReason`          | Optional business explanation when rejected        |

Relationships:

- one `EvidenceSubmission` belongs to exactly one `LiveSession`
- one `EvidenceSubmission` belongs to exactly one `Team`
- one `EvidenceSubmission` belongs to exactly one `MissionNode`

Key constraints:

- every submission must reference exactly one `Team`, one `LiveSession`, and one `MissionNode`
- submissions cannot be accepted when `SessionState` is `Paused`, `Finished`, or `Cancelled`
- `validationState` must follow allowed transitions such as pending to accepted or rejected

### `SessionEvent`

- Type: child entity of `LiveSession`
- Scope: academic core
- Why it exists: preserves auditable session history and significant business events

Suggested fields:

| Field            | Purpose                                               |
| ---------------- | ----------------------------------------------------- |
| `sessionEventId` | Stable event identity                                 |
| `liveSessionId`  | Session reference                                     |
| `eventType`      | Event classification                                  |
| `occurredAt`     | Event timestamp                                       |
| `actorType`      | User, participant, system, or team actor category     |
| `actorId`        | Reference to the actor that originated the event      |
| `teamId`         | Optional affected team reference                      |
| `payloadSummary` | Business-readable description or metadata snapshot    |
| `correlationId`  | Links related operations across logs and integrations |

Relationships:

- one `SessionEvent` belongs to exactly one `LiveSession`

Key constraints:

- `SessionEvent` is append-only
- major session state changes should create a `SessionEvent`

### `SessionParticipant`

- Type: child entity of `LiveSession`
- Scope: committed refinement
- Why it exists: represents an authenticated participant identity acting inside a live session

Suggested fields:

| Field                  | Purpose                                              |
| ---------------------- | ---------------------------------------------------- |
| `sessionParticipantId` | Stable participant identity in session scope         |
| `liveSessionId`        | Session reference                                    |
| `externalIdentityId`   | Identity provider subject or user reference          |
| `displayName`          | Participant-visible name                             |
| `participantStatus`    | Joined, active, disconnected, removed, or equivalent |
| `joinedAt`             | Successful join timestamp                            |
| `lastSeenAt`           | Operational heartbeat for multi-device visibility    |

Relationships:

- one `SessionParticipant` belongs to exactly one `LiveSession`
- one `SessionParticipant` may be represented by one or more `TeamMember`
- one `SessionParticipant` may create many `EvidenceSubmission`
- one `SessionParticipant` may consume at most one `JoinToken` for a successful join flow

Key constraints:

- participant actions must be traceable to an authenticated identity

### `TeamMember`

- Type: child entity of `Team`
- Scope: committed refinement
- Why it exists: makes the participant-to-team association explicit when that relationship needs its own state and attributes

Suggested fields:

| Field                  | Purpose                                 |
| ---------------------- | --------------------------------------- |
| `teamMemberId`         | Stable membership identity              |
| `teamId`               | Team reference                          |
| `sessionParticipantId` | Participant reference                   |
| `membershipStatus`     | Active, pending, removed, or equivalent |
| `joinedAt`             | Membership creation timestamp           |
| `leftAt`               | Optional membership end timestamp       |

Relationships:

- one `TeamMember` belongs to exactly one `Team`
- one `TeamMember` references exactly one `SessionParticipant`

Key constraints:

- within one `LiveSession`, a `SessionParticipant` should belong to at most one active `TeamMember`

### `TreasureEvidenceSubmission`

- Type: child entity of `LiveSession`
- Scope: committed refinement
- Why it exists: specializes evidence handling for QR-supported clue submissions

Suggested fields:

| Field                          | Purpose                           |
| ------------------------------ | --------------------------------- |
| `treasureEvidenceSubmissionId` | Stable specialized identity       |
| `evidenceSubmissionId`         | Base evidence reference           |
| `scannedValue`                 | Raw scanned value                 |
| `targetId`                     | Expected target reference         |
| `deviceCapturedAt`             | Optional client capture timestamp |
| `validationOutcome`            | QR-specific validation result     |

Relationships:

- one `TreasureEvidenceSubmission` extends exactly one `EvidenceSubmission`
- one `TreasureEvidenceSubmission` may lead to one `TargetResolution`

Key constraints:

- specialized treasure evidence must reference a base `EvidenceSubmission`

### `TargetResolution`

- Type: child entity of `LiveSession`
- Scope: committed refinement
- Why it exists: records confirmed successful target resolution for progression, audit, and scoring

Suggested fields:

| Field                  | Purpose                                                |
| ---------------------- | ------------------------------------------------------ |
| `targetResolutionId`   | Stable resolution identity                             |
| `liveSessionId`        | Session reference                                      |
| `teamId`               | Team reference                                         |
| `targetId`             | Resolved target reference                              |
| `evidenceSubmissionId` | Supporting evidence reference                          |
| `resolvedAt`           | Confirmation timestamp                                 |
| `resolvedByUserId`     | Optional operator approver when review is manual       |
| `scoreValue`           | Awarded score snapshot                                 |
| `progressionEffect`    | Business description of what was unlocked or completed |

Relationships:

- one `TargetResolution` belongs to exactly one `LiveSession`
- one `TargetResolution` belongs to exactly one `Team`
- one `TargetResolution` references exactly one `Target`
- one `TargetResolution` may create one `ScoreEntry`

Key constraints:

- the same `Target` should not be resolved twice by the same `Team` in the same `LiveSession` unless replay rules explicitly allow it

### `TriviaAnswerSubmission`

- Type: child entity of `LiveSession`
- Scope: committed refinement
- Why it exists: records the final accepted team answer for one trivia question during a live session

Suggested fields:

| Field                      | Purpose                                                        |
| -------------------------- | -------------------------------------------------------------- |
| `triviaAnswerSubmissionId` | Stable answer identity                                         |
| `liveSessionId`            | Session reference                                              |
| `teamId`                   | Team reference                                                 |
| `triviaQuestionId`         | Question reference                                             |
| `selectedTriviaOptionId`   | Selected option reference                                      |
| `submittedByParticipantId` | Optional participant reference                                 |
| `submittedAt`              | Answer timestamp                                               |
| `isAccepted`               | Indicates whether this answer became the final accepted answer |
| `isCorrect`                | Outcome snapshot used for scoring                              |
| `scoreValue`               | Awarded score snapshot                                         |

Relationships:

- one `TriviaAnswerSubmission` belongs to exactly one `LiveSession`
- one `TriviaAnswerSubmission` belongs to exactly one `Team`
- one `TriviaAnswerSubmission` references exactly one `TriviaQuestion`

Key constraints:

- only one final accepted answer should exist per `Team` and `TriviaQuestion` in the same `LiveSession`

### `JoinContext`

- Type: child entity of `LiveSession`
- Scope: committed refinement
- Why it exists: captures the temporary join flow context binding a participant entry attempt to one session and one team

Suggested fields:

| Field           | Purpose                                              |
| --------------- | ---------------------------------------------------- |
| `joinContextId` | Stable join context identity                         |
| `liveSessionId` | Session reference                                    |
| `teamId`        | Intended team reference                              |
| `joinTokenId`   | Optional token reference                             |
| `status`        | Pending, consumed, expired, cancelled, or equivalent |
| `createdAt`     | Context creation timestamp                           |
| `expiresAt`     | Expiration timestamp                                 |
| `consumedAt`    | Optional successful use timestamp                    |

Relationships:

- one `JoinContext` belongs to exactly one `LiveSession`
- one `JoinContext` may point to exactly one `Team`

Key constraints:

- an expired or consumed `JoinContext` cannot be reused

### `ClueReleaseRecord`

- Type: child entity of `LiveSession`
- Scope: committed refinement
- Why it exists: makes team-specific clue release state explicit so the model can defend release history, duplicate-prevention, and participant board visibility

Suggested fields:

| Field                 | Purpose                                                          |
| --------------------- | ---------------------------------------------------------------- |
| `clueReleaseRecordId` | Stable release identity                                          |
| `liveSessionId`       | Session reference                                                |
| `teamId`              | Team reference                                                   |
| `missionNodeId`       | Released clue reference                                          |
| `releaseMode`         | Manual, automatic, or policy-driven release                      |
| `releasedByUserId`    | Optional operator reference for manual release                   |
| `releasedAt`          | Release timestamp                                                |
| `isCurrent`           | Indicates whether this clue is the currently active visible clue |

Relationships:

- one `ClueReleaseRecord` belongs to exactly one `LiveSession`
- one `ClueReleaseRecord` belongs to exactly one `Team`
- one `ClueReleaseRecord` references exactly one `MissionNode`

Key constraints:

- the same `MissionNode` should not be released twice to the same `Team` in the same `LiveSession`
- only one current active clue should exist per `Team` when the session mode requires linear progression

## ScoringMonitoring

### `ScoreEntry`

- Type: aggregate root
- Scope: academic core
- Why it exists: preserves the immutable scoring ledger that explains every score change

Suggested fields:

| Field              | Purpose                                                                |
| ------------------ | ---------------------------------------------------------------------- |
| `scoreEntryId`     | Stable score identity                                                  |
| `liveSessionId`    | Session reference                                                      |
| `teamId`           | Team reference                                                         |
| `entryType`        | Grant, penalty, correction, or equivalent                              |
| `reasonCode`       | Business reason classification                                         |
| `scoreValue`       | `ScoreValue` value object                                              |
| `recordedAt`       | Ledger timestamp                                                       |
| `sourceEntityType` | Optional origin such as `TargetResolution` or `TriviaAnswerSubmission` |
| `sourceEntityId`   | Optional reference to the originating business fact                    |
| `recordedByUserId` | Optional operator or system actor reference                            |

Relationships:

- one `ScoreEntry` belongs to exactly one `Team` and one `LiveSession`
- one `ScoreEntry` may originate from one `TargetResolution`
- one `ScoreEntry` may originate from one `TriviaAnswerSubmission`
- one `ScoreEntry` may originate from one `Penalty`

Key constraints:

- total score must always be explainable from `ScoreEntry`
- `ScoreEntry` is append-only

### `Penalty`

- Type: child entity of `ScoreEntry`
- Scope: committed refinement
- Why it exists: represents the justified deduction fact when a score change originates from a penalty

Suggested fields:

| Field             | Purpose                      |
| ----------------- | ---------------------------- |
| `penaltyId`       | Stable penalty identity      |
| `scoreEntryId`    | Parent score entry reference |
| `liveSessionId`   | Session reference            |
| `teamId`          | Team reference               |
| `penaltyReason`   | `PenaltyReason` value object |
| `appliedAt`       | Application timestamp        |
| `appliedByUserId` | Operator reference           |

Relationships:

- one `Penalty` belongs to exactly one `ScoreEntry`

Key constraints:

- every `Penalty` must record `penaltyReason` and `appliedAt`

### `Ranking`

- Type: aggregate root or read-model aggregate
- Scope: committed refinement
- Why it exists: represents the derived ordered competition view for monitoring and supervision

Suggested fields:

| Field                | Purpose                                   |
| -------------------- | ----------------------------------------- |
| `rankingId`          | Stable ranking identity                   |
| `liveSessionId`      | Session reference                         |
| `generatedAt`        | Projection generation timestamp           |
| `calculationVersion` | Useful for recalculation and traceability |

Suggested projected row fields:

| Field            | Purpose                          |
| ---------------- | -------------------------------- |
| `teamId`         | Ranked team reference            |
| `position`       | Ordered placement                |
| `totalScore`     | Derived total score              |
| `resolutionTime` | `ResolutionTime` tie-break value |

Relationships:

- one `Ranking` belongs to exactly one `LiveSession`
- one `Ranking` is derived from many `ScoreEntry`

Key constraints:

- ordering is by descending score
- ties should use `ResolutionTime` when applicable

## Access and Authentication Supporting Concepts

### `User`

- Type: supporting aggregate root
- Scope: committed refinement
- Why it exists: represents authenticated backoffice actors such as `Administrador` and `Operador` when the application needs local access or assignment references

Suggested fields:

| Field                | Purpose                             |
| -------------------- | ----------------------------------- |
| `userId`             | Stable user identity                |
| `externalIdentityId` | Identity provider subject reference |
| `displayName`        | Business-visible name               |
| `email`              | Contact and login correlation field |
| `role`               | `Role` enum                         |
| `isActive`           | Operational active flag             |
| `createdAt`          | Audit creation timestamp            |
| `updatedAt`          | Audit last modification timestamp   |

Relationships:

- one `User` may supervise many `LiveSession`
- one `User` may have many `IdentityProviderSession`

Key constraints:

- only authorized roles may perform operator or admin actions

### `IdentityProviderSession`

- Type: child entity of `User`
- Scope: committed refinement
- Why it exists: tracks the authenticated session maintained by the external identity provider

Suggested fields:

| Field                       | Purpose                          |
| --------------------------- | -------------------------------- |
| `identityProviderSessionId` | Stable external session identity |
| `userId`                    | Parent user reference            |
| `providerName`              | External provider name           |
| `providerSessionKey`        | External session handle          |
| `startedAt`                 | Session start timestamp          |
| `expiresAt`                 | Session expiration timestamp     |
| `revokedAt`                 | Optional revocation timestamp    |

Relationships:

- one `IdentityProviderSession` belongs to exactly one `User`

Key constraints:

- session validity is controlled by the external provider and may be treated purely as infrastructure if no local traceability is required

### `JoinToken`

- Type: `Identity` access entity scoped to one session and optionally one team
- Scope: committed refinement
- Why it exists: represents the limited-scope token that authorizes participant entry to a specific session and team
- Ownership rationale: `JoinToken` belongs to `Identity` because it models entry authorization and participant access control, not live-session runtime state. It references `LiveSession` and `Team` from `SessionOperations` only as the targets of that authorization.

Suggested fields:

| Field            | Purpose                                           |
| ---------------- | ------------------------------------------------- |
| `joinTokenId`    | Stable token identity                             |
| `liveSessionId`  | Session reference                                 |
| `teamId`         | Team reference                                    |
| `tokenHash`      | Stored token representation                       |
| `issuedAt`       | Issue timestamp                                   |
| `expiresAt`      | Expiration timestamp                              |
| `consumedAt`     | Optional successful use timestamp                 |
| `issuedByUserId` | Optional issuer reference                         |
| `status`         | Active, consumed, expired, revoked, or equivalent |

Relationships:

- one `JoinToken` belongs to exactly one `LiveSession`
- one `JoinToken` may be associated with exactly one `Team`
- one `JoinToken` may be consumed by one `SessionParticipant`

Key constraints:

- a consumed or expired `JoinToken` cannot grant access again
- ownership belongs to `Identity`; `SessionOperations` should consume only the resulting access facts and runtime join state

## Supporting value objects and policies

These concepts should be referenced by the entities above even when they are not drawn as standalone entities in the main diagram.

### Value objects and enums

| Concept                   | Type         | Used by                                  | Why it matters                                    |
| ------------------------- | ------------ | ---------------------------------------- | ------------------------------------------------- |
| `Difficulty`              | Value Object | `Mission`                                | Supports academic mission classification.         |
| `MaximumTime`             | Value Object | `Mission`, `LiveSession`                 | Supports time-bound execution rules.              |
| `SessionSource`           | Value Object | `LiveSession`                            | Identifies source type and source id.             |
| `SessionMode`             | Enum         | `LiveSession`                            | Restricts gameplay to `TreasureHunt` or `Trivia`. |
| `TeamCode`                | Value Object | `Team`                                   | Supports team identification and join flow.       |
| `ScoreValue`              | Value Object | `ScoreEntry`, `Target`, `TriviaQuestion` | Keeps score quantities explicit and rule-safe.    |
| `PenaltyReason`           | Value Object | `Penalty`                                | Supports traceability and justified penalties.    |
| `ResolutionTime`          | Value Object | `Ranking`                                | Supports tie-break rule.                          |
| `QuestionTimer`           | Value Object | `TriviaQuestion`                         | Supports trivia answer time limits.               |
| `GeoPoint`                | Value Object | `Clue`/`Target` metadata                 | Supports optional geolocation metadata.           |
| `MissionActivation`       | Enum         | `Mission`                                | Supports mission readiness.                       |
| `SessionState`            | Enum         | `LiveSession`                            | Supports lifecycle transitions.                   |
| `EvidenceValidationState` | Enum         | `EvidenceSubmission`                     | Supports evidence acceptance/rejection.           |
| `Role`                    | Enum         | `User`                                   | Supports role-based access.                       |

### Policies and services

| Policy or service              | Governs                                     | Why it matters               |
| ------------------------------ | ------------------------------------------- | ---------------------------- |
| `SessionStateTransitionPolicy` | `LiveSession` state changes                 | Supports `RF-04` and `RB-09` |
| `EvidenceAcceptancePolicy`     | Evidence acceptance and rejection           | Supports `RF-09` and `RB-03` |
| `ClueReleasePolicy`            | Clue release behavior per team              | Supports `RF-07` and `RB-04` |
| `ScorePolicy`                  | Score derivation from accepted domain facts | Supports `RF-10` and `RB-07` |
| `SessionCreationPolicy`        | Preconditions to create a live session      | Supports `RF-03` and `RB-01` |
| `OperatorAssignmentPolicy`     | Operator ownership and restrictions         | Supports `RF-16` and `RB-10` |

## Supporting read models and integration contracts

These artifacts are not aggregate roots, but they are necessary to close the gaps behind several `Parcial` and `No` rows in the annex.

### Read models and projections

| Artifact                        | Derived from                                                             | Why it matters                                                                                               |
| ------------------------------- | ------------------------------------------------------------------------ | ------------------------------------------------------------------------------------------------------------ |
| `TeamBoardProjection`           | `LiveSession`, `Team`, `ClueReleaseRecord`, `ScoreEntry`, `SessionEvent` | Supports `RF-06` by exposing timer, score, visible clue, and progress in one participant-facing query model. |
| `OperatorDashboardProjection`   | `LiveSession`, `Team`, `Ranking`, `SessionEvent`, `EvidenceSubmission`   | Supports `RF-13` by consolidating live operational status and recent events for supervision.                 |
| `EvidenceReviewQueueProjection` | `EvidenceSubmission`, `Team`, `MissionNode`, `SessionParticipant`        | Supports `RF-09` and `RF-18` by making pending validation work explicit for operators.                       |
| `SessionAuditTrailProjection`   | `SessionEvent`, `ScoreEntry`, `Penalty`                                  | Supports `RF-15` through a business-readable audit timeline.                                                 |

### Integration and application-facing contracts

| Artifact              | Driven by                                                                             | Why it matters                                                                                                                                                                                                                                                                                                           |
| --------------------- | ------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| `DomainEventContract` | `SessionEvent`, `TargetResolution`, `TriviaAnswerSubmission`, `ScoreEntry`, `Penalty` | Supports `RF-14` by defining the publishable business facts and event contracts that architecture/infrastructure must later deliver through RabbitMQ, including a minimum documented workflow such as `EvidenceSubmissionRegistered` for audit/history, notification, and secondary recalculation or projection support. |
| `CommandModel`        | aggregate roots and child entities                                                    | Supports `RF-17` by showing where write-side invariants are enforced before persistence.                                                                                                                                                                                                                                 |
| `QueryModel`          | projections such as `Ranking` and `TeamBoardProjection`                               | Supports `RF-17` by separating read concerns from transactional aggregates.                                                                                                                                                                                                                                              |

The logical model supports the RabbitMQ requirement by defining publishable business facts and integration contracts, including the minimum expected workflow where `EvidenceSubmissionRegistered` is published only after successful business completion and then consumed by audit/history, notification, and secondary recalculation or projection flows. Actual broker publication, consumption, delivery guarantees, and operational concerns remain responsibilities of architecture and infrastructure, not of the entity model itself.

## Minimum traceability map to later alignment fixes

This section is intentionally compact. It identifies which model elements should be referenced when the roadmap or any diagram-oriented inventory is updated.

| Requirement area             | Main model elements to reference                                            |
| ---------------------------- | --------------------------------------------------------------------------- |
| Mission CRUD and structure   | `Mission`, `MissionNode`, `MissionActivation`, `MaximumTime`, `Difficulty`  |
| Session lifecycle            | `LiveSession`, `SessionState`, `SessionStateTransitionPolicy`               |
| Team participation           | `Team`, `SessionParticipant`, `TeamMember`, `JoinContext`, `JoinToken`      |
| Evidence flow                | `EvidenceSubmission`, `EvidenceValidationState`, `EvidenceAcceptancePolicy` |
| Mission-clue QR refinement   | `Target`, `TreasureEvidenceSubmission`, `TargetResolution`                  |
| Trivia refinement            | `TriviaQuiz`, `TriviaQuestion`, `TriviaOption`, `TriviaAnswerSubmission`    |
| Scoring and penalties        | `ScoreEntry`, `Penalty`, `ScorePolicy`, `ScoreValue`                        |
| Ranking and monitoring       | `Ranking`, `ResolutionTime`, `SessionEvent`                                 |
| `Identity` and authorization | `User`, `Role`, `IdentityProviderSession`, `OperatorAssignmentPolicy`       |

## Anexo. Estado actual del entity spec frente a RF y RB

Este anexo evalúa el modelo

Interpretación de `Cobertura actual del entity spec`:

- `Sí`: la especificación actual ya define suficiente soporte lógico de dominio para defender el requerimiento
- `Parcial`: la especificación ya deja el soporte bien encaminado, pero todavía depende de implementación, mensajería, transporte en tiempo real o arquitectura de aplicación
- `No`: aun con esta especificación, el requerimiento no puede defenderse solo desde el modelo lógico

### Requerimientos funcionales con el estado actual del spec

| Ítem    | Bounded context principal                              | Cobertura actual del entity spec | Evidencia en el spec actual                                                                                                                                      | Observación                                                                                                          |
| ------- | ------------------------------------------------------ | -------------------------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------- | -------------------------------------------------------------------------------------------------------------------- |
| `RF-01` | `MissionDesign`                                        | Sí                               | `Mission`                                                                                                                                                        | El agregado `Mission` ya soporta CRUD lógico, activación y desactivación.                                            |
| `RF-02` | `MissionDesign`                                        | Sí                               | `Mission`, `MissionNode`, `MaximumTime`, `Difficulty`                                                                                                            | La estructura jerárquica y metadatos de misión están suficientemente modelados.                                      |
| `RF-03` | `SessionOperations`                                    | Sí                               | `Mission.activationState`, `MissionActivation`, `LiveSession.sessionMode`, `LiveSession.sourceEntityType`, `LiveSession.sourceEntityId`, `SessionCreationPolicy` | La creación de sesiones desde una fuente válida ya quedó defendida a nivel de modelo.                                |
| `RF-04` | `SessionOperations`                                    | Sí                               | `LiveSession.state`, `lastStateChangedAt`, `stateReason`, `SessionStateTransitionPolicy`                                                                         | El ciclo de vida y sus transiciones válidas ya están explicitados en la especificación.                              |
| `RF-05` | `SessionOperations`                                    | Sí                               | `LiveSession`, `Team`, `SessionParticipant`, `TeamMember`                                                                                                        | El registro de equipos y participantes queda cubierto por el modelo actual.                                          |
| `RF-06` | `SessionOperations`                                    | Sí                               | `Team.currentScore`, `currentProgressNodeId`, `currentClueNodeId`, `releasedClueCount`, `TeamBoardProjection`                                                    | El tablero de equipo ya tiene soporte lógico suficiente en el spec.                                                  |
| `RF-07` | `SessionOperations`                                    | Sí                               | `ClueReleaseRecord`, `ClueReleasePolicy`                                                                                                                         | La liberación de pistas y su trazabilidad ya quedaron modeladas explícitamente.                                      |
| `RF-08` | `SessionOperations`                                    | Sí                               | `EvidenceSubmission`, `MissionNode`, `TreasureEvidenceSubmission`                                                                                                | La asociación de evidencia con el contexto correcto ya está bien soportada.                                          |
| `RF-09` | `SessionOperations`                                    | Sí                               | `EvidenceSubmission.validationState`, `reviewedByUserId`, `reviewedAt`, `rejectionReason`, `EvidenceAcceptancePolicy`, `EvidenceReviewQueueProjection`           | El flujo lógico de envío y revisión de evidencia ya está modelado.                                                   |
| `RF-10` | `ScoringMonitoring`                                    | Sí                               | `ScoreEntry`, `Penalty`, `TargetResolution`, `TriviaAnswerSubmission`, `ScorePolicy`, `sourceEntityType`, `sourceEntityId`                                       | El recálculo y trazabilidad del puntaje ya tienen base suficiente en el spec.                                        |
| `RF-11` | `ScoringMonitoring`                                    | Sí                               | `Penalty.penaltyReason`, `appliedAt`, `appliedByUserId`                                                                                                          | La penalización justificada ya quedó suficientemente especificada.                                                   |
| `RF-12` | `ScoringMonitoring`                                    | Parcial                          | `Ranking`, `QueryModel`, `OperatorDashboardProjection`                                                                                                           | El ranking está modelado, pero la actualización en tiempo real sigue perteneciendo a la capa de transporte.          |
| `RF-13` | `SessionOperations` + `ScoringMonitoring`              | Parcial                          | `SessionEvent`, `OperatorDashboardProjection`, `SessionAuditTrailProjection`, `Ranking`                                                                          | El panel del operador está bien sostenido a nivel de lectura, pero el tiempo real no se resuelve solo con el modelo. |
| `RF-14` | `ScoringMonitoring`                                    | Parcial                          | `DomainEventContract`, `SessionEvent`, `TargetResolution`, `ScoreEntry`, `Penalty`                                                                               | El spec ya define el contrato de eventos de dominio, pero no la publicación real en RabbitMQ.                        |
| `RF-15` | `SessionOperations` + `ScoringMonitoring`              | Sí                               | `SessionEvent`, `SessionAuditTrailProjection`, `ScoreEntry`, `Penalty`                                                                                           | La auditoría y trazabilidad operativa están suficientemente respaldadas.                                             |
| `RF-16` | `Identity` + `SessionOperations`                       | Sí                               | `User.role`, `LiveSession.assignedOperatorUserId`, `OperatorAssignmentPolicy`, `IdentityProviderSession`                                                         | La separación de actores y control de asignación ya tienen base lógica suficiente.                                   |
| `RF-17` | Cross-cutting                                          | Parcial                          | `CommandModel`, `QueryModel`, `Ranking`, `TeamBoardProjection`                                                                                                   | El spec ya identifica la separación write/read, pero CQRS completo sigue siendo una decisión arquitectónica.         |
| `RF-18` | `SessionOperations` + `ScoringMonitoring` + `Identity` | Sí                               | `SessionCreationPolicy`, `EvidenceAcceptancePolicy`, `SessionStateTransitionPolicy`, `ScorePolicy`, `OperatorAssignmentPolicy`                                   | El catálogo principal de validaciones ya quedó incorporado al modelo lógico.                                         |

### Reglas de negocio con el estado actual del spec

| Ítem    | Bounded context principal             | Cobertura actual del entity spec | Evidencia en el spec actual                                                                  | Observación                                                              |
| ------- | ------------------------------------- | -------------------------------- | -------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------ |
| `RB-01` | `MissionDesign` + `SessionOperations` | Sí                               | `Mission.activationState`, `MissionActivation`, `SessionCreationPolicy`                      | La precondición para crear una `LiveSession` ya está formalizada.        |
| `RB-02` | `SessionOperations`                   | Sí                               | Invariante de `LiveSession` y `SessionStateTransitionPolicy`                                 | La restricción de no activar sesión sin equipos ya está declarada.       |
| `RB-03` | `SessionOperations`                   | Sí                               | `EvidenceValidationState`, `EvidenceAcceptancePolicy`, restricciones de `EvidenceSubmission` | La regla de rechazo por estado de sesión ya quedó defendida.             |
| `RB-04` | `SessionOperations`                   | Sí                               | `ClueReleaseRecord`, `ClueReleasePolicy`                                                     | La prevención de doble liberación ya está modelada de forma explícita.   |
| `RB-05` | `SessionOperations`                   | Sí                               | `EvidenceSubmission` con `liveSessionId`, `teamId`, `missionNodeId`                          | La asociación exacta de la evidencia está claramente definida.           |
| `RB-06` | `ScoringMonitoring`                   | Sí                               | `Penalty.penaltyReason`, `Penalty.appliedAt`                                                 | La obligación de registrar motivo y timestamp ya quedó cubierta.         |
| `RB-07` | `ScoringMonitoring`                   | Sí                               | `ScoreEntry`, `sourceEntityType`, `sourceEntityId`                                           | La trazabilidad del puntaje se sostiene completamente en el spec actual. |
| `RB-08` | `ScoringMonitoring`                   | Sí                               | `Ranking.resolutionTime`, `ResolutionTime`                                                   | El criterio de desempate ya quedó explícito.                             |
| `RB-09` | `SessionOperations`                   | Sí                               | `SessionStateTransitionPolicy`, `lastStateChangedAt`, `stateReason`                          | Las transiciones válidas ya están explícitas y auditables.               |
| `RB-10` | `Identity` + `SessionOperations`      | Sí                               | `User.role`, `LiveSession.assignedOperatorUserId`, `OperatorAssignmentPolicy`                | La restricción por operador asignado ya tiene soporte lógico de dominio. |

### Lectura final del estado actual

Después de los refinements aplicados en esta especificación:

- los `RF` y `RB` que antes estaban en `Parcial` por falta de fields, value objects, policies o proyecciones ahora quedaron mayormente cubiertos a nivel de modelo lógico
- los puntos que permanecen en `Parcial` ya no fallan por carencia del entity spec, sino porque dependen de infraestructura o arquitectura externa al modelo

Los únicos `RF` que siguen parcialmente abiertos por naturaleza son:

- `RF-12`, por transporte en tiempo real
- `RF-13`, por transporte en tiempo real y composición de dashboard
- `RF-14`, por publicación real a RabbitMQ
- `RF-17`, por implementación completa de CQRS

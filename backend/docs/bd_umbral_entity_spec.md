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
- Why it exists: represents the ordered mission tree through `Stage`, `Substage`, and `Clue`; substages may own optional clue guidance, and treasure-hunt substages additionally own target objectives

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
| `substagePlayMode` | Required when `nodeType` is `Substage`: `TreasureHunt` or `Trivia` |
| `winnerScoreValue` | Required for treasure-hunt substages; awarded to the first team that resolves all targets |
| `createdAt`     | Audit creation timestamp                                    |
| `updatedAt`     | Audit last modification timestamp                           |

Relationships:

- one `MissionNode` belongs to exactly one `Mission`
- one `MissionNode` may have many child `MissionNode`
- one `Stage` node contains one or more `Substage` nodes
- one `Substage` may contain `Clue` nodes
- one treasure-hunt `Substage` contains one or more `Target`
- one trivia `Substage` contains one `TriviaQuizSelection`

Key constraints:

- `nodeType` must be one of `Stage`, `Substage`, or `Clue`
- the parent-child hierarchy must be acyclic
- the mission hierarchy is bounded and domain-specific, not a configurable workflow engine
- a `Stage` node must contain one or more `Substage` nodes
- only one `Substage` level is allowed
- `Clue` nodes are allowed only under `Substage` nodes
- `Clue` nodes cannot have children
- every `Substage` must declare exactly one `substagePlayMode`
- mixed-mode substages are invalid
- runtime order is stage order, then substage order inside each stage

### `Target`

- Type: child entity of a treasure-hunt `Substage`
- Scope: committed refinement
- Why it exists: defines the QR-validated treasure-hunt objective that teams are trying to find or validate

Suggested fields:

| Field                 | Purpose                                                  |
| --------------------- | -------------------------------------------------------- |
| `targetId`            | Stable target identity                                   |
| `substageNodeId`      | Owning treasure-hunt substage reference                  |
| `targetCode`          | Business identifier used for validation                  |
| `validationType`      | Declares how the target is resolved, for example QR scan |
| `expectedValue`       | Expected comparison or match value                       |
| `clueNodeId`          | Optional associated `Clue` node                          |
| `isActive`            | Allows deactivation without deleting the target          |

Relationships:

- one `Target` belongs to exactly one treasure-hunt `Substage`
- one `Target` may reference at most one optional `Clue` node in the same treasure-hunt `Substage`

Key constraints:

- a treasure-hunt substage must have at least one `Target`
- target resolution does not require clue visibility
- all targets in the active treasure-hunt substage are active immediately
- a target's associated `Clue`, when present, must belong to the same treasure-hunt `Substage`
- target QR identifiers must be unique within the `MissionRuntimeSnapshot`

### `Clue`

- Type: `MissionNode` subtype under a `Substage`
- Scope: committed refinement
- Why it exists: provides player-facing guidance within a substage without controlling substage advancement or target resolution

Suggested fields:

| Field                  | Purpose                                             |
| ---------------------- | --------------------------------------------------- |
| `missionNodeId`        | Stable clue node identity                           |
| `parentNodeId`         | Owning substage node                                |
| `text`                 | Visible guidance content                            |
| `visibilityPolicy`     | Visible at substage start or held for release       |

Relationships:

- one `Clue` belongs to exactly one `Substage`
- one `Clue` may guide at most one treasure-hunt `Target`

Key constraints:

- a `Clue` can exist under a treasure-hunt or trivia `Substage`
- a `Clue` can be associated with a `Target` only when both belong to the same treasure-hunt `Substage`
- operators may release hidden clues to all teams or selected teams
- clue release never advances the substage by itself

### `TriviaQuiz`

- Type: aggregate root
- Scope: committed refinement
- Why it exists: owns reusable trivia authoring that can be selected into mission trivia substages

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
- one `TriviaQuiz` can be selected by many trivia substages

Key constraints:

- a published `TriviaQuiz` must contain at least one `TriviaQuestion`
- a `TriviaQuiz` is not a `SessionSource`

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
- one `TriviaQuestion` contains between 2 and 4 `TriviaOption` (ADR-0002)

Key constraints (numeric ranges fixed by **ADR-0002**, accepted):

- a `TriviaQuestion` must define between **2 and 4** `TriviaOption`s, with exactly one marked `isCorrect`
- `scoreValue` is an integer in **[1, 100]** (see `ScoreValue` value object)
- `timeLimit` is an integer number of seconds in **[5, 120]** (see `QuestionTimer` value object)
- `sequenceOrder` must be unique within the quiz
- published questions selected into a mission must have a valid `scoreValue` and `timeLimit` within the ranges above

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

### `TriviaQuizSelection`

- Type: value object or child entity of a trivia `Substage`
- Scope: committed refinement
- Why it exists: records which whole published `TriviaQuiz` a trivia `Substage` plays

Suggested fields:

| Field                    | Purpose                                      |
| ------------------------ | -------------------------------------------- |
| `triviaQuizSelectionId`  | Stable selection identity                    |
| `substageNodeId`         | Owning trivia substage reference             |
| `triviaQuizId`           | Published quiz selected in full              |

Relationships:

- one `TriviaQuizSelection` belongs to exactly one trivia `Substage`
- one `TriviaQuizSelection` references exactly one published `TriviaQuiz`

Key constraints:

- the whole published quiz is selected; there is no partial or ordered-subset selection
- the referenced quiz must contain at least one question
- every question in the referenced quiz must have valid options, a correct answer, `ScoreValue`, and `TimeLimitSeconds`

## SessionOperations

### `LiveSession`

- Type: aggregate root
- Scope: academic core
- Why it exists: owns live execution of one mission runtime snapshot, timing, transitions, participation, and session-level invariants

Suggested fields:

| Field                    | Purpose                                                                       |
| ------------------------ | ----------------------------------------------------------------------------- |
| `liveSessionId`          | Stable session identity                                                       |
| `sourceMissionId`        | `SessionSource` value object identifying the active source mission             |
| `missionRuntimeSnapshotId` | Immutable runtime snapshot copied at creation time                          |
| `sessionCode`            | Human-usable operational identifier                                           |
| `titleSnapshot`          | Session title copied from the mission at creation time                        |
| `state`                  | `SessionState` enum                                                           |
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

- one `LiveSession` originates from exactly one active `Mission`
- one `LiveSession` owns exactly one immutable `MissionRuntimeSnapshot`
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
- canonical states are `Scheduled`, `Preparing`, `Active`, `Paused`, `Finished`, and `Cancelled`
- creation immediately places the session in `Scheduled`
- team association is allowed only while `Scheduled`
- `Scheduled → Preparing` moves the session into operator readiness; `Preparing → Active` immediately starts the first substage
- only `Active -> Paused -> Active` is valid for pause/resume
- `Finished` happens only through normal completion of the final substage
- early stop uses `Cancelled` and does not calculate a `SessionTeamWinner`
- substage play mode is defined by each snapshotted substage, not by the session
- mixed-mode substages are invalid

### `MissionRuntimeSnapshot`

- Type: immutable child entity or owned model of `LiveSession`
- Scope: academic core
- Why it exists: freezes the mission runtime plan at session creation so later authoring changes cannot mutate live play

Suggested fields:

| Field                      | Purpose                                                     |
| -------------------------- | ----------------------------------------------------------- |
| `missionRuntimeSnapshotId` | Stable snapshot identity                                    |
| `liveSessionId`            | Owning live session reference                               |
| `sourceMissionId`          | Mission used to create the snapshot                         |
| `missionTitle`             | Mission title at creation                                   |
| `maximumTime`              | Effective time limit snapshot                               |
| `stageSnapshots`           | Ordered stages and substages                                |
| `targetSnapshots`          | Treasure-hunt target content, QR identifiers, and clues      |
| `triviaQuestionSnapshots`  | Trivia questions, options, correct answers, timers, scores   |
| `createdAt`                | Snapshot creation timestamp                                 |

Relationships:

- one `MissionRuntimeSnapshot` belongs to exactly one `LiveSession`
- one `MissionRuntimeSnapshot` contains ordered stage and substage snapshots
- one snapshot target may have one optional clue snapshot
- one trivia substage contains ordered trivia question snapshots

Key constraints:

- the snapshot cannot be edited after `LiveSession` creation
- target QR identifiers must be unique within the snapshot
- runtime order is strict mission order: stage order, then substage order
- treasure-hunt substages require at least one target and a winner score value
- trivia substages require at least one selected question with timer, score, valid options, and correct answer

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
| `currentSubstageId`     | Current substage reference from the runtime snapshot   |
| `visibleClueIds`        | Optional clue guidance visible to the team             |
| `releasedClueCount`     | Cached count of clues already made visible to the team |
| `resolutionTime`          | Derived active play time used for final ranking when comparable |
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
- Why it exists: preserves the canonical academic record of *evidence* — the
  generic term for a team submission that proves or resolves progress in the
  active mission substage. It is the umbrella base specialized by
  `TreasureEvidenceSubmission` (the QR/token scan in treasure-hunt substages)
  and `TriviaAnswerSubmission` (the team answer in trivia substages). Both
  forms keep their own concrete fields, validation rules, and events under this
  base.
- Delivery scope: the first delivery implements exactly the two committed forms
  under this umbrella — QR evidence in treasure-hunt substages and trivia
  answers in trivia substages. Text/photo evidence modes are out of scope and
  not part of the current canonical model — see
  `docs/adr/0010-evidence-qr-only-first-delivery.md`

Suggested fields:

| Field                      | Purpose                                            |
| -------------------------- | -------------------------------------------------- |
| `evidenceSubmissionId`     | Stable submission identity                         |
| `liveSessionId`            | Session reference                                  |
| `teamId`                   | Team reference                                     |
| `activeSubstageId`         | Active substage reference from the mission runtime snapshot |
| `submittedByParticipantId` | Optional submitting participant reference          |
| `submissionType`           | Evidence mode: QR scan (treasure hunt) or trivia answer |
| `payloadReference`         | Logical pointer to evidence content                |
| `submittedAt`              | Submission timestamp                               |
| `validationState`          | `EvidenceValidationState` enum                     |
| `reviewedByUserId`         | Optional operator reviewer reference               |
| `reviewedAt`               | Optional decision timestamp                        |
| `rejectionReason`          | Optional business explanation when rejected        |

Relationships:

- one `EvidenceSubmission` belongs to exactly one `LiveSession`
- one `EvidenceSubmission` belongs to exactly one `Team`
- one `EvidenceSubmission` belongs to exactly one active substage in the mission runtime snapshot
- one `EvidenceSubmission` is specialized by exactly one
  `TreasureEvidenceSubmission` (QR) or one `TriviaAnswerSubmission` (trivia)

Key constraints:

- every submission must reference exactly one `Team`, one `LiveSession`, and
  one active substage
- submissions cannot be accepted when `SessionState` is `Paused`, `Finished`, or `Cancelled`
- `validationState` must follow allowed transitions such as pending to accepted or rejected
- a treasure-hunt evidence submission must target the active treasure-hunt substage
- a trivia answer submission must target the active snapshotted trivia question during its timer window

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
- Why it exists: the treasure-hunt form of evidence, specializing
  `EvidenceSubmission` for QR-supported target resolution (the parallel of
  `TriviaAnswerSubmission` for trivia substages)

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
- the target must belong to the active treasure-hunt substage
- each team may successfully resolve each target at most once

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
| `progressionEffect`    | Business description of what was resolved              |

Relationships:

- one `TargetResolution` belongs to exactly one `LiveSession`
- one `TargetResolution` belongs to exactly one `Team`
- one `TargetResolution` references exactly one `Target`
- one `TargetResolution` may create one `ScoreEntry`

Key constraints:

- the same `Target` cannot be resolved twice by the same `Team` in the same `LiveSession`
- target resolution does not require clue visibility
- all targets in the active treasure-hunt substage are active immediately
- the first team to resolve all targets in the active treasure-hunt substage becomes `TreasureHuntSubstageWinner`
- only the treasure-hunt substage winner receives the snapshotted winner score; non-winning teams receive zero for that substage

### `TriviaAnswerSubmission`

- Type: child entity of `LiveSession`
- Scope: committed refinement
- Why it exists: records the final accepted team answer for one trivia question
  during a live session. It is the trivia form of evidence, specializing
  `EvidenceSubmission` (the parallel of `TreasureEvidenceSubmission` for
  treasure-hunt substages).

Suggested fields:

| Field                      | Purpose                                                        |
| -------------------------- | -------------------------------------------------------------- |
| `triviaAnswerSubmissionId` | Stable specialized identity                                    |
| `evidenceSubmissionId`     | Base evidence reference                                        |
| `liveSessionId`            | Session reference                                              |
| `teamId`                   | Team reference                                                 |
| `triviaQuestionSnapshotId` | Snapshotted question reference                                 |
| `selectedTriviaOptionId`   | Selected option reference                                      |
| `submittedByParticipantId` | Optional participant reference                                 |
| `submittedAt`              | Answer timestamp                                               |
| `isAccepted`               | Indicates whether this answer became the final accepted answer |
| `isCorrect`                | Outcome snapshot used for scoring                              |
| `scoreValue`               | Awarded score snapshot                                         |

Relationships:

- one `TriviaAnswerSubmission` extends exactly one `EvidenceSubmission`
- one `TriviaAnswerSubmission` belongs to exactly one `LiveSession`
- one `TriviaAnswerSubmission` belongs to exactly one `Team`
- one `TriviaAnswerSubmission` references exactly one snapshotted trivia question

Key constraints:

- specialized trivia evidence must reference a base `EvidenceSubmission`
- only one accepted answer may exist per `Team` and snapshotted trivia question in the same `LiveSession`
- answers are accepted only during the active question timer window
- late answers and duplicate answer attempts are rejected
- correct answers award the question snapshot's `ScoreValue`; wrong or missing answers award zero

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
- Why it exists: makes team-specific clue visibility explicit so the model can defend release history, duplicate-prevention, and participant board visibility

Suggested fields:

| Field                 | Purpose                                                          |
| --------------------- | ---------------------------------------------------------------- |
| `clueReleaseRecordId` | Stable release identity                                          |
| `liveSessionId`       | Session reference                                                |
| `teamId`              | Team reference                                                   |
| `clueId`              | Released clue reference                                          |
| `targetId`            | Target whose optional clue became visible                        |
| `releaseMode`         | Manual, automatic, or policy-driven release                      |
| `releasedByUserId`    | Optional operator reference for manual release                   |
| `releasedAt`          | Release timestamp                                                |

Relationships:

- one `ClueReleaseRecord` belongs to exactly one `LiveSession`
- one `ClueReleaseRecord` belongs to exactly one `Team`
- one `ClueReleaseRecord` references exactly one clue snapshot

Key constraints:

- the same clue should not be released twice to the same `Team` in the same `LiveSession`
- all-team release creates or implies visibility for every team
- clue visibility does not unlock, resolve, or advance targets by itself

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
| `resolutionTime`   | `ResolutionTime` tie-break value   |

Relationships:

- one `Ranking` belongs to exactly one `LiveSession`
- one `Ranking` is derived from many `ScoreEntry`

Key constraints:

- ordering is by descending score
- if total score ties, lower comparable `ResolutionTime` ranks first
- if `ResolutionTime` is not comparable or is equal, teams share rank

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
| `SessionSource`           | Value Object | `LiveSession`                            | Identifies the active source mission.             |
| `SubstagePlayMode`        | Enum         | `MissionNode`, `MissionRuntimeSnapshot`  | Restricts each substage to `TreasureHunt` or `Trivia`. |
| `TeamCode`                | Value Object | `Team`                                   | Supports team identification and join flow.       |
| `ScoreValue`              | Value Object | `ScoreEntry`, treasure-hunt substage winner award, `TriviaQuestion` | Keeps score quantities explicit and rule-safe.    |
| `PenaltyReason`           | Value Object | `Penalty`                                | Supports traceability and justified penalties.    |
| `ResolutionTime`            | Value Object | `Ranking`                                | Supports tie-break rule using active play time.   |
| `QuestionTimer`           | Value Object | `TriviaQuestion`                         | Supports trivia answer time limits.               |
| `ClueVisibilityPolicy`    | Value Object | `Target`/`Clue`                          | Controls visible-at-start vs operator-release guidance. |
| `GeoPoint`                | Value Object | `Target` metadata                        | Supports optional geolocation metadata.           |
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
| `SubstageAdvancementPolicy`    | Strict mission-order advancement            | Supports runtime completion and play-mode rules |
| `TriviaQuestionTimerPolicy`    | Synchronized trivia question windows         | Supports late/duplicate answer rules |
| `OperatorAssignmentPolicy`     | Operator ownership and restrictions         | Supports `RF-16` and `RB-10` |

## Supporting read models and integration contracts

These artifacts are not aggregate roots, but they are necessary to close the gaps behind several `Parcial` and `No` rows in the annex.

### Read models and projections

| Artifact                        | Derived from                                                             | Why it matters                                                                                               |
| ------------------------------- | ------------------------------------------------------------------------ | ------------------------------------------------------------------------------------------------------------ |
| `TeamBoardProjection`           | `LiveSession`, `Team`, `ClueReleaseRecord`, `ScoreEntry`, `SessionEvent` | Supports `RF-06` by exposing timer, score, visible clue guidance, active substage, and progress in one participant-facing query model. |
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
| Mission CRUD and structure   | `Mission`, `MissionNode`, `SubstagePlayMode`, `Target`, `Clue`, `TriviaQuizSelection`, `MissionActivation`, `MaximumTime`, `Difficulty` |
| Session lifecycle            | `LiveSession`, `MissionRuntimeSnapshot`, `SessionState`, `SessionStateTransitionPolicy` |
| Team participation           | `Team`, `SessionParticipant`, `TeamMember`, `JoinContext`, `JoinToken`      |
| Evidence flow                | `EvidenceSubmission`, `EvidenceValidationState`, `EvidenceAcceptancePolicy` |
| Treasure-hunt QR refinement  | `Target`, `Clue`, `ClueReleaseRecord`, `TreasureEvidenceSubmission`, `TargetResolution` |
| Trivia refinement            | `TriviaQuiz`, `TriviaQuestion`, `TriviaOption`, `TriviaAnswerSubmission`    |
| Scoring and penalties        | `ScoreEntry`, `Penalty`, `ScorePolicy`, `ScoreValue`                        |
| Ranking and monitoring       | `Ranking`, `ResolutionTime`, `SessionEvent`                                   |
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
| `RF-03` | `SessionOperations`                                    | Sí                               | `Mission.activationState`, `MissionActivation`, `LiveSession.sourceMissionId`, `MissionRuntimeSnapshot`, `SessionCreationPolicy` | La creación de sesiones desde una misión activa y lista ya quedó defendida a nivel de modelo.                        |
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
| `RB-05` | `SessionOperations`                   | Sí                               | `EvidenceSubmission` con `liveSessionId`, `teamId`, `activeSubstageId` y, para trivia, `triviaQuestionSnapshotId` | La asociación exacta de la evidencia está claramente definida.           |
| `RB-06` | `ScoringMonitoring`                   | Sí                               | `Penalty.penaltyReason`, `Penalty.appliedAt`                                                 | La obligación de registrar motivo y timestamp ya quedó cubierta.         |
| `RB-07` | `ScoringMonitoring`                   | Sí                               | `ScoreEntry`, `sourceEntityType`, `sourceEntityId`                                           | La trazabilidad del puntaje se sostiene completamente en el spec actual. |
| `RB-08` | `ScoringMonitoring`                   | Sí                               | `Ranking.resolutionTime`, `ResolutionTime`                                                       | El criterio de desempate ya quedó explícito.                             |
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

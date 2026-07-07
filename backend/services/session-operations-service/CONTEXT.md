# Session Operations Service

`session-operations-service` realizes the `SessionOperations` bounded context. It owns live-session runtime state, participation, team assignment, clue progression, evidence intake, and the final decision about admission into a live session.

## Language

### Session Entry

**Final Admission Decision**:
The authoritative decision about whether an actor may enter a specific `LiveSession` in a specific `Team`. This decision belongs to `SessionOperations` because it depends on session-owned state such as join phase, reconnect status, capacity, and participant assignment.
_Avoid_: access validation, authentication decision, identity approval

**SessionOperations**:
The bounded context that owns `LiveSession`, `Team`, `SessionParticipant`, `JoinContext`, and the final admission decision for live-session entry.
_Avoid_: access service, auth service

**JoinContext**:
The session-scoped context for a participant entry attempt, binding one actor's join flow to a specific `LiveSession` and intended `Team`. It belongs to `SessionOperations` as part of live-session state, not to `Users`.
_Avoid_: identity join record, auth context

**SessionParticipant**:
The participant record owned by `SessionOperations` for one actor inside one `LiveSession`, including runtime participation state after access has been granted.
_Avoid_: user session, auth participant

**Open Team Selection**:
The pre-start session policy that allows an unassigned participant with no explicit pre-assignment for the session's attached teams to choose one attached `Team` for themselves while the `LiveSession` has not yet reached `Active`. This policy belongs to `SessionOperations` because it is scoped to one live session and disappears once runtime play has started. A pick produces a session-scoped `SessionParticipant`/`TeamMember` association only; it never writes a `RegisteredTeamMembership` back to `Users`.
_Avoid_: users-owned eligibility, forced-team default, runtime reassignment, write-back to Users

**Participation Block**:
The runtime state applied to a `SessionParticipant` when further participation must stop immediately because a cross-context access fact, such as `User Deactivation`, invalidates continued access.
_Avoid_: soft warning, future-join-only restriction

**TeamMember**:
The canonical term for a participant associated with a `Team` within session operations language. Use this term instead of older membership wording unless a distinct relationship model is introduced later.
_Avoid_: `TeamMembership`

### Live Runtime

**LiveSession**:
The main aggregate for the live execution of one active `Mission` source.
_Avoid_: session, room, match, game, session mode

**SessionState**:
The lifecycle state of a `LiveSession` that governs valid operations and transitions.
_Avoid_: status, phase, mode

**Scheduled**:
The initial `SessionState` set on a `LiveSession` at creation. The session holds its immutable `MissionRuntimeSnapshot` from this point, and team association is only allowed while `Scheduled`. A `Scheduled` session may transition to `Preparing` or be `Cancelled`.
_Avoid_: draft, pending

**Preparing**:
The `SessionState` entered from `Scheduled` in which the session is readied for activation (operator assignment verified, teams confirmed). A `Preparing` session may transition to `Active` or be `Cancelled`.
_Avoid_: setup, staging

**Active**:
The `SessionState` in which live play is running. Entering `Active` from `Preparing` immediately starts the first `Substage`.
_Avoid_: running, live

**Paused**:
The `SessionState` entered only from `Active`, in which live progression is temporarily stopped. Target submissions and trivia answer submissions are not accepted while paused; trivia resumes on the same active question when the session returns to `Active`.
_Avoid_: on hold, stopped

**Finished**:
The terminal `SessionState` reached only through `SessionCompletion` when the final `Substage` completes normally.
_Avoid_: completed, closed, manually finished

**Cancelled**:
The terminal `SessionState` in which the session is terminated without normal completion from `Scheduled`, `Preparing`, `Active`, or `Paused`. Cancelled sessions accept no target submissions or trivia answers, do not advance substages, and do not calculate a `SessionTeamWinner`; existing score history remains visible for audit.
_Avoid_: aborted

**SessionSource**:
The value object that states which active `Mission` a `LiveSession` originates from.
_Avoid_: mode source, origin type

**MissionRuntimeSnapshot**:
The immutable mission runtime plan copied into a `LiveSession` when the session is created, including stages, substages, play modes, target content, clue guidance, scoring values, and trivia question snapshots needed for live play. It cannot be edited after session creation, and target QR identifiers must be unique within it.
_Avoid_: live authoring lookup, mutable mission reference

**Team**:
The entity associated with a `LiveSession` that holds shared progress, score, and participation state.
_Avoid_: squad, group

**TeamCode**:
The value object that uniquely identifies a `Team` in business interactions.
_Avoid_: team id, join code

**ClueReleaseRecord**:
The traceable record that one `Clue` became visible to a specific `Team` in one `LiveSession`; all-team release creates or implies visibility for every team.
_Avoid_: release row, clue unlock log

**SessionEvent**:
A significant domain event recorded for session history, supervision, and audit.
_Avoid_: log, trace, broker message

### Runtime Evidence

**EvidenceSubmission**:
The record of a response or evidence sent by a `Team` for a `MissionNode` during a `LiveSession`.
_Avoid_: submission, response, evidence

**EvidenceValidationState**:
The business state that indicates whether an `EvidenceSubmission` is pending, accepted, or rejected under domain rules.
_Avoid_: review status, decision

**TreasureEvidenceSubmission**:
The QR-based refinement of an `EvidenceSubmission` used for treasure-hunt `Target` validation.
_Avoid_: qr submission, target scan record

**TargetResolution**:
The runtime fact that a `Target` was successfully resolved by a `Team` in a `LiveSession`.
_Avoid_: checkpoint clear, qr success

**DuplicateTargetResolution**:
A later resolution attempt for a `Target` after the same `Team` already has one successful `TargetResolution` for that target.
_Avoid_: target retry, repeated checkpoint

**TargetProgression**:
The treasure-hunt progression model where all targets in the active treasure-hunt `Substage` are active immediately and a `Team` advances by resolving them in any order; resolving all targets in that substage is required before the team can win that substage. Target resolution does not require clue visibility.
_Avoid_: clue progression, clue completion

**TreasureHuntSubstageWinner**:
The first `Team` to resolve all targets in a treasure-hunt `Substage`.
_Avoid_: clue winner, checkpoint winner

**TreasureHuntSubstageScore**:
The snapshotted `ScoreValue` awarded to the `TreasureHuntSubstageWinner`; non-winning teams receive zero for that treasure-hunt `Substage`.
_Avoid_: target partial score, clue score

**SubstageAdvancement**:
The runtime transition that moves teams from one `Substage` to the next by strict mission order and play-mode rules. In a treasure-hunt `Substage`, all teams advance when the `TreasureHuntSubstageWinner` is decided; operators cannot manually force substage advancement.
_Avoid_: manual skip, operator-forced advancement, team-only progression

**TriviaQuestionTimer**:
The authoritative runtime duration for one snapshotted `TriviaQuestion` during a trivia `Substage`.
_Avoid_: client timer, participant timer

**TriviaQuestionAdvancement**:
The runtime transition from one snapshotted `TriviaQuestion` to the next available question when the `TriviaQuestionTimer` expires.
_Avoid_: participant-paced question flow

**SynchronizedTriviaQuestion**:
The single active snapshotted `TriviaQuestion` presented to all teams during the same authoritative timer window in a trivia `Substage`.
_Avoid_: team-specific active question, participant-paced question

**TriviaSubstageCompletion**:
The runtime completion of a trivia `Substage` when the final snapshotted `TriviaQuestion` timer expires. All teams advance to the next `Substage` when one exists.
_Avoid_: team-completed trivia, participant-paced completion

**TriviaAnswerSubmission**:
The answer record submitted by a `Team` for a snapshotted `TriviaQuestion` during a trivia substage of a `LiveSession`. Only one accepted answer is allowed per team per question, and it must arrive during the active question's authoritative timer window.
_Avoid_: quiz answer row, response option

**DuplicateTriviaAnswer**:
A later answer attempt for a snapshotted `TriviaQuestion` after the same `Team` already has one accepted `TriviaAnswerSubmission` for that question.
_Avoid_: answer update, answer retry

**LateTriviaAnswer**:
An answer attempt for a snapshotted `TriviaQuestion` after that question's authoritative timer window has expired.
_Avoid_: delayed score, expired answer

**TriviaQuestionScore**:
The snapshotted `ScoreValue` awarded to a `Team` for a correct `TriviaAnswerSubmission`; wrong or missing answers award zero.
_Avoid_: speed bonus, partial credit

**TriviaSubstageWinner**:
Any `Team` with the highest trivia score in a trivia `Substage` after the final `TriviaQuestionTimer` expires; ties are allowed.
_Avoid_: fastest trivia team, first completed trivia team

**SessionTeamWinner**:
The top-ranked `Team` after a `LiveSession` finishes, based on total score and applicable resolution-time tie-breaking.
_Avoid_: participant winner, most substages won

**SessionRanking**:
The ordered team result for a finished `LiveSession`: higher total score ranks first, lower comparable `ResolutionTime` breaks score ties, and teams share rank when `ResolutionTime` is not comparable or is equal.
_Avoid_: leaderboard guess, fastest-only ranking

**ScoreEntry**:
The traceable scoring fact that explains a score change for a `Team` in a `LiveSession`. Total score and ranking are derived from score entries rather than direct score mutation.
_Avoid_: hidden score update, mutable total

**ResolutionTime**:
The elapsed active play time from `LiveSession` activation until a `Team` completes the final applicable objective used for ranking; time spent in `Paused` does not count.
_Avoid_: wall-clock duration, trivia timer score

**SessionCompletion**:
The automatic transition to `Finished` when the final `Substage` completes, followed by calculation of the `SessionTeamWinner`.
_Avoid_: operator-finished session, manual finalization

## Boundary Rules

**Admission Ownership**:
`SessionOperations` owns the final admission decision because only it has the authoritative session-scoped facts needed to decide late join, reconnect, capacity, assignment, live-state constraints, and whether `Open Team Selection` is still available before live play starts.
_Avoid_: identity-side join authority

**Runtime Authority**:
`SessionOperations` owns live progression, team participation, clue release, and evidence intake. Other services may provide source facts, access facts, or derived scoring views, but they do not control runtime state transitions here.
_Avoid_: scoring-owned progression, authoring-owned participation

**Pre-Start Team Assignment**:
A participant's team membership — whether from `Open Team Selection` or a choice within their `RegisteredTeamMembership` authorized set — is mutable only while the `LiveSession` is `Scheduled` or `Preparing`, and freezes once it reaches `Active`, `Paused`, `Finished`, or `Cancelled`. Switching is always confined to the participant's authorized set (all attached teams when they have no membership; their whitelisted teams otherwise) and is capacity-checked on the team being joined. A participant who holds no team when the session reaches `Active` is not admitted to active play; there is no auto-assignment.
_Avoid_: runtime reassignment, post-activation team switch, auto-seating a no-show

**Cross-Context Access Facts**:
`SessionOperations` must react to access facts emitted by upstream contexts when those facts invalidate continued participation, including `User Deactivation` and registered-team eligibility revocation. It owns the runtime consequence, but not the upstream fact itself.
_Avoid_: ignoring upstream deactivation, upstream-owned runtime mutation

## Required Patterns

**Facade**:
Session orchestration should be exposed through a narrow coordination service that executes session operations and triggers outbound event publication without leaking that coordination into endpoints or handlers.
_Avoid_: endpoint-level orchestration or handlers that manually coordinate every side effect

**State**:
`LiveSession` lifecycle behavior must enforce valid transitions such as `Scheduled`, `Preparing`, `Active`, `Paused`, `Finished`, and `Cancelled`, with play-mode-specific internal phases only when they remain subordinate to the same lifecycle model.
_Avoid_: free-form status mutation or transition rules encoded as scattered conditionals

**Chain of Responsibility**:
QR-supported clue submission validation, trivia answer acceptance, and session-state change validation should be composed from ordered validators.
_Avoid_: one oversized validator or handler that hardcodes every branch

**Template Method**:
Where validation follows one invariant flow with mode-specific checks, keep the shared sequence stable and vary only the specialized steps.
_Avoid_: separate ad hoc workflows that drift apart over time

**Proxy**:
Restricted clues, operator dashboards, protected panels, protected session actions, clue release, and team-only resources should be guarded through role and policy-aware proxies before mutation or data exposure occurs.
_Avoid_: repeating authorization checks inline across every endpoint and handler

## Example Dialogue

Dev: "So if late join is closed or the team is full, Session Operations rejects the entry even when Users says the actor is valid?"

Domain expert: "Exactly. Users proves who the actor is; Session Operations decides whether entry is allowed right now."

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
The session-scoped context for a participant entry attempt, binding one actor's join flow to a specific `LiveSession` and intended `Team`. It belongs to `SessionOperations` as part of live-session state, not to `Identity`.
_Avoid_: identity join record, auth context

**SessionParticipant**:
The participant record owned by `SessionOperations` for one actor inside one `LiveSession`, including runtime participation state after access has been granted.
_Avoid_: user session, auth participant

**TeamMember**:
The canonical term for a participant associated with a `Team` within session operations language. Use this term instead of older membership wording unless a distinct relationship model is introduced later.
_Avoid_: `TeamMembership`

### Live Runtime

**LiveSession**:
The main aggregate for the live execution of one active source, typically a `Mission` and optionally a `TriviaQuiz` in the committed scope.
_Avoid_: session, room, match, game

**SessionState**:
The lifecycle state of a `LiveSession` that governs valid operations and transitions.
_Avoid_: status, phase, mode

**Scheduled**:
The `SessionState` in which the session exists but has not entered preparation.
_Avoid_: planned

**Preparing**:
The `SessionState` in which the session is being readied before activation.
_Avoid_: setup, staging

**Active**:
The `SessionState` in which teams can receive clues and submit evidence.
_Avoid_: running, live

**Paused**:
The `SessionState` in which live progression is temporarily stopped.
_Avoid_: on hold, stopped

**Finished**:
The `SessionState` in which the session has ended normally.
_Avoid_: completed, closed

**Cancelled**:
The `SessionState` in which the session is terminated without normal completion.
_Avoid_: aborted

**SessionSource**:
The value object that states whether a `LiveSession` originates from a `Mission` or a `TriviaQuiz`.
_Avoid_: mode source, origin type

**Team**:
The entity associated with a `LiveSession` that holds shared progress, score, and participation state.
_Avoid_: squad, group

**TeamCode**:
The value object that uniquely identifies a `Team` in business interactions.
_Avoid_: team id, join code

**ClueRelease**:
The business action of making a `Clue` available to a `Team` during a `LiveSession`.
_Avoid_: unlock, reveal, dispatch

**ClueReleaseRecord**:
The traceable record that one `Clue` was released to one `Team` in one `LiveSession`.
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
The QR or target-oriented refinement of an `EvidenceSubmission` used when treasure-hunt runtime needs target validation.
_Avoid_: qr submission, target scan record

**TargetResolution**:
The runtime fact that a `Target` was successfully resolved by a `Team` in a `LiveSession`.
_Avoid_: checkpoint clear, qr success

**TriviaAnswerSubmission**:
The answer record submitted by a `Team` for a `TriviaQuestion` during a trivia `LiveSession`.
_Avoid_: quiz answer row, response option

## Boundary Rules

**Admission Ownership**:
`SessionOperations` owns the final admission decision because only it has the authoritative session-scoped facts needed to decide late join, reconnect, capacity, assignment, and live-state constraints.
_Avoid_: identity-side join authority

**Runtime Authority**:
`SessionOperations` owns live progression, team participation, clue release, and evidence intake. Other services may provide source facts, access facts, or derived scoring views, but they do not control runtime state transitions here.
_Avoid_: scoring-owned progression, authoring-owned participation

## Example Dialogue

Dev: "So if late join is closed or the team is full, Session Operations rejects the entry even when Identity says the actor is valid?"

Domain expert: "Exactly. Identity proves who the actor is; Session Operations decides whether entry is allowed right now."

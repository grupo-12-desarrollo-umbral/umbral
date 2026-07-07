# Users Realignment Handoff

This handoff is for the next session to continue the `grill-with-docs` realignment discussion for the backend bounded contexts, especially the rename and scope split from `identity-access-service` to `users-service`.

## Existing artifacts to read first

- Findings document: `/home/samu/Desktop/umbral/backend/docs/identity-access-service-alignment-findings-2026-07-06.md`
- Updated Users glossary: `/home/samu/Desktop/umbral/backend/services/identity-access-service/CONTEXT.md`
- Updated Session Operations glossary: `/home/samu/Desktop/umbral/backend/services/session-operations-service/CONTEXT.md`
- Backend context map: `/home/samu/Desktop/umbral/backend/CONTEXT-MAP.md`

## Settled decisions

- Both the deployable/service name and the bounded context name should become `Users`.
- `Users` is a fourth bounded context alongside the three academically required ones:
  - `MissionDesign`
  - `SessionOperations`
  - `ScoringMonitoring`
- `users-service` should not own session runtime participation.
- `RegisteredTeam` belongs in `Users` as pre-session reference data created by administrators/operators.
- `SessionParticipant` / session runtime team participation belongs in `SessionOperations`.
- Team identity is split into two concepts:
  - `RegisteredTeam` in `Users`
  - session-scoped team participation in `SessionOperations`
- `RegisteredTeamMembership` belongs in `Users` and means baseline participant authorization for a registered team.
- `RegisteredTeamMembership` is not the live-session roster.
- Only `Participant` users can be eligible through `RegisteredTeamMembership`.
- `Users` grants baseline eligibility.
- `SessionOperations` makes the final admission decision and may only narrow baseline eligibility.
- Team identity is snapshotted into `SessionOperations` when a live session is created.
- Eligibility roster is not snapshotted into `SessionOperations`.
- Eligibility remains authoritative in `Users` and is checked lazily per join.
- If `users-service` is unavailable during a join attempt, `session-operations-service` should fail closed.
- Eligibility checks should return `eligible / not eligible` plus a machine-readable reason code.
- If a user is deactivated in `Users`, they should be blocked immediately from further live-session participation.
- That deactivation should propagate by domain event from `users-service`; `session-operations-service` consumes it and applies a runtime participation block.
- If `RegisteredTeamMembership` is revoked, the participant should also be blocked immediately from further runtime participation through the same cross-context pattern.
- Scope split for participant-facing queries:
  - `Users`: which `RegisteredTeam`s a participant is generally eligible for
  - `SessionOperations`: which teams are joinable for a specific `LiveSession`
- API language for the team authorization roster should be use-case oriented, not raw CRUD.
- New concept `Open Team Selection`: a `SessionOperations`-owned pre-start policy that lets an unassigned participant (no `RegisteredTeamMembership` for any attached team) see all attached teams and self-assign into one.
- `Open Team Selection` is available only while the `LiveSession` has NOT reached `Active`, `Paused`, `Finished`, or `Cancelled` (i.e. only in pre-start states). It disappears once runtime play has started.
- `Users` still owns explicit pre-assignment via `RegisteredTeamMembership`; `SessionOperations` owns the temporary pre-start open-team-picking rule.
- Distinction between two things: "visible in the session lobby" vs "eligible to self-assign into one team before the session becomes `Active`".
- `Open Team Selection` helps only unassigned participants. If a participant already has a `RegisteredTeamMembership`, they are constrained to their pre-authorized team set and may not use `Open Team Selection` to pick a different attached team.

## Decision trail: question and settled answer

- Q: Should the rename affect only the deployable name, or both the deployable and the bounded context?
  A: Both should become `Users`.
- Q: Does that conflict with the academic requirement for three bounded contexts?
  A: No. `Users` is a fourth supporting bounded context alongside `MissionDesign`, `SessionOperations`, and `ScoringMonitoring`.
- Q: Should `users-service` own `Team`, `SessionTeamAssociation`, and `JoinToken`?
  A: Not as one lump. `RegisteredTeam` stays in `Users`; session-scoped participation concepts move to `SessionOperations`.
- Q: Does `Team` belong to `SessionOperations`?
  A: There are two concepts. `RegisteredTeam` belongs to `Users`; runtime session team participation belongs to `SessionOperations`.
- Q: What is the canonical split?
  A: `RegisteredTeam` in `Users`; `Session Team` / session participation in `SessionOperations`.
- Q: If a `RegisteredTeam` name changes later, should existing live sessions reflect it?
  A: No. Team identity should be snapshotted in `SessionOperations`.
- Q: Should `RegisteredTeam` be catalog only, or catalog plus preassigned participant roster?
  A: Catalog plus preassigned authorization roster.
- Q: What does that roster mean?
  A: `RegisteredTeamMembership` means baseline participant authorization for a registered team; it is not the live-session roster.
- Q: Does `RegisteredTeamMembership` grant guaranteed admission?
  A: No. It grants default eligibility only.
- Q: Can `SessionOperations` expand eligibility or only narrow it?
  A: Only narrow it.
- Q: Should eligibility be snapshotted into session state?
  A: No. Eligibility remains authoritative in `Users` and is checked lazily per join.
- Q: Is that inconsistent with snapshotting team identity?
  A: No. Team data is snapshotted; eligibility roster is not.
- Q: If `users-service` is unavailable during a join attempt, what happens?
  A: Fail closed.
- Q: What should the join-eligibility response shape be?
  A: `eligible / not eligible` plus a machine-readable reason code.
- Q: Can a non-`Participant` user ever be eligible through `RegisteredTeamMembership`?
  A: No. Participant only.
- Q: What happens if a user is deactivated after a live session has started?
  A: They are blocked immediately from further participation.
- Q: How does `SessionOperations` learn about that deactivation quickly enough?
  A: Through a domain event from `users-service`; `session-operations-service` consumes it and applies a runtime participation block.
- Q: Who owns participant-facing team queries?
  A: Split by scope. `Users` owns general registered-team eligibility; `SessionOperations` owns live-session joinability for a specific session.
- Q: Should the roster API be raw CRUD or domain use-case language?
  A: Use-case language.
- Q: What happens if `RegisteredTeamMembership` is revoked while the participant is already in an active session?
  A: Immediate block from further participation, via the same cross-context access-fact pattern as user deactivation.
- Q: Earlier we explored `Forced Team` and open choice semantics. Was that settled?
  A: No. That branch was intentionally left unresolved because it was generating too many edge cases. (Now resolved below via `Open Team Selection`.)
- Q: For a `LiveSession` attached to Red and Blue, if participant P has no `RegisteredTeamMembership` for either, should P see both teams as joinable or be ineligible?
  A: P should see both as joinable and be able to pick one — but via a new `SessionOperations` pre-start policy, not by expanding `Users` eligibility.
- Q: Is that self-assignment a `SessionOperations`-owned open session policy or a `Users`-owned baseline eligibility fact?
  A: A `SessionOperations` open session policy (`Open Team Selection`), available only before the session reaches `Active`, `Paused`, `Finished`, or `Cancelled`. `Users` keeps owning explicit pre-assignment via `RegisteredTeamMembership`.
- Q: If P already has a `RegisteredTeamMembership` for Red and the session is still `Scheduled`/`Preparing`, may P use `Open Team Selection` to join Blue instead?
  A: No. `Open Team Selection` is only for unassigned participants; explicit pre-assignment constrains P to the pre-authorized team set.

## Resolved: team-selection semantics (was "Important unresolved area")

The earlier "forced team" branch was generating too many edge cases. It is now resolved by splitting explicit pre-assignment from open pre-start choice:

- `RegisteredTeamMembership` (owned by `Users`) = explicit pre-assignment. When present, it constrains the participant to that pre-authorized team set.
- `Open Team Selection` (owned by `SessionOperations`) = pre-start fallback for participants with no `RegisteredTeamMembership` on any attached team. They may see all attached teams and self-assign into one, but only while the session has not reached `Active`, `Paused`, `Finished`, or `Cancelled`.
- "Visible in the lobby" and "eligible to self-assign before `Active`" are distinct.

What caused the original complexity (kept for context):

- eligibility stays live in `Users`
- joins are checked lazily per join
- participants may belong to multiple `RegisteredTeam`s

Glossaries were already updated this session (see below) to encode `Open Team Selection` and the split.

### Still open follow-ups for the next grilling round

- Once an unassigned participant self-assigns via `Open Team Selection`, is that assignment revocable/switchable before `Active`, or locked on first pick?
- Does self-assignment via `Open Team Selection` create a `RegisteredTeamMembership` in `Users`, or a purely session-scoped assignment in `SessionOperations`?
- What happens to a participant who never self-assigns before the session becomes `Active` — blocked, spectator, or auto-assigned?
- Team capacity interaction: can `Open Team Selection` overfill a team, and who owns the capacity rule?

## Repo docs already updated during this session

- `/home/samu/Desktop/umbral/backend/services/identity-access-service/CONTEXT.md`
  - renamed context language from `Identity` to `Users`
  - added/updated `RegisteredTeam`, `RegisteredTeamMembership`, `User Deactivation`, and access-boundary wording
- `/home/samu/Desktop/umbral/backend/services/session-operations-service/CONTEXT.md`
  - added `Participation Block`
  - added cross-context access-fact wording for runtime blocking

## Recommended next step

Resume `grill-with-docs` from the unresolved membership semantics question:

- should `RegisteredTeamMembership` mean `may join this team` or `must join this team` for a live session that attached that registered team?

Recommendation from the previous session:

- prefer `may join this team` to reduce edge cases and keep the boundary simpler

## Suggested skills

- `grill-with-docs` to continue resolving the remaining domain language and update docs inline
- `ubiquitous-language` if the team/membership vocabulary needs to be consolidated into a single glossary artifact
- `handoff` again at the end of the next session if more domain decisions are settled but implementation has not started

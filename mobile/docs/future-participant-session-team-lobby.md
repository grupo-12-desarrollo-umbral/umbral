# Future Plan — Participant Session + Team Lobby (Expo app)

> Status: **superseded on 2026-06-02** by
> [`plan-participant-session-team-lobby.md`](./plan-participant-session-team-lobby.md).
> Keep this file only as historical context for the original open-question framing.

## Historical note

This document captured the pre-decision exploration for replacing the HU-07A manual
"Session ID + Team ID (GUID)" form. The branch no longer follows this framing.

## Settled outcome

The active plan now treats the lobby as an **HU-07A scope expansion** on
`feature/hu-07a-participant-membership-validation`, with three decisions already made:

1. **Session scoping uses option B.** Identity will own a minimal session-to-team
   association and expose `GET /api/sessions/{code}/teams`.
2. **Join semantics are conditional self-select.** An unassigned participant may
   self-select any team in the session and thereby self-assign; a pre-assigned
   participant is locked to that team.
3. **The existing membership-validation endpoint remains the final gate.** The lobby and
   self-join flow sit in front of it; Identity still returns access facts, not final
   session admission.

## Phase 5 handoff snapshot

As of **2026-06-02**, the mobile client also hardens the lobby with:

1. Distinct load outcomes for invalid session codes, stale auth, forbidden access, and
   network failures.
2. Retry/refresh affordances for transient error and empty-lobby states.
3. An explicit **assigned team unavailable** warning when the participant is locked out
   of every visible team because their own team is no longer active.

## Current source of truth

Use these files instead of this one:

- [`mobile/docs/plan-participant-session-team-lobby.md`](./plan-participant-session-team-lobby.md)
- [`backend/docs/prd/DES-69-participant-membership-validation.md`](../../backend/docs/prd/DES-69-participant-membership-validation.md)
- [`backend/services/identity-access-service/CONTEXT.md`](../../backend/services/identity-access-service/CONTEXT.md)
- `mobile/src/lib/membership/use-team-lobby.ts`
- `mobile/src/lib/membership/team-lobby-state.ts`
- `mobile/src/app/(app)/team-lobby.tsx`

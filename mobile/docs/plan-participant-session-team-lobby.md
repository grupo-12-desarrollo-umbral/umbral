# Plan — Participant Session + Team Lobby (self-select model)

> Status: **proposed.** Implementation plan for
> [`future-participant-session-team-lobby.md`](./future-participant-session-team-lobby.md).
> Replaces the HU-07A manual "Session ID + Team ID (GUID)" form with a guided
> session → team-lobby → team-space flow where participants self-select a team.

## Product model (decided)

The lobby lists the teams **in a session**; a participant joins a team and lands in its
space. The operator's job is to **create the teams** (and optionally pre-assign
participants) so participants can join them. The join rule is **conditional
self-select**:

- **Unassigned participant** → may **self-select any team** in the session; selecting a
  team **self-assigns** their membership.
- **Pre-assigned participant** (operator already put them on a team via HU-05) → is
  **locked to that team**; other teams render disabled and entering them is denied. This
  preserves the spirit of the original HU-07A own-team invariant for assigned players.

New invariant this implies: a participant holds **at most one active team membership per
session**. The lobby payload must tell the client which team (if any) is already the
caller's so it can render the lock state.

This redesign **intentionally overrides four original backlog slices**. Each is a
deliberate product decision that contradicts the slice's original PRD and must be
recorded (decision log + `CONTEXT.md`), not silently changed:

| Slice | Original rule | New rule | Where |
| --- | --- | --- | --- |
| **HU-04** | Only an **Administrator** registers teams | **Operator** can register teams | Phase 1.5 |
| **HU-05** | Only an **Administrator** assigns participants to teams | **Operator** assigns participants to teams | Phase 1.5 |
| **HU-07A** | Participant may enter **only their own team** ("never peek into another team") | **Conditional self-select**: unassigned → any team (self-assigns); pre-assigned → locked to own team | Phases 0, 3 |
| **HU-18** (unbuilt) | — | A **"teams in a session"** association is required to scope the lobby honestly | Phases 0, 2 |

> **Scope decision:** the lobby is treated as an **expansion of HU-07A's own scope and
> acceptance criteria** (not a separate HU-07B slice), so it lands on the current
> `feature/hu-07a-participant-membership-validation` branch. Because that rewrites the
> slice's acceptance criteria, **DES-11 / DES-69 and the PRD
> (`backend/docs/prd/DES-69-…md`) must be updated in this branch** to replace the
> own-team invariant with the conditional self-select model — otherwise the PR ships
> code that contradicts its own stated AC.

## Branch map (which change goes where)

| Phase / change | Branch | Base | Notes |
| --- | --- | --- | --- |
| Phase 1 — mobile skeleton (join → lobby → team-space, option-A tracer) | `feature/hu-07a-participant-membership-validation` (current) | develop | HU-07A scope expansion |
| Phase 2 — `GET /api/sessions/{id}/teams` (session scoping) | `feature/hu-07a-participant-membership-validation` | develop | same slice |
| Phase 3 — participant self-join + one-membership-per-session lock | `feature/hu-07a-participant-membership-validation` | develop | same slice |
| Phase 4 — QR / deep link | `feature/hu-07a-participant-membership-validation` | develop | UX polish, same slice |
| DES-11 / DES-69 + PRD acceptance-criteria update | `feature/hu-07a-participant-membership-validation` | develop | must ride with the code above |
| Phase 1.5 — HU-04 (register) + HU-05 (assign) → operator | **`feature/refactor-hu-04`** | develop | separate auth refactor; own PR |

Everything except the HU-04/HU-05 auth refactor lives on the current branch. The two
branches have **no code dependency** (the mobile tracer reads existing `GET /api/teams`),
so they can proceed in parallel.

## Current reality (verified in code, 2026-06-02)

- **No "teams in a session" concept exists anywhere.** Identity `Team`
  (`DisplayName`, `TeamCode`, `IsActive`) is **global reference data** — not scoped
  to a session.
- `session-operations-service/` exists as a directory but is **empty scaffolding**
  (no source).
- `LiveSessionId` lives only as an **opaque correlation id** on `JoinToken`; nothing
  links a team to a session.
- Identity already exposes: `GET /api/teams` (paged), `GET /api/teams/{id}`,
  `POST /api/teams` (register, **Admin-only** — `RegisterTeamCommand.cs:5`),
  `POST /api/teams/{id}/participants` (assign), and the final gate
  `POST /api/permissions/participant-membership-access` (Participant-policy).
- Mobile already has the reusable final-gate stack: `lib/api/membership.ts`,
  `lib/membership/membership-policy.ts`, `lib/membership/use-membership-access.ts`.
  These are **reused as-is**.

---

## Phase 0 — Team-in-a-session scoping: **option B (decided)**

A team must be scoped to a session for the lobby to be honest. **Decision: option B —
add session→team association inside Identity** and expose `GET /api/sessions/{id}/teams`.
(Considered and rejected: A = global `GET /api/teams`, session label is cosmetic;
C = build the whole `session-operations-service` / HU-18, far more work.)

**A is still used as a throwaway Phase-1 tracer** so the mobile flow is demo-able before
the B endpoint lands; it is deleted in Phase 2.

---

## Phase 1 — Mobile flow skeleton (no new backend)

Build navigation + screens against existing endpoints so the UX is real immediately.

1. **`join.tsx` → session-id only.** Remove the Team ID + token fields; keep a single
   typed **Session ID** input. Retire the GUID-pair harness.
2. **New `team-lobby.tsx`** between join and team-space. Phase 1 populates it from
   `GET /api/teams` (option-A tracer) so it renders real data.
3. **`team-space.tsx`** stays the landing screen, now reached via lobby selection.
4. **`index.tsx`** copy update ("enter a session to see its teams").
5. New `mobile/src/lib/api/teams.ts` (`listTeams`) + a `use-team-lobby` hook
   mirroring the `use-membership-access` shape.

**Deliverable:** login → session id → lobby → tap team → team-space, running today.

---

## Phase 1.5 — HU-04 + HU-05: team management Admin-only → Operator (Identity)

**Why:** in the real flow the **operator** runs the live session, creates its teams,
and assigns participants. Both being Administrator-only is a leftover that doesn't match
how sessions get staffed.

1. **HU-04 — `RegisterTeamCommand`** → authorize **Administrator + Operator** (reuse the
   existing `AdminOrOperator` policy rather than an inline role list).
2. **HU-05 — `AssignParticipantToTeamCommand`** → same change to **Administrator +
   Operator**.
3. **Two gates per command.** Each of these commands is gated in *two* places — the
   `[Authorize(Roles = …)]` attribute **and** a handler-level capability check (e.g.
   `RegisterTeamCommandHandler` throws `AdministratorPanel`). Flip **both**, or the
   attribute will pass and the handler will still throw for an operator.
4. Re-check the remaining `TeamsEndpoints` operations (`UpdateTeam`, `DeactivateTeam`)
   for the same admin-only assumption and decide whether they should follow.
5. **Tests:** extend HU-04/HU-05 handler/endpoint tests so an Operator succeeds and a
   Participant still gets `403`; keep the ADR-0005 coverage gate green.
6. **Record the supersession** in the decision log / `CONTEXT.md`.

**Branch:** **`feature/refactor-hu-04`** — HU-04 and HU-05 are the same authorization
refactor and ride together here, separate from the lobby branch. Coordinate so it
doesn't collide with in-flight HU-04/HU-05 work.

---

## Phase 2 — Backend: session-scoped team discovery (option B)

1. Identity: minimal `LiveSession` reference entity + `SessionTeam` link (or a
   `LiveSessionId` column on a join table); EF migration.
2. `GET /api/sessions/{id}/teams` → for each team `{ teamId, displayName, joinState }`,
   where **`joinState` is computed for the calling participant**: `mine` (caller already
   has a membership on this team), `joinable` (caller has no membership in the session),
   or `locked` (caller is assigned to a *different* team). This single annotated payload
   is what powers the lobby's lock rendering (Phase 3) — no second round-trip.
   Participant-policy (reuse the trusted-header policy already on
   `participant-membership-access`).
3. CQRS query + validator + handler following existing `GetTeams` /
   `ValidateParticipantMembershipAccess` patterns. Tests across
   domain/app/infra/api per DES-69's testing posture; coverage gate stays green.
4. Mobile: point `team-lobby` at the new session-scoped endpoint; render `joinable`
   cards as tappable and `locked` cards disabled; delete the option-A tracer.

---

## Phase 3 — Conditional self-assign on join (decided: 3a + lock)

Tapping a team **self-assigns** the caller's membership, then routes through the
**existing** final gate — but constrained by the operator's pre-assignment:

1. **New participant-facing join command/endpoint** — a self-service variant of
   `POST /api/teams/{id}/participants` that derives the `UserId` from the trusted-header
   identity (today the command takes an operator-supplied `UserId` and is Admin-only).
   Participant-policy.
2. **Enforce the one-membership-per-session invariant in the domain.** Today
   `Team.AssignParticipant` only blocks a duplicate on the *same* `Team` aggregate, so a
   participant could accumulate memberships across teams. Add a cross-team check (scoped
   to the session): if the caller already holds a membership on another team in the
   session, **reject** the self-assign — they are locked to their team. If they already
   hold a membership on *this* team, it's a no-op success (re-entry).
3. **Operator pre-assignment wins.** Because a pre-assigned participant already has a
   membership, step 2 naturally denies any other team — no separate rule needed. The
   lobby's `locked` rendering (Phase 2 `joinState`) is the UX mirror of this gate.
4. **Final gate unchanged.** After a successful self-assign the caller holds a
   membership, so the existing `POST /api/permissions/participant-membership-access`
   passes and stays meaningful for audit.

Mobile: the tap handler calls the join command, then routes via the **existing**
`validateParticipantMembershipAccess` + `membership-policy` — those modules do not
change. A `locked` card is non-tappable; a foreign-team attempt that slips through is
surfaced via the existing `forbidden` outcome copy.

---

## Phase 4 — Session ID acquisition: QR / deep link (UX polish)

1. `expo-camera` QR scanner + `umbral://join?session=<id>` deep link (Expo Router
   linking).
2. QR encodes the session id; scanning pre-fills/skips the join screen straight to the
   lobby.

> Per `mobile/AGENTS.md`: read the Expo v56 versioned docs
> (<https://docs.expo.dev/versions/v56.0.0/>) before writing any of this.

---

## Phase 5 — Hardening & handoff

- Empty/locked states (no teams yet, deactivated team, network error) reusing the
  existing banner patterns.
- Tests: mobile lobby-hook unit tests mirroring `membership-policy.test.ts`; backend
  coverage gate green.
- Decision-log debrief + update `future-participant-session-team-lobby.md` /
  `CONTEXT.md` to record the three supersessions (HU-04, HU-07A, HU-18).

---

## Sequencing & dependencies

- **Phase 1** ships standalone (demo-able now).
- **Phase 1.5** is independent of the data-source choice; can land first.
- **Phase 2** unblocks honest session scoping; **Phase 3** can run in parallel with 2.
- **Phases 4 and 5** are polish, independent of each other.

```
Phase 1 (mobile skeleton, option-A tracer) ──┐
Phase 1.5 (HU-04 + HU-05 → operator)     ────┤
Phase 0 decision (A/B/C) ── Phase 2 (B: GET /api/sessions/{id}/teams) ──┐
                                              Phase 3 (join semantics) ──┤
                                                       Phase 4 (QR/deep link) ──┐
                                                                  Phase 5 (hardening)
```

## Decisions (settled)

1. **Phase 0 — team scoping:** option **B** (session→team association in Identity,
   `GET /api/sessions/{id}/teams`). A used only as a Phase-1 tracer.
2. **Phase 3 — join semantics:** **3a self-assign on join**, with the
   **one-membership-per-session** lock — operator pre-assignment overrides self-select.
3. **Branches:** lobby + mobile skeleton + Phase 3 land on the current
   **`feature/hu-07a-participant-membership-validation`** branch (HU-07A scope
   expansion); the HU-04 **and HU-05** auth refactor goes on
   **`feature/refactor-hu-04`**. DES-11 / DES-69 acceptance criteria + the PRD are
   updated in the 07A branch to record the own-team → conditional-self-select
   supersession. Coordinate with the HU-07A/07B owner (Salomon per the roadmap).

## Still to confirm

- None blocking. Optional: whether `UpdateTeam` / `DeactivateTeam` should also widen to
  operators (Phase 1.5 step 4), and the session-id delivery method for Phase 4
  (typed-now is assumed; QR/deep link is the later layer).

## Current code touchpoints

- `mobile/src/app/(app)/index.tsx` — participant home; entry to the flow.
- `mobile/src/app/(app)/join.tsx` — GUID form → session-id-only input.
- `mobile/src/app/(app)/team-lobby.tsx` — **new** screen (session → teams).
- `mobile/src/app/(app)/team-space.tsx` — landing screen after selection.
- `mobile/src/lib/api/membership.ts` — final gate (reuse as-is).
- `mobile/src/lib/membership/membership-policy.ts` — outcome mapping (reuse as-is).
- `mobile/src/lib/api/teams.ts` — **new** lobby data client.
- `backend/.../identity-access-service/src/Application/Teams/Commands/RegisterTeam/RegisterTeamCommand.cs`
  (+ `RegisterTeamCommandHandler.cs` capability check) — HU-04 authorization change.
- `backend/.../identity-access-service/src/Application/Teams/Commands/AssignParticipantToTeam/AssignParticipantToTeamCommand.cs`
  (+ handler) — HU-05 authorization change.
- `backend/.../identity-access-service/src/Domain/Entities/Team.cs` —
  `AssignParticipant` cross-team one-membership-per-session check (Phase 3).
- `backend/.../identity-access-service/src/Api/Endpoints/PermissionsEndpoints.cs` /
  `TeamsEndpoints.cs` — session-teams discovery + participant self-join surfaces.

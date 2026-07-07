# Users Realignment — Settled Decisions

Date: 2026-07-06
Scope: `identity-access-service` → `Users` rename and the `Users` ↔ `SessionOperations` boundary split, resolved across two `grill-with-docs` sessions.

This is the consolidated decision record. Glossary detail lives in the two `CONTEXT.md` files; this doc is the single "what we settled and why" reference.

---

## 1. Context and bounded-context shape

- The deployable **and** the bounded context both become **`Users`** (was `Identity` / `identity-access-service`).
- `Users` is a **fourth, supporting** bounded context alongside the three academically required ones: `MissionDesign`, `SessionOperations`, `ScoringMonitoring`.
- `Users` delegates authentication to **Keycloak** (the identity provider). It keeps a minimal user DB — **no passwords**.
- `Users` does **not** own live-session runtime participation.

## 2. What `Users` owns vs delegates

**Owns:**
- `User` projection — minimal: `ExternalIdentityId, DisplayName, Email, Role, IsActive` (no passwords).
- `Role` — changes must propagate to **both Keycloak and the local DB**.
- `User Deactivation` — must propagate to Keycloak and emit a domain event; downstream contexts treat it as immediately blocking.
- `IdentityProviderSession`, access facts / access-policy evaluation.
- `RegisteredTeam` (catalog) and `RegisteredTeamMembership` (whitelist) — operator/admin backoffice reference data.

**Delegates to Keycloak:** authentication, credential storage, login.

**Explicitly NOT in `Users` (moves to `SessionOperations`):** session-to-team association, join tokens, live participant→team assignment, participant-team lock.

## 3. Team model — the core split

Team identity is **two concepts**, not one:

| Concept | Owner | Meaning |
|---|---|---|
| `RegisteredTeam` | `Users` | Pre-session team catalog entry created by admins/operators. Reference data. |
| Session team participation (`Team` / `SessionParticipant` / `TeamMember`) | `SessionOperations` | Runtime team state inside one `LiveSession`. |

- Team identity is **snapshotted** into `SessionOperations` when a live session is created. A later `RegisteredTeam` rename does **not** flow into existing live sessions.
- `RegisteredTeam` is **catalog + preassigned authorization roster**, not catalog-only.

## 4. `RegisteredTeamMembership` — whitelist, not mandate  *(settled this session — Q1)*

- `RegisteredTeamMembership` = an **eligibility whitelist**: which teams a `Participant` **may** join. Never **must**.
- Only `Participant` users can be eligible through it.
- When memberships exist for a session's attached teams, the participant's pre-start choice is **restricted to that authorized set**.
- Whether a participant must *end up* on a team is a `SessionOperations` decision, not a `Users` one.
- **Not** the live-session roster; **not** guaranteed admission.

## 5. Eligibility vs admission

- `Users` grants **baseline eligibility** and returns `eligible / not eligible` + a **machine-readable reason code**.
- `SessionOperations` makes the **final admission decision** and may only **narrow** baseline eligibility, never expand it.
- Eligibility is **not snapshotted**; it stays authoritative in `Users` and is **checked lazily per join**.
- If `users-service` is unavailable during a join attempt, `SessionOperations` **fails closed**.

## 6. Cross-context runtime blocking

- If a user is **deactivated** in `Users`, they are blocked immediately from further live participation.
- Propagation is by **domain event** from `Users`; `SessionOperations` consumes it and applies a **`Participation Block`**.
- If a `RegisteredTeamMembership` is **revoked**, the participant is blocked from further runtime participation through the **same** cross-context access-fact pattern.

## 7. `Open Team Selection` — pre-start self-assignment  *(SessionOperations-owned)*

- A `SessionOperations` pre-start policy: an **unassigned** participant (no `RegisteredTeamMembership` for any attached team) may see **all attached teams** and self-assign into one.
- Available **only** while the `LiveSession` has **not** reached `Active`, `Paused`, `Finished`, or `Cancelled` (i.e. only in `Scheduled` / `Preparing`). It disappears once runtime play starts.
- A participant who **already has** a `RegisteredTeamMembership` may **not** use `Open Team Selection` to escape their authorized set.
- "Visible in the lobby" and "eligible to self-assign before `Active`" are **distinct**.

### Where a pick lives *(settled this session — Q2)*

- An `Open Team Selection` pick is **session-scoped only** — it produces a `SessionParticipant` / `TeamMember` association in `SessionOperations`.
- It **never** writes a `RegisteredTeamMembership` back to `Users`. No runtime mutation of upstream reference data.

## 8. Pre-Start Team Assignment — switch and freeze  *(settled this session — Q3/Q4/Q5)*

- A participant's team membership (from `Open Team Selection` **or** a choice within their `RegisteredTeamMembership` authorized set) is **mutable only in `Scheduled` / `Preparing`**.
- It **freezes** once the session reaches `Active`, `Paused`, `Finished`, or `Cancelled`. No runtime reassignment.
- Switching is always **confined to the authorized set** (all attached teams if no membership; the whitelisted teams otherwise) and is **capacity-checked** on the team being joined. Switching out frees a slot.
- **Capacity is owned by `SessionOperations`** (already true in code: `Team.Capacity`).
- A participant who holds **no team when the session reaches `Active`** is **not admitted to active play**. There is **no auto-assignment** (chosen over auto-seating the singleton case — option (i), for determinism and no balancing rule).

## 9. Participant scenarios (session attaches Red + Blue, and Green for the multi case)

| Participant | Authorized set | Pre-start behaviour | No pick by `Active` |
|---|---|---|---|
| **No membership** | all attached | `Open Team Selection`: self-assign any attached team; switch freely among all | Not admitted |
| **One membership** (Red) | {Red} | Take Red or hold; no `Open Team Selection`; nothing to switch to | Not admitted |
| **Two+ memberships** (Red, Blue) | {Red, Blue} | Switch **between Red and Blue** only — never Green; no `Open Team Selection` | Not admitted |

Rule across all three: the whitelist gates the *set*; `Open Team Selection` only fills the empty-set case; `Active` freezes.

## 10. API language

- Participant-facing team queries split by scope:
  - `Users`: which `RegisteredTeam`s a participant is generally eligible for.
  - `SessionOperations`: which teams are joinable for a specific `LiveSession`.
- The team-authorization roster API is **use-case oriented**, not raw CRUD.

---

## Code divergence — realignment targets (not yet implemented)

The decisions above are settled in **docs only**. The code still diverges:

**Benign (docs ahead of code):** the rename hasn't touched code — directory is still `identity-access-service`, namespace `umbral_backend`. `RegisteredTeam` / `RegisteredTeamMembership` / `Open Team Selection` / `Participation Block` exist nowhere in code yet.

**Conflicting (code implements the rejected model):**

- `ParticipantSessionMembershipPolicy` (participant-team **lock** via `ParticipantLockedToAnotherSessionTeamException`) — implements the **rejected mandate/lock** semantics.
- `Team.AssignParticipant` / `TeamMembership.Assign` — `TeamMembership` in code is a **live assignment** (`AssignedAt` + `ParticipantAssignedToTeamEvent`). Same word as the new `RegisteredTeamMembership` whitelist, **opposite meaning**.
- `SessionTeamAssociation`, `JoinToken`, `LiveSessionReference` — session-participation concepts sitting in the wrong context.

These are the concrete **move-and-rewrite** targets for the split.

**Known Keycloak-sync bugs (separate implementation work, not fixed by this grilling):**

- `#81` — role sync to Keycloak is fire-and-forget and swallows failures (should write Keycloak + DB, or fail loud).
- `#82` — user deactivation is not propagated to Keycloak.

---

## Next steps

- **Implement** the Keycloak-sync fixes (`#81`, `#82`) — small, well-scoped.
- **Move** session participation (`SessionTeamAssociation`, `JoinToken`, live assignment + lock) out of `Users` into `SessionOperations`; rename `Team`→`RegisteredTeam`, `TeamMembership`→`RegisteredTeamMembership` (whitelist) in `Users`.
- **Rename** the deployable/namespace `identity-access-service` → `users-service`.
- **ADR** worth writing **once the refactor is committed**: the `Users` ↔ `SessionOperations` split with whitelist-not-mandate membership (hard to reverse then; a future reader will wonder why membership doesn't assign).
- `/to-issues` can turn the targets above into tickets.

## Related artifacts

- `backend/services/identity-access-service/CONTEXT.md` — `Users` glossary
- `backend/services/session-operations-service/CONTEXT.md` — `SessionOperations` glossary
- `backend/CONTEXT-MAP.md` — context map (renamed to `Users`)
- `backend/docs/identity-access-service-alignment-findings-2026-07-06.md` — original findings + realignment target note

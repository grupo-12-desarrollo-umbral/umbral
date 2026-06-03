# Zoom-Out: Sessions, Teams, and Participant Join

A higher-level map answering: Who creates the session? How do participants join
(do they join their team)? Who assigns teams to sessions? Who creates the teams?

Authoritative sources: `backend/docs/prd/.../ddd_solution_model.md` (§3 contexts,
§5 aggregates, §6 events, §8 policies), `backend/CONTEXT-MAP.md`, and
`umbral_user_stories.md`.

## The key subtlety first: there are two `Team`s

The domain deliberately splits `Team` across two bounded contexts:

- **Reference-data `Team`** — lives in **`Identity`** (`identity-access-service`).
  The pre-session catalog: `id`, `name`, `code`, active status, plus the
  `TeamMembership` record. (HU-04 / HU-05)
- **Runtime `Team`** — lives in **`SessionOperations`** (`session-operations-service`),
  *inside* the `LiveSession` aggregate. Carries live state: `currentScore`,
  `joinStatus`, `progressNodeId`, `TeamCode`.

Almost every answer below hinges on which `Team` you mean.

## Answers

### 1. Who creates the session?
The **`Operador`**, in **`SessionOperations`**.

- HU-15 — create a *mission* `LiveSession` from an **active** `Mission`.
- HU-16 — create a *trivia* `LiveSession` from a **published** `TriviaQuiz`.
- HU-17 — a session is created from **exactly one** content source (no mixing
  mission + quiz).

The `Mission` / `TriviaQuiz` themselves are authored by the **`Administrador`** in
**`MissionDesign`** (HU-09–HU-14). Separately, HU-19: the **`Administrador`** assigns
an `Operador` to each session. Governing rules: `SessionCreationPolicy`; events:
`LiveSessionCreated` / `LiveSessionScheduled`.

### 2. How do participants join — do they join their team?
Yes, they join *their* team, and this is a **two-context handshake**:

- HU-06 (`Identity`) — the `participant` authenticates on the mobile client
  (via Keycloak).
- **HU-07 / HU-07A (`Identity`)** — membership validation: confirm the
  authenticated participant belongs to the `Team` (reference-data `TeamMembership`)
  for the active session, and block access to any team that isn't theirs. Driven by
  `AccessPolicy` + `JoinTokenPolicy` (`JoinToken` issued/consumed).
- The **actual join** happens in **`SessionOperations`**: `JoinPolicy` validates
  access + late-join + reconnection, producing `ParticipantJoinedSession` and
  `ParticipantAssignedToTeam`, recorded in `JoinContext` / `SessionParticipant` /
  `TeamMember`.

Trivia rule (HU-07 AC4): no late join after trivia start — only **reconnection** of
already-authorized participants. HU-08 then keeps multiple devices of the same
`Team` in sync without bleeding state across teams.

So: **`Identity` decides *whether* you may enter and which team is yours;
`SessionOperations` performs the runtime join into the live team.**

### 3. Who assigns teams to sessions?
The **`Operador`**, in **`SessionOperations`** (HU-18).

- Adds **registered + active** reference-data teams to a *scheduled* session,
  before it starts.
- A session **cannot start with zero teams**; the assignment is persisted pre-start.
- This is where reference-data `Team` (Identity) becomes a runtime `Team` inside the
  `LiveSession` — event `TeamRegisteredInSession`.

### 4. Who creates the teams?
The **`Administrador`**, in **`Identity`** (HU-04) — creates/edits/deactivates the
reference-data `Team` catalog. HU-05 (also `Administrador`, `Identity`) then assigns
participants into teams via `TeamMembership`. Only **active** teams can be associated
to new sessions (HU-04 AC4).

## Lifecycle, end to end

```
Administrador (Identity)        →  HU-04 create Teams (reference data)
Administrador (Identity)        →  HU-05 assign participants → TeamMembership
Administrador (MissionDesign)   →  HU-09..14 author Mission / TriviaQuiz
Operador (SessionOperations)    →  HU-15/16/17 create LiveSession from one source
Administrador (Identity)        →  HU-19 assign Operador to the session
Operador (SessionOperations)    →  HU-18 associate active Teams → runtime Team
Participant (Identity)          →  HU-06 login, HU-07/07A membership validation
Participant (SessionOperations) →  join → ParticipantAssignedToTeam, reconnect
```

## Module map

| Concern | Bounded context → service | Actor | HU |
|---|---|---|---|
| Team catalog + membership | `Identity` → `identity-access-service` | Administrador | HU-04, HU-05 |
| Mission / Trivia authoring | `MissionDesign` → `mission-design-service` | Administrador | HU-09–14 |
| Create `LiveSession` | `SessionOperations` → `session-operations-service` | Operador | HU-15–17 |
| Assign Operador to session | `Identity` (access facts) | Administrador | HU-19 |
| Assign Teams → session (runtime) | `SessionOperations` | Operador | HU-18 |
| Participant login | `Identity` | Participant | HU-06 |
| Membership validation + reconnect | `Identity` (+ `SessionOperations` `JoinPolicy`) | Participant | HU-07 / HU-07A |

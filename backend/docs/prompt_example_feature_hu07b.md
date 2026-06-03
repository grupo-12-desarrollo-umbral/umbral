# Prompt Example — HU-07B Authorized Participant Reconnection (Feature Slice)

Concrete prompt sequence for driving HU-07B through a full feature slice on `feature/hu-07b-participant-reconnection`. Follows the pattern in [workflow_for_prompts.md](./workflow_for_prompts.md).

**Key difference from HU-07A:** HU-07A established the participant session-code -> team-lobby -> team-selection flow and exposed Identity access facts through `POST /api/permissions/participant-membership-access`. HU-07B must not re-implement that slice in `identity-access-service`. Its main backend work belongs in `session-operations-service`, which owns late join, reconnect, capacity, participant runtime state, live-session constraints, and restoration of live team state.

Drive each backend phase with `@backend/.agents/driver-agent.md`, selecting phases in order: **X.1 -> X.2 -> X.3 -> X.4**. The driver delegates implementation to `@backend/.agents/backend-agent.md`; do not invoke it directly. For the mobile slice, use Step 9 directly with `@mobile/AGENTS.md`. Do not mix backend and mobile work in the same phase; the driver coordinates them as separate scoped steps tied together by the verified backend contract.

---

## Required design patterns

- `Proxy`
  - Why: authorized reconnection restores live team state over the real-time channel.
  - Phase owner: X.2 Application **and** X.4 API
  - Gate obligation: access is enforced through a guard — `AuthorizationBehaviour`, an authorization proxy, or endpoint/hub authorization policy — with **no ad-hoc role or identity `if` checks** in handlers, endpoints, or hub methods.

Transport note (NOT a pattern, NOT a gate): HU-07B is tagged SignalR / WebSockets in the patterns matrix. Unlike HU-07A, this slice is allowed to implement its reconnect/admission hub path in `session-operations-service`. Identity still does **not** own the hub or final runtime admission decision.

---

## Pre-resolved orient (draft skeleton)

> Replace placeholder ids after generator/Linear resolution. This skeleton intentionally
> encodes the corrected service boundary and current HU-07A flow.

### What has already landed and must be reused

**HU-07A baseline**
- participant enters with a session code
- client loads a session-scoped team lobby
- participant selects a team
- Identity returns access facts through `POST /api/permissions/participant-membership-access`
- final admission and runtime restoration are still out of Identity scope

**Identity-side reuse**
- trusted-header authenticated participant flow is already in place
- `POST /api/permissions/participant-membership-access` is the contract HU-07B should consume
- `JoinToken.Consume()` exists in the domain but was intentionally not endpoint-wired in HU-07A

**Session-operations ownership to preserve**
- `LiveSession`, `SessionParticipant`, `JoinContext`, `JoinPolicy`
- final admission decision
- late join vs authorized reconnect rules
- capacity, assignment, live-state constraints
- real-time presence and team/session restoration

### What HU-07B adds

| Concern | New work |
| --- | --- |
| Domain | Extend runtime admission/reconnection modeling and `JoinPolicy` rules for late join vs authorized reconnect, presence recovery, and restoring live team state. |
| Application | Add `ReconnectAuthenticatedParticipant` and orchestration around querying Identity access facts, checking session-owned rules, and resuming presence. |
| Infrastructure | Persist session/runtime participation state needed to recognize reconnecting participants and restore their team/session context. |
| API | Expose the reconnect/admission endpoint and/or hub admission path used by the mobile client, guarded through the service's proxy/authorization approach. |

### Branch state and prerequisite

`feature/hu-07b-participant-reconnection` should branch from **`develop`**, since HU-07A is already merged.

### Linear state

- HU ticket: `<DES-HU07B>` — resolve status and labels via Linear MCP
- Owning service label must be `svc:session-operations-service`
- PRD ref: `<DES-PRD-SESSION-OPS>` — resolve to a local file in `@backend/docs/prd/`
- Supporting Identity PRD/context: `DES-67`, `DES-69`, `@backend/docs/hu07a-context.md`

> Linear live state may have changed. Verify ticket ids and labels, but do not let that
> override the ownership rules already established in the local docs.

---

## 1. Orient — read current service state and contrast against HU-07A

> Skip this step if you have already read the pre-resolved orient section above and the
> local docs are unchanged.

```text
Read the following files and summarise what has already been decided:
- @backend/services/session-operations-service/README.md
- @backend/services/session-operations-service/CONTEXT.md
- @backend/docs/ddd_solution_model.md
- @backend/docs/hu07a-context.md
- @backend/docs/prd/DES-69-participant-membership-validation.md

Then use the Linear MCP to fetch only the current live state of:
- <DES-HU07B> (HU-07B) — status and labels
- the owning session-operations PRD ticket — status and labels

Output:
- the current HU-07A baseline flow (session code -> team lobby -> team selection)
- what HU-07B must add in SessionOperations
- the resolved HU id, PRD id, current status, and labels

Do not start planning or implementing yet.
```

---

## 2. Label the HU ticket as ready-for-agent

```text
Use the Linear MCP to confirm (or add) the label ready-for-agent on <DES-HU07B>.
Confirm the label was applied and output the updated ticket state.
```

---

## 3. Confirm slice readiness

```text
Use the Linear MCP to confirm <DES-HU07B> now carries both
svc:session-operations-service and ready-for-agent labels and output its current
status and acceptance criteria.

Resolve the owning session-operations PRD ticket and the local PRD file in
@backend/docs/prd/. Read the local file if you need implementation decisions.

Output the confirmed HU id, title, acceptance criteria, labels, and PRD ref
before planning the slice.
```

In the remaining examples below, `HU-07B`, `<DES-HU07B>`, and `<DES-PRD-SESSION-OPS>` are placeholders until resolved.

---

## 4. Start the slice

```text
Prepare the authorized participant reconnection slice on branch
feature/hu-07b-participant-reconnection.
Use the HU id and DES id resolved from Linear in the previous step.
This slice affects the mobile client and session-operations-service.

Before implementation, confirm the current baseline is the landed HU-07A flow:
- participant enters with a session code
- the client loads the session-scoped team lobby
- the participant selects a team
- Identity returns access facts via POST /api/permissions/participant-membership-access

This slice must extend that flow; do not recreate HU-07A in identity-access-service.

Move the resolved HU ticket to In Progress and output the exact scope, branch name,
and touched surfaces.
```

---

## 5. Backend phase X.1 — Domain layer

> Run `@backend/.agents/driver-agent.md` and select **X.1** from the phase menu.

```text
Use @backend/.agents/backend-agent.md.
Implement backend phase X.1 for HU-07B in session-operations-service.
Use the owning session-operations PRD and canonical docs.

Before writing anything, inspect the current session-operations domain baseline and
confirm which concepts already exist around LiveSession, SessionParticipant,
JoinContext, SessionState, Team, and JoinPolicy. Extend rather than recreate.

Scope:
- add or extend the runtime admission/reconnection model so authorized reconnect is
  distinct from forbidden late join
- extend JoinPolicy rules for late join vs reconnect, session-state constraints,
  participant assignment, capacity, and presence recovery
- model how a reconnecting participant restores live team/session context without
  weakening session-owned invariants
- preserve the rule that Identity supplies access facts but SessionOperations makes
  the final admission decision

Gate:
- Domain build passes
- no existing SessionOperations lifecycle or admission invariants are broken
- new invariants are expressed as unit tests for JoinPolicy/runtime reconnection behavior

Do not touch other backend layers or mobile.
```

Commit:

```text
feat(session-operations): phase X.1 — domain layer (HU-07B)

Ref: HU-07B
Ref: <DES-HU07B>
Ref: <DES-PRD-SESSION-OPS>
```

---

## 6. Backend phase X.2 — Application layer

> Run `@backend/.agents/driver-agent.md` and select **X.2** from the phase menu.

```text
Use @backend/.agents/backend-agent.md.
Implement backend phase X.2 for HU-07B in session-operations-service.
Use the owning session-operations PRD and canonical docs.

Before writing anything, inspect the existing Application baseline and mirror its
conventions for commands, queries, validators, DTOs, handlers, and permissions.

Scope:
- add the ReconnectAuthenticatedParticipant use case named in the model docs
- orchestrate the call to Identity's POST /api/permissions/participant-membership-access
  as an access-fact dependency, not as the final admission decision
- check session-owned runtime rules through JoinPolicy and related session state
- resume presence and restore the participant's live team/session context when allowed
- Proxy obligation (mandated): authorization for reconnect/admission is enforced through
  a guard — AuthorizationBehaviour, an authorization proxy, or equivalent — not through
  ad-hoc role/identity checks inside the handler
- handler unit tests: allowed reconnect, denied late join, denied wrong-team or
  missing-access-fact path, denied invalid-session-state path, denied capacity path

Gate:
- clean build passes
- handler + validator unit tests pass for all principal paths and rejection branches
- Proxy gate: access is enforced through a structural guard with no ad-hoc role or
  identity checks leaking into handlers

Do not touch Infrastructure, Api, or mobile.
```

Commit:

```text
feat(session-operations): phase X.2 — application layer (HU-07B)

Ref: HU-07B
Ref: <DES-HU07B>
Ref: <DES-PRD-SESSION-OPS>
```

---

## 7. Backend phase X.3 — Infrastructure layer

> Run `@backend/.agents/driver-agent.md` and select **X.3** from the phase menu.

```text
Use @backend/.agents/backend-agent.md.
Implement backend phase X.3 for HU-07B in session-operations-service.
Use the owning session-operations PRD and canonical docs.

Scope:
- persist the runtime/session participation state needed to recognize a reconnecting
  participant and recover their team/session context
- add repository implementations and EF configuration for any new runtime recovery state
- add a migration if needed
- if hub/group recovery needs backing storage or projections, implement them here
- integration tests must prove runtime participation state can be stored and restored

Gate:
- migration succeeds
- repository/infrastructure integration tests pass against a real PostgreSQL instance via Testcontainers
- no ephemeral-only reconnect assumption remains where durable recovery is required

Do not touch Api or mobile.
```

Commit:

```text
feat(session-operations): phase X.3 — infrastructure layer (HU-07B)

Ref: HU-07B
Ref: <DES-HU07B>
Ref: <DES-PRD-SESSION-OPS>
```

---

## 8. Backend phase X.4 — API layer

> Run `@backend/.agents/driver-agent.md` and select **X.4** from the phase menu.

```text
Use @backend/.agents/backend-agent.md.
Implement backend phase X.4 for HU-07B in session-operations-service.
Use the owning session-operations PRD and canonical docs.
Follow @backend/.claude/skills/aspnet-backend-testing/ for test type and layer placement.

Scope:
- expose the reconnect/admission endpoint and/or hub admission path used by the mobile client
- wire the endpoint/hub to the existing ReconnectAuthenticatedParticipant application flow
- keep transport thin: no business logic in endpoint or hub methods
- Proxy obligation (mandated): protect reconnect/admission through endpoint/hub authorization
  policy or proxy guard, not inline `if` checks
- integration tests should prove an authorized reconnect restores the expected live context,
  while unauthorized or invalid reconnect attempts are rejected with the correct status/result

Gate:
- endpoint/hub integration tests run through the service host, not only handler unit tests
- unauthorized, invalid-session-state, and forbidden-late-join paths are rejected correctly
- Proxy gate: authorization is structural and not duplicated inline across endpoint/hub methods
- service coverage reaches the enforced threshold

Do not touch mobile.
```

Commit:

```text
feat(session-operations): phase X.4 — api layer (HU-07B)

Ref: HU-07B
Ref: <DES-HU07B>
Ref: <DES-PRD-SESSION-OPS>
```

---

## 8.5. Docker rebuild + smoke

```text
From the repo root, rebuild and start the backend stack for manual verification:

1. docker compose build session-operations-service
2. docker compose up -d session-operations-service
3. Run the reconnect/admission smoke path against the implemented endpoint and/or hub flow
4. Confirm the happy path restores the participant's live team/session context
5. Confirm a forbidden late-join path is rejected
```

**Gate:** the reconnect/admission path is reachable in the running stack and the observed behavior matches the implemented runtime rules.

---

## 9. Mobile slice

```text
Generate a multi phase plan in a markdown file, like the one in
@mobile/plans/hu-03-frontend-role-permission-assignment.md, save it in
@mobile/plans/ for the following:
Use @mobile/AGENTS.md.

Implement the participant mobile reconnect flow for HU-07B using the verified
session-operations reconnect/admission contract.

The backend exposes the reconnect flow through a **SignalR hub** at `/hubs/sessions`,
authenticated via trusted headers (X-User-Id, X-User-Role, X-User-Email). The mobile
client must install the SignalR client SDK first:

    npx expo install @microsoft/signalr

Then use it to:
1. Connect to the hub (LongPolling or WebSocket transport)
2. Invoke `ReconnectAsync(liveSessionId, { TeamId, DisplayName, TeamCapacity, Token })`
3. On success, the server adds the connection to `live-session:{id}`, `team:{id}`,
   `participant:{id}` groups and returns a `ReconnectParticipantResultDto` with
   `LiveSessionId`, `TeamId`, `SessionParticipantId`, `TeamDisplayName`, `IsReconnect`,
   and `SessionState`

Scope:
- preserve the current HU-07A flow: session code -> team lobby -> team selection
- when the participant loses connection or resumes the app, open a SignalR connection
  to `/hubs/sessions` and invoke `ReconnectAsync` instead of replaying the whole
  join flow blindly
- restore the participant into the correct live team/session context when the backend
  allows it, using the group membership and response DTO to drive UI state
- surface the denied late-join / invalid-session-state / lost-access cases clearly
  (the backend throws `HubException` on rejection)

Gate:
- mobile uses the verified backend contract from HU-07B (SignalR hub, `ReconnectAsync`
  method, `ReconnectParticipantResultDto` response shape)
- reconnect resumes the participant into the expected live view when allowed
- denied reconnect states are rendered explicitly and do not silently fall back to a
  broken live screen
- HubException messages are surfaced or mapped to user-facing error states
```

Commit:

```text
feat(frontend): authorized participant reconnection — HU-07B

Ref: HU-07B
Ref: <DES-HU07B>
Ref: <DES-PRD-SESSION-OPS>
```

---

## 10. Close-out

```text
Before opening the PR:
- confirm all four backend phase commits exist
- confirm the reconnect/admission smoke path was exercised
- confirm the frontend/mobile flow uses the verified HU-07B backend contract
- summarize any remaining follow-up explicitly, especially if Identity needs a small
  token-consumption addition

Then open the PR with the resolved HU id and PRD ref.
```

## Rationale

**HU-07B is a boundary-enforcement slice as much as a feature slice.** The local docs are already clear that Identity supplies access facts while `SessionOperations` owns final admission, late join, reconnect, capacity, and runtime restoration. This skeleton therefore treats HU-07A as the current baseline flow and scopes HU-07B as a full slice in `session-operations-service`. If later design work proves that explicit join-token consumption must be exposed from Identity, that should be a small follow-up, not a reason to move the owning slice back into `identity-access-service`.

# HU-07B Context — Authorized Participant Reconnection

> Paste this section into any agent session that needs context for HU-07B.
> Last updated: 2026-06-03 | Branch: `feature/hu-07b-participant-reconnection`
>
> Scope correction note: HU-07B should be driven as a `session-operations-service`
> slice, not as a second full slice in `identity-access-service`. The current
> participant flow after HU-07A is: participant enters with a session code, the
> client loads the session-scoped team lobby, the participant selects a team, and
> Identity returns access facts. Final admission, reconnect, late-join policy,
> capacity, presence recovery, and restoration of live team state belong to
> `SessionOperations`.

## State

- `<DES-HU07B>` (HU-07B): labels expected `Feature`, `ready-for-agent`, `svc:session-operations-service` — verify in Linear before driving
- HU-07A predecessor: merged to `develop`; use its landed flow as the baseline
- Identity PRD reference already landed for the access-fact side: `DES-67`
- Session-operations PRD reference for HU-07B: **resolve via Linear and local `backend/docs/prd/` file before implementation**; do not assume `DES-67` is the owning PRD for this slice
- Branch: `feature/hu-07b-participant-reconnection`, base = **`develop`**

## Required design patterns

| Pattern | Owning phase | Why (from patterns matrix) | Concrete obligation |
|---|---|---|---|
| `Proxy` (mandated) | X.2 Application + X.4 API | "Authorized reconnection restores live team state over the real-time channel." | Admission and reconnect authorization must be enforced through a guard — `AuthorizationBehaviour`, an authorization proxy, or endpoint/hub policy — with **no ad-hoc role/identity `if` checks** in handlers, endpoints, or hub methods. |

Transport note (NOT a pattern, NOT a gate): the matrix tags HU-07B with **SignalR / WebSockets**. Unlike HU-07A, this slice lives in `session-operations-service`, where a hub and runtime presence flow are in-bounds. Identity still does **not** own the hub or final admission decision.

## What predecessors have already landed (reuse candidates for HU-07B)

All of this is on `develop`.

**Identity / access baseline from HU-01 through HU-07A**
- Participant authentication and trusted-header identity flow are already in place
- `POST /api/permissions/participant-membership-access` already exists as the Identity-owned access-fact contract
- HU-07A established the participant session-code -> team-lobby -> team-selection flow
- HU-07A intentionally kept `JoinToken.Consume()` out of the API; validation is read-only and non-consuming
- Identity does not own final admission, reconnect, or live runtime restoration

**Session-operations baseline**
- `SessionOperations` owns `LiveSession`, `SessionParticipant`, `JoinContext`, team assignment, capacity, and final admission decisions
- `JoinPolicy` is the domain policy that governs participant access, team membership, late join, and reconnection rules
- `LiveSession` lifecycle and runtime constraints already belong here, along with SignalR/hub concerns for real-time runtime coordination

## What HU-07B adds

| Concern | New work |
|---|---|
| Runtime admission vs reconnect model | Add or extend the `SessionOperations` model so authorized reconnect is distinct from forbidden late join, with explicit handling for presence recovery and restoration of the participant's team/session runtime context. |
| `JoinPolicy` reconnection rules | Extend policy rules to decide whether a disconnected participant may re-enter, whether a new late join is closed, and how team/capacity/session-state constraints apply. |
| `ReconnectAuthenticatedParticipant` use case | Add the application use case named in the DDD/PRD docs. It should consume Identity access facts, check session-owned runtime rules, and resume presence in the correct live team context. |
| Runtime participation persistence | Persist enough participant/runtime state to recognize a reconnecting participant and restore their live session/team context. If backing storage or projections are required for hub/group recovery, they land here. |
| Reconnect/admission API or hub path | Expose the path the mobile client uses to resume the live session, guarded through the service's proxy/authorization approach rather than inline checks. |
| Optional Identity follow-up only if forced by design | If reconnect truly requires explicit join-token consumption, treat it as a small Identity follow-up. Do not re-scope HU-07B into a second Identity full slice by default. |

## Touched surfaces

- `backend/` `session-operations-service` — domain, application, infrastructure, and API layers
- API/runtime contract boundary between `session-operations-service` and the mobile client for reconnect/admission
- Cross-service dependency on `identity-access-service` via `POST /api/permissions/participant-membership-access`
- Possible SignalR hub/group recovery behavior in `session-operations-service`

## Committed phases

| Commit | Phase | Description |
|---|---|---|
| — | — | No commits yet |

## Known quirks / gotchas

- **Do not regress the current HU-07A flow.** The baseline is session code -> session team lobby -> team selection. Do not rewrite HU-07B as "participant presents a join token first" unless the product decision changes and the docs are updated together.
- **Identity returns access facts; it does not admit.** `POST /api/permissions/participant-membership-access` is an input to HU-07B, not the final decision.
- **Session ownership is the main contrast to enforce.** Late join, reconnect, capacity, assignment, presence recovery, and live-state constraints belong to `SessionOperations`, not `Identity`.
- **Do not hardcode `DES-67` as the owning PRD ref in HU-07B phase commits.** Resolve the session-operations PRD ticket and local PRD file first; use `DES-67` only as supporting context for the Identity side if needed.
- **Proxy must be structural.** Authorization cannot live as scattered `if` checks in hub methods, handlers, or endpoints.
- **SignalR may require durable recovery support.** If hub/group membership is ephemeral, HU-07B may need persistence or projection support to restore runtime context after reconnect.
- **Join-token consumption is optional follow-up, not assumed scope.** HU-07A explicitly left token consumption out of the API to avoid burning the token during validation; only add Identity follow-up work if HU-07B's final design genuinely requires it.

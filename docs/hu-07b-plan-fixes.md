# Plan — Resolve HU-07B mobile-plan/contract gaps

Fixes the four findings from validating `mobile/plans/hu-07b-participant-reconnection.md`
against the backend on `feature/hu-07b-participant-reconnection`. One phase per finding,
each using the best-option fix agreed in review.

- **F1** — `TeamCapacity` does not exist in the contract → remove from the client.
- **F2** — `HubException` rejection reasons are not distinguishable in production →
  add stable backend error codes (mirrors the REST `ProblemDetailsExceptionHandler`).
- **F3** — the outcome union misses two real reconnect rejections → add them (depends on F2).
- **F4** — the LongPolling-fallback auth claim is inaccurate → correct the plan text.

Order: **F1 → F4** are pure plan edits (do first, cheap). **F2** is the only code change
and the one that unblocks the granular denied-state UX; **F3** rides on it.

---

## Phase 1 — F1: Remove `TeamCapacity` from the mobile client

**Why:** `Team.Capacity` (`backend/.../Domain/Entities/Team.cs:47`) is server-owned and
enforced in `JoinPolicy.EnsureCanJoin` (`team.ActiveMemberCount >= team.Capacity`). The
hub/REST request records are `(Guid TeamId, string DisplayName, string? Token)` —
`SessionsHub.cs:49-52`, `SessionsEndpoints.cs:34-37` — and the validator
(`ReconnectAuthenticatedParticipantCommandValidator.cs`) has **no** capacity rule. The
client must never source, default, persist, or send capacity.

Scope: documentation only (no app code exists yet).

- [ ] **Phase 0** of the mobile plan: delete the "resolve `TeamCapacity` source" bullet;
      remove `"teamCapacity":4` from the REST curl example.
- [ ] **Phase 1** of the mobile plan: request/DTO types and the reconnect-context store
      become `{ liveSessionId, teamId, displayName, token? }` (drop `teamCapacity`).
- [ ] Update the hub-method signature snippet (lines ~37-40) and the result-DTO notes to
      match the real records — no `TeamCapacity` field.
- [ ] **Key Risk #3:** delete the capacity half; keep only the `Token` (defaults to
      `null`) part.

**Exit:** no occurrence of `TeamCapacity`/`teamCapacity` remains in the mobile plan; the
documented request shape matches `ReconnectParticipantHubRequest` exactly.

---

## Phase 2 — F4: Correct the LongPolling fallback claim

**Why:** `WebSocketTokenExtractionTransform.OnMessageReceived`
(`backend/api-gateway/src/Transforms/WebSocketTokenExtractionTransform.cs:9-10`) only
copies `?access_token=` into the token **when the request carries `Upgrade: websocket`**.
LongPolling/SSE are not WS upgrades, so they do not use the query extraction — they
authenticate via the `Authorization` header on their HTTP requests instead.

Scope: documentation only.

- [ ] In the mobile plan's "Mobile conventions" and **Key Risk #4**, reword: WS uses
      `accessTokenFactory` → `?access_token=` (extracted by the transform); LongPolling,
      if used, authenticates via the `Authorization` header `@microsoft/signalr` sets on
      its HTTP requests. Drop the phrase "gateway supports both via the same
      `access_token` extraction."
- [ ] Decide explicitly: keep LongPolling as a header-auth fallback, or drop it. If kept,
      add a Phase 0 smoke step that confirms a LongPolling handshake authenticates through
      the gateway (it is **not** covered by the WS transform).

**Exit:** the plan no longer claims the query-string extraction covers LongPolling; the
fallback's real auth path is stated correctly.

---

## Phase 3 — F2: Surface stable reconnect rejection codes over the hub (backend)

**Why:** This is the only finding that blocks a stated goal — explicit, non-broken denied
states. Today the hub throws plain domain exceptions and `EnableDetailedErrors` is on only
in Development (`backend/.../Api/DependencyInjection.cs:19`). In production SignalR strips
the message and the client sees one generic *"An unexpected error occurred invoking
'ReconnectAsync'."* for **every** rejection — `forbidden-late-join`,
`invalid-session-state`, and `lost-access` become indistinguishable. `HubException`
messages, by contrast, are always forwarded to clients. So we wrap domain/validation
failures into `HubException` carrying a machine-readable **code** — the hub analogue of
the existing `ProblemDetailsExceptionHandler` (`Api/Services/ProblemDetailsExceptionHandler.cs:37-52`).

Scope: backend (session-operations-service) + mobile policy. This was "out of scope" in
the original mobile plan; it needs a small session-ops ticket.

### Backend

- [ ] Add a `IHubFilter` (e.g. `Api/Hubs/DomainExceptionHubFilter.cs`) registered via
      `AddSignalR(...).AddHubFilter<DomainExceptionHubFilter>()`. In `InvokeMethodAsync`,
      catch and rethrow as `HubException` with a stable code prefix, reusing the same
      exception → meaning mapping as `ProblemDetailsExceptionHandler`:
  | Domain exception | Code |
  |---|---|
  | `LateJoinNotAllowedException` | `LATE_JOIN_NOT_ALLOWED` |
  | `ForbiddenAccessException` | `FORBIDDEN` |
  | `ParticipantRemovedFromSessionException` | `PARTICIPANT_REMOVED` |
  | `ParticipantAssignedToDifferentTeamException` | `WRONG_TEAM` |
  | `ParticipantAlreadyConnectedException` | `ALREADY_CONNECTED` |
  | `TeamCapacityReachedException` / `TeamJoinClosedException` | `TEAM_UNAVAILABLE` |
  | `ValidationException` | `VALIDATION_FAILED` |
  | `NotFoundException` / `TeamNotFoundException` | `NOT_FOUND` |
  | `UnauthorizedAccessException` | `UNAUTHORIZED` |
  | (default) | `ERROR` |
- [ ] Emit a parseable, stable shape in the `HubException` message (e.g. a `code:` prefix
      or small JSON `{ "code": "..." }`) so the client keys on the **code**, never English
      prose. Keep it identical across environments (do not rely on `EnableDetailedErrors`).
- [ ] Tests: a hub integration/unit test per code asserting the rejection surfaces the
      expected code (extend `tests/IntegrationTests/Api/ReconnectParticipantHubTests.cs`).

### Mobile (Phase 0 + Phase 2 of the mobile plan)

- [ ] Phase 0: capture the real `HubException` code shape in the known-good transcript and
      pin the **code → outcome** mapping (not message text).
- [ ] Phase 2 `reconnect-policy.ts` `interpretHubError`: parse the code and map to the
      union. Default unknown code → `error`; connection/negotiate failure →
      `network-error`; 401 on negotiate → `unauthorized`.
- [ ] Update mobile **Key Risk #2** to reflect that rejection reasons arrive as a stable
      *code* (not a free-text message that only exists in dev).

**Exit:** each rejection reaches the client as a distinct, environment-stable code; the
mobile policy maps code → outcome with a safe `error` default; hub tests are green.

---

## Phase 4 — F3: Complete the outcome union (mobile)

**Why:** `JoinPolicy.EnsureCanReconnect` (`Domain/Services/JoinPolicy.cs:41-52`) rejects
two cases the mobile union omits: **already connected elsewhere**
(`ParticipantAlreadyConnectedException`) and **assigned to a different team**
(`ParticipantAssignedToDifferentTeamException`). Without slots they fall through to generic
`error`. This phase depends on Phase 3 — without codes there is nothing to map to.

Scope: mobile plan + policy.

- [ ] Extend `ReconnectOutcome` with `{ kind: 'already-connected' }` and
      `{ kind: 'wrong-team' }` (or fold `wrong-team` into `lost-access` if the UX prefers).
- [ ] Map `ALREADY_CONNECTED` and `WRONG_TEAM` codes (from Phase 3) in `interpretHubError`.
- [ ] Phase 3 UI: add explicit copy + a clear next step for each (e.g. already-connected →
      "You're already connected on another device"; wrong-team → back to lobby). Never a
      dead end.
- [ ] Add the two scenarios to the Phase 4 manual test matrix and the policy unit tests.

**Exit:** every reconnect rejection the backend can throw has a corresponding explicit
outcome + screen; no real rejection silently lands on the generic `error` state.

---

## Sequencing & ownership

1. **F1 (Phase 1)** and **F4 (Phase 2)** — plan-only, do immediately, no dependencies.
2. **F2 (Phase 3)** — backend ticket against session-operations-service; the long pole.
   Blocks F3 and the granular mobile denied-state UX.
3. **F3 (Phase 4)** — mobile, after F2 lands.

If F2's backend change is rejected, fall back to a **coarse** mobile union
(`reconnected` / `unauthorized` / `network-error` / `denied`), drop the granular screens
from the mobile plan, and skip F3 — but that abandons the plan's explicit denied-state
goal, so it is the fallback, not the recommendation.

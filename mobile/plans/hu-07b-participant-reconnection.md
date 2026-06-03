# Plan — HU-07B: Authorized Participant Reconnection (Expo / React Native)

Source: `backend/docs/prompt_example_feature_hu07b.md` (Step 9 — mobile slice)
PRD ref: `<DES-PRD-SESSION-OPS>` (resolve from Linear)
Branch: `feature/hu-07b-participant-reconnection`

## Goal

Extend the landed HU-07A participant flow with **authorized reconnection** over the
session-operations **SignalR hub**. When a participant loses connectivity or resumes
the app, the client opens a hub connection and invokes `ReconnectAsync` instead of
blindly replaying the session-code → lobby → team-selection flow. On success the
server restores the participant into their live team/session context (group
membership + a result DTO); on rejection it throws `HubException`, which the client
maps to explicit, non-broken denied states.

The mobile client only **consumes** the verified backend contract. Final admission,
late-join vs. reconnect rules, capacity, and runtime restoration are owned by
session-operations — the client invents **no** access rules of its own (mirrors the
HU-07A `membership-policy` / `access-policy` discipline).

All traffic goes through the **api-gateway** (port `8000`) — never directly to a
service. The hub is reached at `${apiBaseUrl()}/hubs/sessions`.

---

## What already exists (verified)

### Backend HU-07B contract — already implemented and reachable

- **SignalR hub**: `app.MapHub<SessionsHub>("/hubs/sessions")` in
  `session-operations-service/src/Api/Program.cs`, guarded by
  `[Authorize(Policy = Participant)]` (structural Proxy/policy gate — no inline role
  checks; the client must not add its own).
- **Hub method** (`Api/Hubs/SessionsHub.cs`):
  ```
  Task<ReconnectParticipantResultDto> ReconnectAsync(
      Guid liveSessionId, ReconnectParticipantHubRequest { Guid TeamId, string DisplayName, string? Token })
  ```
  On success the server adds the connection to groups
  `live-session:{id:D}`, `team:{id:D}`, `participant:{id:D}` and returns the DTO.
  On rejection it throws **`HubException`** (FluentValidation / domain / authorization
  failures surface as a hub error, not a status code).
- **Result DTO** (`Application/Sessions/DTOs/ReconnectParticipantResultDto.cs`):
  ```jsonc
  {
    "liveSessionId":          "guid",
    "teamId":                 "guid",
    "teamDisplayName":        "string",
    "sessionParticipantId":   "guid",
    "participantDisplayName": "string",
    "sessionState":           "string",   // live-session lifecycle state
    "isReconnect":            true,        // true = resumed, false = first admission
    "joinedAt":               "ISO-8601",
    "lastSeenAt":             "ISO-8601"
  }
  ```
- **REST fallback** (same command, same DTO): `POST /api/sessions/{liveSessionId:guid}/participants/reconnect`
  in `Api/Endpoints/SessionsEndpoints.cs`. Useful as a transport-agnostic smoke
  check and as a non-realtime fallback if hub negotiation fails on a device.
- **Validation** (`ReconnectAuthenticatedParticipantCommandValidator`): `LiveSessionId`,
  `TeamId` non-empty; `DisplayName` non-empty. Violations →
  `HubException` over the hub / `400` over REST.

### Gateway + auth wiring — already supports browser/RN SignalR

- Gateway route `session-ops-hubs`: `Path: /hubs/{**catch-all}` → `session-ops`
  cluster, `AuthorizationPolicy: "default"` (validates the Keycloak JWT).
- `WebSocketTokenExtractionTransform.OnMessageReceived` copies the SignalR
  `?access_token=` query param into the token **only when the request carries
  `Upgrade: websocket`** — so the WS handshake (which can't set an `Authorization`
  header) authenticates correctly through the gateway. **This means
  `@microsoft/signalr`'s `accessTokenFactory` is the supported auth path for WS.**
  LongPolling/SSE are not WS upgrades and are not covered by this transform; they
  authenticate via the `Authorization` header on their HTTP requests instead.
- Gateway strips `Authorization` and injects trusted headers `X-User-Id`,
  `X-User-Role`, `X-User-Email`. The service authenticates the hub via the
  `TrustedHeaders` scheme + `RequireRole("Participant")` — the client just sends the
  same Keycloak access token it already stores.

### Mobile HU-07A baseline (the flow we extend, do not recreate)

- `app/(app)/index.tsx` → "Join your session" → `app/(app)/join.tsx` (6-char alnum
  session code) → `app/(app)/team-lobby.tsx` (lists `/api/sessions/{code}/teams`,
  joins via `/api/teams/{id}/participants/self`, validates membership access) →
  `app/(app)/team-space.tsx`.
- `team-space.tsx` is **today a placeholder** and already documents the boundary:
  *"the real-time hub connection, group join, reconnection, and shared-state sync
  belong to session-operations (HU-07B)."* — that is exactly this slice. It currently
  receives `liveSessionId`, `teamId`, `reason` as route params.
- Conventions to mirror:
  - **Networking:** `expo/fetch` via `src/lib/api/client.ts`; `ApiError(status, code, message)`;
    token from `src/lib/auth/token-store.ts` (`getAccessToken`).
  - **Host:** `src/lib/host.ts` derives the dev LAN host → gateway `:8000`,
    Keycloak `:8080`. Reuse `apiBaseUrl()` for the hub URL.
  - **Hook + policy split:** `lib/<domain>/use-*.ts` returns
    `{ status, outcome, <action>, reset }` with a discriminated-union outcome; a
    sibling `*-policy.ts` turns transport/results into the union (no rules in the UI).
  - **Tokens:** `expo-secure-store` only; never log tokens.
  - **Files:** kebab-case; routes only in `app/`; `@/*` alias; tests in
    `src/__tests__/*.test.ts` (jest-expo).
  - **Theme/UI:** `@/constants/theme` tokens + `@/components/ui/*` primitives,
    inline styles, light mode only.

---

## Mobile conventions (SDK 56 — read first)

- Read the versioned docs at `https://docs.expo.dev/versions/v56.0.0/` before coding —
  SDK 56 / RN 0.85 / expo-router 56 differ from training data.
- Install the SignalR SDK with the Expo-aware installer (pins a compatible version):
  ```
  npx expo install @microsoft/signalr
  ```
- **Transport:** RN ships a global `WebSocket`, so prefer `WebSockets` transport with
  `accessTokenFactory` → the gateway's `WebSocketTokenExtractionTransform` lifts the
  `?access_token=` query param into the token (it only fires on an `Upgrade: websocket`
  request). `LongPolling`/SSE are **not** WS upgrades, so they are not covered by that
  transform — they authenticate via the `Authorization` header `@microsoft/signalr`
  sets on their HTTP requests. If LongPolling is kept as a fallback, add a Phase 0
  smoke step confirming a LongPolling handshake authenticates through the gateway. Do
  **not** force `skipNegotiation` through the gateway.

---

## Phase 0 — Contract spike & de-risk (no app UI yet)

Prove the hub round-trips and resolve the open contract inputs **before** writing
screens. Several `ReconnectAsync` arguments are not currently produced by the HU-07A
client and must be sourced or defaulted.

- [ ] Bring up the stack (`docker compose up` in `backend/`) and rebuild
      `session-operations-service` per Step 8.5 so the hub + endpoint are live.
- [ ] Get a participant token via direct grant (same as HU-06) and smoke the
      **REST** reconnect first (simplest transport, same command):
      ```bash
      curl -s -X POST "$GW/api/sessions/$LSID/participants/reconnect" \
        -H "Authorization: Bearer $TOKEN" -H 'Content-Type: application/json' \
        -d '{"teamId":"...","displayName":"participant","token":null}' | jq
      ```
      Confirm the happy path returns the DTO and a forbidden late-join / wrong-team /
      invalid-state attempt is rejected.
- [ ] Smoke the **hub** through the gateway with `access_token` in the query string to
      confirm WS negotiation + auth works end-to-end (a tiny node `@microsoft/signalr`
      script against `${GW}/hubs/sessions?access_token=$TOKEN`, invoking
      `ReconnectAsync`). Capture a known-good transcript and the `HubException`
      `{ "code": ... }` shape on rejection, and pin the **code → outcome** mapping (see
      Key Risk #2) — not message prose.
- [ ] **RESOLVE open contract inputs** (document the decision inline, like HU-06 did):
  - `DisplayName` → from the auth profile (`useAuth().profile.displayName`).
  - `LiveSessionId` / `TeamId` → from the live team context the participant last
    held (route params today; must be persisted for resume — see Phase 1).
  - `Token` → the join token. HU-07A deliberately did **not** wire token consumption,
    and the prompt flags Identity token-consumption as a possible follow-up. Default
    to `null` unless the spike shows the backend requires it; note it as a close-out
    follow-up if reconnect depends on it.
- [ ] Decide the **reconnect trigger** policy: (a) on app foreground/resume
      (`AppState`), (b) on hub connection drop (`onreconnecting`/`onclose`), (c) on
      manual "Rejoin" action. Confirm which apply.

**Exit:** a known-good transcript for both REST and hub reconnect; every
`ReconnectAsync` argument has a resolved client source; transport + trigger decisions
recorded.

---

## Phase 1 — Realtime connection + reconnect context (deep module, no UI)

Encapsulate SignalR and the persisted live context so screens stay thin.

- [ ] **Hub URL helper** — add `hubBaseUrl()` (or reuse `apiBaseUrl()`) in
      `src/lib/host.ts`; the hub lives at `${apiBaseUrl()}/hubs/sessions`.
- [ ] **Connection factory** (`src/lib/realtime/sessions-hub.ts`):
  - Build a `HubConnection` with `withUrl(url, { accessTokenFactory: getAccessToken, transport: WebSockets })`
    and `withAutomaticReconnect()`.
  - Expose `start()`, `stop()`, and a typed `reconnect(liveSessionId, request)` that
    calls `invoke('ReconnectAsync', liveSessionId, request)` and returns the DTO.
  - Centralize transport/keepalive config; never log the token.
- [ ] **DTO + request types** (`src/lib/realtime/sessions-hub-types.ts`): mirror
      `ReconnectParticipantResultDto` and the `ReconnectParticipantHubRequest`
      (`teamId`, `displayName`, `token?`) exactly.
- [ ] **Reconnect context store** (`src/lib/realtime/reconnect-context.ts`):
  - Persist the minimal data needed to resume — `{ liveSessionId, teamId, displayName,
    token? }` — in `expo-secure-store` (set on a successful join/admission, cleared on
    sign-out / explicit leave).
  - `saveReconnectContext`, `loadReconnectContext`, `clearReconnectContext`. This is
    what makes "resume the app and rejoin" possible without replaying the lobby.
- [ ] Write the team-space landing to call `saveReconnectContext` so a later resume has
      the inputs (wire-in happens in Phase 3; the store + call site land here).

**Exit:** `tsc --noEmit` clean; the factory compiles and the context store round-trips
in a unit test (save → load → clear).

---

## Phase 2 — Reconnect hook + outcome policy (testable, no UI)

Mirror `use-team-join` / `membership-policy`: a hook that drives state and a pure
policy that maps results/`HubException` to a discriminated union the UI renders.

- [ ] **Policy** (`src/lib/realtime/reconnect-policy.ts`):
  - `ReconnectOutcome` union:
    `{ kind: 'reconnected'; result: ReconnectParticipantResultDto }`
    | `{ kind: 'forbidden-late-join' }` | `{ kind: 'invalid-session-state' }`
    | `{ kind: 'lost-access' }` | `{ kind: 'already-connected' }`
    | `{ kind: 'wrong-team' }` | `{ kind: 'unauthorized' }`
    | `{ kind: 'network-error' }` | `{ kind: 'error' }`.
  - `interpretHubError(error)` extracts the backend **code** from the `HubException`
    message and maps it to the union (see Key Risk #2 for the code shape). Mapping:
    `LATE_JOIN_NOT_ALLOWED` → `forbidden-late-join`; `TEAM_UNAVAILABLE` →
    `invalid-session-state`; `FORBIDDEN` / `PARTICIPANT_REMOVED` / `NOT_FOUND` →
    `lost-access`; `ALREADY_CONNECTED` → `already-connected`; `WRONG_TEAM` →
    `wrong-team`; `UNAUTHORIZED` → `unauthorized`; `VALIDATION_FAILED` / `ERROR` /
    unknown → `error`. Transport failures (no code) classify first: 401 on negotiate /
    token rejected → `unauthorized`; connection/negotiate failure → `network-error`.
  - Keep **no access rules** here — only translate backend codes to UI vocabulary.
- [ ] **Hook** (`src/lib/realtime/use-reconnect.ts`):
  - State: `idle | connecting | reconnecting | reconnected | denied | error`.
  - `reconnect(context)` orchestrates: `start()` connection → `reconnect(...)` invoke →
    on success update reconnect context (`lastSeenAt`) and return
    `{ kind: 'reconnected', result }`; on throw → `interpretHubError`.
  - `reset()`; clean teardown (`stop()`) on unmount / sign-out.
  - On `unauthorized`, call `useAuth().signOut()` (same pattern as the lobby).
- [ ] **Unit tests** (`src/__tests__/reconnect-policy.test.ts`,
      `reconnect-hook.test.ts`): allowed reconnect; forbidden late-join; invalid
      session state; lost-access; unauthorized → sign-out; network error. Mock the
      hub connection (no real socket in jest).

**Exit:** `jest` green for all reconnect outcome branches; the hook never leaks a raw
`HubException` to the UI.

---

## Phase 3 — UI wiring: live team-space, resume trigger, denied states

Turn `team-space` from an HU-07A placeholder into the live reconnect surface and add
the resume entry points decided in Phase 0. **Preserve the HU-07A join flow.**

- [ ] **Live team-space** (`app/(app)/team-space.tsx`):
  - On mount, if a reconnect context exists, run `useReconnect().reconnect(context)`.
  - `reconnecting` → loading state ("Restoring your team space…").
  - `reconnected` → render the restored live context from the DTO: `teamDisplayName`,
    `sessionState`, and an `isReconnect` indicator (resumed vs. first admission).
    Replace the "coming soon" copy.
  - Keep the existing access-confirmed framing but drive it from the DTO, not just
    route params.
- [ ] **Denied / invalid states** — render each `ReconnectOutcome` explicitly; do **not**
      silently fall back to a broken live screen:
  - `forbidden-late-join`: "The session has moved on — late join isn't allowed."
  - `invalid-session-state`: "This session isn't accepting participants right now."
  - `lost-access`: "You no longer have access to this team." → offer return to lobby.
  - `already-connected`: "You're already connected on another device." → back to home.
  - `wrong-team`: "You're assigned to a different team — head back to the lobby to
    rejoin." → back to lobby.
  - `network-error`: retry affordance (reuse the lobby's "Try again" pattern).
  - `unauthorized`: sign out (handled in the hook).
  - Each denied state offers a clear next step (back to `index` / `join`), never a dead
    end. Surface the mapped message via a `selectable` error region.
- [ ] **Resume on foreground** (`app/_layout.tsx` or an app-scoped effect): on
      `AppState` → `active` while authenticated, if a reconnect context exists, route
      to `team-space` (or trigger reconnect) per Phase 0's policy. Avoid replaying the
      whole join flow.
- [ ] **Connection-drop handling:** wire the connection's `onreconnecting`/`onreconnected`
      to a lightweight banner so a transient drop shows "Reconnecting…" rather than
      kicking the user out.
- [ ] **Sign-out / leave:** clear the reconnect context and stop the connection so a
      different user doesn't inherit stale live context.

**Exit:** on-device, a participant who reached a team space and backgrounds/kills the
app is restored into the live team view on resume; a forbidden/invalid reconnect shows
the correct explicit state; the HU-07A first-join path is unchanged.

---

## Phase 4 — States, polish & manual verification

- [ ] Loading / reconnecting / denied / error states polished; haptics on
      success/error (reuse the `fireHaptic` pattern from the lobby).
- [ ] Network-failure handling (gateway/hub unreachable) with retry; transient WS drop
      shows the reconnecting banner, not a hard failure.
- [ ] Confirm no token is logged anywhere (connection factory, hook, policy).
- [ ] Manual test matrix:
  | Scenario | Expected |
  |----------|----------|
  | Joined participant resumes app, session still live | restored into live team-space (`isReconnect: true`) |
  | First admission via hub | live team-space (`isReconnect: false`) |
  | Session moved past join window | `forbidden-late-join` state |
  | Session in non-joinable state | `invalid-session-state` state |
  | Participant lost team access | `lost-access` state → back to lobby |
  | Participant already connected elsewhere | `already-connected` state → back to home |
  | Reconnect with a mismatched team | `wrong-team` state → back to lobby |
  | Token expired / rejected on negotiate | sign-out |
  | Gateway/hub down | network error + retry |
  | Transient WS drop | "Reconnecting…" banner, auto-recovers |
- [ ] Update `mobile/README.md`: SDK install (`@microsoft/signalr`), hub URL, and how
      reconnect resume works.

---

## Out of scope

- Backend hub/command implementation (landed in session-operations X.1–X.4).
- Identity join-token **consumption** — if reconnect proves to require a live `Token`,
  raise it as the close-out follow-up the prompt anticipates; do not implement it here.
- Multi-device team sync, live scoreboard, full in-session game UX beyond restoring the
  team/session context.
- Any client-side access/admission rules — the hub's `[Authorize]` policy + command
  are the only authority (Proxy gate).

## Key risks

1. **WS auth through the gateway** — Mitigated/verified: the gateway already extracts
   `?access_token` (`WebSocketTokenExtractionTransform`), so `accessTokenFactory` is the
   supported path. Don't set an `Authorization` header on the WS handshake; don't
   `skipNegotiation`.
2. **`HubException` carries a stable code, not free-text prose** — the backend's
   `DomainExceptionHubFilter` (session-ops) wraps domain/validation failures into a
   `HubException` whose message is `{"code":"...","message":"..."}`. The code is
   identical across environments; the English prose is not (it is only the bare
   `HubException` message in production, and SignalR prepends a wrapper when
   `EnableDetailedErrors` is on in dev). So `interpretHubError` keys on the **code**
   (extracted from the message), never the prose, and defaults an unknown/absent code to
   a generic `error` so the screen never silently breaks. Pin the code→outcome mapping in
   Phase 0 against the real backend.
3. **Missing client input (`Token`)** — not produced by the HU-07A flow. Resolve the
   source in Phase 0. `Token` defaults to `null` pending the Identity follow-up.
4. **`@microsoft/signalr` in RN/Expo Go** — verify WS transport works in Expo Go on the
   target device early (Phase 0 hub smoke from a device, not just node). LongPolling is
   the documented fallback if a device blocks WS through the gateway — but it
   authenticates via the `Authorization` header `@microsoft/signalr` sets on its HTTP
   requests, **not** the `?access_token=` extraction (that transform only fires on a WS
   upgrade). If LongPolling is kept, smoke a LongPolling handshake through the gateway in
   Phase 0; otherwise drop it explicitly.

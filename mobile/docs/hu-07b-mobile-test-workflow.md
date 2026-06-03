# HU-07B — Participant Reconnection: Phase 0 Workflow

Branch: `feature/hu-07b-participant-reconnection`
App: `mobile/` (Expo / React Native)
Backend: `docker compose up` in `backend/`

This document is the phase 0 contract spike for HU-07B. It does three things:

1. proves the reconnect contract against the **gateway** first over REST, then over
   the **SignalR hub**
2. records the client-side source for each `ReconnectAsync` argument
3. locks the transport and trigger decisions that later phases will implement

All traffic goes through `http://<host>:8000`. Do not call services directly.

## Phase 0 artifacts

- Hub probe script: `npm run smoke:reconnect:hub`
- SignalR dependency: `@microsoft/signalr@10.0.0` installed with `npx expo install`

## Resolved client inputs

These are resolved from the current mobile codebase and should not be re-decided in
screen code later.

| Hub argument | Mobile source | Evidence |
|---|---|---|
| `displayName` | `useAuth().profile.displayName` | `src/lib/auth/auth-context.tsx` hydrates `profile` from `GET /api/users/me` and already uses `profile.displayName` as the authenticated participant display name. |
| `liveSessionId` | current route params after the HU-07A join flow; persist this for resume in Phase 1 | `src/app/(app)/team-lobby.tsx` routes to `/(app)/team-space` with `liveSessionId`. |
| `teamId` | current route params after the HU-07A join flow; persist this for resume in Phase 1 | `src/app/(app)/team-lobby.tsx` routes to `/(app)/team-space` with `teamId`. |
| `token` | default to `null` for reconnect unless the backend spike proves it is required | HU-07A intentionally left token consumption unwired; the reconnect validator accepts `null`. |

## Transport decision

- Primary transport: `WebSockets`
- Authentication path: SignalR `accessTokenFactory`
- Fallback transport for diagnostics only: `LongPolling`
- Do not force `skipNegotiation` through the gateway

The gateway already supports SignalR `?access_token=` extraction for WS/SSE handshakes.
The mobile app should reuse that path instead of inventing header-based auth.

## Trigger decision

Phase 3 should implement all three triggers, with different responsibilities:

- App foreground/resume (`AppState` -> `active`): attempt reconnect when stored live
  context exists.
- Hub drop (`onreconnecting` / `onclose`): keep the participant in the team-space
  surface and show a reconnecting state instead of kicking them out.
- Manual retry / rejoin action: exposed on denied/network-error states as the last
  recovery path.

This keeps resume behavior automatic without making transient transport failures look
like access loss.

## Prerequisites

1. Start the backend stack:

```bash
cd backend
docker compose up -d
```

2. Install mobile dependencies if needed:

```bash
cd ../mobile
npm install
```

## Step 1 — Get a participant token

Use the same direct-grant flow as HU-06 / HU-07A:

```bash
GW=http://localhost:8000
KC=http://localhost:8080

token_for() {  # token_for <username> <password>
  curl -s -X POST "$KC/realms/umbral/protocol/openid-connect/token" \
    -d grant_type=password -d client_id=umbral-mobile \
    -d username="$1" -d password="$2" -d 'scope=openid profile email' \
    | python3 -c "import sys,json; print(json.load(sys.stdin)['access_token'])"
}

PARTICIPANT_TOKEN=$(token_for participant participant123)
```

## Step 2 — Prepare a reconnectable session

The reconnect surface needs an existing live session plus a participant who already
belongs to the target team. Use the same team-assignment prep as HU-07A, then seed a
live session in `session-operations-service` with a disconnected participant state.

The backend integration tests show the required shape:

- an active or scheduled `LiveSession`
- a team registered inside that session
- a participant admitted once, then disconnected for the reconnect happy path

Reference seed logic:

- `backend/services/session-operations-service/tests/IntegrationTests/Api/ReconnectParticipantEndpointTests.cs`
- `backend/services/session-operations-service/tests/IntegrationTests/Api/ReconnectParticipantHubTests.cs`

Capture these values for the next steps:

- `LIVE_SESSION_ID`
- `TEAM_ID`
- `DISPLAY_NAME`

## Step 3 — Smoke the REST reconnect first

REST is the simplest transport for checking the command contract before the hub:

```bash
curl -s -X POST "$GW/api/sessions/$LIVE_SESSION_ID/participants/reconnect" \
  -H "Authorization: Bearer $PARTICIPANT_TOKEN" \
  -H 'Content-Type: application/json' \
  -d "{\"teamId\":\"$TEAM_ID\",\"displayName\":\"$DISPLAY_NAME\",\"token\":null}" \
  | jq
```

Expected happy-path DTO shape:

```json
{
  "liveSessionId": "guid",
  "teamId": "guid",
  "teamDisplayName": "string",
  "sessionParticipantId": "guid",
  "participantDisplayName": "string",
  "sessionState": "string",
  "isReconnect": true,
  "joinedAt": "ISO-8601",
  "lastSeenAt": "ISO-8601"
}
```

Also capture at least one rejected attempt:

- first join into an already active session -> `403`
- wrong team for an already assigned participant -> `409`
- blank display name -> `400`

## Step 4 — Smoke the hub through the gateway

The probe script uses the same SignalR package later phases will consume:

```bash
cd mobile

GW=http://localhost:8000 \
TOKEN="$PARTICIPANT_TOKEN" \
LIVE_SESSION_ID="$LIVE_SESSION_ID" \
TEAM_ID="$TEAM_ID" \
DISPLAY_NAME="$DISPLAY_NAME" \
TRANSCRIPT_FILE=/tmp/hu07b-hub-success.json \
npm run smoke:reconnect:hub
```

Notes:

- default transport is `WebSockets`
- use `TRANSPORT=long-polling` only as a fallback diagnostic
- the script never prints the bearer token

The output is a JSON transcript with either `outcome: "success"` and `result`, or
`outcome: "error"` and the error `name` / `message`.

### Capture the rejection shape

Run the same command against a known-bad input, for example a first join into an
already active session or a wrong-team reconnect:

```bash
GW=http://localhost:8000 \
TOKEN="$PARTICIPANT_TOKEN" \
LIVE_SESSION_ID="$LIVE_SESSION_ID" \
TEAM_ID="$WRONG_TEAM_ID" \
DISPLAY_NAME="$DISPLAY_NAME" \
TRANSCRIPT_FILE=/tmp/hu07b-hub-denied.json \
npm run smoke:reconnect:hub
```

That transcript is what `reconnect-policy.ts` should key off later. Keep the policy
translation in one place; do not scatter message matching into screens.

## Step 5 — Close-out checklist

- REST happy path captured through the gateway
- REST rejection path captured through the gateway
- Hub happy path captured through the gateway over `WebSockets`
- Hub rejection path captured with the serialized error message
- `displayName`, `liveSessionId`, `teamId`, and `token` sources recorded
- reconnect trigger policy fixed: resume, transport-drop, and manual retry

## Phase 4 — On-device manual test matrix

Run these against the live stack with Expo Go on a physical device (or a simulator
reaching the gateway at the host configured in `.env.local`). Each row maps a real
backend condition to the explicit client state the participant must see — no row may
land on a broken or blank live screen.

| # | Scenario | How to produce it | Expected client state |
|---|----------|-------------------|-----------------------|
| 1 | Joined participant resumes app, session still live | Join a team, background or kill the app, reopen | `team-space` shows "Live session resumed" (`isReconnect: true`) with team + session state |
| 2 | First admission via hub | Fresh join straight through the HU-07A lobby into `team-space` | "Joined live session" (`isReconnect: false`) |
| 3 | Session moved past the join window | Advance the session to `active`/`preparing`, then reconnect | `forbidden-late-join`: "The session has moved on — late join isn't allowed." |
| 4 | Session in a non-joinable state | Finish/cancel the session, then reconnect | `invalid-session-state`: "This session isn't accepting participants right now." |
| 5 | Participant lost team access | Remove the participant / reconnect to a wrong team | `lost-access`: "You no longer have access to this team." → "Back to home" |
| 6 | Token expired / rejected on negotiate | Let the access token expire (or clear it) and resume | Signed out automatically (handled in `use-reconnect`) |
| 7 | Gateway / hub down | Stop `docker compose` (or the gateway) and resume | `network-error` notice + "Try again" retry |
| 8 | Transient WS drop | Briefly drop Wi-Fi while in `team-space`, then restore | "Reconnecting…" banner shows, then auto-recovers — participant is not kicked out |

Confirm while running the matrix:

- The bearer token never appears in Metro / device logs (connection is pinned to
  `LogLevel.Warning`; the smoke script never prints it either).
- Every denied row offers a clear next step (retry or back to home) — never a dead end.
- The HU-07A first-join path (scenario 2) is unchanged from before this slice.
- Signing out from a restored session clears the persisted context: a different user
  signing in on the same device is not resumed into the previous team.

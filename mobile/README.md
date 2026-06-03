# Umbral Mobile

Expo (React Native) participant app for the Umbral escape-room platform.

## Prerequisites

- Node 20+
- [Expo Go](https://expo.dev/go) installed on your iOS or Android device/emulator
- The Umbral backend stack running locally (`docker compose up` in `backend/`)

## Environment setup

Copy the example env file and fill in the host for your testing target:

```bash
cp .env.example .env.local
```

Edit `.env.local`:

| Variable | iOS Simulator | Android Emulator | Physical device |
|---|---|---|---|
| `EXPO_PUBLIC_API_BASE_URL` | `http://localhost:8000` | `http://10.0.2.2:8000` | `http://<LAN-IP>:8000` |
| `EXPO_PUBLIC_KEYCLOAK_URL` | `http://localhost:8080` | `http://10.0.2.2:8080` | `http://<LAN-IP>:8080` |

The other two vars are fixed for local dev — leave them as-is:

```
EXPO_PUBLIC_KEYCLOAK_REALM=umbral
EXPO_PUBLIC_KEYCLOAK_CLIENT_ID=umbral-mobile
```

To find your LAN IP on Linux: `ip route get 1 | awk '{print $7; exit}'`

> **Restart the Metro bundler** after changing any `EXPO_PUBLIC_*` variable.

## Running

```bash
npm install
npx expo start
```

Scan the QR code with Expo Go, or press `i` for the iOS simulator / `a` for Android emulator.

## Backend stack

```bash
cd ../backend
docker compose up
```

Services:
- **API Gateway** → `http://localhost:8000`
- **Keycloak** → `http://localhost:8080`

## Seed users

| Username | Password | Role | Expected result |
|---|---|---|---|
| `participant` | (see `backend/deploy/keycloak/import/umbral-realm.json`) | Participant | Signs in → participant home |
| `operator` | (see realm JSON) | Operator | Rejected — wrong role |
| `admin` | (see realm JSON) | Administrator | Rejected — wrong role |

## Realtime reconnection (HU-07B)

Participants who reached a team space and then lost connectivity, backgrounded, or
killed the app are restored into their live session over the session-operations
**SignalR hub** instead of replaying the join flow.

- **SDK:** `@microsoft/signalr`, installed with the Expo-aware installer so the
  version is pinned to one compatible with SDK 56:
  ```bash
  npx expo install @microsoft/signalr
  ```
- **Hub URL:** `${EXPO_PUBLIC_API_BASE_URL}/hubs/sessions` — derived by `hubBaseUrl()`
  in `src/lib/host.ts`. Like every other call it goes through the API Gateway, never
  directly to a service.
- **Auth:** the WebSocket handshake can't set an `Authorization` header, so the
  client uses SignalR's `accessTokenFactory` (the same Keycloak access token from
  `expo-secure-store`). The gateway extracts the `?access_token=` query param and
  authenticates the hub. The connection logs at `Warning` level so the token (carried
  in the connection URL) is never echoed.
- **How resume works:** on a successful join the minimal live context
  (`liveSessionId`, `teamId`, `displayName`, `teamCapacity`) is persisted to
  `expo-secure-store` (`src/lib/realtime/reconnect-context.ts`). On cold start or
  return-to-foreground, `app/_layout.tsx` routes an authenticated participant with a
  stored context back to `team-space`, which invokes the hub's `ReconnectAsync` and
  renders the restored DTO (or an explicit denied state). The context is cleared on
  sign-out and explicit leave so a different user never inherits a stale session.
- **Access rules live on the backend.** The client only consumes the verified
  contract: late-join vs. reconnect, capacity, and session-state rules are owned by
  session-operations and surface as a `HubException`, which
  `src/lib/realtime/reconnect-policy.ts` maps to UI vocabulary — the client invents
  no access rules of its own.
- **Manual verification:** see `docs/hu-07b-mobile-test-workflow.md` for the contract
  spike and the Phase 4 device test matrix.

## Architecture notes

- Auth uses Keycloak direct-grant (Resource Owner Password) — see `src/lib/auth/keycloak.ts`.
- JWTs are stored exclusively in `expo-secure-store`; nothing is logged or stored in AsyncStorage.
- All backend calls go through the API Gateway (`EXPO_PUBLIC_API_BASE_URL`), never directly to services.
- Design tokens live in `src/constants/theme.ts`; see `mobile/DESIGN.md` for the design system guide.

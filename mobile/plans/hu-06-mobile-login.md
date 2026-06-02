# Plan — HU-06: Participant Mobile Login (Expo / React Native)

Source PRD: `backend/docs/prd/DES-68-prd-hu-06-inicio-de-sesion-de-participantes.md`
Branch: `feature/hu-06-participants-login`

## Goal

Two deliverables in the Expo app (`mobile/`):

1. A **mobile design system** that reuses the frontend "Lantern Control Room"
   ember/parchment colors (**light mode only**), adapted to native via the
   `building-native-ui` skill conventions (theme tokens + inline styles).
2. A **participant login view** that authenticates against Keycloak with a
   **native email/password form** (direct grant), provisions the user through
   the backend, and gates access to the `Participant` role only.

All backend calls go through the **api-gateway** (never directly to services).

---

## What already exists (verified)

**Backend HU-06 backbone is already implemented** — this is a client-only build.

- **Gateway** (`backend/api-gateway`) validates the JWT, strips `Authorization`,
  and injects trusted headers `X-User-Id`, `X-User-Role`, `X-User-Email`
  (`Transforms/TrustedHeadersTransform.cs`). Routes `/api/users/**` →
  `identity-access-service`.
- **Endpoints** (`identity-access-service/src/Api/Endpoints/UsersEndpoints.cs`):
  - `POST /api/users/authenticated` — Post-Login Provisioning.
    Body: `{ "displayName": string }`.
    Returns `AuthenticateUserResultDto`:
    ```jsonc
    {
      "actor":  { "externalIdentityId": "...", "displayName": "...",
                  "email": "...", "role": "Participant", "isActive": true },
      "access": { "capability": "...", "isAllowed": true, "reason": "..." }
    }
    ```
  - `GET /api/users/me` — authenticated profile (`AuthenticatedActorProfileDto`):
    `{ externalIdentityId, displayName, email, role, isActive }`.
- **Keycloak realm** (`backend/deploy/keycloak/import/umbral-realm.json`):
  - Realm `umbral`; realm roles `Administrator`, `Operator`, `Participant`.
  - Public client **`umbral-mobile`** — `publicClient: true`,
    `standardFlowEnabled: true`, `directAccessGrantsEnabled: true`,
    redirect URIs `http://localhost/*`.
  - Seed users: `participant` (Participant), `operator`, `admin`.

**Decisions locked for this plan**
- Auth flow: **Direct grant** (Resource Owner Password) — custom in-app form
  POSTs to the Keycloak token endpoint. Full control of the login UI.
- Design: **same colors as frontend `DESIGN.md`, light mode only**, native-adapted.

---

## Mobile conventions (from `mobile/.agents/skills`)

- Read the versioned Expo docs (`https://docs.expo.dev/versions/v56.0.0/`) before
  coding — SDK 56 / RN 0.85 / expo-router 56 differ from training data.
- **Styling:** inline styles + a theme-constants module. No Tailwind/NativeWind,
  no `StyleSheet.create` unless reused. `{ borderCurve: 'continuous' }` for radii,
  CSS `boxShadow` (never legacy `shadow*`/`elevation`).
- **Networking:** `expo/fetch`, not axios. Always check `response.ok`.
- **Tokens:** `expo-secure-store` (never AsyncStorage for secrets).
- **Files:** kebab-case. Routes live in `app/`; components/utils never co-located
  in `app/`. Path alias `@/*` already configured in `tsconfig.json`.
- **Env:** `EXPO_PUBLIC_*` for client-readable config; restart dev server on change.

---

## Phase 0 — Foundations & integration spike (de-risk first)

Prove the auth round-trip end-to-end with curl before writing app code.

- [x] Bring up the stack: `docker compose up` in `backend/` (gateway + Keycloak +
      identity-access-service + Postgres).
- [x] Get a participant token via direct grant and confirm the gateway accepts it:
  ```bash
  # 1. token from Keycloak (umbral-mobile, direct grant)
  curl -s -X POST \
    "$KC/realms/umbral/protocol/openid-connect/token" \
    -d grant_type=password -d client_id=umbral-mobile \
    -d username=participant -d password=<seed-pw> \
    -d scope='openid profile email' | jq .access_token
  # 2. round-trip through the GATEWAY (not the service directly)
  curl -s -X POST "$GW/api/users/authenticated" \
    -H "Authorization: Bearer $TOKEN" -H 'Content-Type: application/json' \
    -d '{"displayName":"participant"}' | jq
  curl -s "$GW/api/users/me" -H "Authorization: Bearer $TOKEN" | jq
  ```
- [x] **RESOLVED — audience mismatch.** A second `oidc-audience-mapper` named
      `umbral-web-audience` was added to the `umbral-mobile` client in
      `backend/deploy/keycloak/import/umbral-realm.json`. Mobile access tokens
      now carry `"aud": ["umbral-mobile", "umbral-web"]`, satisfying the gateway's
      `Keycloak.Audience = "umbral-web"` validation. No gateway code change needed.
      Re-import the realm (or reprovision the stack) to pick up this change.
- [x] **RESOLVED — dev base URL strategy.** From docker-compose:
      - **API Gateway** → host port `8000` → base URL `http://<host>:8000`
      - **Keycloak** → host port `8080` → token URL `http://<host>:8080`
      - iOS Simulator: `<host> = localhost` (simulator routes to host loopback).
      - Android Emulator: `<host> = 10.0.2.2` (AVD special alias for host loopback).
      - Physical device: `<host> = <your LAN IP>` (e.g. `192.168.1.x`);
        run `ip route get 1` or check network settings to find it.
      Set both in `.env.local` based on your testing target:
      ```
      EXPO_PUBLIC_API_BASE_URL=http://localhost:8000    # iOS sim
      EXPO_PUBLIC_KEYCLOAK_URL=http://localhost:8080    # iOS sim
      ```
      Switch to `10.0.2.2` for Android emulator or your LAN IP for a device.
- [x] Add `.env` (+ `.env.example`) with:
      `EXPO_PUBLIC_API_BASE_URL` (gateway), `EXPO_PUBLIC_KEYCLOAK_URL`,
      `EXPO_PUBLIC_KEYCLOAK_REALM=umbral`, `EXPO_PUBLIC_KEYCLOAK_CLIENT_ID=umbral-mobile`.
      Add `types/env.d.ts` for typing.
- [x] Confirm **Expo Go** is sufficient (it is — only `expo-secure-store` is added,
      which Expo Go supports). No custom dev client needed.

**Exit:** a known-good curl transcript: participant authenticates → provisioned →
profile read through the gateway; admin/operator behavior noted.

---

## Phase 1 — Mobile design system (light mode, ember/parchment)

Translate `frontend/DESIGN.md` tokens into native, extending the existing
`mobile/src/constants/theme.ts`. **Light mode only.**

- [x] **Tokens** (`src/constants/theme.ts` + split files if it grows):
  - `colors` — port from frontend `DESIGN.md` (ember accents, parchment/ivory
    neutrals, ink/muted text, semantic success/warning/critical). Keep them as
    hex/rgb for RN (convert the oklch values once). Drop the `dark` map (or keep
    a single light palette as the only theme).
  - `typography` — display / headline / title / body / label / mono scale (size,
    weight, lineHeight, letterSpacing) as style objects consumable inline.
  - `spacing` — keep the existing numeric scale.
  - `radii` — pill / panel / card / control.
  - `shadows` — `boxShadow` strings (inset sheen, lantern halo) per the design's
    "tonal layering" rule.
- [x] **Primitive components** (`src/components/ui/`, kebab-case):
  - `text.tsx` — variant prop mapping to the type scale; `selectable` on data.
  - `button.tsx` — `primary` (ember fill) / `secondary` (raised surface);
    pressed/disabled/loading states; haptics on iOS.
  - `text-field.tsx` — labeled input: raised surface, continuous radius, focus
    ring, error state; `secureTextEntry` + show/hide for password.
  - `panel.tsx` / `card.tsx` — bordered surface with inset sheen.
  - `screen.tsx` — root `ScrollView` wrapper with
    `contentInsetAdjustmentBehavior="automatic"` + safe-area handling.
  - `brand-mark.tsx` — Umbral wordmark/lantern logo for the login header.
- [x] **Lock light mode:** set `app.json` `userInterfaceStyle` to `light` (or
      force the light palette in the theme hook) so the system stays single-theme.
- [x] Write `mobile/DESIGN.md` documenting the native token system and the
      "ember rule / no-pure-black / quiet hierarchy" guidance, mirroring frontend.

**Exit:** ✅ Gallery screen (`src/app/explore.tsx`) renders Text/Button/TextField/Panel/Card/BrandMark
in the ember/parchment light theme. `tsc --noEmit` passes clean.

---

## Phase 2 — Auth domain & API layer

Encapsulate auth so screens stay thin (deep modules, per the PRD's spirit).

- [x] **API client** (`src/lib/api/client.ts`) — `expo/fetch` wrapper over
      `EXPO_PUBLIC_API_BASE_URL`; typed `ApiError(status, code, message)`;
      attaches `Authorization: Bearer <token>` from secure store.
- [x] **Token storage** (`src/lib/auth/token-store.ts`) — `expo-secure-store`
      get/set/remove for access + refresh tokens.
- [x] **Keycloak client** (`src/lib/auth/keycloak.ts`):
  - `signInWithPassword(email, password)` → POST token endpoint with
    `grant_type=password`, `client_id=umbral-mobile`, `scope=openid profile email`.
    Returns `{ accessToken, refreshToken, idToken }`. Map Keycloak
    `invalid_grant` → friendly "wrong email or password".
  - `refresh(refreshToken)` (optional this HU) and `signOut()`.
  - Derive `displayName`/`email` from the id_token (mirror `frontend/lib/keycloak.ts`).
- [x] **Identity calls** (`src/lib/api/identity.ts`):
  - `bootstrapAuthenticatedUser(displayName)` → `POST /api/users/authenticated`.
  - `getAuthenticatedProfile()` → `GET /api/users/me`.
  - Typed DTOs matching `AuthenticateUserResultDto` / `AuthenticatedActorProfileDto`.
- [x] **Access guard** (`src/lib/auth/access-policy.ts`) — single place that
      decides "may this actor enter the mobile flow": `isActive === true` AND
      `role === 'Participant'` AND `access.isAllowed === true`. The client mirrors
      the backend `AccessPolicy`; it does not invent new checks (PRD: no ad-hoc
      role checks scattered across screens).
- [x] **Auth provider + hook** (`src/lib/auth/auth-context.tsx`, `use-auth.ts`):
  - State: `idle | authenticating | authenticated | rejected | error`.
  - `signIn(email, password)` orchestrates: Keycloak token → store →
    bootstrap provisioning → access guard → hydrate profile.
  - Distinguish rejection reasons: `wrong-credentials`, `deactivated`,
    `wrong-role`, `network`.
  - `signOut()` clears tokens + state.
  - Restore session on launch from stored token (call `/me`, re-check guard).

**Exit:** unit-test the access guard (participant ✓; deactivated ✗; admin/operator
✗) and the Keycloak error mapping, without UI.

---

## Phase 3 — Login view, routing & access gating

Wire the design system + auth layer into screens.

- [x] **Routing** (`app/_layout.tsx`): wrap the tree in `AuthProvider`; redirect
      logic — unauthenticated → `/(auth)/login`; authenticated+allowed →
      `/(app)`; authenticated-but-rejected → a rejection screen. Splash/loading
      while restoring the stored session.
- [x] **Login screen** (`app/(auth)/login.tsx`):
  - `brand-mark` header, email + password `text-field`s, primary "Sign in" button.
  - Inline validation (required, email shape), disabled/loading button state,
    inline error banner (`selectable`).
  - On submit → `useAuth().signIn`; route on success; show mapped error otherwise.
  - Dismiss keyboard / safe-area aware; no `SafeAreaView` (use ScrollView inset).
- [x] **Authed landing** (`app/(app)/index.tsx`): minimal "you're in" placeholder
      that greets the participant and exposes **Sign out**. This is the boundary —
      session join / team membership / reconnect are **out of scope** (HU-07A/07B).
- [x] **Rejection screen** (`app/(auth)/access-denied.tsx`): clear message for
      deactivated users and non-`Participant` roles; offer sign-out / retry.
- [x] Remove the starter routes (`app/index.tsx`, `app/explore.tsx`, tabs) that
      don't belong to the participant flow.

**Exit:** on-device, `participant` logs in and lands in `(app)`; `admin`/`operator`
get the access-denied screen; killing/reopening the app restores the session.

---

## Phase 4 — States, polish & manual verification

- [x] Loading, empty, and error states polished; haptics on submit/success/error.
- [x] Network-failure handling (gateway unreachable) with retry affordance.
- [ ] Manual test matrix against seed users:
  | User | Expected |
  |------|----------|
  | participant / correct pw | provisioned → `(app)` landing |
  | participant / wrong pw | "wrong email or password" |
  | admin or operator | access-denied (wrong role) |
  | deactivated participant | access-denied (deactivated) |
  | gateway down | network error + retry |
- [x] Confirm no token is ever logged; tokens only in SecureStore.
- [x] Update `mobile/README.md` with env setup + run instructions.

---

## Out of scope (per PRD)

- Team/session membership validation, `JoinToken`, session admission → **HU-07A**.
- Authorized reconnection / shared-state recovery → **HU-07B**.
- Multi-device team sync; full game UX beyond the auth/access boundary.
- Any JWT re-validation inside services (gateway is the only validator).

## Key risks

1. ~~**Audience mismatch**~~ — **RESOLVED.** Second audience mapper added to
   `umbral-mobile` in `umbral-realm.json`. Tokens now carry `aud: umbral-web`.
2. ~~**Dev networking**~~ — **RESOLVED.** Gateway `:8000`, Keycloak `:8080`.
   iOS sim → `localhost`, Android emulator → `10.0.2.2`, device → LAN IP.
   Set in `.env.local` (see Phase 0).
3. **Direct grant trade-off** — Resource Owner Password is less OAuth-canonical
   than PKCE; acceptable per decision, but isolate it in `keycloak.ts` so a
   later swap to the browser/PKCE flow is a one-module change.

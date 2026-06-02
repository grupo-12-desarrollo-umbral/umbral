# HU-06 — Mobile Login: Test Workflow

Branch: `feature/hu-06-participants-login`  
App: `mobile/` (Expo Go, no custom dev client needed)  
Backend: already running via `docker compose up` in `backend/`

---

## Step 1 — Pick your target and set `.env.local`

File: `mobile/.env.local`

The default is set to `localhost` (iOS simulator / macOS only). On Linux, use one of:

**Physical device (recommended on Linux):**
```
EXPO_PUBLIC_API_BASE_URL=http://10.134.148.20:8000
EXPO_PUBLIC_KEYCLOAK_URL=http://10.134.148.20:8080
EXPO_PUBLIC_KEYCLOAK_REALM=umbral
EXPO_PUBLIC_KEYCLOAK_CLIENT_ID=umbral-mobile
```

**Android emulator:**
```
EXPO_PUBLIC_API_BASE_URL=http://10.0.2.2:8000
EXPO_PUBLIC_KEYCLOAK_URL=http://10.0.2.2:8080
EXPO_PUBLIC_KEYCLOAK_REALM=umbral
EXPO_PUBLIC_KEYCLOAK_CLIENT_ID=umbral-mobile
```

> To find your current LAN IP: `ip route get 1 | awk '{print $7; exit}'`  
> **Restart Metro** after editing `.env.local`.

---

## Step 2 — Start Metro

```bash
cd mobile
npx expo start
```

Scan the QR code with **Expo Go** on your device, or press `a` for Android emulator.

---

## Step 3 — Seed credentials

From `backend/deploy/keycloak/import/umbral-realm.json`:

| Username | Password | Role |
|---|---|---|
| `participant` | `participant123` | Participant |
| `operator` | `operator123` | Operator |
| `admin` | `admin123` | Administrator |

---

## Step 4 — Test matrix

### T1 · Participant correct password
- Input: `participant` + `participant123`
- Expected: spinner → **Welcome, participant** home screen with Sign out button

### T2 · Wrong password
- Input: `participant` + `wrongpassword`
- Expected: **"Wrong email or password."** banner; button stays enabled; haptic error on iOS

### T3 · Admin / operator (wrong role)
- Input: `admin` + `admin123`
- Expected: **Access Denied** screen — "Access Restricted — this app is for participants only"

### T4 · Gateway down (network error + retry affordance)
```bash
docker stop backend-api-gateway-1
```
- Try signing in as participant
- Expected: **"Network error. Check your connection and try again."** banner; button label becomes **"Try again"**
```bash
docker start backend-api-gateway-1
```
- Tap "Try again" → should succeed

### T5 · Session restore (kill / reopen)
- Sign in as participant
- Kill Expo Go (swipe away from app switcher)
- Reopen and navigate back to the app
- Expected: goes directly to **Welcome** screen — no login prompt (token persisted in SecureStore)

### T6 · Sign out
- From participant home, tap **Sign out**
- Expected: returns to login screen with empty fields

### T7 · Client-side validation (empty submit)
- Tap **Sign in** with empty fields
- Expected: **"Email is required."** and **"Password is required."** errors inline; haptic pulse on iOS; no network call

---

## Step 5 — Confirm no token leakage

Watch the Metro terminal output during the tests above. No `access_token:`, `Bearer ey…`, or token strings should appear. The auth path has zero `console.log` calls.

---

## Optional — Backend smoke test with curl

Verify the full round-trip before touching the app:

```bash
# 1. Get a token from Keycloak
TOKEN=$(curl -s -X POST http://localhost:8080/realms/umbral/protocol/openid-connect/token \
  -d grant_type=password -d client_id=umbral-mobile \
  -d username=participant -d password=participant123 \
  -d 'scope=openid profile email' \
  | python3 -c "import sys,json; print(json.load(sys.stdin)['access_token'])")

# 2. Bootstrap through the gateway
curl -s -X POST http://localhost:8000/api/users/authenticated \
  -H "Authorization: Bearer $TOKEN" \
  -H 'Content-Type: application/json' \
  -d '{"displayName":"participant"}' | python3 -m json.tool

# 3. Profile
curl -s http://localhost:8000/api/users/me \
  -H "Authorization: Bearer $TOKEN" | python3 -m json.tool
```

Step 2 should return `"role": "Participant"` and `"isAllowed": true`.

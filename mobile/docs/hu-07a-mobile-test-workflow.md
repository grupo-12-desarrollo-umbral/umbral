# HU-07A — Participant Membership Validation: Mobile Test Workflow

Branch: `feature/hu-07a-participant-membership-validation`
App: `mobile/` (Expo Go, no custom dev client needed)
Backend: already running via `docker compose up` in `backend/`

> Builds on [HU-06](./hu-06-mobile-test-workflow.md). Sign-in still works exactly
> as before; this slice adds the **Join your session** flow that lands a verified
> participant in their **team space**.
>
> What Identity returns here is an **access fact** (`AccessDecision`), not a
> session admission — the team-space screen confirms access only and explicitly
> does **not** join the real-time hub (that is HU-07B). Validation is also
> **read-only / non-consuming**: re-checking never burns a join token.

---

## Step 1 — `.env.local` and Metro

Identical to HU-06 — reuse your existing `mobile/.env.local` (gateway `:8000`,
Keycloak `:8080`, realm `umbral`, client `umbral-mobile`). See
[HU-06 Step 1–2](./hu-06-mobile-test-workflow.md#step-1--pick-your-target-and-set-envlocal).

```bash
cd mobile
npx expo start
```

Scan the QR with **Expo Go**, or press `a` for the Android emulator.
**Restart Metro after editing `.env.local`.**

---

## Step 2 — Seed credentials

From `backend/deploy/keycloak/import/umbral-realm.json`:

| Username | Password | Role |
|---|---|---|
| `participant` | `participant123` | Participant |
| `operator` | `operator123` | Operator |
| `admin` | `admin123` | Administrator |

The join flow needs a participant who **already belongs to a team**, plus a
**join token** minted for a `(session, team)` pair. Step 3 sets that up over curl;
Step 4 drives it from the app.

---

## Step 3 — Backend prep (curl)

Everything below talks to the **gateway** (`:8000`). Set a couple of helpers:

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
ADMIN_TOKEN=$(token_for admin admin123)
```

### 3a · Provision the participant (once)

The participant must exist in Identity before it can be put on a team. The app
does this on first login, or do it directly:

```bash
curl -s -X POST "$GW/api/users/authenticated" \
  -H "Authorization: Bearer $PARTICIPANT_TOKEN" \
  -H 'Content-Type: application/json' \
  -d '{"displayName":"participant"}' | python3 -m json.tool
```

### 3b · Register a team and capture its id

```bash
TEAM_ID=$(curl -s -X POST "$GW/api/teams" \
  -H "Authorization: Bearer $ADMIN_TOKEN" \
  -H 'Content-Type: application/json' \
  -d '{"displayName":"Team Red","teamCode":"RED-07A"}' \
  | python3 -c "import sys,json; print(json.load(sys.stdin)['teamId'])")
echo "TEAM_ID=$TEAM_ID"
```

### 3c · Find the participant's internal user id

```bash
USER_ID=$(curl -s "$GW/api/users" -H "Authorization: Bearer $ADMIN_TOKEN" \
  | python3 -c "import sys,json; print(next(u['id'] for u in json.load(sys.stdin)['items'] if u['role']=='Participant'))")
echo "USER_ID=$USER_ID"
```

### 3d · Assign the participant to the team (creates the `TeamMembership`)

```bash
curl -s -X POST "$GW/api/teams/$TEAM_ID/participants" \
  -H "Authorization: Bearer $ADMIN_TOKEN" \
  -H 'Content-Type: application/json' \
  -d "{\"userId\":$USER_ID}" | python3 -m json.tool
```

> This `TeamMembership` is the authorization fact the validation checks. Without
> it, every request for `TEAM_ID` is denied with a **403** (foreign team).

### 3e · Mint a join token for a `(session, team)` pair

`liveSessionId` is an **opaque** correlation id — make one up. Mint the token for
the same session + team you'll type into the app.

```bash
SESSION_ID=$(python3 -c "import uuid; print(uuid.uuid4())")
echo "SESSION_ID=$SESSION_ID"

JOIN_TOKEN=$(curl -s -X POST "$GW/api/join-tokens" \
  -H "Authorization: Bearer $ADMIN_TOKEN" \
  -H 'Content-Type: application/json' \
  -d "{\"liveSessionId\":\"$SESSION_ID\",\"teamId\":\"$TEAM_ID\",\"expiresInSeconds\":900}" \
  | python3 -c "import sys,json; print(json.load(sys.stdin)['token'])")
echo "JOIN_TOKEN=$JOIN_TOKEN"
```

Keep these three handy — you'll type them into the app:

```bash
echo "SESSION_ID = $SESSION_ID"
echo "TEAM_ID    = $TEAM_ID"
echo "JOIN_TOKEN = $JOIN_TOKEN"
```

> The plaintext token is returned **exactly once**; only its hash is stored.
> If you lose it, mint another.

---

## Step 4 — In-app test matrix

Sign in as `participant` / `participant123`, then tap **Join your session**.
The form has three fields: **Session ID**, **Team ID**, **Join token (optional)**.

### T1 · Membership only (no token)
- Session ID = `SESSION_ID`, Team ID = `TEAM_ID`, token **blank**
- Expected: spinner → routes to **team space** with **"Access confirmed"**, the
  team and session ids shown, and the "Connecting … coming soon" notice.
  (Session id is opaque here — any valid GUID is accepted when no token is given.)

### T2 · Membership + valid token
- Session ID = `SESSION_ID`, Team ID = `TEAM_ID`, token = `JOIN_TOKEN`
- Expected: routes to **team space**; the confirmation reason mentions the token
  was validated. Success haptic on iOS.

### T3 · Foreign team (not your team) → **403 forbidden**
- Generate a team id you do **not** belong to:
  `python3 -c "import uuid; print(uuid.uuid4())"`
- Session ID = `SESSION_ID`, Team ID = the foreign id, token blank
- Expected: stays on the form with banner
  **"You don't belong to this team. You can only enter the team you were assigned to."**

### T4 · Token / context mismatch → **200 denied**
- Mint or reuse `JOIN_TOKEN` (issued for `SESSION_ID`), but enter a **different**
  Session ID (generate another GUID) with the **same** `TEAM_ID` and the token.
- Expected: banner **"Join token does not match the requested session/team context."**
  (Proxy passes because you belong to the team; the token then fails the context check.)

### T5 · Invalid token → **200 denied**
- Session ID = `SESSION_ID`, Team ID = `TEAM_ID`, token = `not-a-real-token`
- Expected: banner **"Join token is invalid."**

### T6 · Expired token → **200 denied**
- Mint a 1-second token, wait, then validate:
  ```bash
  SHORT=$(curl -s -X POST "$GW/api/join-tokens" \
    -H "Authorization: Bearer $ADMIN_TOKEN" -H 'Content-Type: application/json' \
    -d "{\"liveSessionId\":\"$SESSION_ID\",\"teamId\":\"$TEAM_ID\",\"expiresInSeconds\":1}" \
    | python3 -c "import sys,json; print(json.load(sys.stdin)['token'])")
  echo "$SHORT"; sleep 3
  ```
- Enter `SESSION_ID` + `TEAM_ID` + `$SHORT`
- Expected: banner **"Join token has expired."**

### T7 · Read-only / non-consuming
- Repeat **T2** (valid token) two or three times in a row.
- Expected: **allowed every time** — validation never consumes the token, so it
  stays usable for the real join/reconnection later.

### T8 · Client-side validation (no network call)
- Tap **Validate access** with empty fields → inline **"Session ID is required."**
  / **"Team ID is required."**
- Enter a non-GUID (e.g. `abc`) → **"Enter a valid session ID."** /
  **"Enter a valid team ID."**; error haptic on iOS; nothing hits the network.

### T9 · Gateway down (network error)
```bash
docker stop backend-api-gateway-1
```
- Submit a valid T2 request
- Expected: banner **"Network error. Check your connection and try again."**
```bash
docker start backend-api-gateway-1
```
- Submit again → succeeds.

### T10 · Back / navigation
- From **team space**, tap **Back** → returns to the join form.
- From the join form, tap **Back** → returns to the participant home.

---

## Step 5 — Confirm no token leakage

Watch the Metro terminal during the tests. No `JOIN_TOKEN`, `access_token:`,
`Bearer ey…`, or token strings should appear — the membership path has zero
`console.log` calls, and tokens live only in SecureStore (access/refresh) or
transient form state (join token).

---

## Optional — Validation endpoint smoke test (curl)

Hit the surface the app calls, directly through the gateway:

```bash
# Allowed (membership only)
curl -s -X POST "$GW/api/permissions/participant-membership-access" \
  -H "Authorization: Bearer $PARTICIPANT_TOKEN" -H 'Content-Type: application/json' \
  -d "{\"liveSessionId\":\"$SESSION_ID\",\"teamId\":\"$TEAM_ID\"}" | python3 -m json.tool
# → { "capability": "...", "isAllowed": true, "reason": "...", "liveSessionId": "...", "teamId": "..." }

# Allowed (membership + token)
curl -s -X POST "$GW/api/permissions/participant-membership-access" \
  -H "Authorization: Bearer $PARTICIPANT_TOKEN" -H 'Content-Type: application/json' \
  -d "{\"liveSessionId\":\"$SESSION_ID\",\"teamId\":\"$TEAM_ID\",\"token\":\"$JOIN_TOKEN\"}" \
  | python3 -m json.tool

# Foreign team → HTTP 403 ProblemDetails
curl -s -o /dev/null -w '%{http_code}\n' -X POST \
  "$GW/api/permissions/participant-membership-access" \
  -H "Authorization: Bearer $PARTICIPANT_TOKEN" -H 'Content-Type: application/json' \
  -d "{\"liveSessionId\":\"$SESSION_ID\",\"teamId\":\"$(python3 -c 'import uuid;print(uuid.uuid4())')\"}"
```

> A denied **token** returns `200` with `"isAllowed": false` and a structured
> `reason`. A **foreign team / non-participant** is rejected upstream with `403`.
> That status split is exactly what the app maps to its banners.

---

## Mapping: outcome → what you see

| Backend result | App outcome | Where |
|---|---|---|
| `200` `isAllowed: true` | route to **team space** | `interpretDecision` → `allowed` |
| `200` `isAllowed: false` | denial banner with backend `reason` | `interpretDecision` → `denied` |
| `403` | "You don't belong to this team…" | `interpretError` → `forbidden` |
| `401` | signed out (stale session) | `interpretError` → `unauthorized` |
| `400` | "Check the session and team identifiers…" | `interpretError` → `invalid-input` |
| network failure | "Network error…" | `interpretError` → `network-error` |

Logic lives in `mobile/src/lib/membership/membership-policy.ts` (unit-tested in
`mobile/src/__tests__/membership-policy.test.ts`).

# Issue #223 — Manual Test: participant scans a target QR on the play surface

Goal: a **participant** (mobile) on the live treasure-hunt board opens a camera
viewfinder, scans a target's QR code, and gets immediate, explained feedback — an
**accepted** scan confirms success and advances the target-progress numerator; a
**rejected** scan shows the backend's reason (unknown QR, or a duplicate/
already-resolved target). No geofencing/GPS — coordinates stay display-only (#156).

This builds directly on the **HU-23** board (`frontend/docs/hu-23-manual-test.md`):
same stack, same seed, same session. You need the operator on `localhost:3000`, the
mobile app on a **real device** (simulators have no camera) pointed at the gateway
`localhost:8000`, and a way to display the seeded target QR codes.

> The scanner is a native module — build a **dev client** (`npm run android` /
> `npm run ios`), not Expo Go.

---

## 1. Bring up the stack (once)

```bash
# backend + gateway + Keycloak
cd backend && docker compose up -d

# operator web app
cd ../frontend && pnpm install && pnpm dev       # -> http://localhost:3000

# seed the SAME startable treasure-hunt session HU-23 uses
cd frontend && pnpm exec playwright test tests/e2e/session-treasure-hunt-manual-seed.spec.ts

# mobile dev client (separate terminal) — expo-camera needs a native build
cd ../mobile && npm install && npm run android    # or: npm run ios
```

## 2. Grab the session code and the target QR payloads

The seed prints the session code (copy it), and authors **3 active targets** whose
QR codes are fixed and printable:

```
  HU-23 manual-test session ready (Preparing):
    session code: <COPY THIS>
```

| Target   | QR payload to scan |
| -------- | ------------------ |
| Target 1 | `TH-E2E-QR-1`      |
| Target 2 | `TH-E2E-QR-2`      |
| Target 3 | `TH-E2E-QR-3`      |

Render each as a QR image to point the camera at — e.g. `qrencode -o qr1.png
TH-E2E-QR-1` (or any online QR generator), then display it on another screen or
print it. Also make one **bogus** code (e.g. `TH-E2E-QR-BOGUS`) for the reject path.

## 3. Credentials

| Who         | Where                | Login                                           |
| ----------- | -------------------- | ----------------------------------------------- |
| Operator    | web `localhost:3000` | `op-1` / `operator123`                          |
| Participant | mobile app           | `participant-1@umbral.local` / `participant123` |

`participant-1` is pre-seeded onto **Gilded Owls**, the team attached to the session.

## 4. Run it

### Step A — Participant (mobile): join, then wait for Start

1. Sign in as **participant-1@umbral.local / participant123**.
2. **Join your session** → enter the **session code** → pick **Gilded Owls** → join.
3. You land on "Restoring your team space…" until the operator starts the session.

### Step B — Operator (web): Start the session

Sign in as **op-1 / operator123**, open **Treasure Hunt E2E**, click **Start**
(Preparing → Active). The participant flips to the treasure-hunt board.

### Step C — Launch the scanner + camera permission

1. A floating **SCAN TARGET** button rides above the team strip on every tab. Tap it.
2. First launch → the OS camera-permission prompt. **Deny** it once: the overlay
   shows "Camera access needed" + an **Allow camera** action (no crash). **Allow** →
   the live viewfinder opens with a reticle and "Line up a target QR code to scan it."

### Step D — Accepted scan

1. Point the camera at **`TH-E2E-QR-1`**.
2. The overlay shows **TARGET RESOLVED** (success haptic on iOS).
3. Tap **Done** → on the MAP tab the count advances **`0 / 3` → `1 / 3` targets**.
4. **Scan another** keeps the camera open — scan `TH-E2E-QR-2` → **`2 / 3`**.

That accepted scan advancing the numerator — with the reason surfaced on rejects
below — is issue #223 working. ✅

### Step E — Rejected scans (explained)

Re-open the scanner and scan each; the overlay shows the reason in red with **Try
again** / **Close**:

| Scan                                     | Reason shown                                              |
| ---------------------------------------- | -------------------------------------------------------- |
| The **bogus** QR (`TH-E2E-QR-BOGUS`)     | "The scanned value does not resolve to a target."        |
| A target you **already** resolved (QR-1) | "The target has already been resolved by this team."     |

## 5. Quick extra checks (optional)

| Check                | How                                                    | Expect                                                              |
| -------------------- | ------------------------------------------------------ | ------------------------------------------------------------------ |
| Trivia has no scanner| Join a `Trivia` session instead                        | Trivia question stage renders — **no** SCAN TARGET button          |
| Progress persists    | After an accepted scan, background/reopen the app      | Board re-fetches and the advanced `resolved / total` count sticks  |
| Session paused       | Operator **Pause**s, then scan                         | Rejected with "Session paused" (not a raw error, no crash)         |
| No score leak        | Read the accepted-scan confirmation                    | Success only — no points/score shown (score travels server-side)   |

---

## Known limitations (by design — not bugs)

- **"Target outside the active substage" reject isn't reachable with this seed.**
  The HU-23 seed authors a single treasure-hunt substage, so there is no second
  substage's target to scan out-of-context. The path exists (backend
  `TargetOutsideActiveSubstage`) — it just needs a two-substage mission to exercise.
- **No live push on resolution.** The board has no `TeamBoardUpdated` push for a
  scan yet, so the numerator advances via an on-demand re-fetch fired by the
  accepted scan — not a broadcast. A second device won't see the change until it
  re-fetches (reconnect/rejoin).
- **Camera needs a real device + dev client.** `expo-camera` is native; simulators
  have no camera and Expo Go won't bundle it.

## Troubleshooting

- **No SCAN TARGET button.** The active substage isn't `TreasureHunt` (you joined a
  Trivia session) or the board hasn't loaded — re-seed with the spec above.
- **Camera stays black / "Preparing the camera…".** Permission still resolving, or
  you're on a simulator — use a real device.
- **Every scan says "Connection issue".** The app can't reach the gateway; on a
  physical device use your machine's LAN IP, not `localhost` (see `mobile/README.md`).
- **Accepted but the count didn't move.** The post-scan re-fetch failed silently —
  leave/rejoin to pull a fresh board, and confirm the gateway proxies the
  `participants/target-scans` route.

---

## Appendix — the seed and the endpoint

- **Seed:** reuses `frontend/tests/e2e/session-treasure-hunt-manual-seed.spec.ts`
  (inlined in `frontend/docs/hu-23-manual-test.md`). Its targets carry
  `QrCode = 'TH-E2E-QR-' || seq`, which is exactly what you scan here.
- **Endpoint:** the scanner POSTs to
  `POST /api/sessions/{liveSessionId}/participants/target-scans`
  `{ teamId, scannedValue, token }` (Participant policy). A correct scan → 200; a
  wrong/duplicate/out-of-context scan → RFC 7807 **422** (`type: target-scan-rejected`)
  with the reason in `detail`, surfaced verbatim by the app.

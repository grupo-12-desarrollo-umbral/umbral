# #153 — Manual Test: operator sets target coordinates, participant sees them on a real map

Goal: an **operator** (web) authors treasure-hunt targets with geographic coordinates
via the numeric inputs or the click-to-pick Leaflet map, and a **participant**
(mobile) sees those targets rendered as ember pins on a real Leaflet map (WebView)
in the treasure-hunt board's **MAP** tab. When no coordinates are set, the map
shows a clear empty state ("NO MAP LOCATION YET").

Two surfaces to cover: the **operator dashboard** mission editor (frontend) and
the **mobile participant** board (mobile). The seeded session below gives the
mobile test pre-placed coordinates — the operator dashboard test is verified
through the UI as a separate flow. Child issues: #154 (backend coordinates),
#155 (play surface), #156 (real map view).

> Prefer to not do it by hand? Unit tests cover the Leaflet HTML builder, the
> `TargetMap` component tree, and the empty state in both `frontend/` and
> `mobile/`:
> - `frontend`: `pnpm exec vitest run tests/unit/app/dashboard/mission/target-map-html.test.ts tests/unit/app/dashboard/mission/target-map.test.ts`
> - `mobile`: `npx jest src/__tests__/target-map-html.test.ts src/__tests__/target-map.test.tsx`
>
> The automated e2e `tests/e2e/mission-hierarchy.spec.ts` drives the coordinate
> inputs and asserts the location row — but it does not drive the Leaflet iframe
> (OSM tiles need a real browser render). This manual doc adds the visual map
> verification on both surfaces.

---

## 1. Bring up the stack (once)

```bash
# backend + gateway + Keycloak
cd backend && docker compose up -d

# operator web app
cd ../frontend && pnpm install && pnpm dev       # -> http://localhost:3000

# seed a startable treasure-hunt session with pre-placed coordinates + one team
cd frontend && pnpm exec playwright test tests/e2e/session-target-map-manual-seed.spec.ts

# mobile app (separate terminal)
cd ../mobile && pnpm install && pnpm start        # open in Expo Go / emulator
```

## 2. Grab the seeded session code

The seed run above authors a runtime-ready treasure-hunt mission whose three
targets carry real geographic coordinates (Bogotá, Madrid, Tokyo), attaches the
**Gilded Owls** team, and stages a session in **Preparing** — one operator "Start"
click from Active.

It prints the session you need — **copy the session code**:

```
  #156 manual-test session ready (Preparing):
    title: Target Map E2E
    session code: <COPY THIS>
    team: Gilded Owls — 3 targets with coordinates (Bogotá, Madrid, Tokyo)
    -> join on mobile as participant-1 / Gilded Owls, then Start as op-1 once staged.
```

## 3. Credentials

| Who         | Where                | Login                                           |
| ----------- | -------------------- | ----------------------------------------------- |
| Admin       | web `localhost:3000` | `admin-1` / `admin123`                          |
| Operator    | web `localhost:3000` | `op-1` / `operator123`                          |
| Participant | mobile app           | `participant-1@umbral.local` / `participant123` |

`participant-1` is pre-seeded onto the **Gilded Owls** team.

---

## 4. Test A — Operator dashboard: coordinate inputs + interactive map picker

This path tests the mission-editor map surface without a session — no mobile needed.

### Step A1 — Create a treasure-hunt mission

1. Sign in as **admin-1 / admin123**, open **Missions**, click **New mission**.
2. Give it a name (e.g. "Map Test"), set a time limit, click **Create**.
3. Expand the mission, add a **Stage**, then add a substage — pick **TreasureHunt**
   as the play mode.

### Step A2 — Add a target with the map picker

1. Under the treasure-hunt substage, fill **Name** and **QR code**.
2. In the **Location** section you see two coordinate inputs (Latitude / Longitude)
   and an interactive Leaflet map below them.
3. **Click the map** anywhere — a draft pin drops and the coordinate inputs update
   to the clicked location. Verify the numbers change.
4. Click **Add** to save the target. The target row shows `📍 lat, lng`.

### Step A3 — Verify the target list and map overview

1. Add 2–3 more targets with different coordinates (type directly, or click).
2. Below the target list, a read-only **Map overview** renders all placed targets
   as pins. Verify you see one pin per target.
3. One target with both coordinates typed as `0` (or left blank) should show
   **"No location set"** in its row — and its pin is absent from the overview
   (unplaced targets are not rendered on the map).

### Step A4 — Edit and clear coordinates

1. In a target row, click **Edit**. Change the coordinates and click **Save**.
   The row and overview update.
2. Edit a target, clear both coordinate fields (or leave blank), save. The row
   shows **"No location set"** and the pin disappears from the overview.

### Step A5 — Verify the map is isolated

- The interactive map renders inside a sandboxed `<iframe srcDoc sandbox="allow-scripts">`.
  There is no console error about Leaflet or OSM in the parent page's dev tools
  (verify: open browser DevTools → Console, no errors from the mission editor page).

✅ That covers the operator-side map editor.

---

## 5. Test B — Mobile: real map with ember pins on the treasure-hunt board

This path relies on the seed from step 1 (the mission already has coordinates).

### Step B1 — Participant (mobile): join the team

1. Sign in as **participant-1@umbral.local / participant123**.
2. Tap **Join your session** → enter the **session code** from step 2.
3. In the team lobby, pick **Gilded Owls** → join. You land in the team space.

### Step B2 — Operator (web): Start the session

1. Sign in as **op-1 / operator123**, open **Sessions**, pick **Target Map E2E**,
   click **Open live operation**.
2. Click **Start** (drives Preparing → Active). The active substage becomes the
   treasure-hunt substage; the participant's board flips to the treasure-hunt board.

### Step B3 — Verify the map (mobile)

The treasure-hunt board opens with the **MAP** tab selected by default:

1. You should see a **real Leaflet map** with OpenStreetMap tiles and **three
   ember pins** — one near Bogotá (Colombia), one near Madrid (Spain), and one
   near Tokyo (Japan). The map is centred on the first target (Bogotá).
2. Pinch-zoom and pan should work (WebView).
3. No crash, no blank WebView, no placeholder grid.

### Step B4 — Verify the empty state fallback

1. Use the existing **Treasure Hunt E2E** seed (HU-23), whose targets have no
   coordinates (all 0,0):

   ```bash
   cd frontend && pnpm exec playwright test tests/e2e/session-treasure-hunt-manual-seed.spec.ts
   ```

2. Copy the session code, join as participant-1 / Gilded Owls, and have the
   operator Start.
3. The MAP tab shows **"NO MAP LOCATION YET"** with the subtitle "This target
   has no place on the map." — no WebView, no crash.
4. The CLUES tab still shows the three seeded clues; only the map is empty.

✅ That covers the mobile map view.

---

## 6. Quick extra checks (optional)

| Check                      | How                                                                  | Expect                                                     |
| -------------------------- | -------------------------------------------------------------------- | ---------------------------------------------------------- |
| No crash on NaN coords     | Create a target with `NaN` via edit (not possible through UI input, but the guard exists) | `filter(isPlaceable)` drops it; map still renders others |
| Backend validates ±90/±180 | In the mission editor, type `91` for latitude and save               | Backend returns 400 with `target-latitude-out-of-range`    |
| Map overview on empty list | Delete all targets from a substage                                   | "No targets added." text or empty overview (no iframe)     |
| No coordinates → 0,0 sent  | Add a target leaving both coordinate fields blank (no map click)    | Target persists at (0,0); row shows "No location set"      |

## 7. Known limitations (by design — not bugs)

- **WebView-based map (not native `react-native-maps`).** Chosen to avoid a
  Google Maps API key and config-plugin requirement — runs the same in Expo Go
  and the dev client. OSM tiles load from CDN, so a network call is made.
- **No geofencing or proximity.** Coordinates are display/context only; QR-code
  scanning still owns target resolution (HU-31). Latitude and longitude are
  passed through to the board but never used for arrival detection.
- **No visual smoke-test was done this session.** The WebView map + OSM tiles
  have never been rendered on a device/emulator — the dev-client binary wasn't
  rebuilt after adding `react-native-webview` (a native autolinked dependency).
  Unit tests cover the component tree and generated HTML, but a live render is
  the first thing to verify in the next session with a device.
- **`npx expo install react-native-webview`** will reconcile the version pin
  (`13.16.0`) to Expo SDK 56's exact pin if desired. The current pin is a
  hand-pick that installed cleanly.
- **Timer reads `00:00 / Expired` and never ticks (DES-93).** Same limitation
  as all treasure-hunt substages — only trivia question windows drive the
  authoritative timer. The board still renders correctly; only the timer is
  inert.
- **Score and target progress stay 0 until HU-31.** Same as in the HU-23 seed.

## 8. Troubleshooting

- **Map shows blank WebView on mobile.** The dev-client binary must be rebuilt
  after adding `react-native-webview`. Run `npx expo run:android` / `run:ios`
  or trigger an EAS dev build. The existing binary won't load the WebView
  native module.
- **OSM tiles don't load.** The mobile device must have network access to
  `https://tile.openstreetmap.org`. The WebView also needs `domStorageEnabled`
  (set by `TargetMap`). If on a restricted network, tiles won't render — but
  the empty state is still the graceful fallback for 0 coordinates.
- **Map overview in the operator dashboard is blank.** The `<iframe sandbox>`
  may block scripts in some browser setups. Check the DevTools Console — no
  error means the iframe loaded but tiles failed (network).
- **WebView shows a text string with escaped HTML (no map).** There is a
  name-injection guard (`<` escaped to `&lt;`) in `buildTargetMapHtml`. If you
  see raw HTML as text inside the WebView instead of a map, the `source={{ html }}`
  prop may not be working — verify `react-native-webview` is installed and
  autolinked.
- **Mobile can't reach the backend.** It targets the gateway on `localhost:8000`;
  on a physical device use your machine's LAN IP, not `localhost`.

---

## Appendix — the seed spec

The seed run in step 1 lives at
`frontend/tests/e2e/session-target-map-manual-seed.spec.ts` (keep this doc in
sync if it changes). It mirrors `session-treasure-hunt-manual-seed.spec.ts`
(HU-23) with one delta: `MissionTargets` rows carry `"Latitude"` and
`"Longitude"` columns so the runtime snapshot includes `ActiveTargetDto`
coordinates for the Map tab. Three targets at Bogotá (4.71, -74.07), Madrid
(40.42, -3.70), and Tokyo (35.68, 139.65) produce three distinct ember pins
spread across the globe.

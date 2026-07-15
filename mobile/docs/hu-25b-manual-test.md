# HU-25B — Manual Test: participant ranking + live updates

Goal: verify the mobile **TEAMS** tab shows the real session ranking, and that the
standings **change live** (no pull-to-refresh) when a team's score moves.

The key thing to understand before you start: **nobody "awards points" from a
button.** A treasure-hunt score changes only when a **participant scans a target
QR**. That scan resolves the target in `session-operations-service`, which
publishes a `TargetResolved` fact on RabbitMQ; `scoring-monitoring-service`
consumes it, records a score entry, recomputes the ranking, and broadcasts
`RankingChanged` over SignalR. So to move a score you act **as the participant**
(scan a target), not as the operator. §4 Step C gives you two ways to do that —
the in-app camera scanner, and a camera-free `curl` that hits the exact same
endpoint.

You need the backend stack up (scoring-monitoring-service with its `ScoringHub`
at `/hubs/scoring`, the gateway routing it), the mobile app pointed at the
gateway, and the HU-25B session seeded (§1).

> **Dependency:** live target scoring requires the scoring service's
> `TargetResolvedConsumer` (Application/Scores/Consumers). Without it a scan still
> resolves and returns 200, but the score never moves because the `TargetResolved`
> event has no consumer. If scores don't budge after a 200 scan, see §7.

---

## 1. Bring up the stack (once)

```bash
# 1. Full backend — includes scoring-monitoring-service + gateway.
#    Use down -v to wipe volumes for a clean slate (fresh Keycloak realm, etc.).
cd backend && docker compose up -d

# 2. Seed the test data via the e2e seed spec.
cd ../frontend && npx playwright test tests/e2e/hu-25b-ranking-manual-seed.spec.ts

# 3. Mobile app (separate terminal).
cd ../mobile && pnpm install && pnpm start    # open in Expo Go / emulator
```

The seed spec creates a treasure-hunt mission **"HU-25B Ranking E2E"** (one stage,
one TreasureHunt substage), assigns operator **op-1**, registers teams **Gilded
Owls** and **Crimson Foxes**, leaves the session in **Preparing** (so the operator
can Start on demand), and pre-seeds ranking data directly into the
`scoring_monitoring` database so the TEAMS tab has real rows from the first render.

**The seed's console output prints the values you'll need** — copy them:
`session code`, `live session id`, and the team ids.

### What the seed defines

| Team | Reference team id | Seeded baseline (before any scan) |
|---|---|---|
| **Gilded Owls** (participant-1's team) | `a0000000-0000-0000-0000-000000000001` | 350 pts, position 1 |
| **Crimson Foxes** | `a0000000-0000-0000-0000-000000000002` | 200 pts, position 2 |

The substage has **two scannable targets**. Scanning one adds its score **on top
of** the seeded baseline — that's what produces the live jump you're testing:

| Target | QR payload (`scannedValue`) | Score | Notes |
|---|---|---|---|
| **Astrolabe** | `HR-25B-QR-1` | **+150** | Clue visible when the substage starts |
| **Tapestry** | `HR-25B-QR-2` | **+100** | Clue hidden until the operator releases it (the clue is only a hint — the target is scannable regardless) |

> A target resolves once. Re-scanning the same QR returns **422** (duplicate). To
> add a second increment, scan the **other** target.

---

## 2. How the chain works (context)

Scanning a target moves the score through this chain — the mobile push is the last link:

| Step | Where | What happens |
|---|---|---|
| 1. Scan | mobile → `session-operations-service` | `POST /participants/target-scans` resolves the `Target`; raises `TargetResolvedEvent` carrying the score |
| 2. Publish | session-operations-service | Outbox publishes `TargetResolvedIntegrationEvent` to RabbitMQ (exchange `session-target-resolved`) — **score never travels on the HTTP response, only on this fact** |
| 3. Consume | scoring-monitoring-service | `TargetResolvedConsumer` → `RecordScoreEntryCommand` (`ScoreSourceType.TargetResolution`) writes a `score_entries` row |
| 4. Recompute | scoring-monitoring-service | New entry raises `ScoreEntryRegistered` → `RecalculateRankingCommand` folds all entries into a fresh ranking |
| 5. Broadcast | scoring-monitoring-service | `RankingBroadcaster` pushes `"RankingChanged"` with `RankingSnapshotDto` to the `live-session:{id}` group on `ScoringHub` |
| 6. Render | mobile | `ScoringHubClient` receives the push → `useRanking` updates the snapshot; TEAMS tab re-renders |

The trivia path is the same chain with `AnswerRegisteredConsumer` at step 3 — but
this seeded mission has **no trivia**, so target scans are your only trigger here.
Push payloads are filtered by `liveSessionId` to prevent cross-session leaks (§4
Step D).

---

## 3. Credentials

| Who | Where | Login (username / password) |
|---|---|---|
| Participant | mobile app | `participant-1@umbral.local` / `participant123` — Gilded Owls |
| Operator | web UI `localhost:3000` | `op-1@umbral.local` / `operator123` |
| Keycloak admin | console `localhost:8080` | `admin` / `admin` |

Accounts are created by the frontend global-setup (`tests/setup/global-setup.ts`).
The mobile login form accepts the **email** as the identifier. For a **Keycloak
direct-grant token** (the `curl` path in §4 Step C), use the bare username
`participant-1` with client `umbral-web` — that's what the working e2e specs use.

---

## 4. Run it

### Step A — Join the seeded live session

1. Launch the app → sign in as `participant-1@umbral.local` / `participant123`.
2. Tap **Join session** and enter the **session code** printed by the seed spec.
3. The app reconnects to both hubs (`SessionsHub` + `ScoringHub`) and fetches the
   initial REST ranking snapshot.
4. **Operator Starts the session** (this is the operator's real job — Start it, not
   "award points"). Either:
   - Web UI: sign in at `localhost:3000` as `op-1@umbral.local` / `operator123` and **Start**; or
   - API: transition the session to **Active** (see the Start `curl` in Step C.0).
   Scans are rejected with **409** until the session is **Active**.
5. Once the treasure-hunt board appears, open the **TEAMS** tab (third tab).

### Step B — Confirm the seeded ranking renders

1. TEAMS renders a `PodiumLeaderboard`: 2nd left, 1st center (tallest), 3rd right,
   plus a scrollable list for positions 4+.
2. You should see the **baseline**: **Gilded Owls 350 (1st)**, **Crimson Foxes 200 (2nd)**.
3. Your own team (Gilded Owls) is highlighted with an ember accent and a `"(You)"` suffix.
4. Empty-state (only if you point at a session with no scoring): trophy emoji +
   *"Standings will appear once the round begins."*
5. Three distinct states, don't confuse them: **podium** (rows loaded) ·
   **trophy empty-state** (fetch succeeded, zero rows) · **error card + RETRY**
   (fetch failed — e.g. ranking endpoint 500, see §7). The old
   *"Sample standings — not live yet"* placeholder cards now mean only *"still
   loading the first snapshot"* — if they persist, the fetch is stuck, not empty.

### Step C — Change a score and watch it update live

Pick **one** path. Both hit the same endpoint and drive the same live update. Keep
the mobile TEAMS tab visible while you do it.

#### C.0 — Prerequisite: make sure the session is Active

If you didn't Start via the web UI, do it by API (operator token):

```bash
OP_JWT=$(curl -s -X POST http://localhost:8080/realms/umbral/protocol/openid-connect/token \
  -H 'Content-Type: application/x-www-form-urlencoded' \
  -d 'grant_type=password&client_id=umbral-web&username=op-1&password=operator123' | jq -r .access_token)

LIVE_SESSION_ID="<paste from seed output>"

curl -i -X PATCH "http://localhost:8000/api/sessions/$LIVE_SESSION_ID/state" \
  -H "Authorization: Bearer $OP_JWT" -H 'Content-Type: application/json' \
  -d '{"targetState":"Active"}'          # expect 200/204
```

#### Path 1 — In-app camera scanner (real UI; needs a camera)

The scanner is **camera-only** (there's no manual-entry field), so you need a device
with a working camera — a **physical phone in Expo Go** is the reliable choice
(iOS Simulator has no camera; the Android emulator needs a virtual-camera scene).

1. Display a QR that **encodes the exact string** `HR-25B-QR-1` (Astrolabe) — e.g.
   generate one with `qrencode -o qrs/hu25b-qr-1.png -s 16 'HR-25B-QR-1' && ls -l qrs/hu25b-qr-1.png`
   so it saves somewhere obvious and the PNG is comfortably larger than a small default
   QR (typically 400x400 or bigger, depending on the payload and margin), then show it
   on your computer screen. `qrencode` is quiet on success, so the trailing `ls` is the
   confirmation that the file was written. (The repo's `qrs/*.png` may already hold
   these, but only trust one whose decoded text equals the seed's `QrCode` value.)
2. On the board, open the target scanner and point the camera at the QR.
3. The scanner shows **"TARGET RESOLVED"** on success (or the backend's reason on reject).
4. Watch TEAMS: **Gilded Owls jumps 350 → 500** and re-renders live. Scan `HR-25B-QR-2`
   for another **+100 → 600**.

#### Path 2 — `curl` the scan endpoint (no camera; fastest, emulator-friendly)

This is the reliable way when you *"can't emit events yourself in the UI."* It posts
the same payload the camera would, as the participant:

```bash
PART_JWT=$(curl -s -X POST http://localhost:8080/realms/umbral/protocol/openid-connect/token \
  -H 'Content-Type: application/x-www-form-urlencoded' \
  -d 'grant_type=password&client_id=umbral-web&username=participant-1&password=participant123' | jq -r .access_token)

LIVE_SESSION_ID="<paste from seed output>"

curl -i -X POST "http://localhost:8000/api/sessions/$LIVE_SESSION_ID/participants/target-scans" \
  -H "Authorization: Bearer $PART_JWT" -H 'Content-Type: application/json' \
  -d '{"teamId":"a0000000-0000-0000-0000-000000000001","scannedValue":"HR-25B-QR-1","token":null}'
```

- **200** `{ "isResolved": true, ... }` → the target resolved. Within a moment the
  mobile TEAMS tab updates on its own: **Gilded Owls 350 → 500**, positions recompute.
- **422** `type: target-scan-rejected` → the QR was unknown, out-of-context, or already
  resolved (re-scan). **409** → session isn't Active (do C.0). **403** → the `teamId`
  isn't the caller's team in this session (it should be Gilded Owls
  `a0000000-…-0001`; if it still rejects, use the teamId the app shows for your team).
- Run it again with `"scannedValue":"HR-25B-QR-2"` for **+100 → 600**.

> To make a team **overtake** another (podium reorder), score the *other* team:
> grab a token for a participant on Crimson Foxes and scan against
> `teamId a0000000-…-0002`, or just scan both targets for Gilded Owls and watch the
> gap widen.

### Step D — Verify cross-session isolation

1. Open a **second** session's team space (different `liveSessionId`).
2. Trigger a scan in the **first** session (Step C).
3. Confirm the **second** session's ranking is **not** affected.

---

## 5. SignalR verification (optional — via browser dev tools)

If you have a browser-based SignalR client (the web UI, or a small console app):

```javascript
// 1. Connect to the ScoringHub with a participant JWT
const connection = new signalR.HubConnectionBuilder()
  .withUrl("http://localhost:8000/hubs/scoring", {
    accessTokenFactory: () => "<participant-jwt>"
  })
  .build();

// 2. Join the session group
await connection.start();
await connection.invoke("JoinSessionGroup", "<liveSessionId-guid>");

// 3. Subscribe to the ranking event
connection.on("RankingChanged", (snapshot) => {
  console.log("Ranking snapshot received:", snapshot);
});

// 4. Now trigger a scan (§4 Step C) — the callback fires with the new rows
```

Expected `snapshot` shape when it arrives:

```json
{
  "liveSessionId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "generatedAt": "2026-07-15T12:00:00+00:00",
  "calculationVersion": 2,
  "rows": [
    {
      "teamId": "a0000000-0000-0000-0000-000000000001",
      "teamDisplayName": "Gilded Owls",
      "position": 1,
      "totalScore": 500,
      "resolutionTime": "01:23:45"
    }
  ]
}
```

---

## 6. Quick checks

| Check | How | Expect |
|---|---|---|
| **Score updates on scan** | Scan `HR-25B-QR-1` (Step C, either path) with TEAMS visible | Gilded Owls 350 → 500 live, no manual refresh |
| **REST fallback** | Stop `scoring-monitoring-service`, then refresh the TEAMS tab | Ranking still loads from `GET /api/sessions/{id}/ranking?teamId=...` |
| **Empty state** | Point at a session with no scoring activity | Trophy emoji + *"Standings will appear once the round begins."* |
| **Own-team highlight** | Find your team in the list | Ember accent / "(You)" on Gilded Owls' row |
| **Duplicate scan** | Scan the same target twice | Second scan is **422** (target already resolved); score doesn't double |
| **Reconnect recovery** | Airplane mode briefly, then reconnect | ScoringHub auto-reconnects; `onreconnected` re-joins the session group |
| **Session switch** | Leave and join a different session | Hub leaves the old group, joins the new one; ranking fetches the new session's data |

---

## 7. Troubleshooting

These are the failures that block the flow. Most are backend/deploy issues, not
mobile bugs — check them before suspecting the app.

| Symptom | Cause | Fix |
|---|---|---|
| **Scan returns 200 but the score/ranking never changes** | Two distinct causes: **(1)** `scoring-monitoring-service` has no consumer for `TargetResolvedIntegrationEvent` (the fact is dropped); or **(2)** the consumer exists but its contract's **message-type URN** doesn't match the publisher's, so MassTransit routes every event to the `TargetResolved_skipped` queue unconsumed. Cause (2) bites when the two services declare the record in **different namespaces** — `[EntityName]` only pins the *exchange*, not the type URN. | First check `docker compose exec rabbitmq rabbitmqctl list_queues name messages` — a non-zero `TargetResolved_skipped` (or `AnswerRegistered_skipped`) is the tell for cause (2). Fix (2): pin the URN on the scoring-side record, e.g. `[MessageUrn("umbral_backend.Application.Sessions.Common:TargetResolvedIntegrationEvent")]` (omit the `urn:message:` prefix — MassTransit prepends it). Fix (1): ensure `TargetResolvedConsumer` exists (Application/Scores/Consumers) and is registered — `bus.AddConsumer<TargetResolvedConsumer>()` in `MassTransitMessagingRegistration.cs`. Cold-rebuild scoring either way: `docker compose up -d --build scoring-monitoring-service`. |
| **Scan returns 409** | Session isn't **Active** — scans are blocked in Preparing/Paused/Finished/Cancelled | Start the session (§4 Step C.0) so it transitions to `Active`. |
| **Scan returns 422** | Retained rejection: unknown QR, target outside the active substage, or already resolved | Use the exact `scannedValue` (`HR-25B-QR-1` / `HR-25B-QR-2`); a duplicate is expected on a second scan of the same target. |
| **Scan returns 403** | `teamId` in the body isn't the caller's team in this live session (membership guard compares `ReferenceTeamId`) | Use Gilded Owls `a0000000-0000-0000-0000-000000000001`, or the teamId the app shows for your team. |
| **TEAMS shows 0 scores**; `GET .../ranking` returns **403** | Ranking membership guard compared the wrong team id | Guard compares `ReferenceTeamId` (`ValidateParticipantSessionMembershipQueryHandler.cs`). Ensure session-ops runs the current build. |
| **No podium — TEAMS shows the "Sample standings — not live yet" placeholder cards**; `GET .../ranking` returns **500** with `TypeLoadException: Could not load type 'Invalid_Token.0x…'` | `dotnet watch` in `scoring-monitoring-service` corrupted its hot-reload state on a *rude edit* (new type + DI registration, a migration, or a changed DTO shape — e.g. the `RankingSessionMembershipGuard` / `AddTeamDisplayNameToRankingRow` work). `/health` still reports `Healthy`, so the container looks fine while every ranking request 500s. The mobile fetch rejects, `snapshot` stays `null`, and the TEAMS tab falls back to placeholder cards. | Cold-restart (or rebuild) scoring so it loads a freshly-compiled assembly: `docker compose restart scoring-monitoring-service` (or `up -d --build scoring-monitoring-service`). Verify: `curl -s -o /dev/null -w '%{http_code}' "http://localhost:8000/api/sessions/$LIVE_SESSION_ID/ranking?teamId=…" -H "Authorization: Bearer $PART_JWT"` → **200** with rows. Rule of thumb: after adding types/DI/migrations to a service, cold-rebuild it rather than trusting hot reload. |
| **Live updates never arrive** and the app logs a SignalR `Connection disconnected with error 'Error: … while the application is running.'` right after join (podium still renders — REST works, only pushes are dead) | Same `dotnet watch` hot-reload trap as the row above, different blast radius. A **rude edit to `ScoringHub`'s constructor** (e.g. the Part B `IRankingSessionMembershipGuard` + `CurrentUserContext` injection) can't be hot-applied, but watch still logs `🔥 Hot reload succeeded`. DI's compiled activator keeps calling the deleted parameterless ctor, so **every** connection throws `HotReloadException: Attempted to invoke a deleted method implementation… at ScoringHub..ctor()` in `OnConnectedAsync`; the server closes the socket with that text attached, which the client reports through `_stopConnection` — so it **reads like a client-side crash but is thrown server-side**. `/health` stays `Healthy`. Confirm with `docker compose logs scoring-monitoring-service \| grep HotReloadException`. | `docker compose restart scoring-monitoring-service`. **`up -d --build` is a no-op here** — the source is bind-mounted and the image is unchanged, so Compose leaves the container running; only restarting the `dotnet watch` process yields a freshly compiled assembly. Then reload the JS and rejoin (the app needs a new connection). Rule of thumb: after touching a hub's ctor or its DI, restart the service — hot reload's success message doesn't mean the edit took. |
| **Live updates never arrive**; `/hubs/scoring/negotiate` returns **404** | Gateway matched `/hubs/scoring` exactly, so `/negotiate` fell through to session-ops (no ScoringHub) | Gateway route must be `"/hubs/scoring/{**catch-all}"`. `curl /hubs/scoring/negotiate?negotiateVersion=1` → **401**, not 404. |
| **"You don't belong to this team"** on join; identity-access **404** for `/api/permissions/participant-eligible-teams` | Endpoint missing from the running binary (`dotnet watch` compiled but didn't register the new action) | Cold-rebuild: `docker compose up -d --build identity-access-service`. Verify: no headers → 401, valid participant → 200. |
| Same team shows **two different names** (podium vs lobby) | Stale `registered_teams` row under an old name | Fixed in the seed (upserts the reference-catalog name). Re-run the seed spec to refresh. |

---

## 8. What changed

| File | Change |
|---|---|
| `backend/.../scoring-monitoring-service/.../Scores/Consumers/TargetResolvedConsumer.cs` | **New** — consumes `TargetResolvedIntegrationEvent` and records a `RecordScoreEntryCommand` (`ScoreSourceType.TargetResolution`). Closes the treasure-hunt write path so scans actually score (trivia already had `AnswerRegisteredConsumer`). |
| `backend/.../scoring-monitoring-service/.../Scores/Common/TargetResolvedIntegrationEvent.cs` | **New** — consumer-side contract, `[EntityName("session-target-resolved")]` bound to the publisher's exchange. |
| `backend/.../scoring-monitoring-service/.../Infrastructure/Messaging/MassTransitMessagingRegistration.cs` | Registered `TargetResolvedConsumer`. |
| `mobile/src/lib/realtime/scoring-hub.ts` | **New** — SignalR client for `/hubs/scoring`: `onRankingChanged`, `joinSessionGroup`, auto-reconnect with group re-join. |
| `mobile/src/lib/realtime/use-ranking.ts` | Optional 4th param `scoringClient`. When present, subscribes to `RankingChanged` pushes filtered by `liveSessionId`. |
| `mobile/src/app/(app)/team-space.tsx` | `LiveTeamSpace` creates a `ScoringHubClient`, joins the session group on mount, tears down on unmount, passes it to `useRanking`. Degrades gracefully when the hub is unreachable. |
| `mobile/src/__tests__/ranking-hook.test.ts` | Tests: push replaces snapshot, different session ignored, unsubscribe on unmount, no subscription when client is null. |
| `mobile/src/components/treasure-hunt-board.tsx` | TEAMS tab now renders a distinct **error card + RETRY** (via `rankingError` / `onRetryRanking`, mapped by `rankingErrorCopy`) when the ranking fetch fails with no usable snapshot — so a 500 no longer masquerades as the "no standings yet" placeholder. A stale snapshot with rows still wins over an error. |
| `mobile/src/app/(app)/team-space.tsx` | Passes `useRanking`'s `error` / `refetch` through to the board as `rankingError` / `onRetryRanking`. |
| `frontend/tests/e2e/hu-25b-ranking-manual-seed.spec.ts` | **New** — seeds the mission, session, two teams, and baseline ranking for this manual test. |

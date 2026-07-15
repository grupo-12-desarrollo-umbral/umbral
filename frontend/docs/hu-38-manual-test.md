# HU-38 — Manual Test: operator applies a justified penalty, team score drops

Goal: an **operator** (web) applies a penalty **with a justification** to a team
during an **Active** session, and the team's total **drops by 100** — the entry
persists, RabbitMQ recalculates the ranking, and the new total is DB-visible.

You only need the operator on `localhost:3000` and the backend on the gateway
(`localhost:8000`); no mobile is required, since this HU is about the **write** path
and §5 asserts it against the DB.

If you also want to *see* the participant side — the team's total dropping live and the
**PENALTY APPLIED** toast — bring up the mobile app too and follow the optional
**Step A** and **Step E**. Order matters: the device must be on the board **before** the
penalty is applied, or you get the new total with no toast (see the note in Step A).

> Prefer to not do it by hand? The operator surface has an automated live e2e:
> `pnpm exec playwright test tests/e2e/hu-38-operator-penalty.spec.ts` (drives the
> real PenaltyPanel and asserts the −100 note plus the 350→250 recalc against the
> real gateway). This manual guide walks the same click-path by eye.

---

## 1. Bring up the stack (once)

```bash
# backend + gateway + Keycloak + RabbitMQ
cd backend && docker compose up -d

# operator web app
cd ../frontend && pnpm install && pnpm dev       # -> http://localhost:3000

# seed a startable two-team session with a pre-loaded 350/200 ranking (needs the backend up)
cd frontend && pnpm exec playwright test tests/e2e/hu-25b-ranking-manual-seed.spec.ts

# (optional) mobile app, separate terminal — only for Steps B + E
cd mobile && pnpm install && pnpm start          # open in Expo Go / emulator
```

## 2. Grab the seeded session code

The seed stages a treasure-hunt session in **Preparing**, one operator "Start" click
from Active, with **two** teams and a pre-computed ranking (**Gilded Owls 350**,
**Crimson Foxes 200**) so the penalty has a real total to subtract from.

It prints the session you need — **copy the session code _and_ the live session id**:

```
  HU-25B manual-test session ready (Preparing):
    session code: <COPY THIS>
    live session id: <COPY THIS TOO>
    ranking: pre-seeded (Team A=350, Team B=200)
```

## 3. Credentials

| Who                                   | Where                | Login (username / password)                                   |
| ------------------------------------- | -------------------- | ------------------------------------------------------------- |
| Operator                              | web `localhost:3000` | `op-1` / `operator123`                                         |
| Participant *(optional, Steps B + E)* | mobile app           | `participant-1@umbral.local` / `participant123` — Gilded Owls |

Penalties are **operator-only** — an admin session is rejected by the server action.

Accounts come from the frontend global-setup (`tests/setup/global-setup.ts`). The mobile
login form takes the **email** as the identifier; for a Keycloak direct-grant token
(`curl`) the bare username `participant-1` also works, with client `umbral-web`.

> Ignore the `participant-1 / tester123` line in the seed spec's header comment — that
> password is wrong (it 401s), and the "sub" beside it is actually the Gilded Owls team
> id. The real password is `participant123`, and the sub is minted per-environment by
> Keycloak.

## 4. Authorize the operator in scoring (now automatic)

`scoring-monitoring-service` checks a `session_operator_assignments` projection before
accepting a penalty. **This projection now fills itself:** assigning an operator (which the
HU-25B seed does) raises `LiveSessionOperatorAssignedEvent`, which
`session-operations-service` publishes as `LiveSessionOperatorAssignedIntegrationEvent` on the
`session-operator-assigned` exchange; scoring's `LiveSessionOperatorAssignedConsumer` upserts the
row keyed by the operator's **Keycloak sub**. So on an up-to-date stack you can skip straight to
Step 5 — no manual patch.

> The services run `dotnet watch` over bind-mounted source, and the publisher wiring is a "rude"
> ctor/DI edit that hot-reload cannot apply. If your containers were **already running** before this
> change landed, restart both once so they pick it up:
> `cd backend && docker compose restart session-operations-service scoring-monitoring-service`,
> then **re-seed** (Step 1) so the assignment is published under the new wiring.

**Manual fallback.** If the **Apply** click still 403s with "You are not authorized to penalize
teams in this session" — e.g. a session seeded before the restart, or you want to authorize a row
by hand — patch it directly. The projection key is the operator's **Keycloak sub**. **Substitute
your real live session id for `PASTE_LSID`**:

```bash
OP_SUB=$(docker exec backend-postgres-1 psql -U postgres -d identity_access -t -A -c \
  "SELECT \"ExternalIdentityId\" FROM users WHERE \"Email\"='op-1@umbral.local'")

docker exec backend-postgres-1 psql -U postgres -d scoring_monitoring -c \
  "INSERT INTO session_operator_assignments (live_session_id, assigned_operator_user_id) \
   VALUES ('PASTE_LSID', '$OP_SUB') \
   ON CONFLICT (live_session_id) DO UPDATE SET assigned_operator_user_id = EXCLUDED.assigned_operator_user_id;"
```

## 5. Run it

### Step A — (Optional) Participant (mobile): join and verify the baseline ranking

Skip this if you only care about the write path. Do it **now**, while the seeded session is
still **Preparing**. A first participant join after the session is already **Active** is
rejected, so don't leave this until after Step B.

1. Launch the app → sign in as **`participant-1@umbral.local` / `participant123`**.
2. Tap **Join your session** → type the **session code** from the seed output into
   **Session Code** → tap **View teams**.
3. In the team lobby, tap **Gilded Owls** (participant-1's team — the one the seed gives
   the 350 baseline).
4. The app lands on the team space and connects to both hubs (`SessionsHub` +
   `ScoringHub`), fetching the initial REST ranking snapshot.
5. Open the **TEAMS** tab and verify the initial render, not just that the screen opens:
   **Gilded Owls 350 (1st)**, **Crimson Foxes 200 (2nd)**, and your own team accented with
   a `(You)` suffix.
6. Treat this as the mobile baseline assertion: the participant must see a real ranking
   snapshot before the penalty is applied, with no manual refresh.

If the TEAMS tab instead shows **"Standings will appear once the round begins."**, that is
the mobile empty-state for a successful ranking fetch with **zero rows**. In this HU that is
**not expected**: the reused HU-25B seed preloads a **350 / 200** ranking before you ever
press **Start**, so that message means you are not looking at the seeded ranking snapshot for
this session.

If you want the API equivalent of the mobile join flow, run it **before** Step B:

```bash
PART_JWT=$(curl -s -X POST http://localhost:8080/realms/umbral/protocol/openid-connect/token \
  -H 'Content-Type: application/x-www-form-urlencoded' \
  -d 'grant_type=password&client_id=umbral-web&username=participant-1&password=participant123' | jq -r .access_token)

SESSION_CODE="<paste from seed output>"

curl -s "http://localhost:8000/api/sessions/by-code/$SESSION_CODE/teams/lobby" \
  -H "Authorization: Bearer $PART_JWT"

# Copy Gilded Owls' runtime teamId from the lobby response, then join that team.
RUNTIME_TEAM_ID="<paste Gilded Owls teamId from lobby response>"

curl -i -X POST "http://localhost:8000/api/sessions/by-code/$SESSION_CODE/teams/$RUNTIME_TEAM_ID/join" \
  -H "Authorization: Bearer $PART_JWT"
```

Expect the lobby call to return both teams and the join call to return **200** with a
`teamMembershipId`. After that, the same participant can enter the team space in the app.

If you want QR images for the two HU-25B treasure-hunt targets while checking the seeded
session itself, generate them with:

```bash
mkdir -p qrs && qrencode -o qrs/hu25b-qr-1.png -s 16 'HR-25B-QR-1' \
  && qrencode -o qrs/hu25b-qr-2.png -s 16 'HR-25B-QR-2' \
  && ls -l qrs/hu25b-qr-*.png
```

Those QRs are **not required** to verify HU-38's penalty path, because the ranking baseline is
seeded directly into the DB. They are only useful if you want to sanity-check the underlying
HU-25B session by driving target scans too.

> **Why the order matters.** The toast fires on a *decrease* from a total the app has
> already seen — `useScoreDrop` deliberately stays silent when there is no previous value
> (`score-drop-toast.tsx`). If you join *after* the penalty, the first snapshot is already
> 250 and you get a correct board with **no toast**, which looks like a bug and isn't. Have
> the device sitting on 350 before Step C.

### Step B — Operator (web or API): start the session

1. Sign in as **op-1 / operator123**, open **Sessions**, pick **HU-25B Ranking E2E**,
   click **Open live operation**.
2. Click **Start** (drives Preparing → Active). The **Penalty** panel
   (`penalty-panel`) now renders its form; before Active it shows only the
   `penalty-inactive` note.

If you want to activate the session without the web UI, use the same **gateway** state
transition the operator screen calls:

```bash
OP_JWT=$(curl -s -X POST http://localhost:8080/realms/umbral/protocol/openid-connect/token \
  -H 'Content-Type: application/x-www-form-urlencoded' \
  -d 'grant_type=password&client_id=umbral-web&username=op-1&password=operator123' | jq -r .access_token)

LIVE_SESSION_ID="<paste from seed output>"

curl -i -X PATCH "http://localhost:8000/api/sessions/$LIVE_SESSION_ID/state" \
  -H "Authorization: Bearer $OP_JWT" \
  -H 'Content-Type: application/json' \
  -d '{"targetState":"Active"}'
```

Expect **200** or **204**. Once the session is **Active**, the penalty form is available.

Older notes that post directly to a service port are stale for this flow. For HU-38, prefer the
gateway on `localhost:8000`; old `localhost:5003` penalty examples are wrong for the current repo
layout.

### Step C — Operator (web): apply the penalty

In the operator hero, the **Penalty** panel:

1. Pick **Gilded Owls** in the **Team** selector.
2. Type a **Reason** (e.g. "Used a phone during a no-device substage.") — the button
   stays disabled until both a team and a non-empty reason are set.
3. Click **Apply penalty**.

You must see the success note: **"Penalty applied: −100 pts."** (`penalty-success`),
and no error. The 100 is the flat entry magnitude, not the team total.

If you want the API equivalent of the penalty action, post it through the **gateway** too:

```bash
OP_JWT=$(curl -s -X POST http://localhost:8080/realms/umbral/protocol/openid-connect/token \
  -H 'Content-Type: application/x-www-form-urlencoded' \
  -d 'grant_type=password&client_id=umbral-web&username=op-1&password=operator123' | jq -r .access_token)

LIVE_SESSION_ID="<paste from seed output>"

curl -i -X POST "http://localhost:8000/api/sessions/$LIVE_SESSION_ID/penalties" \
  -H "Authorization: Bearer $OP_JWT" \
  -H 'Content-Type: application/json' \
  -d '{"teamId":"a0000000-0000-0000-0000-000000000001","reason":"Used a phone during a no-device substage."}'
```

Use the penalized team's **referenceTeamId** in the body. For Gilded Owls, that is
`a0000000-0000-0000-0000-000000000001`. Expect success only after Step 4 has patched the
scoring-side `session_operator_assignments` row.

### Step D — Verify the recalculation (DB)

The penalty raises `ScoreEntryRegistered`; the consumer recalculates the ranking.
**Substitute your live session id for `PASTE_LSID`**:

```bash
docker exec backend-postgres-1 psql -U postgres -d scoring_monitoring -t -A -c \
  "SELECT rr.team_display_name, rr.total_score, r.calculation_version \
   FROM rankings r JOIN ranking_rows rr ON rr.ranking_id = r.id \
   WHERE r.live_session_id = 'PASTE_LSID' \
   ORDER BY r.calculation_version DESC, rr.position;"
```

Expect **Gilded Owls | 250** (350 − 100, **subtracted** not added) at
**`calculation_version = 2`**, name intact — and **Crimson Foxes | 200** unchanged.

That subtraction landing on the correct team with the version bumped — no manual
recalc — is HU-38 working. ✅

### Step E — (Optional) Participant (mobile): verify the live score drop

If you did Step A, the device you left on the TEAMS tab updates **on its own** within a
moment of the Apply click — no pull-to-refresh, no rejoin:

1. Verify the ranking changes **live**: **Gilded Owls drops 350 → 250** and the podium
   re-renders without any manual refresh. Crimson Foxes stays 200, so the Owls keep 1st —
   the *number* moves, the order doesn't.
2. Verify the participant-visible feedback too: a toast slides in at the top,
   **PENALTY APPLIED** / **"Score dropped by 100 pts"** (`score-drop-toast`), red-accented,
   auto-dismissing after ~4s (tappable to dismiss). VoiceOver/TalkBack announces
   *"Penalty applied. Score dropped by 100 points."*
3. This is the mobile proof for HU-38: the participant sees both the updated ranking value
   and the penalty toast from the SignalR push path, with no polling.

The toast renders at screen level, so it fires on **whichever tab** you're on — the TEAMS
tab is only where you also watch the number itself move.

That's the whole chain, and no part of it is polled:

| Step | Where | What happens |
|---|---|---|
| 1. Apply | web → scoring-monitoring-service | `POST /api/sessions/{id}/penalties` → `ApplyPenaltyCommand` writes a **Penalty** `score_entries` row (flat −100) |
| 2. Recompute | scoring-monitoring-service | Entry raises `ScoreEntryRegistered` → RabbitMQ → `ScoreEntryRegisteredConsumer` → `RecalculateRankingCommand` folds every entry into a fresh ranking (`calculation_version` bumps) |
| 3. Broadcast | scoring-monitoring-service | `Ranking.Refresh` raises `RankingRefreshed` → `BroadcastRankingRefreshedHandler` → `RankingBroadcaster` pushes `"RankingChanged"` to the `live-session:{id}` group on `ScoringHub` |
| 4. Render | mobile | `ScoringHubClient` receives it → `useRanking` swaps the snapshot → `useScoreDrop` sees 350 → 250 → `ScoreDropToast` |

The push keys rows on **`referenceTeamId`**, the same id `team-space.tsx` matches your team
on — which is exactly what the Team-id note below is about.

> **Team-id note.** The penalty must land on Gilded Owls' **referenceTeamId**
> (`a0000000-0000-0000-0000-000000000001`) — the axis scoring/ranking key on — not the
> runtime session-scoped team id the operator panel also carries. The `PenaltyPanel` sends
> the referenceTeamId for you; a build predating that fix files the penalty under the runtime
> id, leaving Gilded Owls at 350 and creating a stray ranking row under a different GUID (see
> Troubleshooting).

## 6. Quick extra checks (optional)

| Check              | How                                                              | Expect                                                                 |
| ------------------ | ---------------------------------------------------------------- | ---------------------------------------------------------------------- |
| Mobile baseline *(optional)* | Do Step A before the penalty                                      | TEAMS shows **Gilded Owls 350 (1st)**, **Crimson Foxes 200 (2nd)**, and **(You)** on Gilded Owls |
| Mobile live drop *(optional)* | Leave the participant on the app, then do Step C                  | TEAMS updates **350 → 250** on its own and the **PENALTY APPLIED** toast appears |
| Reason required    | Clear the reason, try to submit                                  | **Apply** disabled; if forced, `penalty-error`: "requires an explicit reason" |
| Below-zero clamp   | Apply penalties to a team until its total would go under 100     | Ranking floors at **0** (no crash); `scoring` logs show no `ArgumentOutOfRangeException`, the `ScoreEntryRegistered_error` queue stays at 0 |
| Active gate        | Before Start (Preparing), open the panel                        | `penalty-inactive` note, **no** form                                   |
| Not authorized     | Skip step 4, then Apply                                          | `penalty-error`: "not authorized to penalize teams in this session"    |
| Queue drained      | RabbitMQ mgmt `localhost:15672` (guest/guest) after Apply        | `ScoreEntryRegistered` back to 0 messages, no dead-letters             |

## 7. Known limitations (by design — not bugs)

- **Flat 100-point magnitude.** `ApplyPenaltyCommandHandler.BasePenaltyMagnitude` is a
  fixed `100`; the UI/API take a team + reason only, not an amount.
- **Operator-assignment projection fills automatically.** session-ops publishes
  `LiveSessionOperatorAssignedIntegrationEvent` when an operator is assigned, and scoring's
  consumer upserts the `session_operator_assignments` row — so Step 4's manual patch is now only a
  fallback (stale sessions, or authorizing a row by hand). The event carries the operator's Keycloak
  sub, not the internal numeric user id, because that is the axis scoring authorizes on.
- **Operators cannot read the ranking (participants can).** `RankingController` admits
  `ParticipantOrOperator`, but its membership guard calls a session-ops endpoint that is
  `Participant`-only, so an **operator** token gets a hard 403 from `GET /api/sessions/{id}/ranking`
  while a participant member gets 200. This is why **Step D** verifies the recalculation against
  the DB rather than the ranking API. Participants — including mobile (Steps A + E) — are
  unaffected.

  > Earlier revisions of this doc claimed the ranking API was blocked for everyone by a
  > membership "404". That was wrong: the membership endpoint never returns 404 (it always
  > answers 200 with a reason code), and the read path works for participant members. The
  > 403 above is operator-only. Verified 2026-07-15 against the live stack.
- **No RabbitMQ `PenaltyApplied` integration event.** HU-38 owes no transport (per the
  required-patterns matrix); recalculation is driven by `ScoreEntryRegistered` only. The
  `PenaltyApplied` **domain** event still fires in-process.

## 8. Troubleshooting

- **Penalty panel shows the inactive note.** The session isn't Active — press **Start**
  as op-1 (Step B).
- **Apply button is disabled.** Either no team is selected or the **Reason** is empty —
  both are required.
- **`penalty-error` "not authorized".** The scoring `session_operator_assignments` row is
  missing or keyed by the wrong id. On an up-to-date stack the seed fills it automatically; if it's
  missing, the services likely predate the auto-publish wiring (restart both per Step 4 and re-seed),
  or you're on a stale session — use Step 4's manual fallback with op-1's **sub** (not the integer
  user id) and the correct live session id. A row published under the wrong id also lands here if the
  assignment never resolved a Keycloak sub (session-ops logs a skip warning).
- **Total didn't change / no v2 ranking.** The `ScoreEntryRegistered` message may be
  stuck — check `docker compose logs scoring-monitoring-service` and the
  `ScoreEntryRegistered_error` queue in RabbitMQ mgmt.
- **Gilded Owls stays 350 and a stray row appears under an unfamiliar GUID at 0.** You are
  on a build predating the referenceTeamId fix — the penalty was filed under the runtime
  team id (a phantom ranking group that clamps to 0) instead of the referenceTeamId. Rebuild
  the frontend and `docker compose restart session-operations-service` (it runs `dotnet watch`,
  so a plain `up --build` is a no-op) to pick up the fix, then re-apply the penalty.
- **`penalty-team-select` has no options / the team is absent.** A team without a
  `referenceTeamId` is intentionally excluded from the penalty selector (it can't be scored);
  confirm the seeded team carries one.
- **(Mobile) The total is right (250) but no toast appeared.** You joined *after* the penalty,
  so the app never saw 350 — `useScoreDrop` suppresses the toast when there's no previous
  value. Working as designed. Re-seed, join first (Step A), then apply.
- **(Mobile) TEAMS says "Standings will appear once the round begins."** That text is the
  `PodiumLeaderboard` empty-state for a ranking snapshot with **zero rows**. For HU-38 that is
  wrong: the reused HU-25B seed should already have **Gilded Owls 350** / **Crimson Foxes 200**
  before Start. Most likely causes: you joined the wrong session code, you are looking at a stale
  session from an older seed run, or the ranking seed step did not land for this `liveSessionId`.
  Re-run the seed spec, copy the fresh **session code** and **live session id**, then join that
  exact session before Step B.
- **(Mobile) The board renders but never updates; the total stays 350.** The REST fetch works
  and the hub push doesn't, so `JoinSessionGroup` failed — `team-space.tsx` swallows that and
  degrades to REST-only, silently. Check `docker compose logs scoring-monitoring-service` for
  `ParticipantSessionMembershipClient` warnings: it now logs the real reason code
  (`session-ops-http-403`, `session-ops-timeout`, …). A `/hubs/scoring/negotiate` **404** means
  the gateway route regressed — it must be `"/hubs/scoring/{**catch-all}"`, and a correct
  route answers `negotiate` with **401**, not 404.
- **(Mobile) Live updates die right after a backend edit**, `/health` still green. `dotnet watch`
  can't hot-apply a rude edit to `ScoringHub`'s ctor or DI, but still logs "Hot reload succeeded";
  every connection then throws server-side and reads like a client crash. Confirm with
  `docker compose logs scoring-monitoring-service | grep HotReloadException`, then
  `docker compose restart scoring-monitoring-service` (`up --build` is a no-op — the source is
  bind-mounted). See `mobile/docs/hu-25b-manual-test.md` §7 for the full write-up.

---

## Appendix — the specs

The seed run in step 1 lives at
`frontend/tests/e2e/hu-25b-ranking-manual-seed.spec.ts` (reused as-is; it stages the
two-team session with the 350/200 ranking this test subtracts from). The automated live
e2e for the operator penalty click-path is
`frontend/tests/e2e/hu-38-operator-penalty.spec.ts` — keep this doc in sync if either
changes.

The optional mobile steps (B + E) are **not** covered by any automated test — the e2e
asserts the operator UI and the DB only, and the mobile ranking tests
(`mobile/src/__tests__/ranking-hook.test.ts`) mock both the hub and the REST fetch. The
surfaces they exercise:

| Surface | File |
|---|---|
| Score-drop detection + toast | `mobile/src/components/score-drop-toast.tsx` |
| Hub subscription / snapshot swap | `mobile/src/lib/realtime/use-ranking.ts` |
| SignalR client (`JoinSessionGroup`, re-join on reconnect) | `mobile/src/lib/realtime/scoring-hub.ts` |
| Own-team score, hub lifecycle, toast placement | `mobile/src/app/(app)/team-space.tsx` |
| Podium rows + `(You)` accent | `mobile/src/components/podium-leaderboard.tsx` |

For the participant ranking surface in its own right (including the treasure-hunt scan
path that *raises* a score), see `mobile/docs/hu-25b-manual-test.md` — this doc is the
penalty-shaped counterpart to it.

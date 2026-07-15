# HU-38 — Manual Test: operator applies a justified penalty to a team

Goal: an **operator** (web) applies a **justified penalty** (reason required) to a
team in an assigned live session, and the append-only deduction is confirmed as a
single `ScoreEntry` ledger entry — not a mutable total.

> Applies once **HU-38** has landed: the backend `POST /penalties`, the operator
> penalty control (web), and the `AppliedPenaltyDto` contract. Until then use
> this as the acceptance script.
>
> ⚠️ The frontend slice is complete but **three backend blockers** prevent the
> happy path from exercising end-to-end through the gateway today. This script
> documents the intended operator-web flow, provides a **direct-to-scoring**
> curl fallback to verify the backend contract, and flags each blocker.

You need the operator on `localhost:3000` and the backend services up.

---

## 1. Bring up the stack (once)

```bash
# backend + gateway + Keycloak
cd backend && docker compose up -d

# operator web app
cd ../frontend && pnpm install && pnpm dev       # -> http://localhost:3000

# seed a live session with attached teams (separate terminal; needs the backend up)
cd frontend && pnpm exec playwright test tests/e2e/session-treasure-hunt-manual-seed.spec.ts
```

## 2. Grab the seeded session code

The seed spec prints what you need — **copy the session code**:

```
  HU-38 manual-test session ready (Preparing):
    title: Treasure Hunt E2E
    session code: <COPY THIS>
    teams attached: Gilded Owls + Red Herrings
    -> Start as op-1 to reach Active, then apply a penalty.
```

## 3. Credentials

| Who      | Where                | Login                  |
| -------- | -------------------- | ---------------------- |
| Operator | web `localhost:3000` | `op-1` / `operator123` |

`op-1` is the **assigned** operator of the seeded session.

## 4. Run it

### Step A — Start the session

1. Sign in as **op-1 / operator123** and open the seeded session.
2. Click **Start** (Preparing → Active).
3. The operator hero renders with the **team progress panel** and the control row
   containing **Release clue**, **Operative clue**, and **Penalty**.

### Step B — Apply a justified penalty

1. In the **Penalty** panel, select **Gilded Owls** from the team picker.
2. Enter a reason in the textarea, e.g.:
   > "Team used a phone during a no-device substage."
3. The **Apply penalty** button is **disabled** while the reason is blank or no
   team is selected.
4. Click **Apply penalty**.
5. The success note appears:
   > "Penalty applied: −100 pts."

That confirmation — an append-only ledger entry with a positive magnitude
rendered as a deduction — is HU-38 working. ✅

## 5. Quick extra checks (the HU-38 acceptance guard)

| Check                  | How                                                               | Expect                                                                                            |
| ---------------------- | ----------------------------------------------------------------- | ------------------------------------------------------------------------------------------------- |
| Reason required (UI)   | Leave the reason blank                                            | **Apply penalty** is disabled                                                                     |
| Reason required (API)  | Submit a whitespace-only reason (curl)                            | 400 / ProblemDetails — the backend validator rejects it                                           |
| Non-owning operator    | *(needs a second operator)* apply as a non-assigned operator      | 403 / ProblemDetails (`Proxy` blocks non-owning operators)                                        |
| Non-live session       | Open the panel on a Scheduled/Finished session                    | Renders the inactive note: "Penalties can be applied once the session is Active or Paused."       |
| Entry not a total      | Apply two penalties to the same team                                | Two separate "−100 pts" confirmations; no running total is displayed or computed client-side    |
| Score reflection       | Check the team progress panel score after the apply succeeds      | **Blocked today (D3)** — no read surface reflects the deduction; see Known Limitations            |

## Known limitations (by design — not bugs)

Don't treat these as HU-38 failures:

- **Gateway route missing (D1).** `POST /api/sessions/{id}/penalties` is **not**
  routed by the gateway yet; requests fall through to `session-ops` and 404.
  Use the **direct-to-scoring curl** in the Troubleshooting section to verify the
  backend contract until `backend/api-gateway/` adds the `scoring-penalties` route.
- **Ownership Proxy denies every Operator (D2).** `ScoringSessionAuthorizationProxy`
  reads a projection fed by an integration event nobody publishes, and its id type
  does not match session-operations'. Until the backend fixes D2, only an
  `Administrator` can apply a penalty backend-side — but this slice is
  **operator-facing only**, so the operator web path is blocked. Test with the
  direct curl and a manually-assigned projection row if needed.
- **No read surface reflects the deduction (D3).** The operator panel's
  `teamProgress[].score` is session-owned and never sees scoring events; the
  ranking fold **adds** penalty magnitudes instead of subtracting them. So the
  team score in the UI does **not** drop after a penalty. Fixing D3 is a
  backend/context-map decision — the frontend will re-read the authoritative
  snapshot via `loadOperatorPanel` once the correct surface exists.
- **Penalty amount is hardcoded.** `ApplyPenaltyCommandHandler` uses
  `ScoreValue.Create(100)`; the control offers no amount input. A future HU may
  make the magnitude configurable.
- **No ranking / audit / history surfaces.** Penalty reversal, editing, or a
  queryable ledger history are later HUs.

## Troubleshooting

- **Penalty panel is missing.** You are not signed in as an **Operator**, or the
  session is not selected. The panel only renders inside the operator hero on an
  assigned live session.
- **Apply penalty returns 404.** The gateway does not route the endpoint yet (D1).
  Use the direct-to-scoring curl below to verify the backend contract.
- **Apply penalty returns 403.** The ownership Proxy denies the operator (D2) or
  you're not the assigned operator. Until D2 is fixed, test the backend contract
  with the curl fallback.
- **Apply penalty returns 400.** The reason is blank or whitespace-only, or a
  required id is empty. The backend validator is authoritative.
- **Success says "−100 pts" but the team score doesn't change.** Expected today
  (D3). The success note confirms the **ledger entry** was appended; the score
  reflection requires a backend read-surface fix.
- **Panel shows the inactive note on an Active session.** The frontend checks
  `state === 'Active' || state === 'Paused'`. Confirm the session state in the
  hero header; a stale SignalR push may lag behind the real state.

### Direct-to-scoring curl (bypasses the gateway route bug)

```bash
# Obtain a token for op-1 (or an administrator while D2 blocks operators)
TOKEN=$(curl -s -X POST "http://localhost:8080/realms/umbral/protocol/openid-connect/token" \
  -H "Content-Type: application/x-www-form-urlencoded" \
  -d "grant_type=password&client_id=umbral-web&username=op-1&password=operator123" \
  | jq -r '.access_token')

# Apply a penalty directly to the scoring service (bypassing the missing gateway route)
curl -X POST "http://localhost:5003/api/sessions/{liveSessionId}/penalties" \
  -H "Authorization: Bearer ${TOKEN}" \
  -H "Content-Type: application/json" \
  -d '{ "teamId": "<team-id>", "reason": "Team used a phone during a no-device substage." }'
```

Expected: **201 Created** with an `AppliedPenaltyDto`:
```json
{
  "scoreEntryId": "...",
  "teamId": "...",
  "penaltyAmount": 100,
  "reason": "Team used a phone during a no-device substage.",
  "appliedAt": "2026-07-15T..."
}
```

- `penaltyAmount` is a **positive magnitude** (`100`). The deduction is carried by
  the entry's `ScoreEntryType.Penalty`, which the DTO does not expose. The UI
  renders it as `−100 pts`.
- Replace `{liveSessionId}` and `<team-id>` with the runtime ids from the operator
  panel or the seeded session response.
- Scoring service port `5003` is the direct route; the gateway (`localhost:8000`)
  404s on this path until D1 is resolved.

---

## Appendix — seed fallback

If the existing treasure-hunt seed spec does not print a usable session, run the
frontend's manual seed directly:

```bash
cd frontend
pnpm exec playwright test tests/e2e/session-treasure-hunt-manual-seed.spec.ts
```

It authors a `Treasure Hunt E2E` mission, creates a session in **Preparing**, attaches
**Gilded Owls** and **Red Herrings**, and assigns `op-1`. The spec logs the session
code and the live session id to the console. Copy the code, sign in as `op-1`, click
**Start**, then run the penalty flow above.

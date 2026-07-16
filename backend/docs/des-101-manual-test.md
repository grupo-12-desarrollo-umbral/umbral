# DES-101 — Manual Test: trivia results at question close (HU-35 reveal + HU-36B operator review)

Goal: when a trivia question **closes**, two audiences see the withheld outcome data land in
real time on the same close signal:

- **Participant** (mobile) — HU-35: the **correct option** is revealed, the optional
  **explanation** shows, and the team is told **correct / incorrect** (with points).
- **Operator** (web) — HU-36B: a **post-close answer review** table shows, per team, the
  **submitted option**, whether it was **correct**, and the **points** awarded.

> This is a **read + push-enrichment** slice — no new aggregate, no persistence, no migration.
> The enriched `QuestionClosed` push carries the question-level reveal to everyone on
> `live-session:{id}`; each audience then fires an authorized read for its own slice.
>
> ✅ Unlike HU-38, this path exercises **end-to-end through the gateway** today: both new
> endpoints route via the existing `/api/sessions/{**catch-all}` rule, both are implemented,
> and the frontend + mobile are wired. This script drives the happy path on both surfaces and
> provides a **through-the-gateway curl** to verify each backend contract directly.

You need the operator on `localhost:3000`, the mobile app connected to a participant, and the
backend services up. Stage **both** surfaces before pressing **Start** — the live clock
(≈5 s pre-game countdown + 3 × 30 s seed questions) only runs after Start, so you can catch
each close live instead of always landing on a Finished card.

---

## 1. Bring up the stack (once)

```bash
# backend + gateway + Keycloak
cd backend && docker compose up -d

# operator web app
cd ../frontend && pnpm install && pnpm dev       # -> http://localhost:3000

# participant mobile app (separate terminal)
cd ../mobile && pnpm install && pnpm start        # Expo — open on a device/simulator

# seed a startable trivia session with a team attached (separate terminal; needs the backend up)
cd frontend && pnpm exec playwright test tests/e2e/session-answered-monitor-manual-seed.spec.ts
```

## 2. Grab the seeded session code

The seed spec prints what you need — **copy the session code**:

```
  HU-36A manual-test session ready (Preparing):
    title: Answered Monitor E2E
    session code: <COPY THIS>
    -> open it as op-1, click "Start" once web + mobile are staged.
```

The seed creates **Answered Monitor E2E** in **Preparing**, attaches **Gilded Owls**, assigns
**op-1**, and (via global-setup) seeds **participant-1** into Gilded Owls so a mobile answer is
authorized. It stops one operator **Start** click short of Active on a mission with **three
trivia questions**.

## 3. Credentials

| Who         | Where                     | Login                        |
| ----------- | ------------------------- | ---------------------------- |
| Operator    | web `localhost:3000`      | `op-1` / `operator123`       |
| Participant | mobile (Expo)             | `participant-1` (Gilded Owls)|
| Admin       | (curl / assign path only) | `admin-1` / `admin123`       |

`op-1` is the **assigned** operator of the seeded session; `participant-1` belongs to the
attached **Gilded Owls** team.

## 4. Run it

### Step A — Stage both surfaces, then Start

1. Sign in on web as **op-1 / operator123** and open the seeded **Answered Monitor E2E** session.
2. On mobile, sign in as **participant-1**, join with the **session code**, and land in the
   team space for **Gilded Owls**.
3. Back on web, click **Start** (Preparing → Active). The ≈5 s pre-game countdown runs, then
   question 1 activates on both surfaces.

### Step B — Participant answers, operator watches

1. On mobile, while question 1 is active, **submit an answer** (pick any option).
2. On web, the operator's pre-close **answered board** (HU-36A) flips Gilded Owls to
   *answered* — this is the existing monitor, not yet the reveal.

### Step C — Let the question close (the DES-101 moment)

Wait for the 30 s question timer to expire (or the operator advances). On close the backend holds
a **reveal window** (≈5 s) before activating the next question — that dwell is what keeps the reveal
on screen; without it the next question would overwrite it instantly. During the window:

1. **Mobile (HU-35 reveal).** The stage enters **reveal**:
   - the **correct option** is highlighted (`reveal-correct-option`),
   - the team **outcome chip** shows `correct` / `incorrect` / `no-answer`
     (`reveal-team-outcome-*`) with **points** (`reveal-points`),
   - the **explanation** renders when the question configured one (`reveal-explanation`).
   Controls stay locked; no reload.
2. **Web (HU-36B review).** The **Answer review** panel (`trivia-answer-review-panel`) appears
   with **one row per team** (`trivia-answer-review-row-{teamId}`) showing the submitted
   option, a **correct / incorrect / no-answer** badge (`trivia-answer-review-correct-{teamId}`),
   and the **points**. No reload — it fetched on the same `QuestionClosed` signal.

Those two surfaces updating live off one close signal — question-level reveal from the push,
team/operator slices from authorized reads — is DES-101 working. ✅ Repeat across questions 2
and 3 to watch it each close.

## 5. Quick extra checks (the DES-101 acceptance guard)

| Check                          | How                                                                    | Expect                                                                                     |
| ------------------------------ | ---------------------------------------------------------------------- | ------------------------------------------------------------------------------------------ |
| No reveal pre-close (A-5)      | Read `my-result` / `answer-review` (curl) while the question is active | Rejected — the read refuses a question that has not closed                                 |
| No correctness leak on activate| Inspect the `QuestionActivated` push (mobile/web devtools)             | Bare option strings only — no `IsCorrect`, no `correctOptionSequenceOrder`                  |
| Team told correct/incorrect    | Answer *wrong* on mobile, let it close                                 | Outcome chip = `incorrect`; correct option still highlighted; points reflect the miss      |
| Never-answered team            | Let a question close **without** submitting on mobile                  | Mobile chip = `no-answer`; operator row for that team shows the no-answer badge, null points|
| Operator sees points           | After close, read the web review panel points column                   | Per-team points match the awarded `ScoreValue`                                              |
| Real-time, no reload           | Do **not** refresh either surface across a close                       | Both the reveal and the review appear from the live `QuestionClosed` push                   |
| Ranking after close            | Check the mobile podium/leaderboard                                    | Already shipped (`podium-leaderboard`) — HU-35 ranking AC met with no DES-101 change        |

## 6. Endpoints & contracts (what the surfaces read)

Both route through the gateway (`localhost:8000`) via `/api/sessions/{**catch-all}` → session-ops.

**Enriched close push** — `QuestionClosed` on group `live-session:{id}`:
```jsonc
{ "liveSessionId": "...", "questionIndex": 1, "closedAt": "...", "wasExpiredByTimer": true,
  "correctOptionSequenceOrder": 2, "explanation": "…or null" }
```

**Participant — my team result (HU-35):**
```
GET /api/sessions/{liveSessionId}/trivia/questions/{sequenceOrder}/my-result   (Participant)
```
```jsonc
{ "selectedOptionSequenceOrder": 2, "isCorrect": true, "scoreValue": 100,
  "correctOptionSequenceOrder": 2, "explanation": "…or null" }
// answer fields are null when the team never answered; reveal fields stay present.
```

**Operator — post-close answer review (HU-36B):**
```
GET /api/sessions/{liveSessionId}/trivia/questions/{sequenceOrder}/answer-review   (Operator)
```
```jsonc
{ "liveSessionId": "...", "questionSequenceOrder": 1,
  "teams": [ { "teamId": "...", "teamCode": "GILDED-OWLS", "displayName": "Gilded Owls",
               "selectedOptionSequenceOrder": 2, "isCorrect": true, "scoreValue": 100,
               "answeredAt": "2026-07-16T..." } ] }
// never-answered teams appear with null option/isCorrect/scoreValue/answeredAt.
```

`sequenceOrder` is the **1-based** question order (the seed mission has questions `1`, `2`, `3`).
`correctOptionSequenceOrder` is 1-based and matches the option row `index + 1`.

## Known limitations (by design — not bugs)

Don't treat these as DES-101 failures:

- **Reveal is signal-triggered, not a fat push.** The `QuestionClosed` push carries only the
  question-level reveal (correct option + explanation), identical for everyone in the group.
  The team result (HU-35) and the operator table (HU-36B) are separate **reads** the clients
  fire on the close signal — so authorization stays on the read path.
- **The read slices add no persistence / migration.** The `my-result` and `answer-review` reads
  project from already-persisted `TriviaAnswerSubmission` data. (Separately, the **reveal window**
  that gives the reveal its dwell — the `AddQuestionRevealWindow` migration — adds two
  `live_sessions` columns for the deferred next-question activation; that is orchestration state, not
  a new results aggregate.)
- **Ranking is out of scope for this slice.** HU-35's "team sees updated ranking after close"
  is already served by `GET /api/sessions/{id}/ranking` + the live `RankingChanged` push, and
  mobile already renders it (`podium-leaderboard`). No DES-101 change and **not** in the
  operator dashboard (no AC requires it there).
- **scoring-monitoring: nothing to do.** DES-101 touches only `session-operations-service`.

## Troubleshooting

- **Mobile shows only "Question closed" with no reveal.** The enriched push didn't arrive or
  the `my-result` read failed. The question-level reveal (correct option + explanation) comes
  straight off the push and should show even if the read is slow; the outcome chip/points wait
  on `my-result`. Confirm the participant is still in `live-session:{id}` (reconnected).
- **Operator review panel is missing.** You're not signed in as the assigned **Operator**, or
  the question hasn't closed yet. The panel renders on the `QuestionClosed` signal for the
  session the operator has selected.
- **`answer-review` returns 403.** The resolver **Proxy** denies a non-assigned operator
  (Administrator sees any session; the assigned Operator sees only theirs). Authz flows through
  `ISessionAdministrationAccessResolver`, not an ad-hoc role check.
- **`my-result` / `answer-review` returns 4xx while a question is still active.** Expected —
  the read rejects a not-yet-closed question (A-5, no pre-close reveal leak).
- **Reveal or review is stale after switching sessions.** Both loaders drop a late response for
  a session the operator/participant has since switched away from; re-select and let the next
  question close.

### Through-the-gateway curl (verify each contract directly)

```bash
# Participant token (participant-1) for the my-result read
PTOKEN=$(curl -s -X POST "http://localhost:8080/realms/umbral/protocol/openid-connect/token" \
  -H "Content-Type: application/x-www-form-urlencoded" \
  -d "grant_type=password&client_id=umbral-web&username=participant-1&password=participant123" \
  | jq -r '.access_token')

# Operator token (op-1) for the answer-review read
OTOKEN=$(curl -s -X POST "http://localhost:8080/realms/umbral/protocol/openid-connect/token" \
  -H "Content-Type: application/x-www-form-urlencoded" \
  -d "grant_type=password&client_id=umbral-web&username=op-1&password=operator123" \
  | jq -r '.access_token')

# HU-35 — participant's own team result for a CLOSED question (sequenceOrder 1)
curl -s "http://localhost:8000/api/sessions/{liveSessionId}/trivia/questions/1/my-result" \
  -H "Authorization: Bearer ${PTOKEN}" | jq

# HU-36B — operator per-team review for the same closed question
curl -s "http://localhost:8000/api/sessions/{liveSessionId}/trivia/questions/1/answer-review" \
  -H "Authorization: Bearer ${OTOKEN}" | jq
```

- Replace `{liveSessionId}` with the id from the operator panel or the seed console output.
- Run these **after** the question closes; before close they reject (A-5).
- The gateway (`localhost:8000`) routes both via the existing `/api/sessions/{**catch-all}`
  rule — no new gateway route was needed.

---

## Appendix — seed fallback

If the seed spec above does not print a usable session, re-run it directly:

```bash
cd frontend
pnpm exec playwright test tests/e2e/session-answered-monitor-manual-seed.spec.ts
```

It authors the `Answered Monitor E2E` trivia session in **Preparing**, attaches **Gilded Owls**,
assigns `op-1`, and (via global-setup) seeds `participant-1` into the team so a mobile answer is
authorized. It logs the session code and stops before Active. Copy the code, stage web (op-1) and
mobile (participant-1), then click **Start** and run the close flow above.

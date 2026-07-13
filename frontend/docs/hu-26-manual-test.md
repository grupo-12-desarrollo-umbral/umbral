# HU-26 — Manual Test: operator releases a hidden clue, participant sees it reveal

Goal: an **operator** (web) releases a treasure-hunt target's **hidden** clue to
one team or all teams during an **Active** session, and the released
**participant** (mobile) sees that clue appear on their live board — with a small
ember dot on the **CLUES** tab marking it as new.

You need two things running side by side: the operator on `localhost:3000`, and
the mobile app on a phone/emulator pointed at the same backend (gateway
`localhost:8000`).

> Prefer to not do it by hand? The operator surface has an automated live e2e:
> `pnpm exec playwright test tests/e2e/session-clue-release.spec.ts` (covers the
> success + released count, the duplicate conflict, and the Active-only gate
> against the real gateway). This manual guide adds the participant-side reveal,
> which the e2e leaves out of scope.

---

## 1. Bring up the stack (once)

```bash
# backend + gateway + Keycloak
cd backend && docker compose up -d

# operator web app
cd ../frontend && pnpm install && pnpm dev       # -> http://localhost:3000

# seed a startable treasure-hunt session with HIDDEN clues + two teams (needs the backend up)
cd frontend && pnpm exec playwright test tests/e2e/session-clue-release-manual-seed.spec.ts

# mobile app (separate terminal)
cd ../mobile && pnpm install && pnpm start        # open in Expo Go / emulator
```

## 2. Grab the seeded session code

The default seed only creates Trivia missions, so the seed run above authors a
runtime-ready treasure-hunt mission whose clues are **`HiddenUntilOperatorRelease`**
and stages a session in **Preparing**, one operator "Start" click from Active. It
attaches **two** teams so you can prove a release to one team doesn't leak to the
other.

It prints the session you need — **copy the session code**:

```
  HU-26 manual-test session ready (Preparing):
    title: Clue Release E2E
    session code: <COPY THIS>
    teams attached: Gilded Owls (participant's team) + Maple Runners (other team)
    -> join on mobile as participant-1 / Gilded Owls, then Start as op-1 once staged.
```

The seeded mission has one treasure-hunt substage with **3 active targets**, each
carrying a **hidden** clue.

## 3. Credentials

| Who         | Where                | Login                                           |
| ----------- | -------------------- | ----------------------------------------------- |
| Operator    | web `localhost:3000` | `op-1` / `operator123`                          |
| Participant | mobile app           | `participant-1@umbral.local` / `participant123` |

`participant-1` is pre-seeded onto the **Gilded Owls** team, which is one of the two
teams the seed attaches — join that team on mobile.

## 4. Get a target id (interim step)

No operator read exposes the runtime target ids yet (tracked in **DES-94**), so the
release control takes a **raw target UUID**. Read one from the runtime snapshot — the
targets exist as soon as the session is created (you don't need to Start first, though
the *release* in Step C does require Active).

**Substitute your real session code for `PASTE_CODE_HERE`** (the code the seed printed
in step 2, e.g. `A52540`) — leaving a placeholder returns zero rows:

```bash
docker exec backend-postgres-1 psql -U postgres -d session_operations -t -A -c \
  "SELECT t.id, t.name FROM live_session_mission_runtime_snapshot_targets t \
   JOIN live_sessions s ON s.id = t.live_session_id \
   WHERE s.session_code = 'PASTE_CODE_HERE' \
     AND t.is_active = true AND t.clue_visibility_policy = 'HiddenUntilOperatorRelease' \
   ORDER BY t.sequence_order;"
```

It prints the three targets (`Target 1/2/3`) with their UUIDs, e.g.
`292f1652-9c67-456c-a2d4-574a0ea0adf9|Target 1` — **copy one UUID** (the part before
the `|`). If it prints nothing, the code is wrong or still a placeholder — list every
seeded session's code by swapping the `WHERE` line for
`WHERE s.title_snapshot LIKE 'Clue Release E2E%'` and re-selecting `s.session_code`.

## 5. Run it

### Step A — Participant (mobile): join the team

1. Sign in as **participant-1@umbral.local / participant123**.
2. Tap **Join your session** → enter the **session code** from step 2.
3. In the team lobby, pick **Gilded Owls** → join. You land in the team space
   ("Restoring your team space…" until the operator starts).

### Step B — Operator (web): Start the session

1. Sign in as **op-1 / operator123**, open **Sessions**, pick **Clue Release E2E**,
   click **Open live operation**.
2. Click **Start** (drives Preparing → Active). Entering Active sets the active
   substage to the first (treasure-hunt) substage; the participant's board flips to
   the treasure-hunt board with an **empty** CLUES tab ("No clues yet.").

### Step C — Operator (web): release a hidden clue

In the operator hero, the **Release clue** panel (`clue-release-panel`) is now live:

1. Paste the target UUID from step 4 into **Target id**.
2. Pick **Gilded Owls** in the **Team** selector (leave **All teams** to release to
   both).
3. Click **Release**.

You must see the success note: **"Released to 1 team."** (or "Released to 2 teams."
for All teams), and no error.

### Step D — Verify the reveal (mobile)

On the participant's board:

- If they're **not** on the CLUES tab, a small **ember dot** appears on the **CLUES**
  segment — the new-clue marker.
- Open **CLUES**: the released clue is now a card ("Target 1" · "Find landmark #1 and
  scan its code.") where it was empty before. Opening the tab **clears the dot**.

That live reveal — the operator's release surfacing on the participant board with no
manual reload — is HU-26 working. ✅

## 6. Quick extra checks (optional)

| Check              | How                                                         | Expect                                                              |
| ------------------ | ----------------------------------------------------------- | ------------------------------------------------------------------- |
| No duplicate       | Release the **same** target to **Gilded Owls** again        | `clue-release-error`: "already released to that team for this target" |
| All teams          | Release a **different** target with **All teams** selected  | "Released to 2 teams."                                              |
| Not releasable     | Release a **random** UUID (no hidden clue)                  | `clue-release-error`: "no releasable hidden clue in the active substage" |
| Active gate        | Before Start (Preparing), open the panel                    | `clue-release-inactive` note, **no** Release button                 |
| No cross-team leak | Sign a second participant onto **Maple Runners**; release only to Gilded Owls | Maple Runners' board does **not** reveal that clue |

## 7. Known limitations (by design — not bugs)

- **Raw target UUID input.** The operator pastes a target id because no operator read
  exposes the active substage's target ids yet. The friendly target picker is a
  follow-up once **DES-94** (operator-panel `targets[]`) lands — the release contract
  itself won't change.
- **No RabbitMQ `ClueReleased` event.** Release reveals the clue over SignalR only; the
  integration event is **DES-92**.
- **The countdown reads `00:00 / Expired` and never ticks (DES-93).** The authoritative
  timer is wired only to the active trivia-question window; a treasure-hunt substage has
  none. The board still renders correctly — only the timer is inert.
- **Score and target progress stay 0.** `currentScore` and `resolvedTargets` don't move
  until target-resolution lands (HU-31). Release changes **visibility only** — it does
  not advance the substage or resolve a target.

## 8. Troubleshooting

- **"Release clue" panel shows the inactive note.** The session isn't Active — press
  **Start** as op-1 (Step B).
- **Release button is disabled.** The **Target id** field is empty — paste a UUID.
- **`clue-release-error` "no releasable hidden clue".** The UUID isn't a hidden-clue
  target of the *active* substage — re-copy from the step-4 query (it filters to
  `HiddenUntilOperatorRelease`).
- **Clue doesn't reveal on mobile.** Confirm you released to **Gilded Owls** (the
  participant's team), not the other team or nobody. Backgrounding/reopening the app
  re-fetches the board snapshot as a fallback.
- **Mobile can't reach the backend.** It targets the gateway on `localhost:8000`; on a
  physical device use your machine's LAN IP, not `localhost`.

---

## Appendix — the seed spec

The seed run in step 1 lives at
`frontend/tests/e2e/session-clue-release-manual-seed.spec.ts` (keep this doc in sync if
it changes). It mirrors the HU-23 seed
(`session-treasure-hunt-manual-seed.spec.ts`) with two deltas: clue `Visibility` is
**`HiddenUntilOperatorRelease`** (so the board withholds the clues until an operator
releases them), and a **second** registered team (Maple Runners) is attached so a
single-team release can be shown not to leak. The automated live e2e for the operator
surface is `session-clue-release.spec.ts`.

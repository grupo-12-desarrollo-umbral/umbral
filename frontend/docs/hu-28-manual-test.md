# HU-28 — Manual Test: operator authors an operative clue, participant sees it live

Goal: an **operator** (web) authors a free-text **operative clue** during a live
session and assigns it to one/several/all teams; the assigned **participant**
(mobile) sees it live — on a **Trivia** substage via a collapsed clue chip + an
arrival toast, and on a **TreasureHunt** substage as a card on the CLUES tab.
Authoring is **guidance, not progress** — it advances no substage and changes no
score.

You need two things running side by side: the operator on `localhost:3000`, and
the mobile app on a phone/emulator pointed at the same backend (gateway
`localhost:8000`).

---

## 1. Bring up the stack (once)

```bash
# backend + gateway + canonical seed data (incl. the shared HU-171 quiz this seed reuses)
cd backend && ./scripts/seed-all.sh

# operator web app
cd ../frontend && pnpm install && pnpm dev       # -> http://localhost:3000

# stage a startable mixed-mode session (Trivia -> TreasureHunt, Gilded Owls attached)
pnpm exec playwright test tests/e2e/session-substage-progress-manual-seed.spec.ts

# mobile app (separate terminal)
cd ../mobile && pnpm install && pnpm start        # open in Expo Go / emulator
```

> Reusing the **HU-171 seed** deliberately: it stages one session whose first
> substage is **Trivia** (exercises B2 — chip + toast) and whose second is
> **TreasureHunt** (exercises B1 — CLUES tab), so one session covers both reveal
> surfaces. HU-28 authors the operative clue **live from the operator UI** — the
> seed adds no operative clue itself.

## 2. Grab the seeded session code

The session is left in **Preparing**, one operator "Start" click from Active. It
prints the session you need — **copy the session code**:

```
  HU-171 manual-test session ready (Preparing):
    title: Substage Progress E2E
    session code: <COPY THIS>
    -> join on mobile as participant-1 / Gilded Owls, then Start as op-1 once staged.
```

## 3. Credentials

| Who         | Where                | Login                                           |
| ----------- | -------------------- | ----------------------------------------------- |
| Operator    | web `localhost:3000` | `op-1` / `operator123`                          |
| Participant | mobile app           | `participant-1@umbral.local` / `participant123` |

`participant-1` is pre-seeded onto the **Gilded Owls** team, which is the team the
seed attaches to the session — join that team on mobile.

## 4. Run it

### Step A — Participant (mobile): join the team

1. Sign in as **participant-1@umbral.local / participant123**.
2. Tap **Join your session** → enter the **session code** printed by the seed.
3. In the team lobby, pick **Gilded Owls** → join.
4. You land in the team space ("Restoring your team space…" until the operator starts).

### Step B — Operator (web): Start the session

1. Sign in as **op-1 / operator123** and open **Substage Progress E2E**.
2. Click **Start** (Preparing → Active). Entering Active sets the active substage
   to the first (**Trivia**) substage, so mobile shows the question stage.

### Step C — Operator (web): author an operative clue

In the operator hero, find the **"Operative clue"** section (below the clue-release
panel).

1. Type a clue in **Clue text**, e.g. `Look beneath the blue banner.`
2. Check **All teams** (or just the **Gilded Owls** checkbox).
3. Click **Assign clue**.
4. You see a success note: **"Assigned to 1 team."** and the form clears.

> Before Start (Preparing), this section instead reads **"Operative clues can be
> assigned once the session is Active or Paused."** with no submit — that's the
> Active-or-Paused live gate. **Assign** stays disabled until the text is
> non-empty **and** ≥1 team is selected.

### Step D — Verify on mobile (Trivia surface, B2)

The instant you assign, on the participant's trivia screen you must see:

- A **top-pinned toast**: an ember dot + **"NEW OPERATIVE CLUE"** + one line of the
  clue text. It slides in, holds ~4s, and auto-dismisses.
- Below the question stage, a collapsed chip: **"OPERATIVE CLUES · 1"** with an
  **ember dot** (unseen). Tap it (or tap the toast) to expand a parchment
  **"OPERATIVE CLUE"** card showing the full clue text.

That toast + reviewable chip on a trivia substage is **B2** working. ✅

### Step E — Verify on mobile (TreasureHunt surface, B1)

1. As operator, let the trivia questions run out (trivia auto-advances on timer
   expiry); after the last question the session advances to the **TreasureHunt**
   substage and the participant's screen flips to the treasure-hunt board.
2. Author **another** operative clue (Step C) assigned to Gilded Owls.
3. On mobile, the **CLUES tab** gains an **ember dot**; open it to see an
   **"OPERATIVE CLUE"** card (with your text) alongside the seeded target clue.
   **No toast here** — the CLUES-tab dot already signals it.

That operative-clue card on the CLUES tab is **B1** working. ✅

## 5. Quick extra checks (optional)

| Check                  | How                                                        | Expect                                                              |
| ---------------------- | ---------------------------------------------------------- | ------------------------------------------------------------------ |
| Multi / all teams      | Attach a 2nd team first, then assign to **All teams**      | Success note reads **"Assigned to 2 teams."**                      |
| No cross-team leak     | Assign to only a team you are **not** on                   | The participant sees **nothing** (server-scoped per team)          |
| Not progress           | Watch target progress / score after assigning              | `0 / N targets` and SCORE stay unchanged — authoring advances nothing |
| Missed the toast       | Ignore the trivia toast until it auto-dismisses            | The clue is still there — tap the **"OPERATIVE CLUES"** chip        |
| Reconnect snapshot     | Background the app 10s, reopen                             | Clue re-fetches from the REST snapshot (not lost)                  |

---

## Known limitations (by design — not bugs)

- **Guidance, not progress.** Authoring an operative clue never advances the
  substage, resolves a target, or changes score — it only adds a clue to the
  assigned teams' boards (`currentScore` / `resolvedTargets` stay 0 until HU-31).
- **Author-only.** The backend exposes only the authoring POST — there is **no
  edit, retract, or list** endpoint, so an assigned clue can't be removed.
- **Trivia has no toast on the treasure-hunt board.** B2's toast is **trivia-only**;
  the treasure-hunt board reuses its existing CLUES-tab ember dot instead (avoids
  double-signalling).
- **The timer reads `00:00 / Expired` on a treasure-hunt substage (DES-93).** Not a
  bug — the authoritative timer only tracks the active trivia-question window.

## Troubleshooting

- **"Operative clues can be assigned once the session is Active or Paused."** The
  session isn't live — click **Start** as op-1 (Active), or **Pause** also works.
- **Assign button stays disabled.** You need **non-empty** clue text **and** ≥1 team
  selected.
- **Participant doesn't see the clue.** You assigned it to a team the participant
  isn't on — assign to **Gilded Owls** or **All teams**.
- **Clue doesn't appear live.** It should arrive via the P0 live push; if not,
  background/reopen the app to force a REST re-fetch, and confirm the api-gateway
  is up (`docker compose restart api-gateway`).
- **Stuck on "Restoring your team space…".** The operator hasn't pressed Start, or
  no team is attached. Start the session as op-1.
- **Mobile can't reach the backend.** It targets the gateway on `localhost:8000`;
  on a physical device use your machine's LAN IP, not `localhost`.

---

## Appendix — the seed spec

This test reuses the HU-171 mixed-play-mode seed
(`frontend/tests/e2e/session-substage-progress-manual-seed.spec.ts`) — its full
contents are already inlined in `frontend/docs/hu-171-manual-test.md` (Appendix).
HU-28 adds no seed of its own; the operative clue is authored live from the
operator UI in Step C.

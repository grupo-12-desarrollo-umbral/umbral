# Handoff — HU-36A live-start: timer desync + missing answered roster (2026-07-11)

Worktree: `/home/samu/Desktop/umbral-hu-36a` · Branch: `feature/hu-36a-trivia-answered-monitor`
Base commit: `580bfc1 feat(session-operations): HU-36A answered/not-answered monitor (backend + frontend)`

Continues the prior session captured in `frontend/docs/handoff-session-2026-07-10-hu36a-pause-timer-fix.md`
(the pause/timer-freeze fix). That fix is **still uncommitted** and remains in the working tree; this
session added two more fixes **on top of it**, in the same worktree. Nothing is committed yet.

---

## 1. What was reported

Starting a session from the operator live-operation view:
- the two countdowns were **out of sync** — the trivia-round "question" timer showed **0** while the
  top "Question timer" showed **30**; and
- there was **no visible answers/teams section** (the answered-monitor board).

## 2. Root causes (confirmed by driving a real session in a headless browser)

The bugs only appear on the **real operator flow**: open a session while it is **Preparing**, then click
**Start**. The existing e2e (`tests/e2e/session-answered-monitor.spec.ts`) drove sessions to **Active
before** opening the page, so it never exercised this path.

1. **Timer desync (first question only).** The `Start` (Preparing→Active) transition response carries a
   **pre-game placeholder** timer: `activeQuestion` present but `remainingSeconds: 0`, `isExpired: true`,
   `isAdvancing: false`. `runTransition` (and `loadTimerSnapshot`) hydrated the client trivia countdown
   from that placeholder **after** SignalR's `QuestionActivated` had already started it ticking at 30 —
   freezing it at **0**. The top "Question timer" self-recovered via the backend's 1 Hz worker ticks
   (`AuthoritativeSessionTimerWorker`, uses `remainingMilliseconds`), so it kept showing ~30. Later
   questions come purely from SignalR, so Q2+ were fine — only the first question desynced.
   - Note: the rendered text `"Question 10s"` is `"Question 1"` + `"0s"` (seq order glued to `{left}s`),
     i.e. the countdown was genuinely **0**, not 10. This misled early analysis — watch for it.

2. **Answered board stuck on "Waiting for the team roster…".** The roster was only fetched at mount.
   Opening while Preparing returns `no_active_question` → empty roster; nothing refetched it when the
   question later activated, so the board never populated.

## 3. The fix (all in the working tree — see `git diff`)

- `frontend/app/dashboard/timer-snapshot.ts` (new): `isNonLiveQuestionSnapshot(timer)` — a snapshot whose
  question has `remainingSeconds <= 0` and is not advancing is the pre-game placeholder / a just-expired
  question, **not** a live countdown. A genuinely **paused** question always has remaining time, so this
  excludes only the dead states (keeps the pause-freeze fix intact).
- `frontend/app/dashboard/DashboardClient.tsx`:
  - New `applyTimerSnapshotToTriviaRound()` used by both `loadTimerSnapshot` and `runTransition`; it
    hydrates the trivia countdown only when the snapshot is **not** a non-live placeholder. `runTransition`
    also skips loading the placeholder into the top panel (avoids a 1 s `00:00` blip on Start).
  - `onQuestionActivated` now also calls `loadAnsweredMonitor(...)` so the roster loads the moment a
    question activates (covers opening the session before any question was active).
- `frontend/app/dashboard/TriviaRoundPanel.tsx`: added `data-testid="trivia-round-time-left"` so the
  countdown is assertable in e2e.

## 4. Tests added / status

- `frontend/tests/unit/app/dashboard/timer-snapshot.test.ts` (new, 5 cases): pins placeholder-vs-paused.
- `frontend/tests/e2e/session-operator-live-start.spec.ts` (new): mirrors the real flow (open Preparing →
  Start), asserts both countdowns start > 0, agree within ~3 s, **tick down together**, the 3-team roster
  renders (`/ 3 answered`), and **Pause freezes both** on the same second.
- Verification run this session (in `frontend/`):
  - `pnpm exec vitest run tests/unit/app/dashboard tests/unit/app/lib/realtime` → **32 passed**.
  - `pnpm exec playwright test tests/e2e/session-operator-live-start.spec.ts --project=chromium` → **1 passed** (~1 min).
  - `pnpm exec tsc --noEmit` → clean **except** the pre-existing, unrelated
    `tests/unit/app/lib/keycloak-tokens.test.ts` `TS2540 Cannot assign to 'NODE_ENV'` (noted in the prior
    handoff; not touched here).
  - `pnpm exec eslint` on the four changed source files → clean.

## 5. Working-tree state (UNCOMMITTED)

```
 M frontend/app/dashboard/DashboardClient.tsx
 M frontend/app/dashboard/TriviaRoundPanel.tsx
 M frontend/app/lib/realtime/use-trivia-round-state.ts            (prior session's pause fix — net-unchanged here)
 M frontend/tests/unit/app/lib/realtime/use-trivia-round-state.test.ts  (prior session's pause tests)
?? frontend/app/dashboard/timer-snapshot.ts
?? frontend/tests/unit/app/dashboard/timer-snapshot.test.ts
?? frontend/tests/e2e/session-operator-live-start.spec.ts
?? frontend/docs/handoff-session-2026-07-10-hu36a-pause-timer-fix.md
?? frontend/docs/handoff-session-2026-07-11-hu36a-start-timer-sync-and-roster.md  (this file)
```

The `git diff` for `DashboardClient.tsx` shows the base call sites as `hydrateActiveQuestion(x)` (no 2nd
arg) because the diff is vs commit `580bfc1`, which predates the prior session's uncommitted pause fix.
The current working tree's `applyTimerSnapshotToTriviaRound` **preserves** the `isAdvancing` argument the
pause fix relies on. Don't "restore" the single-arg form.

## 6. Next steps

- **Commit** the combined work (prior pause fix + this session's two fixes). Per repo memory: local-squash
  the branch to one commit **before** opening the PR (keep the merge arc; never the GitHub squash button);
  commit messages carry **no** `Co-Authored-By: Claude` trailer.
- Optional follow-up still open from the prior handoff (out of scope, unfixed): the SignalR **other-tab**
  pause path (`onStateChanged(Paused) → resetTriviaRound()`) **blanks** the trivia panel instead of freezing
  it. Not the reported bug; revisit only if multi-tab parity matters.

## 7. How to reproduce / re-verify (fixtures + gotchas)

Environment reach-the-board details, identity-rotation caveats, and the fixture matrix are in the prior
handoff (`handoff-session-2026-07-10-hu36a-pause-timer-fix.md` §2–3) and
`frontend/docs/hu-36a-frontend-test-workflow.md` — not duplicated here. Key reminders:
- Backend stack + Keycloak must be up (`docker ps`; gateway `:8000`, Keycloak `:8080`). Frontend dev on
  `:3000` runs from **this** worktree.
- **Running any e2e re-provisions `op-1` in Keycloak with a fresh `sub`/identity Id** (`global-setup`), which
  **orphans** the hand-driven fixture sessions from the prior handoff. Re-create a fixture (or just run the
  new spec, which creates its own session) rather than relying on the old codes after an e2e run.
- The new spec is the fastest deterministic repro; the manual browser procedure is prior-handoff §3.

## Suggested skills for the next session

- **signalr-websockets-aspnetcore** — if extending the timer/pause realtime behaviour (hub events
  `SessionTimerUpdated` / `QuestionActivated` / `TeamAnswered` drive the two countdowns and the board).
- **verify** — drive the live flow (open Preparing → Start) before committing, not just tests/typecheck.
- **code-review** — review the combined diff before the PR.
- Use `pnpm exec vitest` / `pnpm exec tsc --noEmit` / `pnpm exec playwright test` in `frontend/`; then
  squash-to-one before the PR (repo convention).

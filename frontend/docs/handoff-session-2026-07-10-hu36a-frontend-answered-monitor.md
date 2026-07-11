# Handoff — HU-36A Frontend Answered/Not-Answered Monitor

**Date:** 2026-07-10 (updated 2026-07-11)
**Worktree:** `/home/samu/Desktop/umbral-hu-36a` (branch `feature/hu-36a-trivia-answered-monitor`)
**Plan (source of truth):** `frontend/plans/hu-36a-frontend-answered-monitor.md` (lives in the **main** worktree `/home/samu/Desktop/umbral/frontend/plans/`)
**Status:** ✅ **Committed, squashed, pushed. PR [#163](https://github.com/grupo-12-desarrollo-umbral/umbral/pull/163) open against `develop`.** Full backend + e2e suites run; all HU-36A tests green (see Session 2).

---

## Session 2 update (2026-07-11) — review, commit, PR, full test run

**Branch decision (resolved):** kept on the backend branch `feature/hu-36a-trivia-answered-monitor`
so backend endpoint + frontend board + e2e ship as one cohesive PR. Whole branch (6 commits: 5 backend
phase commits X.1–X.4 + route-doc fix, plus the frontend commit) was rebased onto latest `origin/develop`
and **squashed to a single commit** (`8af3269`) under the merge arc — never GitHub's squash button.
PR **#163** opened via the `safe-pr-creator` skill (fixed template; base `develop`).

### `/code-review high` findings — 4 of 5 fixed, all folded into the squashed commit
1. **Transient errors mislabeled "not authorized"** — `loadAnsweredMonitor` collapsed every `{error}` to
   the unauthorized state. Fixed: action now returns a 4-way union (`data` / `noActiveQuestion` /
   `unauthorized` / `error`); genuine auth (`IdentityError.code === 'unauthorized'`) is kept distinct from
   transient/5xx/network failures, which render a new retryable **error** state in the panel.
2. **Stale `TeamAnswered` flipped the wrong question** — the live handler ignored the event's question
   order. Fixed: `teamAnswered` action carries `order`; the reducer drops it when `order !== activeQuestionOrder`.
3. **Empty roster read as "no active question"** — conflated two states. Fixed: split into a distinct
   `answered-monitor-no-roster` ("waiting for the team roster…") state that still shows `Question N`.
4. **Stale-session race** (affected the timer loader too) — fixed in **both** loaders via a
   `selectedRealtimeSessionIdRef` synced in the reset effect; each loader drops a late response whose
   `liveSessionId` no longer matches the current selection.
5. **Unknown-team live answer dropped** — *not fixed (by design).* Self-heals on next snapshot; a proper
   fix needs the backend `TeamAnswered` payload to carry `displayName`/`teamCode` (it only has `teamId`).

### Full test results (Session 2)
- Frontend unit (`vitest run`): **48 passed** (+2 new: error state, no-roster/no-question split).
- Frontend typecheck (`next build`): clean.
- Backend unit: **491 passed** (Domain 267 + Application 224).
- Backend integration (Testcontainers): **245 passed**. One `AuthoritativeSessionTimerWorker` timing test
  (unrelated auto-advance path) flaked once, **passed on isolated re-run**.
- Full Playwright e2e: **172 passed, 3 failed — all pre-existing, none HU-36A**:
  - `mission-hierarchy.spec.ts` "adds a target with a clue" / "seeds from the saved target" — in-progress
    target-score feature (`addTarget` → 500);
  - `session-operator-timer.spec.ts` countdown — its `beforeAll` hardcodes `missionId: 1`, absent on the
    persistent dev DB (see e2e gotcha below), so setup gets an empty body → `Unexpected end of JSON input`.
  - **`session-answered-monitor.spec.ts` passed.**

**Env note (not a code issue):** the integration project first failed to build with `NETSDK1064` because the
`src/*` restore assets pointed at a stale `/tmp/nuget` packages path. Resolved by cleaning `obj/bin` under the
service and pinning `NUGET_PACKAGES=/home/samu/.nuget/packages` for the restore+build.

---

## What this session did

Implemented all 4 phases of the plan. The frontend was built in the **same worktree as the HU-36A backend**
(`feature/hu-36a-trivia-answered-monitor`), not the plan's nominal `feature/hu-36a-frontend-answered-monitor`
branch, so backend endpoint + frontend board + e2e form one cohesive feature and the e2e can drive the real stack.

**Decision to confirm with the user:** keep it on the backend branch, or split the frontend onto its own branch.

### Key situational change vs. the plan
The plan held Phase 2 at "contract altitude" because the snapshot endpoint was unbuilt. **It is now implemented**
in this worktree and was verified against source, unblocking Phase 2 into code-complete:

- **Route (Q1):** `GET /api/sessions/{liveSessionId}/answered-monitor`
  (`backend/services/session-operations-service/src/Api/Controllers/SessionsController.cs:241`). Gateway routes it
  via the existing `/api/sessions/{**catch-all}` rule — no gateway change needed.
- **DTO (Q2):** `TriviaAnsweredMonitorDto { liveSessionId, substageSnapshotId, questionSequenceOrder, teams: [{ teamId, teamCode, displayName, answered, answeredAt }] }`
  (camelCase on the wire). It carries its **own roster**, so the `/teams` fallback (Q3) is unnecessary.
- **Q4:** the monitor DTO's `teamId` == the `TeamAnswered` event's `teamId` == runtime `Team.TeamId`. The board keys
  rows on that id consistently; no reference-id reconciliation needed.
- **New finding (not in plan):** "no active trivia question" is a **409 Conflict** (the backend *throws*
  `TriviaAnswerRequiresActiveQuestion/TriviaSubstageException`), not an empty 200. Handled explicitly (see below).

### Files changed (see `git diff` / `git status` in the worktree)
- `frontend/app/lib/definitions.ts` — `TeamAnsweredNotificationDto`, `TriviaAnsweredMonitorDto`, `TriviaTeamAnsweredStatusDto`.
- `frontend/app/lib/realtime/session-state-client.ts` — `onTeamAnswered` option + `normalizeTeamAnswered` + `TeamAnswered` subscription (mirrors the 5 existing normalizers/subscriptions).
- `frontend/app/lib/sessions.ts` — `getOperatorTriviaAnsweredMonitor` (mirrors `getOperatorSessionTimerSnapshot`; **409 → `throw new Error('no_active_question')`**, 403/401 → `IdentityError`).
- `frontend/app/actions/sessions.ts` — `getTriviaAnsweredMonitorAction` returning a **3-way** union: `{ data } | { noActiveQuestion: true } | { error }`.
- `frontend/app/dashboard/AnsweredMonitorPanel.tsx` + `answeredMonitorPanel.module.css` — **new** presentational panel. Props are leak-safe by shape (no option/correctness/points field).
- `frontend/app/dashboard/DashboardClient.tsx` — `answeredMonitorReducer` + state; wired `onTeamAnswered` (live add) and boundary clears into the **existing** `onQuestionActivated`/`onQuestionClosed`/`onSubstageAdvanced` callbacks; snapshot seed via `loadAnsweredMonitor` in the reset effect + `onReconnected`; `<AnsweredMonitorPanel>` mounted after `<TriviaRoundPanel>`.
- **Tests:** `tests/unit/app/lib/realtime/session-state-client.test.ts`, `tests/unit/app/dashboard/answered-monitor-panel.test.ts`, `tests/e2e/session-answered-monitor.spec.ts`.

### State model (DashboardClient)
`answeredMonitorReducer` holds `{ loading, unauthorized, activeQuestionOrder, roster[], answeredAt{} }`.
`answeredAt` is keyed by runtime team id (presence ⇒ answered). Snapshot `loaded` seeds it; live `teamAnswered`
adds to it; `questionActivated` keeps the roster but clears answers + sets the new order; `questionClosed`
(also used for substage-advanced) clears + sets `activeQuestionOrder=null` → empty state. Rows are derived by
**roster enumeration** (`answered = id in answeredAt`), never by broadcast absence.

---

## Gate results (all green, this session)
- `next build` (the typecheck gate — there is no `typecheck` script) — **pass**. The lone `tsc --noEmit` error is a
  **pre-existing** `NODE_ENV` readonly issue in `tests/unit/app/lib/keycloak-tokens.test.ts:46` (present in HEAD, not
  in the build graph, unrelated).
- `vitest run` — **46 passed** (10 new).
- e2e `tests/e2e/session-answered-monitor.spec.ts` — **pass (~45s)** against the live docker stack: real snapshot
  endpoint → operator hero mounts the board, roster row `data-answered="false"`, count shown, no leak.

Run gates from the worktree (`node_modules` was installed here via `pnpm install --offline --frozen-lockfile`):
```
cd /home/samu/Desktop/umbral-hu-36a/frontend
node_modules/.bin/vitest run
node_modules/.bin/next build
node_modules/.bin/playwright test session-answered-monitor.spec.ts --workers=1
```

---

## Deliberate scope decision — e2e does NOT drive a real live flip
The "participant answers → row flips to `data-answered="true"` live" path was **not** e2e-driven. Producing a real
`TeamAnswered` requires an *accepted* participant answer, which is gated by cross-service participant-membership
validation (`RuntimeParticipationGuard` → identity-access `POST /api/permissions/participant-membership-access`).
Confirmed blockers:
- The backend's own integration tests **stub** this (`FakeParticipantMembershipAccessClient`).
- identity-access looks up `registered_teams` by the **runtime** team id, but the runtime `Team.TeamId`
  (e.g. `1c19cc4b…`) ≠ the reference/registered id (`a0000000-…-0001`), so it can't be seeded without hacking the
  identity DB. `global-setup` also deletes `registered_team_memberships`.

The live-flip mechanic is instead covered **deterministically by unit tests**: the `TeamAnswered` subscription
normalizer test + the panel's `data-answered="true"` render test. This is documented in the e2e spec header.
If a future session wants a real live-flip e2e, it must first solve the runtime→reference team-id membership
seeding (a backend concern).

### e2e gotchas learned
- `missionId` must be resolved dynamically — the seed mission ("E2E Seed Mission", Ready) is **not** id 1 in the
  persistent dev DB (it was id 26). The spec queries `mission_design."Missions"` for it (mirrors how the timer spec
  should, but that one hardcodes `1` and only works on a fresh CI DB).
- beforeAll seeds a **sub-keyed** admin identity row so the gateway-JWT `operator-assignment` path resolves the actor
  (same trick as `session-operator-timer.spec.ts`).
- Port 3000 must be free so Playwright's `webServer` (`pnpm dev`, `reuseExistingServer: !CI`) starts **this**
  worktree's code, not a stale server.

---

## Next steps for the continuing session
1. ✅ **Done (Session 2):** kept on the backend branch, squashed to one commit (`8af3269`), PR #163 open. See Session 2 update above.
2. ✅ **Done (Session 2):** full e2e suite run — no HU-36A regressions; the 3 failures are pre-existing/unrelated. See Session 2 update above.
3. **Open — merge gate call (owner's):** the 3 pre-existing e2e failures (target-score `addTarget` 500 ×2, timer
   spec's hardcoded `missionId: 1`) are not this branch's code. Decide whether they block the merge on your CI gate,
   or fix them on their own branches. Worth doing regardless: give `session-operator-timer.spec.ts` the same dynamic
   mission-id resolution `session-answered-monitor.spec.ts` uses, so it stops failing on the persistent dev DB.
4. If a real live-flip e2e is desired, resolve the participant-membership seeding blocker described above first.

## Suggested skills
- `frontend` `/run` or `/verify` — to re-drive the board in the real app if further UI changes are made.
- `code-review` (or `/code-review high`) on the diff before opening the PR.
- `aspnet-backend-testing` — only if the participant-membership seeding blocker is tackled to enable a real live-flip e2e.

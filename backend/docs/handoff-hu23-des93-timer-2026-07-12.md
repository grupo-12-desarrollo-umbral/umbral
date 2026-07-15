# Handoff — HU-23 live team-board: env unblock + push-filter fix (2026-07-12)

Branch: `fix/hu-23-board-refetch-on-active` (off `develop`) · **everything uncommitted**

---

## LATEST (2026-07-12, continued) — HU-23 close decision + DES-93 timer design

This continuation session did **no code changes** — it assessed HU-23 for "Done" and
locked the design for the real timer fix. Read this block first; Findings 1–3 below
(the board render fix) are still the uncommitted working-tree state and remain valid.

### Should HU-23 (DES-31) be marked Done? — **not yet**

Against the 5 ACs: **4/5 solidly met**, 1 consciously descoped.

| AC | Status |
|----|--------|
| Ver puntaje acumulado | ✅ renders 0 — real scoring descoped to HU-31 (already in ticket body) |
| Ver pistas habilitadas | ✅ 3 clue cards |
| Ver temporizador de la sesión | ⚠️ **the gap** — widget renders but shows 00:00 / Expired, never ticks → split to **DES-93** |
| Info se actualiza sin recarga | ✅ verified (REST + push) |
| Actualización en tiempo real vía SignalR | ✅ `TeamBoardUpdated` frame captured on the wire |

Two reasons to hold off on Done: (1) the work is **uncommitted** — no commit/PR/review;
Done should follow the merge. (2) DES-31 was already flipped Done → reverted to In
Progress on 2026-07-11 when the board didn't render (the exact bug Findings 1–3 fixed).
Don't repeat that. Close only after: `/verify` + `/code-review` → squash to one commit →
PR → merge → then Done, with a note that AC#3 is met at "renders" level and live
countdown is DES-93 (mirroring how score-0 is already descoped to HU-31 in the body).

### No Linear writes were made

Two writes were drafted but **not posted** (auto-mode guard blocked them, pending
explicit user go-ahead — still open decisions):
1. A DES-31 comment recording on-device verification + the AC#3 timer descope.
2. A DES-93 description update locking in the design below.
Repo quirk to remember: `gh pr edit --title/--body` silently aborts in this repo — use
`gh api` REST PATCH for PR edits. Linear writes go via the linear MCP tools.

### DES-93 — the real timer fix (user-confirmed design)

A "known limitation" note does **not** make the timer work; DES-93 is the increment that
does. **Decision:** treasure-hunt substages carry **their own `MaximumTime`**, parallel to
how a trivia substage's time is the sum of its questions' countdowns. Trivia stays
question-derived; treasure-hunt gets a new explicit field. Work goes on a **new `des-93`
branch off `develop`** — NOT folded into the HU-23 board PR (different concern/service).

This is a **two-service, migration-bearing** feature: `Substage` has `PlayMode` but **no
time field**, and neither does the runtime `SubstageSnapshot`. Scope (verified against
source last session — re-verify before coding):

- **Phase 1 — mission-design-service** (field's home): add `MaximumTime` to `Substage` for
  treasure-hunt (e.g. `CreateTreasureHunt(title, seq, maxTime)`), validated required/positive
  for TreasureHunt, trivia unchanged; EF Core config + PostgreSQL migration (new column);
  authoring command/API + validators.
- **Phase 2 — session-operations-service** (consume): add `MaximumTime` to `SubstageSnapshot`
  and carry it in the runtime-snapshot projection at `POST /api/sessions`; in `LiveSession`
  add a treasure-hunt substage timer seeded from the active substage's `MaximumTime`,
  anchored at substage activation, frozen on pause — **mirror the existing question-timer
  machinery** (`ResumeQuestionTimer` / `FreezeQuestionTimer` /
  `CalculateAdvancingQuestionTimerRemaining`, `LiveSession.cs` ~764–799); branch
  `GetAuthoritativeSessionTimerSnapshot` by active-substage `PlayMode`;
  `AuthoritativeSessionTimerWorker` decides expiry (report-only vs auto-advance).

Existing anchors already in `LiveSession.cs`: `StartedAt` (`StartedAt ??= occurredAt`
~line 689), session-level `MaximumTime` (seed 60 min), `PausedAt` / `LastStateChangedAt`.
**Mobile needs no change** — the board already renders the timer, so it starts counting
once the snapshot carries a nonzero value.

### Two open decisions for the user before coding

1. Post the two Linear writes above? (both blocked, awaiting go-ahead)
2. Depth: draft the full DES-93 plan first (recommended for a migration-bearing two-service
   change; user was leaning this way) vs. go straight to Phase 1. See `prd-to-plan`.

---

This continues the slice described in
[`backend/docs/hu23-teamboard-live-push-handoff-2026-07-11.md`](backend/docs/hu23-teamboard-live-push-handoff-2026-07-11.md)
(read it first for the DES-31 scope, the backend `BroadcastTeamBoardNotificationHandler`, and the
mobile `sessionState` re-fetch). Manual test:
[`frontend/docs/hu-23-manual-test.md`](frontend/docs/hu-23-manual-test.md). This doc only records what
**this** session found and changed — do not duplicate the above.

## In one line

The board still didn't show after the prior slice. Two independent causes: (1) the backend service was
**wedged** so the new push handler was never registered, and (2) a **mobile push-filter bug** silently
dropped every `TeamBoardUpdated` frame. Fixed both; also surfaced a previously-swallowed board-fetch
error.

## Finding 1 — backend service was wedged (env, not code)

Symptom: mobile stuck on the trivia fallback; operator showed the session `Active`.

- Verified live DB (`session_operations`) for session code `09E559`
  (`3063a42f-0839-4e41-b4f8-815b9fbbc9f6`): `state=Active`, `active_substage_id` → a `TreasureHunt`
  substage (`play_mode=TreasureHunt`). Team *Gilded Owls*: per-session id `d7f15865-…`, reference id
  `a0000000-…`. So the backend **data** was correct; the query handler returns a valid board.
- Operator "No active question / No active trivia question" is **expected** for a treasure-hunt
  substage (that panel is trivia-only) — not a bug.
- Root cause: `backend-session-operations-service-1` `dotnet watch` was parked at **"Fix the error to
  continue"**. After an OOM (`Exited with error code 137`), a partial NuGet restore left
  `error NETSDK1064: Package MediatR.Contracts 2.0.1 was not found` on `Domain.csproj`, so the restart
  never completed. The old process kept serving REST, but **MediatR registers notification handlers via
  assembly scan at startup** — a hot-reloaded new `INotificationHandler` is NOT added to the DI
  registry. So `BroadcastTeamBoardNotificationHandler` was never actually invoked.
- Fix applied (env only, no code): `dotnet restore src/Api/Api.csproj` inside the container (restore now
  succeeds), then `docker restart backend-session-operations-service-1`. Clean build, app started,
  handler now registered. Session state persisted (Postgres), `09E559` still `Active`.

If this recurs: the tell is the "Fix the error to continue" line + a stale process start time. Same fix
(restore inside the container, then restart).

## Finding 2 — mobile push-filter dropped every `TeamBoardUpdated` (real bug, FIXED)

`mobile/src/lib/realtime/use-team-board.ts` filtered pushes with `if (pushed.teamId !== teamId) return;`
where the hook's `teamId` prop is the **reference** id (`a0000000-…`, what the lobby/reconnect/REST
guard require — the guard `ValidateAsync` checks identity-access membership keyed by the reference id).
But every board DTO (REST snapshot **and** push) carries the **per-session** id (`Team.TeamId`,
`d7f15865-…`) from `ProjectParticipantTeamBoard` / `ParticipantTeamBoardDtoFactory`. So the equality
check was always true → **every push discarded**.

- The SignalR **group** routing is correct (`team:{result.TeamId}` join == `team:{board.TeamId}`
  broadcast, both per-session), so the push reached the connection — only the client filter threw it
  away.
- `useSessionTimer` filters its push by `liveSessionId` **only** (no team check), which is why the
  board still rendered on Start: the timer push flips `sessionState`→`Active`, triggering the board's
  REST re-fetch. The board **push** path was dead; the **re-fetch** path masked it for the Start case.
  Substage-advance (keeps `State=Active`, no re-fetch) would have shown nothing live — that's the case
  the push exists for.
- The old unit test used `'team-1'` for both the prop and the DTO, so it never exercised the mismatch.

**Fix:** removed the `teamId` equality check (kept the `liveSessionId` guard), mirroring
`useSessionTimer`; the single server-side `team:{perSessionId}` group already scopes the push. Reworked
`mobile/src/__tests__/team-board-hook.test.ts` to use distinct `REFERENCE_TEAM_ID` (prop) vs
`PER_SESSION_TEAM_ID` (`BASE_BOARD.teamId`) so it now guards the contract — verified by temporarily
re-adding the buggy line and confirming 2 tests fail, then removing it.

## Finding 3 — board-fetch error was swallowed (UX gap, FIXED)

`team-space.tsx` destructured only `{ board }` from `useTeamBoard`, ignoring `error`. A failed/401/403
board fetch fell silently to the trivia surface — indistinguishable from "no active substage", which
slowed this very diagnosis. Wired the error through: new `boardErrorCopy()` + a `signalCritical` `Panel`
banner shown **only when the error actually masks the board** (`const maskedBoardError = board ? null :
boardError`), so a retained good board during a failed re-fetch stays untouched. Covered by two new
`team-space-question-stage.test.tsx` tests.

## Files changed this session (all uncommitted, working tree only)

- `mobile/src/lib/realtime/use-team-board.ts` — push-filter fix + JSDoc rewrite.
- `mobile/src/__tests__/team-board-hook.test.ts` — distinct ref/per-session ids; contract-guarding test.
- `mobile/src/app/(app)/team-space.tsx` — board-error banner wired through.
- `mobile/src/__tests__/team-space-question-stage.test.tsx` — 2 banner tests.

Untracked backend/frontend files from the prior slice (the handler, its test, the manual-test docs, the
seed spec) are unchanged this session. No backend source changed here — Finding 1 was an env fix only.

## Verification (green this session)

- Mobile: `tsc --noEmit` clean, eslint clean, jest **196/196**.
- Backend: service rebuilt clean and started; handler registered. **Not re-driven on device.**

## What still needs doing / how to re-test

- **Re-seed a fresh Preparing session** — `09E559` is already `Active`, so you can't watch the
  Preparing→Active flip on it. Re-run `frontend/tests/e2e/session-treasure-hunt-manual-seed.spec.ts`.
- **Ensure the mobile app runs the current JS bundle** — all mobile changes are uncommitted; a stale
  Metro bundle won't have them.
- **On-device verification not yet done.** Watch: (a) board flips live on Start, and (b) — the real
  proof of the push fix — the board updates on **substage-advance** with no reload (the path with no
  re-fetch fallback).
- Backend `currentScore`/`resolvedTargets` still push as 0 until HU-31/DES-42 (unchanged; by design).
- Nothing committed/pushed. Per project convention, local-squash the branch to one commit before
  opening a PR (keep the merge arc; do not use the GitHub squash button). Run `/verify` +
  `/code-review` first.

## Suggested skills for the next session

- `/verify` and `/code-review` — before committing.
- `signalr-websockets-aspnetcore` — if touching the emit/group logic again.
- `cqrs-mediatr-aspnetcore` — the push is a MediatR notification handler.
- `native-data-fetching` — mobile hook/fetch patterns.
- `handoff` — if compacting the next session again.

## Archived previous handoff

The `HANDOFF.md` that occupied this file before was an unrelated **RabbitMQ academic-requirements
alignment** doc (2026-07-11). Because `/HANDOFF.md` is gitignored (not recoverable from git), it was
preserved verbatim at
[`backend/docs/handoff-rabbitmq-academic-requirements-2026-07-11.md`](backend/docs/handoff-rabbitmq-academic-requirements-2026-07-11.md)
before this handoff replaced it.

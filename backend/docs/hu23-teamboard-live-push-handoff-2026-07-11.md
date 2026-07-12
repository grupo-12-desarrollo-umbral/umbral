# Handoff — HU-23 team-board live push + mobile stale-board fix (2026-07-11)

Date: 2026-07-11 · Branch: `fix/hu-23-board-refetch-on-active` · **uncommitted**

Context for HU-23/DES-31 already lives in [`hu23-context.md`](hu23-context.md) and
[`hu23-brief.md`](hu23-brief.md) — read those for scope/canon. This handoff only records the bug
we hit live and the slice we did to close it. Manual-test walkthrough:
[`../../frontend/docs/hu-23-manual-test.md`](../../frontend/docs/hu-23-manual-test.md).

## In one line

The mobile treasure-hunt board never rendered after the operator pressed Start; root cause was a
one-shot board fetch with **no backend push**. Fixed both ends: a mobile re-fetch on session-state
change **and** the missing backend `TeamBoardUpdated` SignalR emit (DES-31's last two ACs).

## The bug we were debugging (symptom → root cause)

- **Symptom.** Operator session `Active` with a `TreasureHunt` active substage, but the mobile
  participant stayed on the trivia "No active question yet" fallback — never the board.
- **Verified the backend was correct.** DB: `live_sessions.active_substage_id` → the `TreasureHunt`
  substage (play_mode `TreasureHunt`, seq 1). Hitting the REST endpoint as participant-1 returned
  the right board (HTTP 200, `activeSubstage.playMode="TreasureHunt"`, 3 targets, 3 clues):
  `GET {gw}/api/sessions/{id}/participants/team-board?teamId=…`. So the data was there.
- **Root cause (mobile).** `mobile/src/lib/realtime/use-team-board.ts` fetched the board **only** on
  reconnect / SignalR transport reconnect (`reconnectNonce`). The participant joined while the
  session was still `Preparing` (`active_substage_id = NULL` → `activeSubstage: null` →
  `playMode` undefined → trivia fallback in `team-space.tsx:309`). Operator Start → `Active`, but
  **no `TeamBoardUpdated` push existed**, so the board never refreshed. The screen was stuck on the
  stale pre-Active snapshot.
- **Immediate workaround (still valid):** background the app ~10s + reopen → SignalR auto-reconnect
  bumps `reconnectNonce` → re-fetch. That is the "Reconnect snapshot" row in the manual test.

## The slice we did

Two coherent halves, all uncommitted on `fix/hu-23-board-refetch-on-active` (off `develop`).

### 1. Backend — the missing `TeamBoardUpdated` emit (was never wired)

The SignalR plumbing already existed and was DI-registered but **nothing called it**:
`ITeamBoardBroadcaster` + `Api/Hubs/SignalRTeamBoardBroadcaster.cs` (method `"TeamBoardUpdated"`,
group `team:{teamId:D}`). New file:

- `src/Application/Sessions/EventHandlers/BroadcastTeamBoardNotificationHandler.cs` — implements
  `INotificationHandler<SessionStateChangedEvent>` **and** `INotificationHandler<SubstageAdvancedEvent>`,
  both delegating to one read-only helper: re-load session → for each `liveSession.Teams` project
  `ProjectParticipantTeamBoard(team.TeamId, now)` → build DTO exactly like
  `GetParticipantTeamBoardQueryHandler` → `BroadcastTeamBoardUpdatedAsync`. No mutation, no
  `UpdateAsync`, so no re-entrancy loop. Auto-registered via MediatR assembly scan (same as every
  other handler — none are manually listed in `Api/DependencyInjection.cs`).
- Tests: `tests/Application.UnitTests/Sessions/EventHandlers/BroadcastTeamBoardNotificationHandlerTests.cs`
  (state-change, substage-advance, one-broadcast-per-team on a 3-team session).

**Load-bearing correctness — group-id match (the trap we checked).** Broadcaster targets
`team:{board.TeamId}`; `board.TeamId == snapshot.TeamId == team.TeamId` = the **per-session**
`Team.TeamId` (e.g. `d7f15865-…`), NOT the `ReferenceTeamId` (`a0000000-…`). The hub joins the
participant to `team:{result.TeamId}` where `result.TeamId == admission.Team.TeamId` — the same
per-session id. So iterating `session.Teams` and keying on `team.TeamId` hits the right group.
`GetTeam()` accepts either id, which is why the reference-id REST query still resolves.

**Events chosen:** every `SessionStateChangedEvent` (covers Preparing→Active, pause/resume/end) and
every `SubstageAdvancedEvent` (active substage + visible clues change; note this keeps `State=Active`,
so the mobile re-fetch below can't catch it — the push is required here).

### 2. Mobile — re-fetch on session-state change

- `mobile/src/lib/realtime/use-team-board.ts` — new `sessionState?: string | null` prop added to the
  fetch effect deps; `team-space.tsx` threads `useSessionTimer`'s `sessionState` in. Chosen over
  `snapshotVersion` because `snapshotVersion` only bumps on the REST fetch (reconnect/resync); the
  live `TimerUpdated` push flips `sessionState` to `Active` **without** bumping it. Test added in
  `mobile/src/__tests__/team-board-hook.test.ts` (Preparing→Active triggers a second fetch).

### 3. Docs kept in sync

- `frontend/docs/hu-23-manual-test.md` "Known limitations" reworded (push now exists on state-change
  + substage-advance; score/progress still deferred).
- The now-false "no `TeamBoardUpdated` … don't wait for one" comment fixed in **both**
  `frontend/tests/e2e/session-treasure-hunt-manual-seed.spec.ts` (lines ~16-18) and its inlined copy
  in the manual-test Appendix (kept in sync per that doc's line 141 contract).

## Verification (green this session)

- Backend: `dotnet build src/Api/Api.csproj` clean (2 pre-existing unrelated CS8629 warnings).
  `dotnet test tests/Application.UnitTests` → **235/235** (incl. 3 new). Docker stack NOT re-driven.
- Mobile: jest **194/194**, `tsc --noEmit` clean, eslint clean.
- **Not yet driven on a physical device** — the live flip on Start should be watched on-device before
  commit (the whole reason for the fix).

## What is NOT done / deferred (do not treat as HU-23 gaps here)

- `currentScore` / `resolvedTargets` still push as **0** — meaningful only once **HU-31 / DES-42**
  (QR target resolution) lands. Live clue release is **HU-27 / DES-37**. Ranking is DES-54.
- No push is emitted on score/target-progress change (no such backend event exists yet). When DES-42
  lands, add its resolution event to this handler's notification list.

## Git state

Everything above is **uncommitted** on `fix/hu-23-board-refetch-on-active`. Untracked new files:
the backend handler + its test, `frontend/docs/hu-23-manual-test.md`, the seed spec, this handoff.
Modified: the three `mobile/` files. (`frontend/docs/hu-14a-manual-test.md` is unrelated pre-existing.)
Nothing pushed. DES-31 is In Progress in Linear (bounced Done→In Progress 2026-07-12 00:11 UTC);
its two live-update ACs are what this slice satisfies.

## Suggested skills for the next session

- `signalr-websockets-aspnetcore` — if extending the emit (e.g. adding the DES-42 resolution event,
  or reviewing group/hub-context usage).
- `cqrs-mediatr-aspnetcore` — the emit is a MediatR notification handler; use when touching it.
- `aspnet-backend-testing` — for the handler test patterns.
- `/verify` and `/code-review` before committing; on-device check via `mobile/` Expo run.

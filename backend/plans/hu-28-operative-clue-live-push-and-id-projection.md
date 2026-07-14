# HU-28 — Backend: Operative-Clue Live Push + Board Id Projection

**Ref:** HU-28
**Branch:** feature/hu-28-operative-clues (authoring X.1–X.4 already committed on this branch)
**Date:** 2026-07-14
**Pairs with:** `frontend/plans/hu-28-frontend-operative-clue-authoring.md` (operator web + mobile
reveal). That slice **consumes** the two contract changes below; this plan **produces** them.

> ⚠️ **Read first:** `backend/AGENTS.md` + `../CONTEXT-MAP.md`. This is session-operations-service
> (Clean Architecture / MediatR / SignalR). Verify every anchor against source before asserting it.

---

## Context

HU-28 authoring is landed (X.1–X.4): an operator authors a free-text operative clue and assigns it to
one/several/all teams during an Active/Paused session, and it already surfaces on the participant board
through the **existing** `visibleClues[]` projection (team-scoped, server-enforced). Two gaps remain
that make the participant experience correct and live — both small, both backend-only:

1. **No live push.** Authoring raises `OperativeClueAddedEvent` but nothing handles it, so the clue only
   reaches a participant on the next board fetch (reconnect / `sessionState` change). For a live game
   this is a broken reveal.
2. **No stable clue identity on the board.** `VisibleClueDto` carries only `targetSnapshotId` (null for
   operative clues), so the mobile client has no id to key list rendering / new-clue tracking on. The id
   already exists on the domain object and the event — it just isn't projected.

Both are prerequisites for the frontend/mobile slice: the mobile board keys on `operativeClueId` (P4.1)
and the trivia surfacing (P4.2) needs live delivery.

## Verified anchors (source-checked this session)

- `Application/Sessions/EventHandlers/BroadcastTeamBoardNotificationHandler.cs` — handles
  `SessionStateChangedEvent` + `SubstageAdvancedEvent`; re-loads the session read-only and broadcasts
  **every** team's board via `_teamBoardBroadcaster.BroadcastTeamBoardUpdatedAsync(board, ct)`. Never
  mutates state (no re-entrancy). **Nothing handles `OperativeClueAddedEvent`.**
- `Domain/Events/OperativeClueAddedEvent.cs` — carries `OperativeClueId`, `LiveSessionId`, **`TeamId`**,
  `ClueText`, `CreatedByUserId`, `CreatedAt`. One event raised **per team** at
  `Domain/Entities/LiveSession.cs:473–487`.
- `Domain/ValueObjects/VisibleClue.cs` — VO with private ctor + `Create(targetSnapshotId, clueText,
  targetName)` and `CreateForSubstage(clueText)`; operative clues use `CreateForSubstage` at
  `LiveSession.cs:1001` (drops the id).
- `Application/Dtos/Sessions/ParticipantTeamBoardDto.cs:38` — `VisibleClueDto(Guid? TargetSnapshotId,
  string ClueText, string? TargetName)`.
- `Application/Sessions/Common/ParticipantTeamBoardDtoFactory.cs:27` — maps `VisibleClue` → `VisibleClueDto`.

---

## Phase 0.1 — Push the assigned team's board on `OperativeClueAddedEvent`

**Scope:** add a third notification handler that re-projects + pushes **only the assigned team's** board
(the event is team-scoped — unlike the two existing events which change every board). Reuse the existing
project+broadcast path; no new group, broadcaster, or mobile invoke.

Refactor the per-team project+broadcast into a shared helper, keep `BroadcastAllTeamBoardsAsync`
behaviour-identical, and add a single-team path for the new event:

```csharp
public sealed class BroadcastTeamBoardNotificationHandler
    : INotificationHandler<SessionStateChangedEvent>,
        INotificationHandler<SubstageAdvancedEvent>,
        INotificationHandler<OperativeClueAddedEvent>   // + added
{
    // ...existing ctor / fields...

    // HU-28: an operative clue is team-scoped, so re-project + push ONLY the assigned team's board
    // (not all teams like the events above, which change every board). Same read-only path.
    public Task Handle(OperativeClueAddedEvent notification, CancellationToken cancellationToken)
        => BroadcastSingleTeamBoardAsync(
            notification.LiveSessionId, notification.TeamId, cancellationToken);

    private async Task BroadcastAllTeamBoardsAsync(Guid liveSessionId, CancellationToken cancellationToken)
    {
        var liveSession = await LoadAsync(liveSessionId, cancellationToken);
        var now = _timeProvider.GetUtcNow();
        foreach (var team in liveSession.Teams)
            await ProjectAndBroadcastAsync(liveSession, team.TeamId, now, cancellationToken);
    }

    private async Task BroadcastSingleTeamBoardAsync(
        Guid liveSessionId, Guid teamId, CancellationToken cancellationToken)
    {
        var liveSession = await LoadAsync(liveSessionId, cancellationToken);
        await ProjectAndBroadcastAsync(
            liveSession, teamId, _timeProvider.GetUtcNow(), cancellationToken);
    }

    private async Task<LiveSession> LoadAsync(Guid liveSessionId, CancellationToken cancellationToken)
        => await _liveSessionRepository.GetByIdAsync(liveSessionId, cancellationToken)
            ?? throw new NotFoundException(nameof(LiveSession), liveSessionId);

    private async Task ProjectAndBroadcastAsync(
        LiveSession liveSession, Guid teamId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var snapshot = liveSession.ProjectParticipantTeamBoard(teamId, now);
        var timerDto = SessionTimerSnapshotDtoFactory.Create(liveSession, teamId, snapshot.TimerSnapshot);
        var board = ParticipantTeamBoardDtoFactory.Create(liveSession, snapshot, timerDto);
        await _teamBoardBroadcaster.BroadcastTeamBoardUpdatedAsync(board, cancellationToken);
    }
}
```

**Fan-out (deliberate).** The domain raises one event per team, so an "assign to all N teams" fires
N events → N single-team pushes (each team gets exactly **one** update — its own). This removes the
O(N²) all-teams re-broadcast that would result from calling `BroadcastAllTeamBoardsAsync` per event. It
is **not** collapsed to a single batched event — that reshapes the aggregate/event contract + tests; see
Out of Scope.

## Phase 0.2 — Project the operative-clue id onto the board

**Scope:** thread the existing `OperativeClueId` from the domain onto the wire so the client keys on a
real, stable id (removes the mobile `op:{index}:{clueText}` fallback). Four small edits:

- `Domain/ValueObjects/VisibleClue.cs` — add nullable `OperativeClueId`; add a `CreateForOperative(Guid
  operativeClueId, string clueText)` factory (existing `Create` / `CreateForSubstage` pass `null`); add
  `OperativeClueId` to `GetEqualityComponents`.
- `Domain/Entities/LiveSession.cs:1001` — swap
  `VisibleClue.CreateForSubstage(clue.ClueText)` → `VisibleClue.CreateForOperative(clue.OperativeClueId, clue.ClueText)`.
- `Application/Dtos/Sessions/ParticipantTeamBoardDto.cs:38` — `VisibleClueDto` gains `Guid? OperativeClueId`.
- `Application/Sessions/Common/ParticipantTeamBoardDtoFactory.cs:27` — map `clue.OperativeClueId`.

Wire JSON stays camelCase (ASP.NET default) → `operativeClueId`. Target clues carry `null`; exactly one
of `targetSnapshotId` / `operativeClueId` is non-null per clue.

---

## Tests

- **`BroadcastTeamBoardNotificationHandlerTests`** — new case: an `OperativeClueAddedEvent` for `teamId`
  broadcasts **one** `TeamBoardUpdated` for **that** team only (assert not called for other teams); the
  pushed board's `visibleClues` contains the clue with non-null `operativeClueId`, `targetSnapshotId:
  null`, `targetName: null`. Existing two-event cases unchanged (shared helper is behaviour-identical).
- **Factory / projection** — `ParticipantTeamBoardDtoFactory` maps `operativeClueId`; update the
  `VisibleClueDto` construction in existing test setup for the new field.
- **Integration** — extend `AddOperativeClue_ToOneTeam_ReturnsOkAndOnlyAssignedTeamCanSeeItWithoutAdvancing`
  to assert the assigned team's board clue carries a non-null, stable `operativeClueId`; `resolvedTargets`
  stays 0 (no progress side-effect, already asserted).

## Gate

- session-operations unit + integration suites green; coverage gate holds.
- Assigning an operative clue to a team pushes that team's `TeamBoardUpdated` **once**, carrying the
  clue with non-null `operativeClueId` / null target fields; assigning to N teams pushes N single-team
  updates and recomputes no cross-team board.
- `resolvedTargets` unchanged (guidance, not progress).

## Out of Scope

- **Any new endpoint, group, or mobile invoke** — reuses the existing broadcast path.
- **Collapsing `OperativeClueAddedEvent` into a single batched authoring event** — keeps per-team event
  + per-team push; a batched event is a larger aggregate/contract change, revisit only if authoring
  fan-out becomes a measured problem at large team counts.
- **Frontend / mobile consumption** — owned by `frontend/plans/hu-28-frontend-operative-clue-authoring.md`.

## Commit Sequence

```
feat(backend): phase 0.1 — push assigned team board on operative clue — HU-28
feat(backend): phase 0.2 — project operative-clue id onto participant board — HU-28
test(backend): phase 0 — operative-clue push + id projection coverage — HU-28

Ref: HU-28
```

> 0.1 and 0.2 are independent and may land as one commit if you prefer a tighter arc.

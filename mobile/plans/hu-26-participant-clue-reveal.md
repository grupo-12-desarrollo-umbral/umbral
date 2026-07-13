# Plan: HU-26 Mobile — Participant Clue Reveal

**Ref:** HU-26
**Date:** 2026-07-13
**Scope:** Participant-facing clue reveal on the treasure-hunt live board for the Expo mobile app.
**Builds on:** HU-07B participant reconnection (`team-space.tsx`, `useReconnect`), HU-22 authoritative
timer (`useSessionTimer`), HU-23 participant team board (`useTeamBoard`, `TreasureHuntBoard`,
`ParticipantTeamBoardDto`).

> This is the **participant** surface only. The operator release control that *triggers* a reveal is
> the web slice (prompt Step 9) and is **not** planned here. Participants never release clues; there is
> **no** release/mutation action, invoke, or new subscription on mobile. A clue appears here only
> because the operator released it on the web side and the backend re-projected and pushed the board.

---

## Context

When the operator releases a clue to a team, the backend re-projects `ParticipantTeamBoardDto` and
pushes it as `TeamBoardUpdated` over the existing `team:{teamId}` SignalR group. The newly-released
clue arrives as a new `VisibleClueDto` inside `board.visibleClues`.

The mobile participant is **already in the right room and already listening**. Every seam this reveal
needs is live in source today:

- **Hub** — `createSessionsHubConnection` exposes `onTeamBoardUpdated(cb)`, implemented as
  `connection.on('TeamBoardUpdated', cb)` returning a `connection.off` unsubscribe
  (`src/lib/realtime/sessions-hub.ts:106-108`). No new invoke or group join is needed;
  `ReconnectAsync` already joins the per-team group.
- **Hook** — `useTeamBoard` applies every push wholesale via `setBoard(pushed)`, filtered by
  `liveSessionId` only (`src/lib/realtime/use-team-board.ts:82-87`). The `teamId` prop is the
  identity-access *reference* id and is deliberately **not** used to filter pushes — the server-side
  `team:{perSessionId}` group already scopes the push to exactly this team, so another team never
  receives this team's released clue.
- **Component** — `TreasureHuntBoard`'s Clues tab maps `visibleClues.map(c => <ClueCard key={c.targetSnapshotId} clue={c} />)` and shows `No clues yet.` when empty
  (`src/components/treasure-hunt-board.tsx:219-228`). `ClueCard` renders `clue.targetName` (label) over
  `clue.clueText` (mono) (`treasure-hunt-board.tsx:104-113`).
- **Screen** — `LiveTeamSpace`'s `playMode === 'TreasureHunt' && board` branch renders
  `<TreasureHuntBoard … visibleClues={board.visibleClues} />` (`src/app/(app)/team-space.tsx:341-355`).

Because the push flows `TeamBoardUpdated → setBoard → visibleClues prop → ClueCard` with **no missing
wiring**, the primary deliverable is a **verification increment**: a test proving that a
`TeamBoardUpdated` carrying a new `VisibleClueDto` re-renders the Clues section. The reveal-highlight
affordance is a secondary, optional increment kept at plan altitude (see Open Questions).

---

## Verified Backend / Realtime Contract

### REST snapshot (seed on reconnect — unchanged, reused as-is)

```
GET /api/sessions/{liveSessionId}/participants/team-board?teamId={teamId}&token={token}
```

Returns `ParticipantTeamBoardDto`; `board.visibleClues` seeds the Clues tab on (re)connect. This slice
adds **no** new REST call — it reuses `getParticipantTeamBoard` via `useTeamBoard`.

### `VisibleClueDto` (verified `src/lib/realtime/team-board-types.ts:7-11`)

| Field              | Type     | Rendered as                          |
| ------------------ | -------- | ------------------------------------ |
| `targetSnapshotId` | `string` | React `key` on `ClueCard` (not text) |
| `targetName`       | `string` | Card label (`Text variant="label"`)  |
| `clueText`         | `string` | Card body (`Text variant="mono"`)    |

> Note: the DTO carries `targetSnapshotId` in addition to `targetName`/`clueText`. It is the stable
> list key — do not drop it.

### `TeamBoardUpdated` SignalR event

Method name `"TeamBoardUpdated"`, broadcast to group `team:{teamId}` (per-session team id). Payload is
the full `ParticipantTeamBoardDto` (re-projected on every board change, including a clue release).

| Contract point            | Verified behavior                                                                 |
| ------------------------- | --------------------------------------------------------------------------------- |
| Method name               | `TeamBoardUpdated` (`sessions-hub.ts:107`)                                         |
| Payload                   | full `ParticipantTeamBoardDto` — hook applies it wholesale via `setBoard`         |
| Clue delivery             | new clue appears as an added entry in `payload.visibleClues`                      |
| Scoping (cross-team)      | server-side `team:{perSessionId}` group; client filters by `liveSessionId` only   |
| No `teamId` client filter | intentional — DTO's `teamId` is the per-session id, never the reference-id prop    |

---

## Architecture Decisions

- **Read-only, zero new seams.** No invoke, no new group/subscription, no REST call, no mutation. The
  reveal is a pure consequence of the existing `TeamBoardUpdated → setBoard` push path.
- **Board state stays owned by `useTeamBoard`.** The component never computes, merges, or caches clue
  lists; it renders `board.visibleClues` as-is. Any reveal affordance must derive from prop changes
  only, never own or recompute board state.
- **Cross-team isolation is server-enforced.** A team must not see another team's released clue; this
  is already guaranteed by per-team group membership. The client adds no team filter (adding one would
  in fact drop every push — see `use-team-board.ts` doc comment).
- **The verification increment ships first and alone.** It requires **no production code** — only tests
  proving the existing path surfaces a new clue. This is the certain increment.
- **The reveal-highlight affordance is optional and deferred to plan altitude.** It introduces a
  "when does the highlight clear" question that is not trivially verifiable, so it is not written as
  code-complete detail (see Phase 2 and Open Questions).

---

## Environment

No new configuration. This slice reuses, unchanged:

- Existing hub connection + `team:{teamId}` group membership from `ReconnectAsync` (no new env).
- Existing API base URL / `getParticipantTeamBoard` fetch config (HU-23).
- `constants/theme.ts` color tokens for any Phase-2 highlight (`colors.emberAccent`,
  `colors.emberAccentSoft` — already imported in `treasure-hunt-board.tsx`). No new tokens introduced.

---

## testID Contract

The current `TreasureHuntBoard` uses **no** `testID`s; existing tests query by rendered text and by
`accessibilityRole="button"` heuristics (see `treasure-hunt-board.test.tsx`). The Phase-1 verification
increment **reuses that convention** — it asserts on clue text/label strings, adding no testIDs.

testIDs are introduced **only if** the Phase-2 affordance is built, and only for elements that carry no
stable text of their own:

| testID                          | Element                                    | Increment |
| ------------------------------- | ------------------------------------------ | --------- |
| `treasure-hunt-clue-indicator`  | new-clue highlight/badge (if built)        | Phase 2   |

No testID is added to `ClueCard` itself — clue cards remain queryable by `targetName` / `clueText`.

---

## Phases

### Phase 1 — Reveal verification increment (primary; no production code)

**Scope**

- **Hook test** (`src/__tests__/team-board-hook.test.ts`): add a case proving a `TeamBoardUpdated`
  push carrying a **new** `VisibleClueDto` lands in `board.visibleClues`. Reuse the existing
  `makeClient` / `fireBoard` harness and `BASE_BOARD` (which starts `visibleClues: []`). Fire a push
  with `visibleClues: [{ targetSnapshotId: 'clue-1', targetName: 'Brass Astrolabe', clueText: 'Follow the north colonnade.' }]`
  and assert `hook.get().board?.visibleClues` has length 1 with that `targetName`/`clueText`. Add a
  second assertion that a push whose `liveSessionId` differs does **not** add the clue (cross-session
  isolation; mirrors the existing "ignores a push for a different liveSessionId" test).
- **Component test** (`src/__tests__/treasure-hunt-board.test.tsx`): `TreasureHuntBoard` is prop-driven,
  so the render half of the path is proven by re-rendering with a grown `visibleClues`. Add a case that
  renders with `visibleClues: []`, switches to the CLUES tab (existing `switchTab` helper), asserts
  `No clues yet.`, then re-renders (`create`/`renderBoard` with the grown array) and asserts the new
  clue's `targetName` and `clueText` now appear on the Clues tab.

Together these two tests prove the full chain — push → `setBoard` → `visibleClues` prop → `ClueCard`
text — with no production change. This is the acceptance-defining increment.

**Gate**

- New hook test: a `TeamBoardUpdated` push carrying a new `VisibleClueDto` grows `board.visibleClues`
  and exposes the clue's `targetName`/`clueText`; a different-`liveSessionId` push does not.
- New component test: a grown `visibleClues` prop replaces `No clues yet.` with the new clue's text on
  the CLUES tab.
- Full mobile test suite stays green; no source file under `src/` (non-test) is modified in this phase.

---

### Phase 2 — New-clue reveal affordance (optional; plan altitude)

> Build **only** if the clear/dismiss behavior below is resolved into a trivially verifiable rule.
> Until then this stays a contract + gate note, not code. It must not own or recompute board state.

**Intent** — a lightweight indicator that `visibleClues` grew since the participant last looked at the
Clues tab (e.g. a small badge/dot on the CLUES segment, or a brief highlight on the newest `ClueCard`),
derived purely from prop changes (a `useRef` of previously-seen `targetSnapshotId`s inside
`TreasureHuntBoard`). It computes nothing about the board — only "which ids are new to this render."

**Gate (if built)**

- With the CLUES tab unviewed, a re-render adding a clue shows `treasure-hunt-clue-indicator`; viewing
  the CLUES tab clears it.
- The indicator derives from prop diffs only — no board state ownership, no clue-list recomputation,
  no new network/subscription.
- A push adding no clues (e.g. a score-only re-projection) does not show the indicator.

**Blocked on** the Open Questions below (clear trigger + placement). Do not write code for this
increment while either is open.

---

## Acceptance-criteria → test mapping

| Acceptance criterion                                                                       | Test (Phase)                                                                                     |
| ------------------------------------------------------------------------------------------ | ------------------------------------------------------------------------------------------------ |
| A clue the operator releases to this team appears in the Clues section with no manual reload | Hook test: `TeamBoardUpdated` push with a new `VisibleClueDto` grows `board.visibleClues` (P1) |
| The revealed clue shows its target name and clue text                                       | Component test: grown `visibleClues` renders `targetName` + `clueText` on CLUES tab (P1)          |
| Empty → first-clue transition replaces the empty state                                      | Component test: `No clues yet.` → clue text after re-render (P1)                                  |
| A team never sees another team's released clue                                              | Hook test: different-`liveSessionId` push does not add the clue (P1); server per-team group       |
| No release/mutation action exists on the participant surface                                | Enforced by scope — no invoke/mutation added; Out of Scope below                                  |
| (Optional) a subtle indicator marks a freshly-revealed clue                                 | Phase-2 gate, if built                                                                            |

---

## Open Questions / Dependencies

1. **Reveal-highlight clear trigger (blocks Phase 2).** When does the "new clue" indicator clear — on
   viewing the CLUES tab, on the next push, or after a timeout? Unresolved, so Phase 2 stays at
   altitude.
2. **Indicator placement (blocks Phase 2).** Badge on the CLUES segment vs. a transient highlight on
   the newest `ClueCard`. Needs a `mobile/DESIGN.md` check before any code.
3. **Dependency: operator release web slice (Step 9).** The reveal is only observable end-to-end once
   the operator can release a clue on the web side. Phase 1 verifies the mobile half against the
   contract independently (mocked push), so it is not blocked on Step 9.
4. **Backend group id shape.** The push targets `team:{teamId}` where `teamId` is the per-session team
   id; the client relies on server-side scoping and filters only by `liveSessionId`. This matches
   `use-team-board.ts` and its tests — no client change needed.

---

## Out of Scope

- **Any release/mutation action on mobile** — participants never release clues; no invoke, button, or
  hub method for release is added.
- **Clues as progress.** `visibleClues` is guidance only, not target progress; target progress stays
  `resolvedTargets / totalActiveTargets` (unchanged).
- **The operator release control** (web slice, Step 9).
- **New subscriptions / groups / REST calls** — the reveal reuses the existing `TeamBoardUpdated` push
  path exclusively.
- **Map, coordinates, standings/ranking, scoring** — untouched HU-23 placeholders.
- **Client-side clue merging, caching, or de-duplication** — the board is applied wholesale by
  `useTeamBoard`.

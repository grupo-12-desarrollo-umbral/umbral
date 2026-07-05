# Plan: HU-22 Frontend — Authoritative Session Timer (active-substage realign)

**Ref:** HU-22 / DES-77 / DES-70
**Branch:** feature/hu-22-authoritative-timer-substage-realign (frontend slice on the same branch, or a follow-up `-frontend` branch off it)
**Date:** 2026-07-05
**Builds on:** HU-16 trivia-substage runtime snapshot, HU-19 operator assignment, HU-21A lifecycle
state machine + live `SessionStateChanged` broadcast, and the **HU-22 backend realignment** (this
branch) which changes the timer contract.
**Supersedes:** `plans/hu-22-frontend-operator-live-session-timer.md` (cycle-1 / DES-30, **Canceled**).
That plan built the operator timer against a **whole-session `MaximumTime` countdown**. This plan
re-points the already-shipped operator timer at the **active-substage (active trivia question)**
window and strips the whole-session copy/semantics.

---

## Altitude choice — Frontend plan concreteness rule (embedded verbatim)

> 1. **Proportion concreteness to certainty.** Write code-complete detail — exact DTO/request types,
>    real component skeletons, exact client-fn + server-action bodies, a `data-testid` contract —
>    only for the **fully-knowable near-term increments** (typically the foundation + first authoring
>    increment). Keep later, large, or blocked increments at **contract + gate altitude**: a contract
>    table, scope, and gate, with no invented bodies. Never write code for an increment blocked on an
>    open question.
> 2. **Verify every code anchor against the real source before writing it.** Open the files the plan
>    names — exported vs. private helpers, exact signatures, the const/env it reads, the line a
>    refactor targets — and write only what the source actually supports. A confident-but-wrong anchor
>    (e.g. "reuse `getIdentityHeaders`" when it is not exported) is worse than an altitude note. If a
>    detail is not verifiable, state the assumption under Open Questions rather than inventing it.
> 3. **Required sections** (both exemplars carry these; a plan missing one is a defect): Context ·
>    Verified Backend Contract (endpoint/shape table) · Architecture Decisions · **Environment** ·
>    **data-testid contract** · phased Scope + Gate per increment · **Acceptance-criteria → test
>    mapping** · Open Questions / Dependencies · Out of Scope.
> 4. **Final forms only, sequential by default.** Write only the final version of each anchor — no
>    "wrong → revised" trails — and keep increments sequential unless the slice genuinely parallelizes.

**Applied here:** the operator timer surface is a **hu-03-shaped, ~2-endpoint slice that already
exists**. Phases 1–2 (type/copy re-point + panel no-countdown state) are fully knowable against the
current source — **code-complete**. Phase 3 (DashboardClient wiring re-point) sits at **contract +
gate altitude** because the exact retained backend DTO fields and whether `SessionTimerUpdated`
still carries a pre-game tick depend on the landed backend DTO — see Open Questions. Phase 4 realigns
the existing tests.

---

## Context

The operator live-session timer is **already wired end-to-end** (verified against source):

- `app/lib/definitions.ts` — `SessionTimerSnapshotDto`, `SessionTimerUpdatedNotificationDto`,
  `SessionTimerStatus`, `ActiveQuestionSnapshotDto`.
- `app/lib/sessions.ts` — `getOperatorSessionTimerSnapshot(liveSessionId)` → `GET /api/sessions/{id}/timer`.
- `app/actions/sessions.ts` — `getSessionTimerSnapshotAction` (`'use server'`, gates `role === 'Operator'`).
- `app/lib/realtime/session-state-client.ts` — SignalR client (`@microsoft/signalr` `^10`), subscribes
  `SessionTimerUpdated` (+ `SessionStateChanged`, `QuestionActivated`, `QuestionClosed`), joins via
  `JoinLiveSessionAsOperatorAsync`, reconnect via `withAutomaticReconnect`.
- `app/dashboard/OperatorSessionTimerPanel.tsx` — pure render of the snapshot (no client clock).
- `app/dashboard/DashboardClient.tsx` — `timerReducer` state, `loadTimerSnapshot`, the realtime effect
  that patches `SessionTimerUpdated` into the snapshot, reconnect reload, and the panel render site
  (~L813).

**Backend authority is already respected** — the panel is a pure render; the only client `setInterval`
in the tree is the trivia-round *per-question approximation* in `use-trivia-round-state.ts`, explicitly
documented as a display approximation and owned by the trivia-round machine (DES-78 / HU-33A — **out of
scope**, do not touch).

**What the contract change breaks (the realignment):**

1. `SessionTimerSnapshotDto.remainingSeconds/totalSeconds` and `SessionTimerUpdated`'s
   `remaining/totalMilliseconds` **no longer describe a whole-session countdown** — they now track the
   **active substage's timer**: the active trivia question window, or **0/absent when there is no
   active question** (treasure-hunt substage or between questions). The nested `activeQuestion` carries
   the question window. (OD-1: treasure-hunt exposes **no** countdown; OD-3: fields redefined in place.)
2. The panel/types carry **whole-session copy**: the `SessionTimerStatus` doc comment
   ("timer counting down (session Active and not expired)"), the `"Session timer"` label, and
   `deriveChipLabel` reading `sessionState` as if the whole session is the clock. These must be
   re-pointed to the active-question meaning.
3. The panel has **no "no active question → no countdown"** state — an Active session with no active
   trivia question would now render `00:00 / Running`, which is wrong under OD-1.
4. `DashboardClient.onTimerUpdated` carries an `isPregame` heuristic
   (`totalMilliseconds <= 10_000 && sessionState === 'Active'`) that disambiguated a short-window
   broadcast from the whole-session countdown. With the whole-session countdown gone, that
   disambiguation must be reconciled (see Open Questions).

**Already satisfied:** there is **no `SessionMode`** anywhere in `app/` (retired in HU-17) — AC #2's
"no session-level `SessionMode`" is an emergent property; Phase 4 asserts its absence.
`maximumTimeMinutes` is **mission/session authoring input** (`CreateSessionRequest`, mission forms),
not a runtime countdown — it stays (the backend keeps `MaximumTime` as authoring metadata).

**No participant surface exists** — `getOperatorSessionTimerSnapshot` and the action are operator-only;
there is no participant live-session route and the SignalR client exposes only an operator join. The
`.../participants/timer` endpoint has **no host UI** in the frontend today. Building one is **out of
scope** for this realignment (see Open Questions / Out of Scope).

---

## Verified Backend Contract (changed this slice)

Anchored to the current frontend types + the HU-22 backend brief (`backend/docs/hu22-brief.md`,
`hu22-context.md`). Field-level retention flagged in Open Questions until the backend DTO lands.

| Endpoint / event | Shape | Change |
|---|---|---|
| `GET /api/sessions/{liveSessionId}/timer` (Operator) | `SessionTimerSnapshotDto` | `remainingSeconds`/`totalSeconds` now = **active trivia-question window** (0/absent when no active question). `activeQuestion` kept. Auth unchanged (assigned Operator). |
| `GET /api/sessions/{liveSessionId}/participants/timer` (Participant) | same `SessionTimerSnapshotDto` | same semantics; **no frontend consumer today** (Out of Scope). |
| SignalR `SessionTimerUpdated` on `SessionsHub` group `live-session:{id}` | `SessionTimerUpdatedNotificationDto` (ms) | `remaining/totalMilliseconds` now = active-question window; freezes on pause, recovers on resume/reconnect. |
| `PATCH /api/sessions/{id}/state` result `.timer` | `SessionTimerSnapshotDto?` | same re-pointed snapshot. |

**Current `SessionTimerSnapshotDto` (definitions.ts:342-355) — shape preserved, meaning re-pointed:**
```ts
export type SessionTimerSnapshotDto = {
  liveSessionId: string
  teamId: string | null
  sessionState: SessionLifecycleState | string
  totalSeconds: number          // now: active-question window total (0 when no active question)
  remainingSeconds: number      // now: active-question remaining (0 when no active question)
  timerStatus: SessionTimerStatus
  isAdvancing: boolean
  isExpired: boolean
  observedAt: string
  advancingSince: string | null
  expiredAt: string | null
  activeQuestion: ActiveQuestionSnapshotDto | null   // present ⇔ a trivia question is active
}
```
`ActiveQuestionSnapshotDto = QuestionActivatedNotificationDto & { remainingSeconds: number }`.

> **OD-3 note:** the backend redefines `Remaining/TotalSeconds` *in place* and keeps the
> `AuthoritativeSessionTimerSnapshot` VO (reused for the question window), so the DTO **shape is
> expected to survive**; only the *meaning* changes. The scalar fields `timerStatus` / `isAdvancing` /
> `advancingSince` / `expiredAt` are **assumed retained, re-pointed to the question window** — confirm
> against the landed DTO (Open Question A) before the Phase 3 field-level edits.

---

## Architecture Decisions

- **Realign, don't rebuild.** The snapshot-first + SignalR-patch + reconnect-reload path is correct and
  backend-authoritative already. This slice re-points *meaning* and adds the *no-active-question* state
  — the smallest diff that satisfies the contract change. No new fetchers, no new components, no
  client-owned clock.
- **`activeQuestion === null` is the "no countdown" signal.** A trivia question is active ⇔
  `snapshot.activeQuestion !== null`. When it is `null` (treasure-hunt substage, between questions, or
  pre-start), the panel shows **no countdown** — a neutral "No active question" state — instead of a
  `00:00` running clock (OD-1). `remainingSeconds === 0 && totalSeconds === 0` is the corroborating
  numeric signal.
- **Remaining time reads from the top-level snapshot, kept equal to the active-question window.** Per
  the contract, top-level `remainingSeconds`/`totalSeconds` already equal the active-question window,
  so the existing `formatRemaining(timer.remainingSeconds)` / `progressPercent` are correct **once an
  active question exists**. No need to re-plumb through `activeQuestion.remainingSeconds` — they
  converge. (If they diverge in the landed backend, prefer `activeQuestion.remainingSeconds` — Open
  Question A.)
- **Copy re-point over field rename.** Because the backend keeps the DTO shape (OD-3), keep field names
  and the `SessionTimerStatus` union; rewrite the *doc comments* and *visible labels* to the
  active-question meaning. Renaming fields would churn the SignalR normalizer, reducer, and tests for
  no contract reason.
- **Keep the trivia-round machine untouched.** `use-trivia-round-state.ts` / `TriviaRoundPanel` /
  `handleQuestionActivated` / `handlePregameTimerTick` are DES-78 / HU-33A. This slice does not
  rewrite them. The `isPregame` routing in `onTimerUpdated` is reconciled minimally (Phase 3) but the
  round machine's ownership of pre-game/question phases is preserved.
- **Pause freeze / resume-reconnect recovery already flow from the backend.** Pause → backend freezes →
  broadcast/snapshot carry the frozen remainder → panel renders it; resume/reconnect → `onReconnected`
  re-fetches the snapshot (`loadTimerSnapshot`) → same question continues from the frozen remainder.
  This slice verifies + locks this behaviour against the *re-pointed* payload; it adds no client freeze
  logic.

---

## Environment

No new environment variables. Reuses:
- `API_GATEWAY_URL` (server-side, `app/lib/sessions.ts:18`) for the timer REST read.
- `NEXT_PUBLIC_API_GATEWAY_URL` (client-side, `session-state-client.ts:37`) for the SignalR hub URL
  (`/hubs/sessions`) + `/api/realtime/hub-token` for the access token.

---

## data-testid contract

The `OperatorSessionTimerPanel` currently exposes **no** testids (e2e asserts the hero via
`data-testid="operator-panel"`). Add these so the realigned states are assertable:

| testid | Element | Purpose |
|---|---|---|
| `session-timer-panel` | panel root `<div>` | panel is mounted |
| `timer-remaining` | remaining-time value span | assert active-question remaining (freeze/recover) |
| `timer-chip` | state chip span | assert Running / Paused / Expired / No question |
| `timer-no-countdown` | the no-active-question block | assert OD-1 "no countdown" state is shown |

(Keep the existing `operator-panel` hero testid; the timer panel nests inside it.)

---

## Phases

### Phase 1 — Type & copy re-point (definitions.ts) — *code-complete*

**Scope**
- Rewrite the whole-session doc comments in `app/lib/definitions.ts` to the active-substage meaning.
  Shape unchanged (OD-3). Touch only comments + no code semantics.

**Edits (`app/lib/definitions.ts`)**
```ts
// Active-question timer status (the authoritative clock is the active trivia question window).
// "Advancing" = the active question timer is counting down (session Active, question open).
// "Frozen"    = the active question timer is held (session Paused).
// "Expired"   = the active question window reached zero.
// There is NO whole-session/mission countdown — a substage with no active question has no countdown.
export type SessionTimerStatus = 'Advancing' | 'Frozen' | 'Expired'

// Response of GET /api/sessions/{id}/timer (Operator) and
// GET /api/sessions/{id}/participants/timer (Participant).
// remainingSeconds/totalSeconds track the ACTIVE SUBSTAGE's timer — the active trivia question
// window — and are 0/absent when no question is active (treasure-hunt or between questions).
// activeQuestion is present ⇔ a trivia question is active.
export type SessionTimerSnapshotDto = { /* fields unchanged */ }

// SignalR "SessionTimerUpdated" — carries the active-substage (active question) remaining, in ms.
export type SessionTimerUpdatedNotificationDto = { /* fields unchanged */ }
```

**Gate**
- `pnpm tsc --noEmit` / `pnpm build` passes.
- No behavioural change. `grep -ri "session timer\|whole.session\|session.*countdown\|mission.*countdown"`
  over `app/lib/definitions.ts` returns only active-substage-scoped copy.

---

### Phase 2 — Panel: active-question meaning + no-countdown state — *code-complete*

**Scope (`app/dashboard/OperatorSessionTimerPanel.tsx` + `operatorSessionTimerPanel.module.css`)**
- Relabel the panel from `"Session timer"` → `"Question timer"` (all three occurrences: loading,
  error/unavailable, and the main render — L42/L57/L76).
- Add a **no-active-question** branch: when `timer.activeQuestion === null`, render the
  `timer-no-countdown` block (neutral copy, e.g. "No active question", no progress bar advancing,
  chip label "No question") instead of a `00:00` countdown.
- Re-point `deriveChipLabel` so the state reads as the *question* clock, not the session clock:
  `activeQuestion === null` → `"No question"`; else `isExpired || timerStatus === 'Expired'` →
  `"Expired"`; `timerStatus === 'Advancing'` → `"Running"`; else → `"Paused"`.
- Add the `data-testid`s from the contract above.
- Keep `formatRemaining` / `progressPercent` unchanged (they already read the re-pointed
  `remainingSeconds`/`totalSeconds`).

**Detection helper**
```ts
function hasActiveQuestion(timer: SessionTimerSnapshotDto): boolean {
  return timer.activeQuestion !== null
}
```

**CSS** — add a `data-tone="idle"` / a `.noCountdown` treatment mirroring the existing
`data-tone="unavailable"` styling (muted, no ember fill). No new palette.

**Gate**
- `pnpm build` passes.
- Active question present → `timer-remaining` shows `MM:SS` and the bar reflects
  `remainingSeconds/totalSeconds`.
- `activeQuestion === null` (Active session, no question) → `timer-no-countdown` visible, **no**
  advancing countdown, chip reads "No question".
- Loading / error / null-snapshot states unchanged (still `--:--` / Unavailable).

---

### Phase 3 — DashboardClient wiring re-point — *contract + gate altitude*

> Contract+gate (not code-complete) because the exact retained DTO fields and whether
> `SessionTimerUpdated` still carries a pre-game tick depend on the landed backend DTO (Open Questions
> A & B). Do **not** write final field-level bodies until those are confirmed against source.

**Scope (`app/dashboard/DashboardClient.tsx`)**
- **`onTimerUpdated` patch (L359-393):** the patched fields (`remainingSeconds`, `totalSeconds`,
  `timerStatus`, `isAdvancing`, `isExpired`, `sessionState`, `observedAt`) stay, now meaning the
  active-question window. Verify the ms→s mapping still holds against the landed broadcast.
- **`isPregame` heuristic (L362-368):** reconcile with the backend realignment. With the whole-session
  countdown gone, `SessionTimerUpdated` should carry only the active-substage window. Determine
  (Open Question B) whether the pre-game tick still rides `SessionTimerUpdated`:
  - If the backend **stops** reusing `SessionTimerUpdated` for pre-game → remove the `isPregame` branch
    and route all `SessionTimerUpdated` payloads to the timer patch (the round machine gets pre-game
    from its own signal). Keep `handlePregameTimerTick` wired only if the round machine still needs it.
  - If it **still** reuses it → leave the heuristic in place (it stays a trivia-round concern, DES-78).
  Either way, **do not rewrite** `use-trivia-round-state.ts`.
- **Snapshot-first + reconnect reload:** keep `loadTimerSnapshot` on session-select and `onReconnected`
  (already re-fetch the authoritative snapshot). Keep `applyTransitionResult` re-seeding the timer from
  `result.timer` after a transition. Verify these carry the re-pointed snapshot.
- **No client clock added.** The panel remains a pure render of the backend snapshot.

**Gate**
- `pnpm build` passes.
- With an active trivia question: `SessionTimerUpdated` ticks `timer-remaining` down; pause →
  displayed value **freezes** (chip "Paused"); resume/reconnect → `loadTimerSnapshot` restores the
  **same question**'s remainder and ticking continues.
- No active question: no advancing countdown patched into the panel.
- Trivia-round phase display (`TriviaRoundPanel`) is unregressed.

---

### Phase 4 — Tests, copy sweep & gate

**Scope**
- **Unit (`tests/unit/app/lib/sessions.test.ts` + a panel test):**
  - Timer read maps auth/404 errors (existing) — keep.
  - Add: snapshot with `activeQuestion` present → panel renders the question remaining; snapshot with
    `activeQuestion === null` → panel renders `timer-no-countdown`, no countdown.
  - SignalR `normalizeTimerNotification` still normalizes the re-pointed payload (units unchanged).
- **E2E (`tests/e2e/sessions.spec.ts` / `session-operator.spec.ts`):**
  - Operator opens an assigned session with an active question → `timer-remaining` visible, decreasing
    on a mocked `SessionTimerUpdated`.
  - Mocked pause payload (frozen) → `timer-remaining` stops, chip "Paused".
  - Mocked resume/reconnect → snapshot re-fetched, same-question remainder shown.
  - Active session, no active question → `timer-no-countdown` shown, no `00:00` running clock.
- **Copy/type sweep gate (AC #2):**
  `grep -rniE "session mode|sessionmode|whole.session|mission.*(timer|countdown)|session.*countdown" app`
  returns **zero** hits that assert a whole-session/mission countdown or a session-level `SessionMode`
  (mission `maximumTimeMinutes` authoring hits are expected and allowed).

**Gate**
- `pnpm test` (vitest) + `pnpm exec playwright test` pass.
- `pnpm build` / typecheck passes.
- The copy/type sweep is clean.

---

## Acceptance-criteria → test mapping

| Acceptance criterion | Verified by |
|---|---|
| Displayed remaining time derived from the active substage (active trivia question window), from the timer read + `SessionTimerUpdated` | Phase 2 panel render; Phase 4 unit (active-question snapshot) + e2e (decreasing on mocked broadcast) |
| Treasure-hunt / no active question → **no countdown** (OD-1) | Phase 2 `timer-no-countdown` branch; Phase 4 unit + e2e (`activeQuestion === null`) |
| Pause **freezes** the displayed timer; resume/reconnect recovers the correct remainder (no client-owned source clock) | Phase 3 wiring (snapshot-first + reconnect reload); Phase 4 e2e (freeze + resume/reconnect) |
| No UI type/copy retains a whole-session/mission countdown or a session-level `SessionMode` (AC #2) | Phase 1 + 2 copy re-point; Phase 4 copy/type sweep gate |
| frontend typecheck/build passes | Every phase gate (`pnpm build`) |

---

## Open Questions / Dependencies

- **A. Retained DTO field set + source of truth for remaining.** OD-3 keeps the
  `AuthoritativeSessionTimerSnapshot` VO and redefines `Remaining/TotalSeconds` in place, so the DTO
  shape is *expected* to survive with `timerStatus`/`isAdvancing`/`advancingSince`/`expiredAt`
  re-pointed to the question window. **Confirm against the landed backend `SessionTimerSnapshotDto`
  before Phase 3 field edits.** If any scalar is dropped/renamed, adjust `definitions.ts` +
  `normalizeTimerNotification` in Phase 1/3. If top-level `remainingSeconds` diverges from
  `activeQuestion.remainingSeconds`, prefer `activeQuestion.remainingSeconds` in the panel.
- **B. Does `SessionTimerUpdated` still carry a pre-game tick?** The `isPregame` heuristic
  (`totalMilliseconds <= 10_000 && Active`) predates the realignment. Determine from the landed backend
  broadcast whether pre-game still rides `SessionTimerUpdated`; drive the Phase 3 keep-or-remove
  decision from that. Do not touch the trivia-round machine either way.
- **C. Participant timer surface.** `GET /api/sessions/{id}/participants/timer` has **no host UI**
  (no participant live-session route; SignalR client is operator-join only). Building a participant
  timer is **out of scope** for this realignment — flag if the product wants it as a follow-up
  (would add `getParticipantSessionTimerSnapshot` + action + a participant join method + a route).
- **D. Elapsed `ResolutionTime` for treasure-hunt (OD-1 tail).** OD-1 allows surfacing *elapsed*
  `ResolutionTime` for treasure-hunt substages "only if the frontend needs a figure." Default here:
  **no figure** — `timer-no-countdown` shows a neutral state. Add elapsed only if product confirms it
  is wanted (would need the backend to expose it in the snapshot).
- **Dependency:** the backend HU-22 slice (X.1–X.4 on this branch) must land + rebuild through the
  gateway before Phase 3/4 can be verified against a live `SessionTimerUpdated`.

---

## Out of Scope

- Participant live-session timer UI / route / participant SignalR join (Open Question C).
- The trivia-round orchestration machine — `use-trivia-round-state.ts`, `TriviaRoundPanel`,
  question activation/advancement/close display (DES-78 / HU-33A).
- Any client-owned countdown as a source of truth (the panel stays a pure render of the backend
  snapshot; the trivia-round per-question approximation is not this slice's).
- Mission/session `maximumTimeMinutes` authoring inputs (they stay — `MaximumTime` survives as
  authoring metadata).
- A per-treasure-hunt-substage authored duration (needs a backend ADR + snapshot field — OD-1).
- Any backend changes.

---

## Commit Sequence

```
feat(frontend): phase 1 — re-point timer types/copy to active-substage — HU-22
feat(frontend): phase 2 — question-timer panel + no-active-question state — HU-22
feat(frontend): phase 3 — re-point dashboard timer wiring to active substage — HU-22
test(frontend): phase 4 — realign timer tests + whole-session copy sweep — HU-22

Ref: HU-22
Ref: DES-77
Ref: DES-70
```

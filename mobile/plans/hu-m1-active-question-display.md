# Plan: HU-M1 Mobile — Active Question Display + Countdown

**Ref:** HU-M1 (DES-82)
**Date:** 2026-07-11
**Scope:** Replace the `team-space.tsx` gameplay placeholder with the real participant question view:
active question prompt, non-interactive options, and an authoritative countdown. **Display only.**
**Builds on:** HU-07A/07B team lobby + reconnect (`team-space.tsx`, `useReconnect`), HU-22 authoritative
timer (`useSessionTimer`, `SessionTimerBar`), and the **frozen EN-M1 / DES-81 contract**
(`mobile/docs/en-m1-mobile-trivia-contract.md`) — the DTO types, hub handlers, and snapshot fetcher it
mandates are already committed.

> This is the **participant** display slice. Answer submission is **out of scope** and gated behind
> **HU-M2 / DES-84**. The closed-session surface is **HU-M3**; this plan renders its empty state but
> does not own its full flow. Mobile never computes remaining time from `Date.now()` as a source of
> truth — the countdown is the backend `SessionTimerUpdated` broadcast, reused verbatim from HU-22.

---

## Context

`LiveTeamSpace` in `src/app/(app)/team-space.tsx` renders only when `useReconnect` reports
`status === 'reconnected'`. Today it shows a join panel, the HU-22 `<SessionTimerBar />`, a
TEAM/PARTICIPANT card, and a Leave button. HU-M1 **replaces the standalone timer bar** with a full-bleed
**question stage** — its sticky header carries the `SessionState` badge + team score, and (while
`Active`) the chip-less countdown, over the prompt + options — with a persistent team footer below, so
the participant sees the question the operator activated. Connection status stays on the existing global
reconnecting banner (a separate axis, not part of the stage).

**The wiring plumbing already exists** — this is why HU-M1 is a display-assembly slice, not a
contract slice:

- **Types** (`src/lib/realtime/trivia-types.ts`, committed with DES-81): `QuestionActivatedNotificationDto`,
  `QuestionClosedNotificationDto`, `SubstageAdvancedNotificationDto`, and (in `timer-types.ts`)
  `SessionTimerSnapshotDto.activeQuestion?: ActiveQuestionSnapshotDto | null`. **All verified against
  backend source on 2026-07-10 — do not re-derive.**
- **Hub handlers** (`src/lib/realtime/sessions-hub.ts`): `onQuestionActivated`, `onQuestionClosed`,
  `onSubstageAdvanced`, `onStateChanged`, `onTimerUpdated` are all exposed on `SessionsHubClient`,
  each returning an unsubscribe. **No new hub method is needed** — the participant is already in the
  `live-session:{id}` group via `ReconnectAsync`.
- **REST snapshot** (`src/lib/api/sessions.ts`): `getParticipantTimerSnapshot` already returns the
  nested `activeQuestion` for the reconnect / late-join path.
- **Reconnect surface** (`src/lib/realtime/use-reconnect.ts`): already returns `{ client,
  reconnectNonce, isHubReconnecting }`, consumed today by `useSessionTimer`.

So HU-M1 adds: **(1)** a hook that folds these already-typed events + the snapshot into a single
active-question view model, **(2)** a non-interactive question **stage**, **(3)** the four empty states,
and **(4)** the wiring. No new types, no new hub method, no backend change.

---

## Contract used (from the frozen DES-81 doc — verified, not re-derived)

The full field-by-field source map is `mobile/docs/en-m1-mobile-trivia-contract.md`. HU-M1 consumes
only the **display** half; the submit half is HU-M2. Relevant shapes (all camelCase on the wire):

### Steady-state — SignalR pushes to `live-session:{liveSessionId}`

- `QuestionActivated` → `QuestionActivatedNotificationDto`: `liveSessionId`, `questionIndex`,
  `sequenceOrder`, `prompt`, `options: readonly string[]`, `timeLimitSeconds`, `activatedAt`.
  **Carries prompt + options**, so the question renders with no REST round-trip.
- `QuestionClosed` → `QuestionClosedNotificationDto`: `liveSessionId`, `questionIndex`, `closedAt`,
  `wasExpiredByTimer`.
- `SubstageAdvanced` → `SubstageAdvancedNotificationDto`: `liveSessionId`, `fromSubstageId`,
  `fromPlayMode`, `toSubstageId: string | null` (**null = final substage completed, session finished**),
  `advancedAt`.
- `SessionTimerUpdated` → the HU-22 timer broadcast; drives the countdown. Reused as-is.
- `SessionStateChanged` → carries `sessionState` (already typed in `sessions-hub-types.ts`).

### Reconnect / late-join — REST

`GET /api/sessions/{liveSessionId}/participants/timer?teamId=&token=` →
`SessionTimerSnapshotDto.activeQuestion` (`ActiveQuestionSnapshotDto | null`): `liveSessionId`,
`questionIndex`, `sequenceOrder`, `prompt`, `options`, `timeLimitSeconds`, **`remainingSeconds`**,
`activatedAt`. **`null` when no question is active.** This is the only path that carries the
active question on a fresh join / after a transport reconnect.

### Session-state vocabulary (backend `Domain/Enums/SessionState.cs`, serialized as string names)

`Scheduled`, `Preparing`, `Active`, `Paused`, `Finished`, `Cancelled`. **Terminal / no-longer-accepting
= `Finished` and `Cancelled`.** `Paused` still accepts participation (timer frozen, question retained);
`Active` is live; `Scheduled` / `Preparing` are pre-start. `sessionState` is a bare string on the wire —
existing code (`result.sessionState`, `useSessionTimer`) already treats it as such.

### Explicitly NOT consumed

- **No answer submission.** `submitTriviaAnswer` / `SubmitTriviaAnswerRequest` stay untouched (HU-M2).
  Options render **non-interactively** — no `Pressable`, no `onPress`, no selected state.
- **`TeamAnswered` is operator-only** — the participant client must not subscribe to it (per the
  contract doc); no handler exists for it and none is added.
- **`triviaSubstageSnapshotId` sourcing** — the open DES-81 decision — is a **submit** concern (HU-M2);
  irrelevant to display. HU-M1 reads none of it.

---

## Architecture decisions

- **Read-only, display-only.** The participant never mutates state and never submits. The question is
  rendered exactly as broadcast/snapshotted.
- **Snapshot first, SignalR second** (mirrors HU-22). On `reconnected`, seed the question from the REST
  snapshot's `activeQuestion`; thereafter apply pushed `QuestionActivated` / `QuestionClosed` /
  `SubstageAdvanced` / `SessionStateChanged`.
- **Reconnect re-seeds from REST.** Re-fetch the snapshot on `reconnectNonce` change (the existing
  `useReconnect` nonce), so a transport reconnect that missed a `QuestionActivated` recovers the
  current question from REST.
- **Countdown = the HU-22 authoritative timer, reused.** The active question shows the existing
  `<SessionTimerBar />` driven by `useSessionTimer` (`SessionTimerUpdated` broadcast). **No per-question
  device-local countdown.** The question DTO's `timeLimitSeconds` / snapshot `remainingSeconds` are
  display metadata only; the ticking value is the backend timer. This keeps a single source of truth
  and avoids two competing clocks.
- **Filter every event by `liveSessionId`** (the participant is in exactly one session group), matching
  `useSessionTimer`.
- **Two orthogonal status axes, not one.** SignalR `reconnecting` is a **client-side connection
  status** (`ReconnectStatus` in `use-reconnect.ts` — `idle | connecting | reconnecting | reconnected |
  denied | error`), **not** a `SessionState`. It is surfaced by the existing global connection banner
  (`isHubReconnecting`) that overlays any state — never as a question-region state and never as a value
  of the SessionState badge. The **question region** is keyed only off session state + question
  lifecycle.
- **One question region, four mutually-exclusive states.** The question area always renders exactly one
  of: `active`, plus the three empty states — `waiting`, `none`, `closed` (session no longer accepting
  participation). Derived in the hook, not scattered in the view. During a transient reconnect the region
  **retains** its last question; the global banner signals possible staleness and the region re-seeds
  from REST on recovery.
- **No new hub method, no backend change.** Everything rides existing group membership and typed handlers.

---

## UI decision — validated by prototype (2026-07-11)

The question layout was chosen with a throwaway UI prototype,
`src/app/(app)/active-question-prototype.tsx` (four variants on stub data; **delete once folded in**).
Winner: **"Full-bleed Stage"** — explicitly **not** a card:

- The question **owns the width** — a full-bleed, edge-to-edge region that escapes the `Screen`
  horizontal padding, rather than a bordered card sitting inside it.
- A **compact sticky header** carries, on its top row, the **current `SessionState` badge** (top-left:
  a small tone dot + label — one of `Scheduled · Preparing · Active · Paused · Finished · Cancelled`)
  and the team **`SCORE`** (top-right). When the state is `Active` a second row shows
  `QUESTION {sequenceOrder}` + the **countdown bar**. The badge + score are shown for **every** state;
  `QUESTION N` + countdown only while `Active`.
- **Countdown bar is chip-less on this surface.** The prototype drops the `<SessionTimerBar />`
  status chip (`Running` / `Paused` / …) because the SessionState badge already carries that status —
  showing both is redundant. The bar renders as the fill + mono `label` only. (Implementation: either
  pass a `showChip={false}` prop to `<SessionTimerBar />` — backward-compatible, default `true` so
  the treasure-hunt surface is unaffected — or render the fill + label inline. The prototype inlines it.)
- **Prompt** uses `typography.display` (30pt), left-aligned, in its own padded block.
- **Options** are **full-width inert tiles**: 1px `borderSoft` hairline separators, alternating
  `ivoryFog`/`warmMist` (zebra) backgrounds, each leading with a **circular outlined letter badge**
  (34px, `emberAccent` border) + option text at `headline` weight 400. No card wrapper, no `Pressable`.
- **Empty states** render as a single centered `Panel` beneath the same sticky header.
- **Persistent footer** (dark `charcoalRoom` strip, mirrors the treasure-hunt play surface): shows
  `YOUR TEAM · {name}` + roster on one line and an `ALL TEAMS ›` affordance. **Tapping it opens a
  full-screen teams sheet** listing your team (ember-bordered, highlighted) and every other team with
  its members, plus a `CLOSE` control. Team identity therefore survives regardless of the question state.
- **Connection status** (`reconnecting`, from `useReconnect`) is **not** part of this chrome — it is the
  existing global connection banner overlaid on top, orthogonal to the SessionState badge (see
  Architecture decisions).

This **supersedes** the "bordered rows inside a card" sketch in Phase 3 and the "fold the timer into the
card / drop it from empty states" option in Phase 5.

### Reconciliation with the prototype (2026-07-11)

The prototype keys its whole body off the raw `SessionState` (six branches: `Active` renders the
question, the other five each render a per-state empty panel). That was a throwaway convenience — the
prototype has no question-lifecycle axis. The real screen **keeps the prototype's visual design and copy
voice intact** but replaces this state model with the derived four-way region union. Only the two
behaviours below change when the prototype is folded in; layout, chrome, tones, and copy are preserved:

1. **`Paused` retains the question.** The prototype's `EMPTY_COPY.Paused` panel (which hides the question
   on pause) is **dropped.** Per the frozen contract `Paused` retains the question with the timer frozen,
   so the region stays `active`: the badge flips to `Paused`, the countdown freezes, and the prompt +
   options stay on screen. `Paused` is **never** an empty state.
2. **The five per-state empty panels collapse to the three derived region states.** The badge still
   shows the exact `SessionState` (tones from the prototype's `STATE_BADGE` map); the empty *body* is
   chosen by the region kind — refined by `sessionState` only where the wording genuinely differs
   (`closed` splits Finished vs Cancelled). Mapping:

   | Prototype branch (`SessionState`) | Plan region kind | Body copy |
   | --- | --- | --- |
   | `Active`, question in hand | `active` | the question stage |
   | `Active` / `Paused`, no question yet | `none` | "No active question yet…" |
   | `Scheduled` / `Preparing` | `none` | pre-start copy (badge already names which) |
   | *(no prototype equivalent)* between questions | `waiting` | "Waiting for the next question…" |
   | `Finished` | `closed` | "This session has ended. Thanks for playing." |
   | `Cancelled` | `closed` | "This session was cancelled by the host." |

**Countdown bar — keep the prototype's inline `CountdownBar`.** It already takes a `TimerDisplay` (fed by
`useSessionTimer`'s `display`, the authoritative HU-22 value), so it satisfies "the ticking value is the
backend timer" while rendering the chosen chip-less fill + mono label. **Do not** swap in
`<SessionTimerBar />` — that would shift the styling and pull in its status chip. This makes the Phase 3
`showChip?` prop **unnecessary**; `session-timer-bar.tsx` is left untouched.

**Scope note — score + teams roster are chrome, not HU-M1 data.** The frozen EN-M1 / DES-81 contract
covers only the question + timer. `SCORE` and the teams roster/sheet are **presentation placeholders**
here (stub data); their live data sourcing belongs to the scoring / roster slices, not HU-M1. HU-M1
owns the question stage + countdown **within** this chrome; it renders the badge/score/footer shells but
does not wire their data. See "Out of scope."

---

## The active-question view model

A discriminated union produced by the hook and consumed by the view. **`reconnecting` is not a member**
— connection status is the separate global banner (see Architecture decisions), orthogonal to this union:

```ts
type ActiveQuestion = {
  questionIndex: number;
  sequenceOrder: number;
  prompt: string;
  options: readonly string[];
  timeLimitSeconds: number;
};

type ActiveQuestionView =
  | { kind: 'active'; question: ActiveQuestion }
  | { kind: 'waiting' }  // a question closed / substage advanced; next one pending
  | { kind: 'none' }     // pre-question: Scheduled/Preparing, or live with no question yet (fresh join)
  | { kind: 'closed' };  // session Finished / Cancelled — no longer accepting participation
```

Separately, the header renders a **`SessionState` badge** straight off `sessionState` (the raw enum
string: `Scheduled | Preparing | Active | Paused | Finished | Cancelled`) — a small always-visible status
chip, distinct from this region union. The region union is derived; the badge is the raw state.

**Derivation / precedence** (highest wins):

1. `closed` — when `sessionState` is `Finished` or `Cancelled` (from snapshot, `SessionStateChanged`,
   or the timer event's `sessionState`), **or** a `SubstageAdvanced` arrives with `toSubstageId === null`.
2. `active` — a question is in hand (from snapshot `activeQuestion`, or the latest `QuestionActivated`
   not yet superseded by a close/advance). Retained across a transient reconnect (the global banner
   signals staleness); re-seeded from the REST snapshot on recovery.
3. `waiting` — a `QuestionClosed` or a substage-to-substage `SubstageAdvanced` (`toSubstageId != null`)
   cleared the question while the session is still live — i.e. we have seen at least one question.
4. `none` — the initial / pre-question state: `Scheduled`, `Preparing`, or `Active`/`Paused` with no
   question seen this session and none in the snapshot. (The prototype's separate `Scheduled` /
   `Preparing` panels collapse here; the badge still names the exact state.)

The `waiting` vs `none` split is tracked by a "have we ever held a question" flag: `none` before the
first question is ever seen; `waiting` after one has been cleared. Both render a distinct empty state
per the ACs. Connection drops do **not** enter this union — they raise the global banner instead.

---

## Phases

### Phase 1 — Active-question view model + mapper

**Scope**

- New `src/lib/realtime/active-question-types.ts`:
  - `ActiveQuestion` and the `ActiveQuestionView` union (above).
  - `toActiveQuestion(dto: QuestionActivatedNotificationDto | ActiveQuestionSnapshotDto): ActiveQuestion`
    — projects both the broadcast and the snapshot shape (they share every field HU-M1 needs) onto the
    narrow view type. No `remainingSeconds` retained (countdown is the timer's job).
  - A pure `isTerminalSessionState(state: string): boolean` → `state === 'Finished' || state === 'Cancelled'`.
- Reuse the committed DTO types from `trivia-types.ts` / `timer-types.ts` — **add no new DTO shapes.**

**Gate** — TypeScript accepts the module; `toActiveQuestion` and `isTerminalSessionState` unit-tested;
no behavior change.

---

### Phase 2 — Active-question hook

**Scope**

- New `src/lib/realtime/use-active-question.ts`:
  `useActiveQuestion({ client, liveSessionId, isReconnected, reconnectNonce, snapshotActiveQuestion, snapshotSessionState })`.
  - **Seeding:** derive the initial view from `snapshotActiveQuestion` + `snapshotSessionState` when
    `isReconnected` (and re-seed on `reconnectNonce` change) — `active` if a question is present,
    `closed` if terminal, else `none`.
  - **Subscriptions** (each filtered by `liveSessionId`, each unsubscribed on cleanup):
    - `onQuestionActivated` → `{ kind: 'active', question: toActiveQuestion(dto) }`; set the
      "have-seen-question" flag.
    - `onQuestionClosed` → `{ kind: 'waiting' }`.
    - `onSubstageAdvanced` → `toSubstageId === null` ? `{ kind: 'closed' }` : `{ kind: 'waiting' }`.
    - `onStateChanged` → terminal ⇒ `{ kind: 'closed' }`; non-terminal is ignored for the question
      region (the timer hook owns pause/resume display).
  - **No `reconnecting` handling here.** Connection status is not this hook's concern — the region
    retains its last view across a transient reconnect and re-seeds from the snapshot on `reconnectNonce`
    change; the global `isHubReconnecting` banner is what tells the user the transport dropped.
  - Also surface the raw `sessionState` (for the header badge) alongside `view`.
  - Apply the precedence rules above. Expose `{ view, sessionState }` (and internal state as needed for tests).

**Design note — where snapshot state comes from.** To avoid two independent REST fetches, this hook
should **not** re-fetch the snapshot itself. Options, in order of preference:
- **(chosen)** Have `useSessionTimer` also surface the snapshot's `activeQuestion` + `sessionState` it
  already fetched (it reads the full `SessionTimerSnapshotDto` today but keeps only timer fields).
  Extend its `TimerState`/result to carry `activeQuestion` + `sessionState`, and feed those into
  `useActiveQuestion` as `snapshotActiveQuestion` / `snapshotSessionState`. One fetch, two consumers.
- *(alt, rejected)* A second `getParticipantTimerSnapshot` call inside this hook — duplicate network,
  possible skew between the two snapshots. Avoid.

**Gate** — unit tests: snapshot-seed → active/none/closed; `QuestionActivated` → active;
`QuestionClosed` → waiting; `SubstageAdvanced(null)` → closed and `(id)` → waiting;
`SessionStateChanged(Finished/Cancelled)` → closed; a `reconnectNonce` change re-seeds the view from the
snapshot (retained question survives, no `reconnecting` region state); event with foreign
`liveSessionId` ignored; `none` before first question vs `waiting` after a close. (Mirror the fake-client
harness in `session-timer-hook.test.ts`.)

---

### Phase 3 — Non-interactive question stage component

**Scope**

- New `src/components/active-question-stage.tsx` (React Native, `mobile/DESIGN.md` idiom — warm surfaces,
  full borders, restrained ember, no nested cards), rendering the **Full-bleed Stage** chosen above:
  - **Sticky header** (full width, `panelSurface`, bottom `borderSoft`), rendered for the active + empty
    states, with two rows:
    - **Top row:** the **`SessionState` badge** on the left (tone dot + label, keyed off the raw
      `sessionState` prop) and the **`SCORE`** on the right (`SCORE` label over a tabular-nums value in
      `emberAccentStrong`). Score is a passed-in prop (stub/placeholder data in HU-M1 — see scope note).
    - **Second row, `Active` only:** muted `label` `QUESTION {sequenceOrder}` over the **chip-less**
      countdown — the prototype's inline `CountdownBar` (fill + mono `label`), fed the `TimerDisplay`
      via a `timerDisplay` prop (the stage does not own the timer). The status chip is suppressed because
      the badge carries session status. **Not** `<SessionTimerBar />` — see "Reconciliation with the
      prototype" for why the inline bar is chosen over reusing the shared component.
  - **Prompt**: `Text` at `typography.display`, selectable, wrapping, in its own padded block.
  - **Options**: map `question.options` to **full-width inert tiles** — a `View` per option with a 1px
    `borderSoft` bottom hairline, zebra `ivoryFog`/`warmMist` backgrounds, a circular outlined letter
    badge (`String.fromCharCode(65 + i)`, `emberAccent` border) + the option `Text`. **No `Pressable`,
    no `onPress`, no selected/disabled state** — display only.
  - Accessibility: prompt `accessibilityRole="header"`; options `accessibilityRole="text"`, **not**
    `button` — not actionable. An `accessibilityHint` may clarify "answering is not yet available".
  - Full-bleed: the options region escapes the host `Screen`'s horizontal padding and spans edge-to-edge;
    long prompts/options wrap without clipping on narrow phones.

**Gate** — component test: renders prompt + N option tiles with letter badges; no option is pressable
(no `onPress` prop / no `button` role); the header shows the SessionState badge + score, and — while
`Active` — the countdown bar with **no** status chip.

**`session-timer-bar.tsx` is left untouched.** Reconciliation chose the prototype's inline chip-less
`CountdownBar` over reusing `<SessionTimerBar />`, so the previously-considered `showChip?` prop is not
added — the treasure-hunt surface and `session-timer-bar.test.tsx` are unaffected by construction.

---

### Phase 4 — Empty-state components

**Scope**

- New `src/components/question-empty-state.tsx` (single mapped component — keeps copy in one place),
  rendered **beneath the stage's persistent header** so the badge/score stay visible and the region never
  collapses. One `Panel` per state, matching the existing `Panel`/`Text` idiom, the `deniedCopy` voice in
  `team-space.tsx`, and the prototype's `EMPTY_COPY` wording (reused, not re-authored). Props:
  `{ kind, sessionState }` — the body is keyed by `kind`, with `closed` refined by `sessionState`:
  - `waiting` — "Waiting for the next question…" (muted). The operator hasn't activated the next one.
    (No prototype equivalent — this is the new between-questions state.)
  - `none` — "No active question yet. Sit tight — the host will start the round." (muted). Covers the
    fresh live state and the pre-start `Scheduled` / `Preparing` states (the badge names which).
  - `closed` — terminal notice (`signalCritical`/muted), split by `sessionState`: `Finished` → "This
    session has ended. Thanks for playing." / `Cancelled` → "This session was cancelled by the host."
    This is the HU-M3 boundary; HU-M1 renders the terminal notice only, no further flow.
- **No `Paused` empty state.** Unlike the prototype, `Paused` retains the question (region stays
  `active`, timer frozen) — see "Reconciliation with the prototype." The empty-state component is never
  rendered for `Paused`.
- **No `reconnecting` empty state.** Connection status is the existing global `isHubReconnecting`
  banner (a spinner + "Reconnecting…" strip overlaying the whole surface), not a question-region state.
  During a reconnect the region keeps showing its last view; the banner signals the drop.

**Gate** — each state renders its distinct copy/tone; snapshot/component test asserts the three are
distinguishable.

---

### Phase 5 — Wire into `LiveTeamSpace`

**Scope**

- In `team-space.tsx` `LiveTeamSpace`:
  - Keep the existing `useSessionTimer(...)` call; consume its (Phase-2-extended) `activeQuestion` +
    `sessionState` snapshot fields and its `display`.
  - Add `useActiveQuestion({ client, liveSessionId, isReconnected: true, reconnectNonce,
    snapshotActiveQuestion, snapshotSessionState })`, consuming `{ view, sessionState }`.
  - Render the **full-bleed question stage** (Phase 3) as the dominant surface — it spans the screen
    width (escaping the `Screen` horizontal padding). Feed it `sessionState` (badge), the team `score`
    (placeholder prop — see scope note), and `timerDisplay={display}`.
    - `view.kind === 'active'` → `<ActiveQuestionStage question={view.question} sessionState={sessionState} score={score} timerDisplay={display} />`.
    - otherwise → the matching empty state from Phase 4, rendered beneath the same stage header (which
      still shows the badge + score).
  - **Connection banner (separate axis):** surface `isHubReconnecting` from `useReconnect` into
    `LiveTeamSpace` and render the existing global reconnecting banner over the surface — it is **not**
    a question-region state (see Architecture decisions). Keep the single global banner; do not add a
    per-region reconnecting placeholder.
  - **Persistent team footer:** render the dark `YOUR TEAM · {name}` + roster strip pinned to the
    bottom; tapping it opens the **teams sheet** (your team + other teams with members, `CLOSE` control),
    mirroring the prototype. Team roster is placeholder data in HU-M1 (see scope note).
  - **Countdown placement (resolved by the prototype):** the timer lives in the stage's **persistent
    header**, chip-less, shown while `Active`. **Remove** the previously-unconditional standalone
    `<SessionTimerBar />` from `LiveTeamSpace` — its single home is now that header.
  - Optional: once `reconnected`, collapse the redundant join panel so the stage stays dominant.
  - **Cleanup:** delete the throwaway `active-question-prototype.tsx` route and its DEV home button
    (in `index.tsx`) once the stage lands.

**Gate** (manual, via the `run` skill / dev client):

- Reconnecting into a session **with an active question** shows the prompt + options + countdown from
  the REST snapshot, no manual reload.
- A mocked/real `QuestionActivated` replaces the empty state with the question.
- `QuestionClosed` → `waiting`; a substage advance into another substage → `waiting`; final-substage
  advance (`toSubstageId: null`) or `Finished`/`Cancelled` → `closed`.
- The header badge tracks `sessionState` across transitions (`Active` → `Paused` → …); the countdown
  shows no status chip.
- Transport drop raises the **global connection banner** (not a question-region state); the region keeps
  its last view and recovery re-seeds from REST.
- Tapping the footer opens the teams sheet; `CLOSE` returns to the question.
- Options are visibly non-interactive (tapping does nothing).
- Layout holds on a narrow phone; long prompts/options wrap.

---

### Phase 6 — Tests

- **Unit** (`src/__tests__/active-question-hook.test.ts`): the Phase-2 gate matrix, using the fake hub
  client pattern from `session-timer-hook.test.ts` (a `Set` of handlers per event, `fireEvent`
  helpers). Cover precedence: `closed` > `active` > `waiting` > `none`, plus `reconnectNonce`-driven
  re-seed (no `reconnecting` region state).
- **Unit** (`active-question-types.test.ts`): `toActiveQuestion` from both DTO shapes;
  `isTerminalSessionState` for all six states.
- **Component** (`active-question-stage.test.tsx`): prompt + option tiles render; no option is pressable;
  header shows the SessionState badge + score and a chip-less countdown while `Active`.
- **Component** (`team-space` or `question-empty-state.test.tsx`): the three empty kinds render distinct
  copy; `closed` shows the Finished vs Cancelled variant per `sessionState`; wiring picks `active` vs
  each empty state from a fake view; `Paused` renders the retained question, **not** an empty state.
- Keep the existing `session-timer-hook` / `session-timer-bar` tests green after the Phase-2
  `useSessionTimer` extension (snapshot fields added, timer behavior unchanged).

---

## Acceptance criteria

- In the live team space, when a question is active the participant sees the **prompt** and its
  **options rendered non-interactively**, plus the authoritative countdown.
- The countdown is driven **only** by the `SessionTimerUpdated` broadcast (HU-22 timer) — never a
  device-local clock; pause freezes it, expiry shows `00:00`.
- On reconnect / late join, the active question is seeded from
  `GET /sessions/{liveSessionId}/participants/timer` (`activeQuestion`), with no manual reload.
- `QuestionActivated` swaps the view to the new question live; `QuestionClosed` / substage advance /
  terminal state transition to the correct empty state.
- The header shows the current **`SessionState` badge** (one of `Scheduled · Preparing · Active ·
  Paused · Finished · Cancelled`) top-left and the team **score** top-right; the countdown carries **no**
  status chip (the badge owns session status).
- **Three distinct empty states** render: `waiting for next question`, `no active question`, and
  `session no longer accepting participation`.
- **Connection status is a separate axis:** `reconnecting` surfaces as the global connection banner, not
  a question-region state and not a badge value; a transport drop does not blank or replace the question.
- The footer shows your team; **tapping it opens a teams sheet** listing all teams + members.
- **No answer submission** is present — options are display-only; no submit code path is touched.
- No new hub method; participant rides the existing `live-session:{id}` group. No backend change.
- Visuals follow `mobile/DESIGN.md` and the chosen **Full-bleed Stage** prototype: a full-width,
  edge-to-edge question region with a persistent header (SessionState badge + score + chip-less
  countdown), a `display`-size prompt, zebra option tiles with circular letter badges, and a persistent
  team footer — warm surfaces, restrained ember accent, no nested cards, tabular live data.

---

## Out of scope

- **Answer submission / option selection** (HU-M2 / DES-84) — including sourcing
  `triviaSubstageSnapshotId` (the open DES-81 decision).
- The full **closed-session** flow and post-close reveal (HU-M3, HU-35); HU-M1 renders only the
  terminal empty-state notice.
- Scoring, correctness, per-team answered indicators, `TeamAnswered` (operator-only).
- **Live data for the header score and the teams footer/sheet.** HU-M1 renders these shells with
  **placeholder** data; sourcing the real team score and full roster (and the standings sheet's data)
  belongs to the scoring / roster slices, not this display slice. The chrome is validated here; its
  wiring is not.
- Editing timer/question state from mobile; any lifecycle control.
- A per-question device-local countdown / interpolation (deferred; the HU-22 backend-tick timer is reused).

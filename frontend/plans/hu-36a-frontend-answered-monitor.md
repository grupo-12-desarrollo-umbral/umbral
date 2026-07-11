# Plan: HU-36A Frontend — Operator Answered / Not-Answered Trivia Monitor

**Ref:** HU-36A · DES-49 · DES-70
**Branch:** feature/hu-36a-frontend-answered-monitor
**Date:** 2026-07-10
**Builds on:** HU-33A/HU-34 operator live surface — `app/dashboard/DashboardClient.tsx` operator hero,
`app/lib/realtime/session-state-client.ts` (SignalR client already joining `live-session-operators:{id}`),
`app/lib/sessions.ts` (gateway data clients), `app/actions/sessions.ts` (operator server actions),
`OperatorSessionTimerPanel` / `TriviaRoundPanel` panel pattern.

---

## Method — frontend plan concreteness rule (governs this document)

> 1. Proportion concreteness to certainty. Write code-complete detail — exact DTO/request types, real component
>    skeletons, exact client-fn + server-action bodies, a data-testid contract — only for the fully-knowable
>    near-term increments (typically the foundation + first authoring increment). Keep later, large, or blocked
>    increments at contract + gate altitude: a contract table, scope, and gate, with no invented bodies. Never
>    write code for an increment blocked on an open question.
> 2. Verify every code anchor against the real source before writing it. Open the files the plan names — exported
>    vs. private helpers, exact signatures, the const/env it reads, the line a refactor targets — and write only
>    what the source actually supports. A confident-but-wrong anchor (e.g. "reuse getIdentityHeaders" when it is
>    not exported) is worse than an altitude note. If a detail is not verifiable, state the assumption under Open
>    Questions rather than inventing it.
> 3. Required sections (both exemplars carry these; a plan missing one is a defect): Context · Verified Backend
>    Contract (endpoint/shape table) · Architecture Decisions · Environment (env vars / config consts reused) ·
>    data-testid contract · phased Scope + Gate per increment · Acceptance-criteria → test mapping · Open
>    Questions / Dependencies · Out of Scope.
> 4. Final forms only, sequential by default. Write only the final version of each anchor — no "wrong → revised"
>    trails — and keep increments sequential unless the slice genuinely parallelizes.

**How that applies here (read before implementing):** Two source realities set the altitude of each phase.
- The **reused `TeamAnswered` SignalR event, the hub, the operator-join, and the 403 ProblemDetails shape are
  implemented and verified** in `session-operations-service`. The realtime increment (Phase 1) and the live-merge
  logic (Phase 3) are therefore **code-complete**.
- The **REST snapshot endpoint `GET .../answered-monitor` and its result DTO do NOT exist in backend source yet**
  — only in `backend/docs/hu36a-brief.md`. So the snapshot data client + response types (Phase 2) are held at
  **contract altitude**: a contract table plus a "mirror this verified sibling" description, with the route
  segment and DTO field shape recorded under **Open Questions**, not invented as final code.

---

## Context

HU-36A gives the **operator** a live board showing, for the currently-active trivia question, **which teams
have answered and which have not** — and nothing more. Before the question closes the operator must never see the
option a team chose, whether it was correct, or any points. Correctness reveal is HU-35; post-close full review is
HU-36B. This slice is the web surface over HU-36A's backend read.

The backend for HU-36A is **mid-flight**. What already exists and is verified in
`backend/services/session-operations-service`:

- The **operator-only `TeamAnswered` SignalR event** (from HU-34) — reused as-is, no new broadcaster.
- The **hub** `/hubs/sessions` with operator-join `JoinLiveSessionAsOperatorAsync(Guid)`, which already places the
  operator connection in the `live-session-operators:{id}` group. Participant connections are never added to that
  group, so `TeamAnswered` is structurally operator-only.
- The **403 RFC 7807** ProblemDetails shape for non-owning operators.

What does **not** yet exist in backend source (spec-only in `hu36a-brief.md`):

- The **`GET /api/sessions/{liveSessionId}/…/answered-monitor`** endpoint.
- Its result DTO (`TriviaAnsweredMonitorDto` / per-team item) — the JSON shape the board's initial snapshot fetch
  will consume is **unverified**.

Frontend realities that shape the design (all verified against source):

1. **SignalR already exists on the frontend.** `@microsoft/signalr@^10` is a production dependency and
   `app/lib/realtime/session-state-client.ts` is a complete operator client. It does **not** currently subscribe to
   `TeamAnswered`; it must be **extended**, not rebuilt.
2. **Session reads go through the API gateway**, not the identity service. The board's data client mirrors
   `getOperatorSessionTimerSnapshot` in `app/lib/sessions.ts` (`API_GATEWAY_URL` + `getGatewayHeaders`), **not**
   the `users.ts` / `IDENTITY_SERVICE_URL` / `getIdentityHeaders` path (that helper is private and unrelated here).
3. **The operator view is one client-rendered hero** in `DashboardClient.tsx` (`data-testid="operator-panel"`),
   not a per-session route. The board mounts as a new panel component beside `OperatorSessionTimerPanel` /
   `TriviaRoundPanel`.

---

## Verified Backend Contract

### Reused SignalR event — `TeamAnswered` (IMPLEMENTED, verified)

Source: `src/Application/Sessions/Common/Notifications/TeamAnsweredNotificationDto.cs:8-13`,
`src/Api/Hubs/SignalRTeamAnsweredBroadcaster.cs:18-36`, `src/Api/Hubs/SessionsHub.cs:78-115`,
`src/Api/Program.cs:31-32`.

| Aspect | Verified value |
|---|---|
| Hub path | `/hubs/sessions` (client already targets this via `NEXT_PUBLIC_API_GATEWAY_URL`) |
| Operator join method | `JoinLiveSessionAsOperatorAsync(Guid liveSessionId)` (client already invokes on start + reconnect) |
| Operator group | `live-session-operators:{liveSessionId:D}` (operator connections only; participants never joined) |
| Client event name | `"TeamAnswered"` (`TeamAnsweredMethod` const) |
| Payload field | Type · notes |
| `LiveSessionId` | `Guid` |
| `TeamId` | `Guid` — the LiveSession runtime team id |
| `TriviaSubstageSnapshotId` | `Guid` — active substage snapshot id (question identity, part 1) |
| `QuestionSequenceOrder` | `int` — one-based active question order (question identity, part 2) |
| `AnsweredAt` | `DateTimeOffset` |
| **Absent by design** | **no `SelectedOptionSequenceOrder`, no `IsCorrect`, no `ScoreValue`** — option-free at the wire |

C# payloads on this hub arrive PascalCase; the existing client normalizes camelCase **or** PascalCase keys
(see every `normalize*` in `session-state-client.ts`). The new normalizer follows that convention.

### 403 non-owner shape (IMPLEMENTED, RFC 7807)

Source: `src/Application/Common/Exceptions/ForbiddenAccessException.cs:5-10`,
`src/Api/Services/ProblemDetailsExceptionHandler.cs:118-142`. Body:

```json
{ "type": "forbidden-access", "title": "Forbidden.", "detail": "You do not have permission to perform this action.", "status": 403 }
```

The board surfaces the **status code** (403) as a not-authorized state; it does not parse `type`.

### Snapshot endpoint — `GET .../answered-monitor` (NOT IMPLEMENTED — contract from design docs only)

**⚠ Unverified against code.** No controller action, query, or result DTO exists in source. The shape below is
the *expected* contract distilled from `backend/docs/hu36a-brief.md:48` and `hu36a-context.md:107-113,171-174`.
Treat every field name/casing and the route segment as **pending backend Phase X.4** — see Open Questions Q1/Q2.

| Aspect | Expected (design-doc) value — CONFIRM at implementation |
|---|---|
| Method / route | `GET /api/sessions/{liveSessionId}/…/answered-monitor` — the `…` segment is unresolved (Q1) |
| Auth | `[Authorize(Policy = Operator)]` + resource-ownership Proxy → **200** assigned operator, **403** non-owner |
| Response — top level | active-question identity `(SubstageSnapshotId, QuestionSequenceOrder)` + ordered per-team list |
| Response — per team | `(TeamId, TeamCode`/`DisplayName, Answered: bool, AnsweredAt: nullable)` |
| **Never present** | chosen option, correctness, points (enforced by backend VO shape, no-leak invariant) |

Verified roster fallback (if the board needs the roster before the monitor endpoint lands): the **already-shipped**
`GET /api/sessions/{liveSessionId}/teams` → `SessionAssociatedTeamsDto { liveSessionId, teams: AssociatedSessionTeamDto[] }`,
where `AssociatedSessionTeamDto = { runtimeTeamId, referenceTeamId, displayName, teamCode, joinStatus }`
(`app/lib/definitions.ts:298-309`; client `getSessionAssociatedTeams` at `app/lib/sessions.ts:180-197`). See Q3.

---

## Architecture Decisions

- **Extend the existing realtime client; do not add a connection.** The operator connection built by
  `createSessionStateRealtimeClient` (`session-state-client.ts:170-247`) already joins
  `live-session-operators:{id}`, so `TeamAnswered` arrives on it. We add one optional `onTeamAnswered` callback +
  one `connection.on('TeamAnswered', …)` subscription + one `normalizeTeamAnswered`, mirroring the five existing
  event subscriptions exactly. A second connection or a raw `WebSocket` would duplicate auth/reconnect and risk a
  participant-reachable path — rejected.
- **Board state is a `Set` of answered runtime-team ids, scoped to the active question identity.** Answered =
  "an accepted answer exists for this team on the active `(TriviaSubstageSnapshotId, QuestionSequenceOrder)`."
  Not-answered = every roster team **absent** from that set — derived by enumerating the roster, never by absence
  in a broadcast list (mirrors the backend rule, `hu36a-context.md:136-138`). When a new question activates or the
  current one closes / the substage advances, the set is **cleared** — we reuse the already-wired
  `onQuestionActivated` / `onQuestionClosed` / `onSubstageAdvanced` callbacks in `DashboardClient` so the board and
  the timer/round panels reset in lockstep.
- **Snapshot fetch restores pre-connect answered state on connect/refresh.** A mid-question connect must show teams
  that answered *before* the operator connected. The live event only covers answers *after* connect, so the initial
  snapshot (Phase 2 endpoint) seeds the answered set. Until that endpoint lands, the board renders the roster with
  all teams not-answered-yet and fills in live — a correct-but-incomplete degrade, flagged in the Phase 3 gate.
- **Data client mirrors `getOperatorSessionTimerSnapshot`, not the identity path.** Gateway base URL
  (`API_GATEWAY_URL`) + private module-local `getGatewayHeaders` (Keycloak bearer) + `cache: 'no-store'` +
  per-status `IdentityError` mapping. `getGatewayHeaders` is **not exported** — the new client fn lives **inside
  `app/lib/sessions.ts`** so it reuses the module-local helper (do not import it elsewhere).
- **Server action returns a discriminated `{ data } | { error }`, mirroring `getSessionTimerSnapshotAction`
  (`app/actions/sessions.ts:90-103`).** A non-owning operator's 403 becomes `{ error }` and renders a
  not-authorized state — the board never throws/crashes and never renders another operator's data. Role gate:
  `if (session.role !== 'Operator') return { error: 'Forbidden' }`.
- **No-leak enforced by type shape, not by discipline.** The board's props/DTO types structurally omit option,
  correctness, and points. There is no field to accidentally render. This mirrors the backend VO invariant and is
  the load-bearing guarantee behind three of the gates.
- **Board mounts inside the operator hero.** New `AnsweredMonitorPanel` renders after `TriviaRoundPanel`
  (`DashboardClient.tsx:937`), inside `data-testid="operator-panel"`, only when
  `role === 'operator' && selectedOperatorSession && selectedOperatorState`.

---

## Environment

No new environment variables. Reused, verified:

- `API_GATEWAY_URL` (server-side, `app/lib/sessions.ts:18`) — the snapshot data client's base URL.
- `NEXT_PUBLIC_API_GATEWAY_URL` (client-side, `session-state-client.ts:39`) — the SignalR hub base URL, already wired.
- Hub token route `/api/realtime/hub-token` — already used by the client's `accessTokenFactory`; unchanged.

**No `typecheck` npm script exists.** `frontend/package.json` scripts are `dev`/`build`/`start`/`lint`/`test`
(`test` = `vitest run`); Playwright runs via its own CLI. The typecheck/build gate is therefore **`pnpm build`**
(Next 16 type-checks during build); unit gate is **`pnpm test`**; e2e is **`pnpm exec playwright test`**.

---

## data-testid contract

Kebab-case, element-scoped; per-team rows use a template literal with the runtime team id; state is exposed as a
separate `data-*` attribute (matching `data-tone`/`data-phase`/`data-current` conventions), never baked into the id.

| testid | element | notes |
|---|---|---|
| `answered-monitor-panel` | board container `<section>` | present whenever the operator hero renders |
| `answered-monitor-active-question` | active-question label | shows the **sequence order only** — never prompt/options |
| `answered-monitor-count` | answered tally | e.g. "3 / 5 answered"; counts only, never option/points |
| `team-answer-status-${runtimeTeamId}` | one row per roster team | carries `data-answered="true" \| "false"` |
| `answered-monitor-empty` | empty state | no active trivia question / empty roster |
| `answered-monitor-unauthorized` | not-authorized state | rendered when the snapshot action returns `{ error }` (403) |

---

## Phases

### Phase 1 — Realtime client: subscribe to `TeamAnswered` (CODE-COMPLETE — fully verified)

**Scope**
- Add the `TeamAnsweredNotificationDto` type to `app/lib/definitions.ts` (verified backend shape).
- Extend `SessionStateClientOptions`, add `normalizeTeamAnswered`, and register the `TeamAnswered` subscription in
  `createSessionStateRealtimeClient`.

**`app/lib/definitions.ts` addition** (place beside the other hub-event DTOs, ~line 407):
```ts
// SignalR "TeamAnswered" hub event payload — operator-only (live-session-operators:{id} group).
// Broadcast when a team's trivia answer is accepted. Option-free BY DESIGN: carries no selected
// option, no correctness, no points — only that the team answered, and on which active question.
export type TeamAnsweredNotificationDto = {
  liveSessionId: string
  teamId: string // runtime team id
  triviaSubstageSnapshotId: string // active-question identity, part 1
  questionSequenceOrder: number // active-question identity, part 2 (one-based)
  answeredAt: string // ISO 8601
}
```

**`app/lib/realtime/session-state-client.ts` changes**

Import the new type (extend the existing `import type { … }` block, lines 9-15):
```ts
  TeamAnsweredNotificationDto,
```

Add the callback to `SessionStateClientOptions` (after `onSubstageAdvanced?`, line 30):
```ts
  onTeamAnswered?: (notification: TeamAnsweredNotificationDto) => void
```

Add the normalizer (beside the other `normalize*` fns, ~line 159) — camelCase-or-PascalCase, matching the file:
```ts
function normalizeTeamAnswered(raw: unknown): TeamAnsweredNotificationDto {
  const n = raw as TeamAnsweredNotificationDto & {
    LiveSessionId?: string
    TeamId?: string
    TriviaSubstageSnapshotId?: string
    QuestionSequenceOrder?: number
    AnsweredAt?: string
  }
  return {
    liveSessionId: n.liveSessionId ?? n.LiveSessionId ?? '',
    teamId: n.teamId ?? n.TeamId ?? '',
    triviaSubstageSnapshotId: n.triviaSubstageSnapshotId ?? n.TriviaSubstageSnapshotId ?? '',
    questionSequenceOrder: n.questionSequenceOrder ?? n.QuestionSequenceOrder ?? 0,
    answeredAt: n.answeredAt ?? n.AnsweredAt ?? '',
  }
}
```

Destructure `onTeamAnswered` in the factory params (add to the object at lines 170-179), and register the
subscription beside the others (after the `onSubstageAdvanced` block, ~line 214):
```ts
  if (onTeamAnswered) {
    connection.on('TeamAnswered', (raw: unknown) => {
      onTeamAnswered(normalizeTeamAnswered(raw))
    })
  }
```

No change to `start`/`stop`/reconnect — the operator group join already happens via
`JoinLiveSessionAsOperatorAsync`, so the subscription receives events immediately on connect.

**Gate**
- `pnpm build` passes (new type + optional callback are additive; existing callers unaffected).
- `pnpm test` — a unit test on `normalizeTeamAnswered` proves it maps both a PascalCase and a camelCase payload to
  the camelCase DTO (mirrors the existing normalizer test style).
- A client instantiated without `onTeamAnswered` registers no `TeamAnswered` handler (backward-compatible).

---

### Phase 2 — Answered-monitor snapshot: types + data client + server action (CONTRACT ALTITUDE — blocked on backend X.4)

> Held at contract altitude because the endpoint route segment (Q1) and the result DTO shape (Q2) are not yet in
> backend source. **No final code body is written here** — per the concreteness rule, do not invent DTO field names
> or a route. When backend Phase X.4 lands, confirm both against the controller + result DTO, then implement by
> mirroring the verified siblings named below.

**Scope (contract, not code)**
- Add `TriviaAnsweredMonitorDto` (+ per-team item type) to `app/lib/definitions.ts` — **shape per the Verified
  Backend Contract table's snapshot row, confirmed against the real result DTO first.** Expected fields: active
  question identity `(substageSnapshotId, questionSequenceOrder)` + `teams: { teamId, teamCode` or `displayName,
  answered, answeredAt }[]`. **Structurally omit** option/correctness/points (no-leak invariant carried into the
  type).
- Add `getOperatorTriviaAnsweredMonitor(liveSessionId: string): Promise<TriviaAnsweredMonitorDto>` **inside
  `app/lib/sessions.ts`** — a direct mirror of `getOperatorSessionTimerSnapshot` (`sessions.ts:160-178`): same
  `await verifySession()`, `await getGatewayHeaders()`, `cache: 'no-store'`, and 401/403/404/!ok →
  `IdentityError(...)` mapping. **Only two things differ and both are blocked:** the URL path (Q1) and the return
  type (Q2).
- Add `getTriviaAnsweredMonitorAction(liveSessionId): Promise<{ data: TriviaAnsweredMonitorDto } | { error: string }>`
  to `app/actions/sessions.ts` — a mirror of `getSessionTimerSnapshotAction` (`sessions.ts:90-103`):
  `verifySession()`, `if (session.role !== 'Operator') return { error: 'Forbidden' }`, `try { data } catch
  IdentityError → { error }`. This action's control flow is fully knowable; only the DTO type symbol it references
  is blocked, so it is implemented together with the type once Q2 resolves.

**Gate**
- `pnpm build` passes once the DTO type + client fn + action are added against the **confirmed** backend contract.
- The client fn's status mapping matches `getOperatorSessionTimerSnapshot` exactly (401/403 → `IdentityError`).
- The action returns `{ error }` (never throws) on a 403, so the caller can render a not-authorized state.
- The DTO type contains **no** option/correctness/points field (compile-time no-leak).

---

### Phase 3 — `AnsweredMonitorPanel` + wire into `DashboardClient` (live-merge CODE-COMPLETE; snapshot hydration depends on Phase 2)

**Scope**
- Create `app/dashboard/AnsweredMonitorPanel.tsx` + `answeredMonitorPanel.module.css` (named-export presentational
  component, mirroring `OperatorSessionTimerPanel` / `TriviaRoundPanel`).
- In `DashboardClient.tsx`: add board state, wire `onTeamAnswered` into the existing
  `createSessionStateRealtimeClient({ … })` block (~line 364), clear the answered set from the already-wired
  `onQuestionActivated` / `onQuestionClosed` / `onSubstageAdvanced` callbacks, seed it from the Phase 2 snapshot in
  the connect/reset effect (~line 494), and render `<AnsweredMonitorPanel>` after `<TriviaRoundPanel>` (line 937).

**Component contract (`AnsweredMonitorPanel.tsx`)** — props carry only leak-safe fields:
```ts
export type AnsweredTeamRow = {
  runtimeTeamId: string
  displayName: string
  teamCode: string
  answered: boolean
  answeredAt: string | null
}

type AnsweredMonitorPanelProps = {
  activeQuestionOrder: number | null // null ⇒ no active trivia question → empty state
  teams: AnsweredTeamRow[]
  unauthorized: boolean // true ⇒ render answered-monitor-unauthorized, render no team data
  loading: boolean
}
```
Rendering rules (knowable, code-complete):
- `unauthorized` → `data-testid="answered-monitor-unauthorized"`, **no roster rendered**.
- `activeQuestionOrder === null` or `teams.length === 0` → `data-testid="answered-monitor-empty"`.
- else → `answered-monitor-active-question` (sequence order only), `answered-monitor-count` (answered/total), and
  one `team-answer-status-${runtimeTeamId}` row per team with `data-answered={answered ? 'true' : 'false'}`.
- The JSX references **only** the five `AnsweredTeamRow` fields — there is no option/correctness/points binding.

**Live-merge state in `DashboardClient` (code-complete)** — the answered set is scoped to the active question so a
new question resets it:
```ts
// answered runtime-team ids for the CURRENT active question; cleared on question/substage boundaries
const [answeredTeamIds, setAnsweredTeamIds] = useState<ReadonlySet<string>>(new Set())
```
Wire into the existing realtime client options object (do not add a second client):
```ts
onTeamAnswered: (n) => {
  if (n.liveSessionId !== selectedRealtimeSessionId) return
  setAnsweredTeamIds((prev) => {
    const next = new Set(prev)
    next.add(n.teamId)
    return next
  })
},
```
Clear the set inside the **existing** boundary callbacks (they already fire for the timer/round panels):
`onQuestionActivated`, `onQuestionClosed`, `onSubstageAdvanced` each add `setAnsweredTeamIds(new Set())`.

**Snapshot hydration (depends on Phase 2 — contract altitude until the endpoint lands)** — in the reset effect
(`DashboardClient.tsx:494-498`), after selecting a session, call `getTriviaAnsweredMonitorAction(id)` and seed
`answeredTeamIds` from the returned answered teams; on `{ error }` set an `unauthorized` flag that renders
`answered-monitor-unauthorized`. **Until Phase 2's endpoint exists, this seed call is stubbed to a no-op** and the
board renders roster-not-answered + live fill only (flagged in the gate). The roster itself comes from the snapshot
DTO (Phase 2) or the verified `/teams` fallback (Q3).

**Deriving `teams: AnsweredTeamRow[]`** (code-complete given a roster) — map the roster, marking
`answered = answeredTeamIds.has(runtimeTeamId)`; `answeredAt` from live events / snapshot where available, else null.

**Gate**
- `pnpm build` passes.
- The board renders one `team-answer-status-*` row per roster team; a team with no accepted answer shows
  `data-answered="false"` (not-answered-yet) — derived by roster enumeration, not broadcast absence.
- A live `TeamAnswered` for a rostered team flips that row to `data-answered="true"` **without reload**.
- On a new question (`onQuestionActivated`) the answered set clears and all rows return to not-answered-yet.
- No rendered node, `data-*` attribute, prop, or type exposes the chosen option, correctness, or points.
- `getTriviaAnsweredMonitorAction` returning `{ error }` renders `answered-monitor-unauthorized` and no team data —
  the board does not crash and shows no other operator's session data.
- **Known degrade until Phase 2 endpoint lands:** a mid-question connect shows already-answered teams as
  not-answered-yet until their next live event; documented, not silently shipped.

---

### Phase 4 — Tests

**Scope**
- **Unit (Vitest, `tests/unit/…` mirroring `app/`):**
  - `normalizeTeamAnswered` maps PascalCase and camelCase payloads (Phase 1).
  - `AnsweredMonitorPanel` renders: not-answered-yet rows from a roster with no answers; a `data-answered="true"`
    row when a team is in the answered set; the empty state for `activeQuestionOrder === null`; the unauthorized
    state for `unauthorized`; and — the load-bearing assertion — that no option/correctness/points text ever
    appears for any prop combination.
- **E2E (Playwright, `tests/e2e/…`, `operatorPage` fixture from `tests/fixtures/auth.ts`):**
  - Operator opens a live session → `answered-monitor-panel` visible; every roster team starts
    `data-answered="false"`; a `TeamAnswered` pulse flips one row to `true` with no reload.
  - A non-owning operator (session not assigned to them) → `answered-monitor-unauthorized`; no team rows; no crash.
  - Assert no option/correctness/points copy is present in the DOM before close.

**Gate**
- `pnpm test` (Vitest) and `pnpm exec playwright test` pass.
- Every acceptance criterion below maps to at least one automated test.

---

## Acceptance-criteria → test mapping

| Acceptance criterion (HU-36A) | Covered by |
|---|---|
| During the active question, the operator sees only whether each team answered or not | P3 render rules; P4 unit (row states) + e2e (roster starts not-answered) |
| Before close, the operator cannot see the option a team chose | P1/P2 type shapes (no option field); P4 unit "no option/correctness/points text" assertion |
| The monitoring view updates during the session without manual reload | P1 `TeamAnswered` subscription + P3 live-merge; P4 e2e (pulse flips a row, no reload) |
| Monitoring respects the operator's authorized sessions | P2 server action `{ error }` on 403 + P3 unauthorized state; P4 e2e (non-owner sees not-authorized) |
| Answered/not-answered updates are broadcast in real time via SignalR, without manual reload | P1 reused operator-only event; P3 wiring; P4 e2e |
| A team with no accepted answer renders not-answered-yet | P3 roster-enumeration derivation; P4 unit + e2e |

---

## Open Questions / Dependencies

- **Q1 — snapshot endpoint route segment (BLOCKING Phase 2).** The user's brief for this slice says
  `GET /api/sessions/{liveSessionId}/answered-monitor`; `backend/docs/hu36a-brief.md:48` says
  `/api/sessions/{liveSessionId}/trivia/answered-monitor`. **Neither is implemented.** Confirm the exact template
  against `SessionsController.cs` once backend Phase X.4 lands, then set the URL in `getOperatorTriviaAnsweredMonitor`.
- **Q2 — snapshot result DTO shape (BLOCKING Phase 2).** `TriviaAnsweredMonitorDto` / per-team item do not exist in
  backend source. Confirm exact field names + JSON casing (per-team `teamCode` vs `displayName` vs both;
  `answeredAt` nullability; active-question identity field names) against the real result DTO before adding the
  frontend type. Do not invent them.
- **Q3 — roster source for the board.** The board renders "all teams, mark not-answered." Preferred source is the
  monitor snapshot DTO (Q2) which is meant to carry the full roster + answered flags together. If the monitor DTO
  omits the roster, fall back to the verified `GET /api/sessions/{liveSessionId}/teams`
  (`getSessionAssociatedTeams`) for `runtimeTeamId`/`displayName`/`teamCode`. Decide once Q2 resolves.
- **Q4 — `TeamAnswered.teamId` ↔ roster id correspondence.** The live event's `teamId` (Guid) must equal the roster
  team key the board renders (`runtimeTeamId`). This is the assumption behind `answeredTeamIds.has(runtimeTeamId)`.
  Verify with a live pulse against a known roster during Phase 3 before trusting the flip; if the event carries the
  reference team id instead, key on `referenceTeamId`.
- **Dependency — gateway routing.** All session reads go through `API_GATEWAY_URL`. The gateway must route the new
  `answered-monitor` path to `session-operations-service`. Confirm the route is registered when the backend endpoint
  lands (out of this slice's scope but blocks the e2e gate).
- **Gate command note.** There is no `pnpm typecheck` script; the typecheck gate is `pnpm build`. If a dedicated
  `typecheck` script is desired, adding `"typecheck": "tsc --noEmit"` to `package.json` is a reasonable, separate
  change — not assumed here.

---

## Out of Scope

- Any backend change (this slice adds no controller, query, DTO, migration, or broadcaster).
- Correctness / right-answer reveal (HU-35) and post-close full answer + points review (HU-36B) — the board must
  expose neither, and does not.
- A participant-facing view of answered state — the `TeamAnswered` group is operator-only by design; no participant
  surface is added.
- A new SignalR connection, hub, or `WebSocket` — the existing `createSessionStateRealtimeClient` is reused.
- A per-session detail route — the board lives inside the existing single-route operator hero in `DashboardClient`.
- Persisting or caching answered state across reloads beyond the on-connect snapshot fetch.

---

## Commit Sequence

```
feat(frontend): phase 1 — subscribe operator realtime client to TeamAnswered (HU-36A)
feat(frontend): phase 2 — answered-monitor snapshot client + server action (HU-36A)   # gated on backend X.4
feat(frontend): phase 3 — operator answered/not-answered board panel (HU-36A)
test(frontend): phase 4 — unit + e2e for answered-monitor board (HU-36A)

Ref: HU-36A
Ref: DES-49
Ref: DES-70
```

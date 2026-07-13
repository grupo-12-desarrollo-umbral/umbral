# Prompt Example — DES-93 TreasureHunt substage authoritative timer (Feature Slice)

Concrete prompt sequence for driving **DES-93** through a full backend slice on `des-93`. Follows the pattern in [workflow_for_prompts.md](./workflow_for_prompts.md).

**There is no HU for this ticket.** DES-93 is implementation debt that closes the **`TreasureHunt` branch of DES-77 (HU-22) AC#1**, which shipped Trivia-only. The authoritative timer the board consumes is wired **only** to the active trivia-question window, so on a `TreasureHunt` substage `GetAuthoritativeSessionTimerSnapshot` returns `TimeSpan.Zero` → `IsExpired == true`, and the HU-23 board shows `00:00 / Expired`. DES-93 adds a **`TreasureHunt` substage timer** — seeded on substage activation, advancing while `Active`, frozen on `Paused` — and **branches** the authoritative accessor on the active substage's `SubstagePlayMode`. This is **additive** (feature flow): nothing is torn out; the trivia path is untouched.

> **Single-service scope (human decision, 2026-07-13).** DES-93's own design is two-service (Fase 1 `mission-design-service` authors a per-substage `MaximumTime` field + migration; Fase 2 `session-operations-service` consumes it). **This slice is `session-operations-service` ONLY.** With `mission-design-service` out of scope there is no per-substage authored duration, so the treasure-hunt timer is seeded from the **session-level `MaximumTime`** already on `LiveSession` (`LiveSession.cs:93`). This satisfies AC#2 ("alineado con el **reloj de la sesión**") and closes the `00:00 / Expired` bug, but does **not** deliver AC#1's literal source ("del `MaximumTime` de la **subetapa**"). The per-substage authored field is a **deferred `mission-design-service` follow-up** — see Rationale. Do not invent a mission-design field here.

When working from the monorepo root, make the target workload explicit in each prompt. For backend steps, point to `@backend/.agents/backend-agent.md`.

---

## Stop 1 acceptance guard

Reject this generated prompt before implementation if it does not explicitly scope all of the following:

- an **active `TreasureHunt` substage** yields an **advancing** authoritative countdown (not `Zero`/`Expired`), seeded from the session-level `MaximumTime`, anchored at **substage activation**
- `GetAuthoritativeSessionTimerSnapshot` **branches on the active substage's `SubstagePlayMode`**: `TreasureHunt` → the substage-timer window; `Trivia` → the existing question window (unchanged)
- pause **freezes** the treasure-hunt timer; resume continues from the frozen remainder (parity with the DES-77 trivia rule)
- the treasure-hunt remaining is pushed live over SignalR (`SessionTimerUpdated`) and returned on the two timer read endpoints; **no** RabbitMQ
- the `State` pattern owns the advancing-vs-frozen decision (per-`SessionState` type via `LiveSessionStateFactory`), **mirroring** the question-timer state methods — not ad-hoc `if (State == …)`
- treasure-hunt timer expiry is **report-only** — it does **not** auto-advance the substage (`CloseAndAdvanceAsync` stays trivia-only); target-resolution advancement (HU-29–32) and question activation/advancement (DES-78) are **not** built or expanded here

**Scope deviation to carry forward (do not silently drop):** the duration source is the **session-level** `MaximumTime`, not a per-`TreasureHunt`-substage authored field — that field is a deferred `mission-design-service` follow-up (Rationale). AC#1's literal "del `MaximumTime` de la subetapa" is therefore **partially** met (observable bug fixed; per-substage source deferred).

---

## Required design patterns

- **`State` (mandated, X.1 Domain)** — `required_patterns_matrix.md:41,110`. The advancing-vs-frozen decision for the treasure-hunt timer is owned by the per-`SessionState` type (`Active` advances, `Paused` freezes) via `LiveSessionStateFactory.For(State)`, **mirroring** `GetQuestionTimerSnapshot` on `ILiveSessionState`/`ActiveLiveSessionState`. Do not replace with `if (State == …)` conditionals.
- **Transport: SignalR (mandated)** — `required_patterns_matrix.md:57,110`. The treasure-hunt remaining broadcasts live as `SessionTimerUpdated` to the `live-session:{id}` group. **No RabbitMQ** in this slice.

> Applies-where note (no new gate): `GET /api/sessions/{id}/timer` and `.../participants/timer` are protected reads, but DES-93 is not matrix-tagged for `Proxy` and not in the applies-where set (HU-04/05/36B). They inherit the standard operator / participant-membership `AuthorizationBehaviour`/gateway guard (ADR-0001/0002) — note only, no new `Proxy` gate.

---

## Pre-resolved orient (as of 2026-07-13)

> Step 1 has already been run. Paste this section into any agent session that needs context before picking up a phase — no need to re-run the orient prompt.

### What predecessors have already landed

DES-93 is **Todo** (`canon-realign`, `svc:session-operations-service`, `svc:mission-design-service`, `Feature`; **`ready-for-agent` not yet applied** — Step 2). It closes the deferred `TreasureHunt` branch of DES-77. Build-on predecessors are Done/merged:

- **DES-77 (HU-22)** — the authoritative timer keyed off the active `SubstagePlayMode`. Landed the `_questionTimer*` window (`LiveSession.cs:16-19,853-948`), `State`-dispatched advance/freeze (`ActiveLiveSessionState.cs:22-35`), seed on `ActivateQuestion` (`:304-321`), freeze/resume via `EnterPaused/ActiveQuestionTimerState` (`:826-839`), `question_timer_*` columns (`LiveSessionConfiguration.cs:68-80`), and the tick worker (`AuthoritativeSessionTimerWorker.cs:51-81`). It **deliberately deferred** the treasure-hunt countdown (its "OD-1"); DES-93 is that follow-up. `GetAuthoritativeSessionTimerSnapshot` hard-wires the question window today (`LiveSession.cs:297-302`) → the bug.
- **DES-31 (HU-23)** — the live team board, the **consumer**. It reads the timer via `ProjectParticipantTeamBoard`/`ProjectOperatorSessionPanel` → `GetAuthoritativeSessionTimerSnapshot` (`LiveSession.cs:520,540`). No board change — it starts counting when the snapshot is non-zero.

Landed-untouched (consumed as-is): DES-22 (HU-15) runtime snapshot; DES-24 (HU-17) no `SessionMode`; DES-76 (HU-21A) state machine + `Enter` hooks. **Downstream, do NOT trespass:** DES-78 (HU-33A) question activation/advancement/close + `TriviaRoundOrchestratorFacade`; HU-29–32 treasure-hunt target resolution + substage advancement. No same-service In Progress predecessor → branch base is `develop`.

### What DES-93 adds (per the ticket's resolved design + PRD DES-70)

| Concern | New work |
|---|---|
| TreasureHunt substage timer (domain) | `_substageTimer*` window on `LiveSession`, mirroring `_questionTimer*`; seeded on substage activation from the session `MaximumTime`; advancing while `Active`, frozen on `Paused`. |
| Authoritative branch (domain) | `GetAuthoritativeSessionTimerSnapshot` branches on the active substage's `PlayMode`: `TreasureHunt` → substage window; `Trivia` → existing question window. |
| `State`-owned advance/freeze (domain) | Parallel `…SubstageTimer…` state methods on `ILiveSessionState`/`LiveSessionStateBase`/`ActiveLiveSessionState`. |
| Timer DTO + queries (app) | Verify-only pass-through — the two timer queries + `SessionTimerSnapshotDtoFactory` map the generic snapshot unchanged (`ActiveQuestion` null for treasure-hunt). Add tests. |
| Persistence + worker (infra) | EF config + **migration adding** `substage_timer_*` columns; `ListActiveTimersAsync` also selects advancing substage timers; worker broadcasts treasure-hunt remaining but is **report-only on expiry**. |
| Endpoints + broadcast (api) | No new endpoints — the two timer GETs + transition `Timer` field now carry the treasure-hunt remaining; `SessionTimerUpdated` broadcast verified. |

### Out of scope for this slice (surface at Stop 1, do not build)

- **A per-`TreasureHunt`-substage authored `MaximumTime` field** + its EF migration + authoring API — that is the deferred **`mission-design-service`** follow-up (Fase 1). This slice uses the session-level `MaximumTime`.
- **Treasure-hunt substage auto-advance on timer expiry** — treasure-hunt advances by **target resolution** (HU-29–32), not a timer. Expiry is report-only.
- **Question activation / advancement / close + trivia round orchestration** — DES-78 (HU-33A). Leave `CloseAndAdvanceAsync` trivia-only and unchanged.
- **Frontend** — HU-23's board already renders the timer (see Step 9). No frontend slice.

### Branch state and prerequisite

`des-93` branches from `develop`. All build-on dependencies (HU-15/17/21A/22/23) are Done/merged; no same-service predecessor is In Progress.

**Before starting:** confirm (grep) that `GetAuthoritativeSessionTimerSnapshot` returns only the question window today (`LiveSession.cs:297-302`), that the `_questionTimer*` window + `EnterPaused/ActiveQuestionTimerState` freeze/resume + the `State` `GetQuestionTimerSnapshot` methods exist (the mirror source), that `SubstageSnapshot` has **no** time field (`:31-37`), and that the session-level `MaximumTime` (`LiveSession.cs:93`) is the available duration source.

### Linear state (as of 2026-07-13)

- DES-93: **Todo**, labels: `canon-realign`, `svc:session-operations-service`, `svc:mission-design-service`, `Feature` — **`ready-for-agent` NOT yet applied (Step 2 adds it)**
- DES-77 (HU-22), DES-31 (HU-23): **Done** (the foundation this slice builds on)
- DES-78 (HU-33A trivia round): **downstream** — do not trespass

> Linear live state may have changed. Use the Linear MCP to verify DES-93 status and labels if needed, but do not re-fetch PRD scope — read the local file at `@backend/docs/prd/DES-70-primera-implementacion-de-session-operations-service-hu-15-a-hu-36.md`.

---

## 1. Orient — read service state and PRD

> **Skip this step if you have read the pre-resolved orient section above.** Run it only if the service source, README, or Linear state may have changed since 2026-07-13.

```text
Read the following and summarise what is implemented today vs. what DES-93 must add:
- @backend/docs/des-93-context.md — the pre-resolved DES-93 context (primary; carries the per-phase derivation)
- @backend/docs/hu22-context.md — the DES-77 timer machinery this slice mirrors
- @backend/docs/prd/DES-70-primera-implementacion-de-session-operations-service-hu-15-a-hu-36.md — US15/US16

Then grep the existing session-operations source to confirm:
- GetAuthoritativeSessionTimerSnapshot returns ONLY the active-question window today (LiveSession.cs:297-302) -> Zero/Expired on a TreasureHunt substage
- the _questionTimer* window + EnterPaused/ActiveQuestionTimerState freeze/resume + the State GetQuestionTimerSnapshot methods exist (the mirror source)
- SubstageSnapshot has no time field; the session-level MaximumTime (LiveSession.cs:93) is the available duration source

Then use the Linear MCP to fetch the current live state and labels of DES-93.

Output: what exists (the trivia timer to mirror), what is missing (the TreasureHunt branch), and the exact per-layer additions.
Do not start planning or implementing yet.
```

---

## 2. Label DES-93 as ready-for-agent

```text
Use the Linear MCP to add the label ready-for-agent to DES-93 if it is missing.
Pass the FULL existing label set in the same update (canon-realign, svc:session-operations-service,
svc:mission-design-service, Feature) plus ready-for-agent — save_issue drops omitted labels.
Output the updated DES-93 ticket state and labels.
```

---

## 3. Confirm slice readiness

```text
Use the Linear MCP to confirm DES-93 carries both svc:session-operations-service and ready-for-agent
labels and output its current status and acceptance criteria.

The PRD scope is already in the local file at
@backend/docs/prd/DES-70-primera-implementacion-de-session-operations-service-hu-15-a-hu-36.md.
Do not re-fetch PRD scope from Linear.

Before planning, explicitly confirm the Stop 1 acceptance guard:
- an active TreasureHunt substage yields an advancing countdown (not Zero/Expired), seeded from the session MaximumTime, anchored at substage activation
- GetAuthoritativeSessionTimerSnapshot branches on the active substage's SubstagePlayMode (TreasureHunt window; Trivia unchanged)
- pause freezes the treasure-hunt timer; resume continues from the frozen remainder
- treasure-hunt remaining pushed via SignalR (SessionTimerUpdated) + returned on the timer reads; no RabbitMQ
- State pattern owns advancing/frozen, mirroring the question-timer state methods
- treasure-hunt expiry is report-only (no auto-advance); question activation/advancement (DES-78) and target resolution (HU-29-32) not built here

Acknowledge the single-service duration-source deviation: the timer is seeded from the session-level MaximumTime
(the per-TreasureHunt-substage authored field is a deferred mission-design-service follow-up), so AC#1's literal
"del MaximumTime de la subetapa" is partially met — the observable Zero/Expired bug is fixed and the countdown
aligns with the session clock (AC#2).

Output the confirmed DES id, title, acceptance criteria, labels, the guard confirmation, and the deviation note before planning the slice.
```

In the remaining steps, `DES-93` is the ticket, `DES-77` the origin (HU-22 AC#1), and `DES-70` the session-operations PRD (local file above).

---

## 4. Start the slice

```text
Prepare the TreasureHunt substage timer slice on branch des-93 (base develop).
This slice affects backend session-operations-service ONLY (no frontend).

The pre-resolved orient at the top of this document lists what exists (the trivia timer to mirror) and what is
missing (the TreasureHunt branch). Do not re-read the PRD for scoping unless you need a precise detail.

This is additive: add a TreasureHunt substage timer (fields + methods) mirroring the _questionTimer* window,
seeded on substage activation from the session-level MaximumTime, and branch GetAuthoritativeSessionTimerSnapshot
on the active substage's SubstagePlayMode. Do NOT touch question activation/advancement or the
TriviaRoundOrchestratorFacade (DES-78). Do NOT build treasure-hunt target resolution or substage advancement
(HU-29-32). Do NOT add a per-substage field to mission-design-service (deferred follow-up) — seed from the
session MaximumTime. Treasure-hunt timer expiry is report-only.

Move DES-93 to In Progress and output the exact scope, branch name (des-93), base branch (develop), and touched surfaces.
```

---

## 5. Backend phase X.1 — Domain layer

```text
Use @backend/.agents/backend-agent.md.
Implement backend phase X.1 for DES-93 in session-operations-service, per the
**X.1 derivation block in @backend/docs/des-93-context.md** (your spec — do not
re-read the canon or re-inspect the tree; open a cited canon section only to
fill a gap the block leaves open).

Gate:
- Domain build passes; a unit test locks: an active TreasureHunt substage -> GetAuthoritativeSessionTimerSnapshot returns an advancing countdown seeded from the session MaximumTime (RemainingDuration > 0, IsExpired == false), NOT Zero/Expired
- Paused freezes the treasure-hunt timer and Active resumes it at the frozen remainder
- an active Trivia substage still returns the active-question window (unchanged)
- timer expiry marks the substage timer expired but raises NO advancement event
- State pattern verified: advancing/frozen decided by per-state types via LiveSessionStateFactory (mirroring the question-timer state methods), not ad-hoc conditionals

Do not touch question activation/advancement (ActivateQuestion/CloseActiveQuestion — DES-78). Do not touch other backend layers or frontend.
```

Commit:

```text
feat(session-operations): phase X.1 - domain layer (DES-93)

Ref: DES-93
Ref: DES-77
Ref: DES-70
```

---

## 6. Backend phase X.2 — Application layer

```text
Use @backend/.agents/backend-agent.md.
Implement backend phase X.2 for DES-93 in session-operations-service, per the
**X.2 derivation block in @backend/docs/des-93-context.md** (your spec — do not
re-read the canon or re-inspect the tree; open a cited canon section only to
fill a gap the block leaves open).

Gate:
- Application build passes; a test proves the operator + participant timer DTOs return the treasure-hunt advancing remaining (IsAdvancing true, status not Expired, ActiveQuestion == null) for a TreasureHunt substage, and still the question window for a Trivia substage
- participant/operator authorization preserved
- the two timer queries + SessionTimerSnapshotDtoFactory map the treasure-hunt snapshot with NO production change (pass-through); if an edit is unavoidable, note why

Do not touch the TriviaRoundOrchestratorFacade or question orchestration (DES-78). Do not touch Infrastructure, Api, or frontend.
```

Commit:

```text
feat(session-operations): phase X.2 - application layer (DES-93)

Ref: DES-93
Ref: DES-77
Ref: DES-70
```

---

## 7. Backend phase X.3 — Infrastructure layer

```text
Use @backend/.agents/backend-agent.md.
Implement backend phase X.3 for DES-93 in session-operations-service, per the
**X.3 derivation block in @backend/docs/des-93-context.md** (your spec — do not
re-read the canon or re-inspect the tree; grep the model snapshot rather than
full-reading it).

Gate:
- Infrastructure build passes
- a migration ADDS the substage_timer_* columns (assert via model snapshot), mirroring the question_timer_* columns
- ListActiveTimersAsync also selects sessions with an advancing substage timer (State Active, _substageTimerAdvancingSince set, _substageTimerExpiredAt null), OR-ed with the existing advancing-question predicate
- a repository integration test round-trips a session carrying a treasure-hunt substage-timer state
- the worker broadcasts the treasure-hunt remaining but does NOT call CloseAndAdvanceAsync on treasure-hunt expiry (report-only); the trivia close/advance path is unchanged

Do not touch Api or frontend.
```

Commit:

```text
feat(session-operations): phase X.3 - infrastructure layer (DES-93)

Ref: DES-93
Ref: DES-77
Ref: DES-70
```

---

## 8. Backend phase X.4 — API layer

```text
Use @backend/.agents/backend-agent.md.
Implement backend phase X.4 for DES-93 in session-operations-service, per the
**X.4 derivation block in @backend/docs/des-93-context.md** (your spec — do not
re-read the canon or re-inspect the tree; open a cited canon section only to
fill a gap the block leaves open).

Gate:
- endpoint integration tests: GET /api/sessions/{id}/timer (operator) and GET /api/sessions/{id}/participants/timer (participant) return the treasure-hunt advancing remaining (NOT Expired) when the active substage is TreasureHunt; authorization enforced
- hub test: a treasure-hunt SessionTimerUpdated broadcast reaches the live-session:{id} group
- service coverage reaches the repo gate target (ADR-0005)

Do not reshape the endpoint routes; the payload now carries the treasure-hunt remaining. Do not add a RabbitMQ publish. Do not touch frontend.
```

Commit:

```text
feat(session-operations): phase X.4 - api layer (DES-93)

Ref: DES-93
Ref: DES-77
Ref: DES-70
```

---

## 8.5. Docker rebuild and smoke

```text
From the monorepo root, rebuild and restart the backend stack after the API phase:

docker compose build session-operations-service api-gateway
docker compose up -d session-operations-service api-gateway

Run curl smoke checks through the gateway against a session whose ACTIVE substage is TreasureHunt:
- GET /api/sessions/{liveSessionId}/timer as an Operator -> 200; RemainingSeconds is a real countdown decreasing over successive reads, TotalSeconds reflects the session MaximumTime, Status is not "Expired", ActiveQuestion is null
- GET /api/sessions/{liveSessionId}/participants/timer as a participant (team + token) -> 200 with the same treasure-hunt remaining
- pause the session (PATCH .../state -> Paused), re-read the timer -> remaining is frozen; resume (-> Active) -> the countdown continues from the frozen remainder
- confirm a connected SignalR client on the live-session:{id} group receives SessionTimerUpdated with the treasure-hunt remaining

Output:
- container status
- smoke command results
- confirmation the HU-23 board would now count down on a TreasureHunt substage (no more 00:00 / Expired) -- this is a behavioural change for the frontend chip
```

---

## 9. Frontend slice

```text
No frontend slice is required for DES-93.

The HU-23 live team board already renders the authoritative timer from SessionTimerSnapshotDto /
SessionTimerUpdated (mobile useSessionTimer -> getParticipantTimerSnapshot; operator panel likewise). The DTO
shape does NOT change — for a TreasureHunt substage its existing RemainingSeconds/TotalSeconds/Status now carry
a real advancing countdown instead of 0 / Expired. So the board starts counting down with zero frontend changes;
this is a purely behavioural change.

Optional (NOT part of this slice, a frontend follow-up if desired): the ticket's "Mientras tanto" note mentions a
temporary HU-23 workaround that may hide/neutralize the timer chip in TreasureHunt mode to avoid a misleading
"Expired". Once DES-93 lands, that workaround can be removed so the chip shows the live countdown. If the frontend
team takes that on, they follow @frontend/AGENTS.md and the standard frontend plan concreteness rule; it needs no
backend change and is out of scope here.
```

> No Step 9b — there is no frontend plan to implement for this slice.

---

## 10. Close-out

```text
Use the Linear MCP to re-check DES-93 acceptance criteria and labels.
Verify the final implementation against the Stop 1 acceptance guard:
- active TreasureHunt substage -> advancing countdown (not Zero/Expired), seeded from the session MaximumTime, anchored at substage activation
- GetAuthoritativeSessionTimerSnapshot branches on the active substage's SubstagePlayMode (Trivia path unchanged)
- pause freezes; resume continues from the frozen remainder
- SignalR SessionTimerUpdated broadcast + timer read surface; no RabbitMQ
- treasure-hunt expiry report-only (no auto-advance); DES-78 / HU-29-32 not built here

Run final backend verification required by the repo instructions.
Summarise:
- commits created
- the timer behaviour added (TreasureHunt substage countdown seeded from the session MaximumTime, branch on SubstagePlayMode, pause/resume, report-only expiry) and the migration adding the substage_timer_* columns
- tests and gates run (incl. ADR-0005 coverage)
- the duration-source deviation (session-level MaximumTime; per-substage authored field deferred to mission-design-service) recorded for the follow-up ticket and the frontend

Create the PR:

gh pr create \
  --base develop \
  --head des-93 \
  --title "feat(session-operations): TreasureHunt substage authoritative timer (DES-93)" \
  --body "Closes the TreasureHunt branch of DES-77/HU-22 AC#1: GetAuthoritativeSessionTimerSnapshot now branches on the active substage's SubstagePlayMode — a TreasureHunt substage exposes an advancing countdown (seeded from the session-level MaximumTime, anchored at substage activation, frozen on pause), a Trivia substage keeps the existing active-question window unchanged. Adds a _substageTimer* window on LiveSession mirroring the question-timer machinery, its State-dispatched advance/freeze, EF config + a migration adding the substage_timer_* columns, the ListActiveTimersAsync predicate, and a report-only worker tick (treasure-hunt expiry does not auto-advance — advancement stays target-resolution-driven, HU-29-32). The HU-23 board now counts down on a TreasureHunt substage with no frontend change (behavioural). Single-service scope: the per-TreasureHunt-substage authored MaximumTime field is a deferred mission-design-service follow-up, so the timer is seeded from the session-level MaximumTime (AC#2 'reloj de la sesión'); AC#1's literal per-substage source is partially met. Question activation/advancement (DES-78) and treasure-hunt play (HU-29-32) are out of scope. SignalR transport verified; no RabbitMQ."
```

---

## Rationale

The observable bug (`00:00 / Expired` on a `TreasureHunt` substage of the HU-23 board) is that the authoritative clock the board consumes is wired **only** to the active trivia-question window: `GetAuthoritativeSessionTimerSnapshot` delegates straight to `GetActiveQuestionTimerSnapshot` (`LiveSession.cs:297-302`), and with no active question `_questionTimerRemainingDuration` is `TimeSpan.Zero` → `IsExpired == true`. DES-77 (HU-22) built the trivia timer and **explicitly deferred** the treasure-hunt countdown (its "OD-1: no treasure-hunt countdown … a per-substage authored duration would need an ADR + a new snapshot field"). DES-93 is that deferred follow-up: the design decision (2026-07-12) resolves the open question — a `TreasureHunt` substage gets an authoritative countdown, anchored at substage activation, frozen on pause (parity with the trivia rule). The build **mirrors** the existing `_questionTimer*` machinery rather than inventing a new pattern; `State` (mandated) already owns advance/freeze via `LiveSessionStateFactory`, so the treasure-hunt timer gets parallel `…SubstageTimer…` state methods. SignalR is the mandated transport for the live push.

**Duration-source decision (single-service scope, human 2026-07-13; generator constraint 3 — noted, not guessed).** DES-93's own design places the duration in a **per-`TreasureHunt`-substage `MaximumTime` field authored in `mission-design-service`** (Fase 1), consumed by `session-operations-service` (Fase 2). Scoping this slice to `session-operations-service` alone removes Fase 1, so there is **no per-substage authored duration** at runtime — `SubstageSnapshot` has no time field (`SubstageSnapshot.cs:31-37`). The slice therefore seeds the treasure-hunt timer from the **session-level `MaximumTime`** already on `LiveSession` (`:93`, seed 60 min). Consequences, carried forward deliberately:

- **AC#2 is fully met** — the countdown is "alineado con el **reloj de la sesión**" (it *is* the session clock).
- **AC#1 is partially met** — the observable half ("una cuenta atrás con sentido, no `Zero`/`Expired`") is delivered, but the literal source ("derivada del `MaximumTime` de la **subetapa** de búsqueda activa") is **not**: multiple treasure-hunt substages would each count the full session time from their own activation, rather than a per-substage authored budget.
- The **per-substage authored field** (the true AC#1 source) is a **deferred `mission-design-service` follow-up ticket** (Fase 1: add `MaximumTime` to `Substage`/`SubstageSnapshot`, EF migration, authoring API + validators; then re-point the seed here from `SubstageSnapshot.MaximumTime`). It is out of this slice by the single-service decision — do not invent a mission-design field.

Treasure-hunt timer expiry is **report-only**: a treasure-hunt substage advances by **target resolution** (canon; HU-29–32), never by a timer, so the worker broadcasts `Expired` and stops rather than calling `CloseAndAdvanceAsync` (which stays trivia-only, DES-78). Question activation/advancement (DES-78) and treasure-hunt target resolution (HU-29–32) remain deliberately out of scope.

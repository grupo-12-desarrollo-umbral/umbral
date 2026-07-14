# DES-93 Context — TreasureHunt substage authoritative timer (completes DES-77/HU-22 AC#1)

> Paste this section into any agent session that needs context for DES-93.
> Last updated: 2026-07-13 | Branch: `des-93` | Base: `develop`
>
> There is **no HU** for this ticket — DES-93 is implementation debt that closes the
> `TreasureHunt` branch of DES-77 (HU-22) AC#1, which shipped **Trivia-only**. Follow the
> `hu09-context.md` / `hu22-context.md` section structure; file/type paths follow the
> ADR-0011 vertical-slice layout (Constraint 11), not any pre-ADR-0011 bucket paths.

## State

- DES-93: **Todo**, labels: `canon-realign`, `svc:session-operations-service`, `svc:mission-design-service`, `Feature`. **`ready-for-agent` is NOT present** — the generator's step-1 gate. Generated on explicit human invocation (2026-07-13); **`ready-for-agent` must be applied to DES-93 before the driver starts** (prompt Step 2 confirms/adds it).
- **Resolved mode: feature flow (additive).** DES-93 carries `canon-realign` but has **no `⚠️ Deuda/Nota de canon` comment** (verified — zero comments), and it is **not** a rebuild (`needs-rebuild` absent) and **not** in the realignment map's superseded column (superseded set = DES-23/28/30/44; `canon-realignment-after-mission-runtime-rewrite.md:66-68,147`). The work is **purely additive** — it adds a `TreasureHunt` substage timer branch that never existed — so there is **no keep/delete/decide** classification: nothing is torn out. Scope is the ticket body's "Decisión de diseño (2026-07-12) — resuelta".
- **Single-service scope (human decision, 2026-07-13).** DES-93's own design is a **two-service** feature: Fase 1 = `mission-design-service` (author an explicit per-`TreasureHunt`-substage `MaximumTime` field + EF migration), Fase 2 = `session-operations-service` (consume it in the timer). This artifact set targets **`session-operations-service` only** — where the observable bug and 4 of 5 ACs live.
  - **⚠️ Duration-source deviation (must carry to the driver + frontend).** With `mission-design-service` out of scope, there is **no per-substage authored duration** at runtime — `SubstageSnapshot` has **no** time field (`SubstageSnapshot.cs:31-37`, `CreateTreasureHunt(title, sequenceOrder)` takes no maxTime). So the `TreasureHunt` substage timer is seeded from the **session-level `MaximumTime`** already on `LiveSession` (`LiveSession.cs:93`, seeded from `maximumTimeMinutes`, seed 60 min). This **satisfies AC#2** ("alineado con el **reloj de la sesión**") and closes the observable `00:00 / Expired` bug (AC#1's *observable* half, AC#3, AC#5), but it does **not** deliver AC#1's literal source ("del `MaximumTime` de la **subetapa** de búsqueda activa"). The per-substage authored field is a **deferred `mission-design-service` follow-up ticket** (Fase 1). Do not invent a mission-design field in this slice.
- **Predecessors (build-on):** DES-77 (HU-22, **Done**) — the authoritative-timer machinery this slice mirrors (`_questionTimer*` window, `State`-dispatched advance/freeze). DES-31 (HU-23, the live team board) — the **consumer**: it already renders the timer via `ProjectParticipantTeamBoard` → `GetAuthoritativeSessionTimerSnapshot` (`LiveSession.cs:517-536`), so no board change is needed — it starts counting the moment the snapshot is non-zero.
- **Predecessors (landed, untouched by this slice):** DES-22 (HU-15) `MissionRuntimeSnapshot`/`LiveSession.Create`; DES-24 (HU-17) no session-level `SessionMode`; DES-76 (HU-21A) the `Scheduled→…→Finished/Cancelled` state machine + `State` `Enter` hooks. Consumed as-is.
- **Downstream — do NOT trespass:** DES-78 (HU-33A trivia round) owns question `ActivateQuestion`/`CloseActiveQuestion`/`TriviaRoundOrchestratorFacade`; HU-29–32 own treasure-hunt **target resolution + substage advancement**. DES-93 owns only the treasure-hunt timer **window** (seed/freeze/resume/remaining) + its authoritative snapshot/broadcast. It does **not** auto-advance a treasure-hunt substage on timer expiry (advancement stays target-resolution-driven).
- PRD DES id: **DES-70** → `backend/docs/prd/DES-70-primera-implementacion-de-session-operations-service-hu-15-a-hu-36.md` (authoritative; never re-fetch PRD scope from Linear). Origin: **DES-77** (HU-22 AC#1, incomplete).
- Branch: **`des-93`**, base **`develop`** (DES-77 Done/merged; no same-service predecessor In Progress).

## Required design patterns

| Pattern | Owning phase | Why (from patterns matrix) | Concrete obligation |
|---|---|---|---|
| `State` (mandated) | X.1 Domain | `required_patterns_matrix.md:41,110`: "`HU-22` — Timer behavior depends on session state (active vs paused); authoritative remaining time pushed and restored on resume/reconnect." DES-93 continues that timer work. | The `TreasureHunt` substage timer's **advancing-vs-frozen** decision is owned by the per-`SessionState` type (`Active` advances, `Paused` freezes), **mirroring** `GetQuestionTimerSnapshot` on `ILiveSessionState`/`ActiveLiveSessionState` — **not** an ad-hoc `if (State == Active)` in `LiveSession`. Realized by adding parallel `…SubstageTimer…` state methods to the existing `LiveSessionStateFactory.For(State)` dispatch. |

Transport note: DES-93 carries **SignalR / WebSockets** (`required_patterns_matrix.md:57,110`). The treasure-hunt remaining time broadcasts live as `SessionTimerUpdated` over the existing `ISessionTimerBroadcaster` / `SessionsHub` surface — verify in X.4. **No RabbitMQ** in this slice.

Applies-where note (no new gate): `GET /api/sessions/{id}/timer` (operator) and `GET /api/sessions/{id}/participants/timer` (participant) are protected reads, but DES-93 is **not** matrix-tagged for `Proxy` and not in the applies-where set (HU-04/05/36B). They inherit the standard operator / participant-membership `AuthorizationBehaviour` guard (ADR-0001/0002) — note only, **no** new `Proxy` gate.

## What predecessors have already landed

**Build-on (read for derivation):**

- **DES-77 (HU-22) — Done.** The authoritative timer keyed off the active `SubstagePlayMode`. It landed the **`_questionTimer*` window** on `LiveSession` (`LiveSession.cs:16-19,808-948`) with `State`-dispatched advance/freeze (`ActiveLiveSessionState.GetQuestionTimerSnapshot` → `GetAdvancingQuestionTimerSnapshot`; base → `GetFrozenQuestionTimerSnapshot`), seeded on `ActivateQuestion` (`:304-321`), frozen/resumed via `EnterPaused/ActiveQuestionTimerState` (`:826-839`), persisted as `question_timer_*` columns (`LiveSessionConfiguration.cs:68-80`, migration `20260604120000_AddAuthoritativeSessionTimerState.cs`), ticked by `AuthoritativeSessionTimerWorker` (`:51-81`). **HU-22 deliberately deferred the treasure-hunt countdown** (its "OD-1: no treasure-hunt countdown … a per-substage authored duration would need an ADR + a new snapshot field") — **DES-93 is that deferred follow-up.** `GetAuthoritativeSessionTimerSnapshot` currently hard-wires to the question window (`LiveSession.cs:297-302`), returning `Zero`/`Expired` for a treasure-hunt substage — the exact bug.
- **DES-31 (HU-23) — Done.** The live team board reads the timer via `ProjectParticipantTeamBoard`/`ProjectOperatorSessionPanel` → `GetAuthoritativeSessionTimerSnapshot` (`LiveSession.cs:520,540`). No board change: it renders whatever the snapshot carries; the `00:00 / Expired` is purely the `Zero` snapshot. Once DES-93 makes the branch return a real countdown, the board counts down unchanged.

**Landed, untouched by this slice:** DES-22 (HU-15) runtime snapshot; DES-24 (HU-17) no `SessionMode`; DES-76 (HU-21A) state machine + `Enter` hooks. Consumed as-is, not modified beyond adding the treasure-hunt timer hook into the existing `Active`/`Paused` `Enter` overrides.

**Coverage:** session-operations-service carries the HU-15/17/21A/22/23/34/36A baseline; measure the real service percentage against the ADR-0005 repo gate at X.4 — do not assume a carried-forward number.

## What this HU adds

| Concern | New work |
|---|---|
| TreasureHunt substage timer (domain) | A `_substageTimer*` window on `LiveSession`, **mirroring** `_questionTimer*`: seeded when a `TreasureHunt` substage becomes active (from the session-level `MaximumTime`), advancing while `Active`, frozen on `Paused`. |
| Authoritative branch (domain) | `GetAuthoritativeSessionTimerSnapshot` **branches on the active substage's `PlayMode`**: `TreasureHunt` → the substage-timer window; `Trivia` → the existing `GetActiveQuestionTimerSnapshot` (unchanged). |
| `State`-owned advance/freeze (domain) | Parallel `GetSubstageTimerSnapshot`/`MarkSubstageTimerExpiredIfElapsed`/`IsSubstageTimerAdvancing` on `ILiveSessionState`/`LiveSessionStateBase`/`ActiveLiveSessionState`, mirroring the question-timer state methods. |
| Timer DTO + queries (app) | **Verify only** — `SessionTimerSnapshotDtoFactory` and the two timer queries map the generic `AuthoritativeSessionTimerSnapshot` unchanged (`ActiveQuestion` is null for a treasure-hunt substage). Add tests. |
| Persistence + worker (infra) | EF config + **migration adding** `substage_timer_*` columns; `ListActiveTimersAsync` also selects sessions with an advancing substage timer; the worker broadcasts the treasure-hunt remaining but is **report-only on expiry** (no `CloseAndAdvanceAsync`). |
| Endpoints + broadcast (api) | **No new endpoints** — the two timer GETs + the transition `Timer` field now return the treasure-hunt remaining for a treasure-hunt substage; `SessionTimerUpdated` broadcast verified. |
| Frontend | **None** — HU-23's board already renders the timer; it starts counting when the snapshot is non-zero. (No frontend slice — see Step 9.) |

## Touched surfaces

- `backend/services/session-operations-service/` — domain (timer members + branch + `State` hooks on `LiveSession` / `SessionStates/*`), application (timer-query tests only), infrastructure (EF config + a **migration adding** `substage_timer_*` columns + `ListActiveTimersAsync` predicate + worker report-only tick), api (verify the two timer GETs + hub broadcast + coverage).
- `frontend/` — **not touched.** HU-23's board consumes the snapshot as-is.
- API/runtime contract boundary: `SessionTimerSnapshotDto` gains **no new field** — for a treasure-hunt substage its existing `RemainingSeconds`/`TotalSeconds`/`Status` now carry a real advancing countdown instead of `0`/`Expired`. This is a **behavioural** change (a previously-`Expired` chip now counts down), not a shape change — call it out for the frontend.

## Committed phases

| Commit | Phase | Description |
|---|---|---|
| — | — | No commits yet |

## Known quirks / gotchas

- **Mirror `_questionTimer*`, do not re-invent.** The whole freeze/resume/expire machinery already exists for trivia (`LiveSession.cs:853-948`, state methods on `ILiveSessionState.cs:15-19` / `LiveSessionStateBase.cs:18-31` / `ActiveLiveSessionState.cs:22-35`). The treasure-hunt timer is the **same shape** with a different seed trigger (substage activation, not `ActivateQuestion`) and a different duration source (session `MaximumTime`, not question `TimeLimitSeconds`).
- **Seed on FIRST substage activation, not on every `Active` enter.** `EnterActiveSessionState` runs on **every** transition to `Active` including pause→resume, and sets `ActiveSubstageId ??= first substage` (`LiveSession.cs:816-824`); the `??=` guards resume. Seeding must key off the pointer being **freshly set** (or the substage timer being unseeded for the current substage) — otherwise resume rewinds the countdown. On `CompleteActiveSubstageAndAdvance` (`:782`), seed the newly-pointed substage if it is `TreasureHunt`. Resume (not reseed) on `Active` `Enter`; freeze on `Paused`/`Finished`/`Cancelled` `Enter` — parity with trivia.
- **`AuthoritativeSessionTimerSnapshot` is generic — reuse it.** It carries `TotalDuration`/`RemainingDuration`/`IsAdvancing`/`AdvancingSince`/`ExpiredAt` and enforces `0 ≤ remaining ≤ total` (`AuthoritativeSessionTimerSnapshot.cs:13-21`). The treasure-hunt snapshot uses the same VO — no new snapshot type.
- **Report-only on expiry.** Trivia expiry auto-advances (`worker → CloseAndAdvanceAsync`, `AuthoritativeSessionTimerWorker.cs:76-79`). A treasure-hunt substage advances by **target resolution** (canon; HU-29–32), **not** by timer — so on treasure-hunt timer expiry the worker **broadcasts `Expired` and stops**; it must **not** call `CloseAndAdvanceAsync`. Gate the advance branch on the active substage being trivia (`ActiveQuestionIndex != null`).
- **Duration source is session-level, by scope.** See the State block's ⚠️ deviation. `MaximumTime.Minutes` (`MaximumTime.cs`) → `TimeSpan.FromMinutes(...)`. This is an interim source; the per-substage authored field is the deferred `mission-design-service` follow-up. Note it wherever AC#1's literal wording is checked.
- **Namespace is `umbral_backend.*`** across all layers; domain tests in `tests/UnitTests/`, application tests in `tests/Application.UnitTests/`, integration in `tests/IntegrationTests/`. Mirror the HU-22 test layout.
- **`des-93` branch, not folded into HU-23's PR** (ticket instruction — distinct concern/service).

## Per-phase derivation — authoritative for implementation

> Primary source for the phase subagent (per `backend-agent.md` "Read first") — the subagent builds
> from these blocks and does not re-read the full canon.
> Mode = **feature flow (additive)**: canon precedence per `backend-agent.md`. No keep/delete/decide
> (nothing is torn out — every change is additive or a verified pass-through).
> Canon: authoritative timer keyed off the active `SubstagePlayMode` (`CONTEXT.md` §`TriviaQuestionTimer` :125-138; `canon-realignment-after-mission-runtime-rewrite.md:24,28-29`); pause freezes + resumes (`CONTEXT.md:52`); treasure-hunt advances by **target resolution, not a timer** (`canon-realignment…:29`). Pattern: `State` (`required_patterns_matrix.md:41,110`); transport SignalR (`:57,110`).
> Grounding is the **current post-HU-22 code** (re-verified 2026-07-13, per the ticket's "re-verificar contra el código").

### Phase X.1 — Domain
**Derive** (`LiveSession.cs`; `CONTEXT.md:52,125-138`; `canon-realignment…:24,29`):
- Add a **`TreasureHunt` substage timer** on `LiveSession`, mirroring the `_questionTimer*` window (`:16-19,853-948`): fields `_substageTimerTotalDuration`, `_substageTimerRemainingDuration`, `_substageTimerAdvancingSince`, `_substageTimerExpiredAt`; methods `GetAdvancingSubstageTimerSnapshot` / `GetFrozenSubstageTimerSnapshot` / `MarkAdvancingSubstageTimerExpiredIfElapsed` / `HasAdvancingSubstageTimer` / `ResumeSubstageTimer` / `FreezeSubstageTimer` / `CalculateAdvancingSubstageTimerRemaining` — 1:1 with the `…QuestionTimer…` equivalents. Reuse `AuthoritativeSessionTimerSnapshot` (`:1-71`, generic).
- **Seed** the substage timer when a `TreasureHunt` substage **becomes active** (anchor = substage activation, per the design decision): in `EnterActiveSessionState` when `ActiveSubstageId` is freshly set to a `TreasureHunt` substage (`:816-824`), and in `CompleteActiveSubstageAndAdvance` when the pointer moves to a `TreasureHunt` substage (`:782`). Seed = total/remaining `= TimeSpan.FromMinutes(MaximumTime.Minutes)` (session-level source — see deviation note), `_substageTimerAdvancingSince = occurredAt`, `_substageTimerExpiredAt = null` — mirror `ActivateQuestion` (`:304-321`). **Seed once per substage activation**; resume (not reseed) on pause→resume.
- **Branch** the authoritative accessor (`GetAuthoritativeSessionTimerSnapshot`, `:297-302`): resolve the active substage (`GetOrderedSubstages()` + `ActiveSubstageId`, cf. `:607-625`); if `PlayMode == SubstagePlayMode.TreasureHunt` → return the substage-timer snapshot via `LiveSessionStateFactory.For(State).GetSubstageTimerSnapshot(this, observedAt)`; else → `GetActiveQuestionTimerSnapshot(observedAt)` (unchanged).
- **`State` hooks:** add `GetSubstageTimerSnapshot` / `MarkSubstageTimerExpiredIfElapsed` / `IsSubstageTimerAdvancing` to `ILiveSessionState` + `LiveSessionStateBase` (base = frozen) + `ActiveLiveSessionState` (Active = advancing), mirroring the `…QuestionTimer…` state methods. Wire substage **resume** into `ActiveLiveSessionState.Enter` and substage **freeze** into `PausedLiveSessionState.Enter` / `Finished` / `Cancelled` `Enter`, alongside the existing `Enter…QuestionTimerState` calls.

**Target files** (create | edit — file to mirror):
- edit `src/Domain/Entities/LiveSession.cs` — add `_substageTimer*` members + methods (mirror the `_questionTimer*` block `:808-948`); seed on substage activation (mirror `ActivateQuestion` `:304-321`); branch `GetAuthoritativeSessionTimerSnapshot` `:297-302`
- edit `src/Domain/Services/SessionStates/ILiveSessionState.cs`, `LiveSessionStateBase.cs`, `ActiveLiveSessionState.cs` — add the three `…SubstageTimer…` state methods (mirror the `…QuestionTimer…` ones `ILiveSessionState.cs:15-19`, `LiveSessionStateBase.cs:18-31`, `ActiveLiveSessionState.cs:22-35`)
- edit `src/Domain/Services/SessionStates/ActiveLiveSessionState.cs` (`Enter`) + `PausedLiveSessionState.cs` (`Enter`) — resume/freeze the substage timer alongside the question timer
- keep (reuse, no change) `src/Domain/ValueObjects/AuthoritativeSessionTimerSnapshot.cs`, `SubstageSnapshot.cs` (`PlayMode`)
- create/edit `tests/UnitTests/Domain/Entities/LiveSessionTests.cs` (+ a treasure-hunt-timer-focused test) — assertions in the Gate

**Pattern this phase owns:** `State` (mandated) — advancing-vs-frozen for the substage timer decided by the per-`SessionState` type via `LiveSessionStateFactory`, mirroring the question timer; **not** an ad-hoc `if (State == Active)` in `LiveSession`.
**Gate:** Domain build passes; a unit test locks: an **active `TreasureHunt` substage** → `GetAuthoritativeSessionTimerSnapshot` returns an **advancing** countdown seeded from the session `MaximumTime` (`RemainingDuration > 0`, `IsExpired == false`) — **not** `Zero`/`Expired`; `Paused` **freezes** it and `Active` **resumes** it at the frozen remainder; an active **`Trivia`** substage still returns the question window (unchanged); timer expiry marks the substage timer expired but raises **no advancement event**. **`State` pattern verified — advancing/frozen decided by per-state types, not ad-hoc conditionals.**

### Phase X.2 — Application
**Derive** (`GetParticipantSessionTimerSnapshotQueryHandler.cs:39-41`; `GetOperatorSessionTimerSnapshotQueryHandler.cs`; `SessionTimerSnapshotDtoFactory.cs`):
- **No production change.** Both timer query handlers already call `GetAuthoritativeSessionTimerSnapshot` and map via `SessionTimerSnapshotDtoFactory.Create` (`:39-41`). The factory maps the generic snapshot and returns `ActiveQuestion = null` when `ActiveQuestionIndex is null` (`SessionTimerSnapshotDtoFactory.cs`) — correct for a treasure-hunt substage. So a treasure-hunt snapshot flows through **unchanged**.
- Add tests proving the DTO for a treasure-hunt substage carries the advancing remaining, `IsAdvancing == true`, `Status` running (not `Expired`), and `ActiveQuestion == null`; and that a trivia substage still returns the question window with `ActiveQuestion` populated.

**Target files** (create | edit — file to mirror):
- create/edit `tests/Application.UnitTests/…` timer-query / DTO-factory tests — mirror the existing HU-22 timer-query tests
- verify (no edit expected) `src/Application/Sessions/Queries/GetParticipantSessionTimerSnapshot/GetParticipantSessionTimerSnapshotQueryHandler.cs`, `GetOperatorSessionTimerSnapshot/GetOperatorSessionTimerSnapshotQueryHandler.cs`, `src/Application/Sessions/Common/SessionTimerSnapshotDtoFactory.cs`

**Pattern this phase owns:** none new (`State` realized in X.1; SignalR in X.4).
**Gate:** Application build passes; a test proves the operator + participant timer DTOs return the treasure-hunt advancing remaining (`ActiveQuestion == null`, status not `Expired`) for a treasure-hunt substage, and still the question window for a trivia substage; operator / participant-membership authorization preserved. If a production edit turns out to be needed, note why (the pass-through was expected to be zero-change).

### Phase X.3 — Infrastructure
**Derive** (`LiveSessionConfiguration.cs:68-80`; `LiveSessionRepository.cs:117` `ListActiveTimersAsync`; `AuthoritativeSessionTimerWorker.cs:51-81`; migration `20260604120000_AddAuthoritativeSessionTimerState.cs`; ADR-0008 shared Postgres testcontainer):
- **Persist** the four `_substageTimer*` fields: map them in `LiveSessionConfiguration` as `substage_timer_total_duration` / `substage_timer_remaining_duration` / `substage_timer_advancing_since` / `substage_timer_expired_at` — mirror the `_questionTimer*` mappings (`:68-80`). Add a **migration** creating those columns — mirror `20260604120000_AddAuthoritativeSessionTimerState.cs` (additive `AddColumn`). Update the model snapshot (cf. `…ModelSnapshot.cs:268-280`).
- **`ListActiveTimersAsync`** (`:117`): extend the predicate so the worker also ticks treasure-hunt sessions — `State == Active && EF.Property<DateTimeOffset?>(session, "_substageTimerAdvancingSince") != null && EF.Property<DateTimeOffset?>(session, "_substageTimerExpiredAt") == null`, OR-ed with the existing advancing-question predicate.
- **Worker** (`AuthoritativeSessionTimerWorker.TickAsync`, `:51-81`): broadcast the **authoritative** snapshot (`GetAuthoritativeSessionTimerSnapshot`) so a treasure-hunt session pushes its remaining; **gate the `CloseAndAdvanceAsync` auto-advance on the active substage being trivia** (`ActiveQuestionIndex != null`) — a treasure-hunt substage is **report-only on expiry** and must not advance.

**Target files** (create | edit — file to mirror):
- edit `src/Infrastructure/Persistence/Configurations/LiveSessionConfiguration.cs` — add the four `substage_timer_*` mappings (mirror `:68-80`)
- create `src/Infrastructure/Migrations/<timestamp>_AddTreasureHuntSubstageTimerColumns.cs` (+ `.Designer.cs`) — additive column add; mirror `20260604120000_AddAuthoritativeSessionTimerState.cs`; regenerate `…ModelSnapshot.cs`
- edit `src/Infrastructure/Persistence/Repositories/LiveSessionRepository.cs` (`ListActiveTimersAsync` `:117`) — OR-in the advancing-substage-timer predicate
- edit `src/Infrastructure/Realtime/AuthoritativeSessionTimerWorker.cs` (`:51-81`) — broadcast the authoritative snapshot; gate auto-advance on trivia (report-only for treasure-hunt)
- create/edit `tests/IntegrationTests/Persistence/LiveSessionRepositoryIntegrationTests.cs` (+ a worker test) — Gate assertions

**Pattern this phase owns:** none.
**Gate:** Infrastructure build passes; a migration **ADDS** the `substage_timer_*` columns (assert via model snapshot); `ListActiveTimersAsync` returns sessions with an advancing **substage** timer (as well as question timers); a repository integration test round-trips a session carrying a treasure-hunt substage-timer state; the worker **broadcasts** the treasure-hunt remaining but **does not** call `CloseAndAdvanceAsync` on treasure-hunt expiry (trivia close/advance path unchanged).

### Phase X.4 — Api
**Derive** (`Api/Controllers/SessionsController.cs` two timer GETs + `PATCH …/state` `Timer`; `Api/Hubs/*`; ADR-0001/0002 gateway auth; ADR-0005 coverage):
- **No new endpoints.** `GET /api/sessions/{liveSessionId}/timer` (operator) and `GET /api/sessions/{liveSessionId}/participants/timer` (participant) now return `SessionTimerSnapshotDto` with a **real advancing remaining** when the active substage is `TreasureHunt` (was `0`/`Expired`). The transition-result `Timer` field carries the same. A live `SessionTimerUpdated` broadcast reaches the `live-session:{id}` group. Authorization unchanged.

**Target files** (create | edit — file to mirror):
- verify (no route reshape) `src/Api/Controllers/SessionsController.cs` (two timer GETs + `Timer` field), `src/Api/Hubs/SessionsHub.cs`, `src/Api/Hubs/SessionStateBroadcaster.cs`
- create/edit `tests/IntegrationTests/Api/…` — timer endpoint tests (treasure-hunt substage → advancing remaining, not `Expired`; auth enforced) + a hub test (`SessionTimerUpdated` carrying the treasure-hunt remaining reaches `live-session:{id}`)

**Pattern this phase owns:** none new — the timer GETs inherit the standard operator / participant `AuthorizationBehaviour` guard (ADR-0001/0002); DES-93 is not in the applies-where `Proxy` set → no new gate. **SignalR transport gate verified here.**
**Gate:** endpoint integration tests (operator + participant timer GET → treasure-hunt advancing remaining, not `Expired`; auth enforced) + a hub test (treasure-hunt `SessionTimerUpdated` broadcast to `live-session:{id}`); **ADR-0005 coverage** (service ≥ repo gate).

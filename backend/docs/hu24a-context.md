# HU-24A Context — Panel del operador en tiempo real de estado y progreso

> Paste this section into any agent session that needs context for HU-24A (DES-32).
> Last updated: 2026-07-12 | Branch: `feature/hu-24a-operator-live-session-panel`

## State

- DES-32 (HU-24A): **Todo**, labels: `svc:session-operations-service`, `Feature`, `ready-for-agent`. Both required labels present.
- **Resolved mode: feature flow.** The service went through the mission-runtime realignment cycle, but DES-32 is **not** in the realignment map's superseded ("old") column, and it carries **no** `canon-realign` / `needs-rebuild` label. It is forward, un-superseded work. Its Linear blockers were re-pointed off the canceled DES-28/DES-30 onto the rebuilds DES-76/DES-77 (`canon-realignment-after-mission-runtime-rewrite.md:197-201`); anchor only on the rebuild ids, never the canceled originals.
- **Superseded predecessors dropped / substituted** (realignment map `:66-69,180-198`): DES-28 (HU-21A cycle-1) → **DES-76**; DES-30 (HU-22 cycle-1) → **DES-77**; DES-44 (HU-33A cycle-1) → **DES-78**; DES-23 (HU-16) → **DES-75**; DES-47 (HU-34B) → merged into **DES-46**. Never cite a canceled original as a predecessor.
- Predecessor DES ids (build-on, Done/merged): **DES-31 (HU-23)** and **DES-49 (HU-36A)** — PRIMARY (the participant board projection and the operator-guarded projection this HU generalizes); **DES-76 (HU-21A)** session state + broadcast; **DES-27 (HU-20)** operator assigned-session reads + ownership seam; **DES-26 (HU-19)** `AssignedOperatorUserId`; **DES-77 (HU-22)** authoritative timer; **DES-78 (HU-33A)** active-substage pointer + `SubstageAdvancedEvent`; **DES-86** per-target score; **DES-22/DES-24 (HU-15/HU-17)** session + immutable snapshot, no `SessionMode`.
- Landed, untouched by this HU: **DES-25 (HU-18)** team association, **DES-46 (HU-34)** trivia answers, **DES-45 (HU-33B)** round close/RabbitMQ, **DES-11/DES-12 (HU-07A/07B)** participant admission/reconnect.
- PRD DES id: **DES-70** → `backend/docs/prd/DES-70-primera-implementacion-de-session-operations-service-hu-15-a-hu-36.md` (local file is authoritative; never re-fetch PRD scope from Linear). US-19 (`:111-112`) is HU-24A: *"As an Operator, I want to monitor session state and progress in real time."* US-20 (`:113-114`, events/evidence/ranking) is the **sibling HU-24B (DES-33)** — out of scope here.
- Blocked by in Linear: **DES-76 (HU-21A)** Done, **DES-27 (HU-20)** Done. No same-service In Progress predecessor → branch base **`develop`**.
- Branch: `feature/hu-24a-operator-live-session-panel`, base **`develop`**.

## Required design patterns

- `Proxy` **(mandated — `required_patterns_matrix.md` HU-24 row, lines 43 and 112)**
  - Why: line 112 — *"Operator real-time panel is a **guarded projection** — only assigned/authorized sessions, blocks unauthorized actions."* `CONTEXT.md` §Proxy (`:225-227`) names *"operator dashboards, protected panels"* as the exact Proxy responsibility. This is a **mandated** `Proxy` (HU-24 sits in the matrix `Proxy` pattern column, **not** the `—`/applies-where set — the sibling HU-23 is `—`; HU-24 is not), so it gets an explicit scope bullet **and** a phase gate line.
  - Phase owner: **X.2 Application** (application-slice resolver Proxy) **and X.4 Api** (endpoint authorization policy + hub-join ownership guard).
  - Concrete obligation: the panel read passes through the **existing** resource-ownership resolver Proxy `ISessionAdministrationAccessResolver` / `SessionAdministrationAuthorizationProxy` (ADR-0009 + ADR-0012 §"resource-ownership guard") — Administrator sees all, Operator only where `LiveSession.AssignedOperatorUserId == actor.UserId`, else `ForbiddenAccessException`. **No ad-hoc role/owner `if` checks** in the handler, controller, or hub. The coarse role gate stays `[Authorize(Policy = AuthorizationPolicies.Operator)]` via `AuthorizationBehaviour`. Do **not** invent a new proxy — reuse `GetAuthorizedSessionAsync` (mirror `GetOperatorTriviaAnsweredMonitorQueryHandler` / `GetOperatorSessionTimerSnapshotQueryHandler`).
- Transport: **SignalR / WebSockets** — hard transport gate (`required_patterns_matrix.md:57,112`). The operator panel must update without manual reload over the **operator-only** group `live-session-operators:{id}` (`SignalRTeamAnsweredBroadcaster.BuildOperatorGroup`). Operators join it — and `live-session:{id}` — via `SessionsHub.JoinLiveSessionAsOperatorAsync` (ownership-guarded at join). HU-24A adds the **initial-snapshot query** (so an operator connecting/refreshing sees the full panel) plus an **operator-panel push** on the transitions that change the panel; the existing HU-21A `SessionStateChanged` broadcast already reaches operators on `live-session:{id}`.

## What predecessors have already landed

HU-24A is a **CQRS read surface over the one aggregate `LiveSession`** (PRD `:222` lists HU-24 as a CQRS operational read). Nearly all runtime + transport it needs already landed; it adds a projection + operator-guarded read slice + an operator-scoped live push, not new runtime state.

**DES-31 / HU-23 — participant team board projection + team-board broadcast (build-on, PRIMARY)**
- Domain: `LiveSession.ProjectParticipantTeamBoard(Guid teamId, DateTimeOffset observedAt)` (`LiveSession.cs:517`) resolves score, timer snapshot, active-substage context, and visible clues for **one** team. HU-24A generalizes this to **all** teams plus session state. Reuse its private helpers `BuildActiveSubstageContext` / `BuildTreasureHuntContext` / `BuildTriviaContext` / `CollectVisibleClues` and value objects `ParticipantTeamBoardSnapshot` / `ActiveSubstageContext` / `VisibleClue`.
- Progress model: treasure-hunt progress = `resolvedTargets / totalActiveTargets` over the active substage's active `TargetSnapshot`s; target-resolution persistence does **not** exist yet (HU-31 owns it) so `resolvedTargets` is currently `0` — expose total active targets, never infer from `CurrentClueNodeId` / `ReleasedClueCount`.
- Transport precedent: `Application/Sessions/EventHandlers/BroadcastTeamBoardNotificationHandler.cs` re-projects each team's board on `SessionStateChangedEvent` **and** `SubstageAdvancedEvent` and pushes via `ITeamBoardBroadcaster` → `SignalRTeamBoardBroadcaster` (team group). HU-24A mirrors this handler + broadcaster **operator-scoped** (the whole panel to `live-session-operators:{id}`), not team-scoped.

**DES-49 / HU-36A — operator-guarded projection Proxy + operator SignalR group (build-on, PRIMARY)**
- The **query to mirror**: `Application/Sessions/Queries/GetOperatorTriviaAnsweredMonitor/` (`Query` + `Handler`), `[Authorize(Roles="Operator")]`, injects `ISessionAdministrationAccessResolver`, calls `GetAuthorizedSessionAsync`, projects via a domain read method, maps via a static DTO factory in `Application/Sessions/Common/`, DTO in `Application/Dtos/Sessions/`. HU-24A copies this shape exactly, swapping the projection.
- The **ownership Proxy** (ADR-0009): `ISessionAdministrationAccessResolver` + `SessionAdministrationAuthorizationProxy` — Administrator all / Operator `AssignedOperatorUserId == actor.UserId` else `ForbiddenAccessException`; actor via `IAuthenticatedActorProfileAccessClient`. `GetAuthorizedSessionAsync` uses `GetByIdAsync` (full hydrate); `GetAuthorizedTimerSessionAsync` uses the timer-scoped read.
- The **operator group**: `live-session-operators:{id}` (`SignalRTeamAnsweredBroadcaster.BuildOperatorGroup`), joined in `SessionsHub.JoinLiveSessionAsOperatorAsync` after `EnsureOperatorCaller()` + the ownership resolver — the reused hub-join guard that keeps participants out.

**DES-76 / HU-21A — session lifecycle + state broadcast (build-on)**
- The canonical `Scheduled → Preparing → Active → Paused → Finished → Cancelled` `State`-pattern machine, `LiveSession.State`, `SessionStateChangedEvent`, and its SignalR broadcast (`SessionStateChangedNotificationHandler` → `ISessionStateBroadcaster` → `live-session:{id}`) are landed. HU-24A **reads** `State` into the panel and **reuses** the existing state broadcast as one of the two panel-refresh triggers; it never adds or mutates a transition.

**DES-27 / HU-20 + DES-26 / HU-19 — operator ownership seam (build-on)**
- HU-19 added `LiveSession.AssignedOperatorUserId` (the field the Proxy checks). HU-20 landed the operator assigned-session reads guarded by the ownership resolver. HU-24A's "only assigned/authorized sessions" AC **is** that Proxy — reuse it, add no new ownership logic.

**DES-77 / HU-22 — authoritative timer (build-on, light).** `LiveSession.GetAuthoritativeSessionTimerSnapshot(observedAt)` + `SessionTimerSnapshotDto`/`SessionTimerSnapshotDtoFactory` already exist; the panel embeds/reuses the timer snapshot rather than inventing one.

**DES-78 / HU-33A — active-substage pointer (build-on, light).** `ActiveSubstageId` / `ActiveQuestionIndex` identify active content; `SubstageAdvancedEvent` is the progress-change trigger the panel push subscribes to alongside `SessionStateChangedEvent`.

**DES-86 — per-target score refactor (build-on, mirror only).** `TargetSnapshot.Score` is the per-target `ScoreValue`; `SubstageSnapshot.WinnerScore` is gone. The panel's score/progress wording must not reintroduce winner-takes-all substage score.

**DES-22/DES-24 (HU-15/HU-17) — foundation (build-on, light).** `LiveSession` owns the immutable `MissionRuntimeSnapshot`; there is no session-level `SessionMode`. The panel reads the frozen snapshot; it never re-derives runtime content from `MissionDesign`.

**Landed, untouched by this HU:** DES-25 (HU-18) team association, DES-46 (HU-34) trivia answers + RabbitMQ, DES-45 (HU-33B) round close, DES-11/DES-12 (HU-07A/07B) participant admission/reconnect. HU-24A reads none of their state beyond the aggregate the projection already loads.

**Superseded / not predecessors (do NOT anchor on):** DES-23, DES-28, DES-30, DES-44, DES-47.

**Coverage:** measure the real `session-operations-service` percentage against `backend/docs/adr/0005-coverlet-msbuild-for-aggregate-coverage.md` at X.4; do not assume a carried-forward value.

## What this HU adds

| Concern | New work |
|---|---|
| Operator session panel read | A domain read over `LiveSession` returning **session state** + **every team's progress** in one projection (the all-teams operator analog of HU-23's single-team board). |
| Session-state visibility | The panel carries the current `SessionState` (from HU-21A) so the operator sees lifecycle without reload. |
| All-teams progress rollup | Per team: current/session-owned score (or 0), active-substage target progress (`resolvedTargets / totalActiveTargets`) or active-question context, and the authoritative timer. Reuses HU-23's helpers; no new progress model. |
| Operator-guarded read | New CQRS query `GetOperatorSessionPanel`, gated by the existing ownership resolver Proxy — operator sees only their assigned session, `ForbiddenAccessException` otherwise. |
| Live without reload | Initial-snapshot query on connect/refresh; a new operator-scoped push re-projects the panel on `SessionStateChangedEvent` + `SubstageAdvancedEvent` to `live-session-operators:{id}`. |
| Blocks unauthorized access | AC #4 is realized by the Proxy: a non-owning operator's panel read and hub subscription are rejected (403 / `ForbiddenAccessException`); the panel exposes no session it does not own. |
| Read verification | No new persisted state or migration — the read rides existing session/team/snapshot persistence; X.3 verifies the aggregate read hydrates teams + active substage/targets. |
| Backend contract | New operator-only `GET /api/sessions/{liveSessionId}/operator-panel` returning session state + per-team progress, plus an operator-group SignalR panel-update payload. |
| Frontend flow | Operator live session panel (**web**, `@frontend/`) subscribing to the snapshot + the operator-group panel push. |

## Touched surfaces

- `backend/services/session-operations-service` (Domain read method + value objects, Application query slice + DTO/factory + broadcaster port, Api endpoint + operator-panel broadcaster + notification handler)
- `frontend/` operator live session panel (web — operator-primary surface)
- `backend/frontend` API contract boundary: `GET /api/sessions/{liveSessionId}/operator-panel` response shape + the operator-group SignalR panel-update payload
- No migration; no RabbitMQ; SignalR reuses the operator group (`live-session-operators:{id}`) — a new operator-panel broadcaster method, not a new group

## Committed phases

| Commit | Phase | Description |
|---|---|---|
| — | X.1 Domain | (no commits yet) |
| — | X.2 Application | (no commits yet) |
| — | X.3 Infrastructure | (no commits yet) |
| — | X.4 Api | (no commits yet) |

## Known quirks / gotchas

- **Proxy, not inline checks.** Ownership goes through `ISessionAdministrationAccessResolver.GetAuthorizedSessionAsync` (mirror `GetOperatorTriviaAnsweredMonitorQueryHandler`); a bare `if (role == Operator …)` in the handler/controller/hub is the ceremony ADR-0012 forbids. This is the **mandated** Proxy — it is a design obligation beyond the coarse `[Authorize(Operator)]` gate the endpoint inherits (ADR-0001/0002).
- **All teams, no team-scope.** Unlike HU-23 (`IRuntimeParticipationGuard`, one team), the operator panel spans **every** `Team` in the session. Do not import the participant team-scope guard; the operator scope is session-ownership, not team-membership.
- **Reuse HU-23's progress helpers in place; do not fork or re-expose them.** `ProjectOperatorSessionPanel` lives on the same `LiveSession` class, so it calls the `private` `BuildActiveSubstageContext`/`BuildTreasureHuntContext`/`BuildTriviaContext` directly — **no** visibility change and **no** extraction into a service (verified: they carry no per-team state, so nothing needs sharing beyond the class). Duplicating the target-vs-clue logic instead is the canon-debt risk to avoid.
- **Progress is target-based, not clue-based.** `resolvedTargets` is `0` until HU-31 lands target resolution; expose `totalActiveTargets`. Never infer progress from `CurrentClueNodeId` / `ReleasedClueCount` (`canon-realignment-after-mission-runtime-rewrite.md:29`; `CONTEXT.md` §TargetProgression `:117-118`).
- **Score is session-owned or zero.** Authoritative score ledger/ranking is `ScoringMonitoring` (HU-37/HU-39). Show `Team.CurrentScore ?? 0`; do not compute a ledger, ranking, penalties, or a `SessionTeamWinner` here. Ranking/events/evidence are HU-24B (DES-33).
- **Reuse the operator group; add a broadcaster method, not a group.** Push the panel to the existing `live-session-operators:{id}` group. The operator already joins it (and `live-session:{id}`) in `JoinLiveSessionAsOperatorAsync`; do not create a second operator group or send the panel to `live-session:{id}` (that leaks operator-scoped rollups to participants).
- **State changes already reach operators.** HU-21A broadcasts `SessionStateChanged` to `live-session:{id}`, which operators join — so raw state is already live. The new operator-panel push exists to deliver the **richer all-teams rollup** on the same triggers (`SessionStateChangedEvent` + `SubstageAdvancedEvent`); the re-projection handler re-loads read-only and never mutates state (no re-entrancy).
- **Blocks-actions boundary.** AC #4 ("bloquea acciones sobre sesiones no autorizadas") is satisfied for this read surface by the Proxy denying non-owned panel reads/subscriptions. HU-24A does **not** re-guard the existing mutation endpoints (`PATCH …/state`, `POST …/teams`) — those are their own HUs; do not expand scope into re-authorizing them.
- **Namespace is `umbral_backend.*`** across all session-ops layers; domain tests live in `tests/UnitTests/`, application tests in `tests/Application.UnitTests/`, integration/API tests in `tests/IntegrationTests/`. Match existing files.

## Per-phase derivation — authoritative for implementation

> Primary source for the phase subagent (per `backend-agent.md` "Read first"). Do not re-read the full canon; open a cited section only to fill a gap this block leaves open.
> Canon: `required_patterns_matrix.md:43,57,112` (mandated `Proxy` + SignalR for HU-24); `CONTEXT.md` §Proxy `:225-227` (operator dashboards/protected panels), §TargetProgression `:117-127`, §Runtime Authority `:195-196`; `bd_umbral_entity_spec.md` §LiveSession `:274-326`, §Team `:362-401`, §MissionRuntimeSnapshot `:327-361`; `ddd_solution_model.md` §SessionOperations (Proxy responsibility, read surfaces); ADR-0009 (operator ownership), ADR-0012 (Proxy placement); PRD DES-70 US-19 `:111-112`, `:220-223`; predecessor context `hu23-context.md` / `hu36a-context.md`. Structure: `backend/structure.md` (ADR-0011 vertical slice — no `Handlers/`/`DTOs/` buckets).

### Phase X.1 — Domain
**Derive** (`bd_umbral_entity_spec.md` §LiveSession `:274-326`, §Team `:362-401`; `CONTEXT.md` §TargetProgression `:117-127`; `hu23-context.md` X.1; `LiveSession.cs:517-619`):
- Add `LiveSession.ProjectOperatorSessionPanel(DateTimeOffset observedAt)` returning an `OperatorSessionPanelSnapshot`: the session identity + current `State` + the authoritative session timer snapshot + an ordered `IReadOnlyList<OperatorTeamProgress>` — one per `Team`, ordered by `TeamCode` (mirror the ordering in `ProjectActiveQuestionAnsweredStatus()`).
- `OperatorTeamProgress` per team = `(TeamId, TeamCode, DisplayName, CurrentScore ?? 0, ActiveSubstageContext?)` where the active-substage context is HU-23's `BuildActiveSubstageContext(...)` result (treasure-hunt `resolvedTargets`/`totalActiveTargets` or trivia active-question context). **Reuse mechanism:** `ProjectOperatorSessionPanel` is a new method on the **same** `LiveSession` class, so it calls the existing `private` helpers (`BuildActiveSubstageContext`, `BuildTreasureHuntContext`, `BuildTriviaContext`, `GetAuthoritativeSessionTimerSnapshot`) **directly** — intra-class access. Do **not** change their visibility (`internal`/`public`) and do **not** extract them into a separate service; there is no obstacle.
- **The active-substage context is session-scoped, not per-team, today.** `BuildActiveSubstageContext(Team team)` does not use its `team` argument (`LiveSession.cs:534-551`); `resolvedTargets` is hardcoded `0` (`:564`, HU-31 owns per-team resolution) and the trivia question index is session-global — so the context is **identical for every team**. Build it **once** and share the reference across the per-team list; do **not** invoke the helper once per team for an identical result. The per-team list shape is chosen for **HU-31 forward-compatibility** (values diverge when target resolution lands), not because per-team progress diverges now — state this so a reviewer does not read false per-team divergence into it. The only genuinely per-team values today are `CurrentScore ?? 0` and team identity.
- **Order the per-team list explicitly by `TeamCode.Value` (`StringComparer.Ordinal`)** — mirror `ProjectActiveQuestionAnsweredStatus()` (`:365`). `Teams` (`:108`) is raw insertion order with no ordering helper; do not rely on it.
- Score = `Team.CurrentScore ?? 0` (session-owned). No score ledger, ranking, penalties, or winner. Progress is target-resolution-based; `resolvedTargets` is `0` until HU-31 (do not infer from clues).
- The panel is valid in any non-pre-start lifecycle state the operator can observe (`Active`/`Paused` primary; terminal states readable as last-known) — it does not throw on state like the trivia monitor does; it reports `State` and whatever active context exists (or none).

**Target files** (create | edit — file to mirror):
- edit `src/Domain/Entities/LiveSession.cs` — add `ProjectOperatorSessionPanel(DateTimeOffset observedAt)` beside `ProjectParticipantTeamBoard` (`:517`); reuse `BuildActiveSubstageContext`/`GetAuthoritativeSessionTimerSnapshot`
- create `src/Domain/ValueObjects/OperatorSessionPanelSnapshot.cs` — mirror `src/Domain/ValueObjects/ParticipantTeamBoardSnapshot.cs`
- create `src/Domain/ValueObjects/OperatorTeamProgress.cs` — mirror `src/Domain/ValueObjects/TeamAnsweredStatus.cs` (per-team VO shape)
- edit/create `tests/UnitTests/Domain/Entities/LiveSessionTests.cs` (or `LiveSessionOperatorPanelTests.cs`) — mirror the HU-23/HU-36A projection tests

**Pattern this phase owns:** none (the mandated `Proxy` is realized in X.2/X.4).
**Gate:** Domain build passes; unit tests prove: panel carries the session `State`; panel includes **every** team (ordered) with score defaulting to `0`; treasure-hunt progress counts active targets and never clues; trivia teams carry active-question/timer context without invented target progress; no score ledger/ranking/winner is computed.

### Phase X.2 — Application
**Derive** (`ddd_solution_model.md` §SessionOperations Proxy + read surfaces; ADR-0009; ADR-0012 §resource-ownership guard; `GetOperatorTriviaAnsweredMonitorQueryHandler.cs`; `SessionAdministrationAuthorizationProxy.cs`):
- `GetOperatorSessionPanelQuery(Guid LiveSessionId)` — `[Authorize(Roles = "Operator")]` CQRS read.
- Handler injects `ISessionAdministrationAccessResolver` (+ `TimeProvider`), calls `GetAuthorizedSessionAsync(liveSessionId, ct)` (**the Proxy** — loads via `GetByIdAsync`, owner-checks, returns the authorized `LiveSession` or throws `ForbiddenAccessException`), then `session.ProjectOperatorSessionPanel(timeProvider.GetUtcNow())` and maps to the result DTO. **No** ownership/role `if` in the handler.
- `OperatorSessionPanelDto` (+ per-team item DTO) in `Application/Dtos/Sessions/`, mapped by a static factory in `Application/Sessions/Common/` — carries `liveSessionId`, `state`, `timer` (reusing `SessionTimerSnapshotDto`), and the ordered per-team progress list.
- Add an operator-panel broadcaster **port** `IOperatorSessionPanelBroadcaster` (Application/Common/Interfaces) for X.4's live push — mirror `ITeamBoardBroadcaster`.

**Target files** (create | edit — file to mirror):
- create `src/Application/Sessions/Queries/GetOperatorSessionPanel/GetOperatorSessionPanelQuery.cs` — mirror `Queries/GetOperatorTriviaAnsweredMonitor/GetOperatorTriviaAnsweredMonitorQuery.cs`
- create `.../GetOperatorSessionPanel/GetOperatorSessionPanelQueryHandler.cs` — mirror `GetOperatorTriviaAnsweredMonitorQueryHandler.cs` (uses `GetAuthorizedSessionAsync`, not the timer-scoped load)
- create `src/Application/Dtos/Sessions/OperatorSessionPanelDto.cs` — mirror `TriviaAnsweredMonitorDto.cs` / `ParticipantTeamBoardDto.cs`
- create `src/Application/Sessions/Common/OperatorSessionPanelDtoFactory.cs` — mirror `TriviaAnsweredMonitorDtoFactory.cs` / `ParticipantTeamBoardDtoFactory.cs`
- create `src/Application/Common/Interfaces/IOperatorSessionPanelBroadcaster.cs` — mirror `ITeamBoardBroadcaster.cs`
- create `tests/Application.UnitTests/Sessions/Queries/GetOperatorSessionPanel/GetOperatorSessionPanelQueryHandlerTests.cs` — mirror the HU-36A handler tests

**Pattern this phase owns:** `Proxy` (resource-ownership resolver — application side).
**Gate:** Application build passes; handler tests assert the projection is returned for the **assigned** operator; **`ForbiddenAccessException` for a non-owning operator**; the ownership decision runs through `ISessionAdministrationAccessResolver` (no ad-hoc `if`); `[Authorize(Roles="Operator")]`; the DTO carries session state + all-teams progress and no score ledger/ranking. **`Proxy` verified — access via the ownership resolver, not inline checks.**

### Phase X.3 — Infrastructure
**Derive** (`LiveSessionRepository.cs` `GetByIdAsync` includes; `hu36a-context.md` X.3; DES-86 per-target score handoff):
- The projection reads `LiveSession.State`, `Teams`, and the `MissionRuntimeSnapshot` stages/substages/targets already persisted by HU-15/HU-18/HU-33A/HU-86. The Proxy's `GetAuthorizedSessionAsync` uses `GetByIdAsync`, which already hydrates them (the same read HU-23/HU-36A rely on). **No new persisted state → no migration.**
- Keep `TargetSnapshot.Score` as per-target score; do not resurrect `WinnerScore` or substage-level scoring columns.

**Target files** (verify | edit):
- verify `src/Infrastructure/Persistence/Repositories/LiveSessionRepository.cs` — `GetByIdAsync` hydrates teams + active-substage target/question snapshot data used by the panel; extend the include only if a gap is proven
- confirm no migration: grep `ApplicationDbContextModelSnapshot.cs` — no new column; **do not** run `ef migrations add`
- add/extend an integration test under `tests/IntegrationTests/` proving the panel read hydrates all teams + active-substage target snapshots for a persisted session

**Pattern this phase owns:** none.
**Gate:** Infrastructure build passes; a repository/integration test proves the panel read loads session `State` + all teams + active-substage target/question snapshot data; model snapshot has no `winner_score`/substage-winner-score regression; **`ef migrations add` is a no-op / not run** (no schema change).

### Phase X.4 — Api
**Derive** (`SessionsController.cs` operator endpoints `:238-264`; `SessionsHub.cs` `JoinLiveSessionAsOperatorAsync` `:78-89`; `BroadcastTeamBoardNotificationHandler.cs`; `SignalRTeamAnsweredBroadcaster.cs`; `required_patterns_matrix.md:57,112`; ADR-0005 coverage):
- `GET /api/sessions/{liveSessionId:guid}/operator-panel` on `Api/Controllers/SessionsController.cs`, `[Authorize(Policy = AuthorizationPolicies.Operator)]`, `sender.Send(new GetOperatorSessionPanelQuery(liveSessionId))` — mirror `GetOperatorAnsweredMonitorAsync` (`:257-264`) / `GetOperatorTimerSnapshotAsync` (`:238-245`).
- Implement `IOperatorSessionPanelBroadcaster` as `SignalROperatorSessionPanelBroadcaster` → sends the panel DTO to `SignalRTeamAnsweredBroadcaster.BuildOperatorGroup(liveSessionId)` (the `live-session-operators:{id}` group) — mirror `SignalRTeamAnsweredBroadcaster` (operator group) and `SignalRTeamBoardBroadcaster` (broadcaster shape).
- Add `BroadcastOperatorSessionPanelNotificationHandler : INotificationHandler<SessionStateChangedEvent>, INotificationHandler<SubstageAdvancedEvent>` that re-loads read-only, projects `ProjectOperatorSessionPanel`, maps via the factory, and pushes to the operator group — mirror `BroadcastTeamBoardNotificationHandler` (same two triggers, operator-scoped instead of per-team).
- Keep hub membership/join guard in `SessionsHub.JoinLiveSessionAsOperatorAsync` as the authorization boundary; do not let participants or non-owning operators receive the panel.

**Target files** (create | edit — file to mirror):
- edit `src/Api/Controllers/SessionsController.cs` — add the endpoint beside `[HttpGet("{liveSessionId:guid}/answered-monitor")]`
- create `src/Api/Hubs/SignalROperatorSessionPanelBroadcaster.cs` — mirror `SignalRTeamAnsweredBroadcaster.cs` (operator group) + `SignalRTeamBoardBroadcaster.cs` (broadcaster method)
- create `src/Application/Sessions/EventHandlers/BroadcastOperatorSessionPanelNotificationHandler.cs` — mirror `BroadcastTeamBoardNotificationHandler.cs`
- register `IOperatorSessionPanelBroadcaster` → `SignalROperatorSessionPanelBroadcaster` in the Api DI wiring (mirror where `ITeamBoardBroadcaster`/`ITeamAnsweredBroadcaster` are registered)
- create `tests/IntegrationTests/Api/OperatorSessionPanelEndpointTests.cs` — mirror `OperatorTriviaAnsweredMonitorEndpointTests` (200 assigned operator / 403 non-owner)
- create/extend a hub test proving the panel push reaches `live-session-operators:{id}` and not participant connections

**Pattern this phase owns:** `Proxy` (endpoint authorization policy + the ownership resolver enforced in the handler; hub-join ownership guard reused). **SignalR transport gate verified here.**
**Gate:** endpoint returns **200** with session state + per-team progress for the assigned operator; **403 (RFC 7807)** for a non-owning operator; the panel reaches only `live-session-operators:{id}` (operators), never participant connections; a `SessionStateChanged`/`SubstageAdvanced` transition pushes an updated panel to the operator group without reload; service coverage meets the repo gate (ADR-0005).

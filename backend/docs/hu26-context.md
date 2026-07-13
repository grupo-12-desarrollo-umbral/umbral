# HU-26 Context — Liberación manual de pistas

> Paste this section into any agent session that needs context for HU-26.
> Last updated: 2026-07-13 | Branch: `feature/hu-26-manual-clue-release`

## State

- DES-36 (HU-26): **Todo**, labels: `svc:session-operations-service`, `Feature`,
  `canon-realign`, `ready-for-agent`
- **Resolved mode:** **feature flow** with a **comment-only canon reword**
  (`canon-realign` **without** `needs-rebuild` → not a rebuild). The AC checklist in
  the ticket body is slightly stale; the authoritative reword lives in the ticket's
  `⚠️ Deuda de canon` comment (2026-06-17) and is applied below:
  1. A clue's scope is a **`Target`** inside the **active substage**, not "la etapa" —
     so *"no liberar dos veces … para la misma etapa"* is re-expressed as **per
     `Target`** (a clue may not be released twice to the same team for the same target).
  2. **Releasing a clue does not advance the substage** and does not resolve a target.
- **Supersession check:** DES-36 is **not** in the realignment map's superseded ("old")
  column (those are DES-23/28/30/44/47). It appears only as a *dependent* of superseded
  rows in the supersession table — safe to build. No predecessor ids were dropped or
  substituted for this HU beyond the standard superseded-originals exclusion.
- PRD: DES-70 (`session-operations-service`), local file authoritative
  (`backend/docs/prd/DES-70-primera-implementacion-de-session-operations-service-hu-15-a-hu-36.md`).
- Predecessor DES ids (build-on): DES-22/DES-24 (HU-15/HU-16 LiveSession + immutable
  snapshot), DES-25 (HU-18 team association), DES-26 (HU-19 operator assignment),
  DES-76 (HU-21A session state machine), DES-77 (HU-22 authoritative timer),
  DES-31 (HU-23 participant team board), DES-32 (HU-24A operator panel).
- **Branch:** `feature/hu-26-manual-clue-release`, **base `develop`**. All build-on
  predecessors are **Done** on `develop`; HU-29 (DES, evidence submission) is **In
  Progress** but is a different aggregate concern (evidence intake) that HU-26 does
  **not** build on, so it does not change the base.

## Required design patterns

- `Facade` **(mandated — `required_patterns_matrix.md` HU-26 row, line 119; ADR-0013)**
  - Why: manual clue release orchestrates a runtime visibility change **plus** a local
    trace record across the aggregate, the ownership guard, and persistence — canon
    (`ddd_solution_model.md §8`) requires that coordination be exposed through a narrow
    application-facing service, not leaked into the handler or endpoint.
  - Phase owner: **X.2 Application**.
  - Concrete obligation: a single `ClueReleaseFacade` / `IClueReleaseFacade`
    orchestration entry point (`ReleaseCluesAsync`) over the ownership resolver
    (Proxy) → aggregate load → per-team release fan-out → persistence. Mirror the
    existing `SessionTeamAssociationFacade`; do **not** inline the coordination in the
    command handler or the controller.
- `Proxy` **(mandated — `required_patterns_matrix.md` HU-26 row, line 119; ADR-0009 + ADR-0012 §resource-ownership guard)**
  - Why: ADR-0004 names restricted clues explicitly — the release action must be gated
    to the session's assigned operator (or an Administrator), not exposed by role alone.
  - Phase owner: **X.2 Application** (resolver in the handler/facade) **and X.4 Api**
    (endpoint authorization policy).
  - Concrete obligation: the release passes through
    `ISessionAdministrationAccessResolver.GetAuthorizedSessionAsync` /
    `SessionAdministrationAuthorizationProxy` (Administrator all / Operator
    `AssignedOperatorUserId == actor.UserId` else `ForbiddenAccessException`). Coarse
    role gate stays `[Authorize(Roles="Operator")]` / endpoint
    `[Authorize(Policy = AuthorizationPolicies.Operator)]`. **No ad-hoc role/owner `if`
    checks** in the handler, facade, controller, or hub.
- Transport: **SignalR / WebSockets** (`required_patterns_matrix.md:57,119`) — verified
  in X.4. On release, the newly-visible clue is pushed to the **affected team's** board
  (reused `team:{teamId}` group / `ITeamBoardBroadcaster`); a release to one team must
  not reach another team's connections.

## What predecessors have already landed

`DES-36` is live, not superseded, and now carries both required labels. Feature flow
with a canon-reword comment. Build-on predecessors (same `LiveSession` aggregate /
runtime snapshot / team board / operator-ownership seam):

**Domain**
- **DES-22/DES-24 (HU-15/HU-16)** — `LiveSession` aggregate root + immutable
  `MissionRuntimeSnapshot` holding `StageSnapshots` / `SubstageSnapshots` /
  `TargetSnapshots` / `ClueSnapshots`. The treasure-hunt clue guidance lives on
  **`TargetSnapshot.ClueText` + `TargetSnapshot.ClueVisibilityPolicy`** (nullable);
  substage-level clues live in `ClueSnapshot` (`IsVisibleWhenSubstageStarts`).
  `LiveSession.CollectVisibleClues()` / `CollectTargetVisibleClues()` /
  `CollectSubstageVisibleClues()` already project **visible** clues and **withhold**
  `HiddenUntilOperatorRelease` clues — with explicit `// HU-26/HU-28` TODO comments
  marking where per-team release state must plug in.
- **DES-76 (HU-21A)** — `LiveSession.State` (`Scheduled → Preparing → Active → Paused
  → Finished → Cancelled`) + `LiveSession.MoveTo(...)` + `SessionStateChangedEvent`.
  State-change "recording" is done via **domain events dispatched through the outbox**
  (`DispatchDomainEventsInterceptor` → `OutboxDomainEventDispatcher`); **there is no
  persisted `SessionEvent` history table today.**
- **DES-77 (HU-22)** — authoritative timer per active `SubstagePlayMode`. Not consumed
  by clue release (release is state-gated, not timer-gated).
- Runtime `Team` (child of `LiveSession`) already carries `CurrentClueNodeId` and
  **`ReleasedClueCount`** columns — but **no release mechanics** (confirmed by the
  `LiveSession.cs` TODOs).

**Application**
- **DES-26 (HU-19)** — operator assignment + `AssignedOperatorUserId` (the ownership
  seam the Proxy checks). `AssignOperatorToSession` command slice is the closest
  operator-scoped **write** slice to mirror.
- **DES-32 (HU-24A) / DES-31 (HU-23)** — `SessionAdministrationAuthorizationProxy` +
  `ISessionAdministrationAccessResolver.GetAuthorizedSessionAsync` (the exact Proxy),
  and `ProjectParticipantTeamBoard(...)` + `BroadcastTeamBoardNotificationHandler`
  (re-projects the team board on `SessionStateChangedEvent` / `SubstageAdvancedEvent`).
- Existing Application-layer facades to mirror: `SessionTeamAssociationFacade`,
  `TriviaRoundOrchestratorFacade` (both in `Application/Sessions/Common/`).

**Infrastructure / API**
- `LiveSessionConfiguration.cs` owns the snapshot (`ClueSnapshots` `OwnsMany`, table
  `live_session_mission_runtime_snapshot_clues`) and runtime `Teams` (`OwnsMany`, table
  `live_session_teams`, with `released_clue_count` / `current_clue_node_id` columns).
  `LiveSessionRepository` + `LiveSessionRepositoryIntegrationTests` are the round-trip
  precedent. API is **MVC controllers** (`SessionsController`, `[Route("api/sessions")]`),
  not minimal API. Live push flows through `SessionsHub` +
  `SignalRTeamBoardBroadcaster` (`ITeamBoardBroadcaster`, method `"TeamBoardUpdated"`,
  group `team:{teamId:D}`).

**Landed, untouched by this HU:** HU-33A/33B/34/36A (trivia runtime), HU-20 (assigned
session reads), HU-07A/07B (membership/reconnect). HU-29 (evidence submission) is **In
Progress** — untouched (different aggregate concern; not a base dependency).

**Coverage:** no predecessor context records a stable aggregate % for
`session-operations-service`; verify the real ADR-0005 gate during phase X.4.

## What this HU adds

| Concern | New work |
|---|---|
| Operator clue release | An operator releases a treasure-hunt `Target`'s optional **hidden** clue (`HiddenUntilOperatorRelease`) to **one team or all teams** during an **Active** session. |
| Per-team release record | New `ClueReleaseRecord` child entity of `LiveSession` (teamId, targetId, clueId?, releaseMode=Manual, releasedByUserId, releasedAt) — the **local trace** ("historial de la sesión"), append-only. |
| No leak | A clue released to one team surfaces on **that team's** board only; the per-team record makes visibility independent. |
| No duplicate | The same clue may **not** be released twice to the same team for the same `Target` (uniqueness on `(teamId, targetId)`). |
| Does not advance | Release changes visibility only — it does **not** advance the substage or resolve the target. |
| Orchestration | `ClueReleaseFacade` (Facade) coordinates authorize → load → per-team fan-out → persist. |
| Access guard | Release gated by the operator-ownership `Proxy` (assigned operator / Administrator only). |
| Backend contract | `POST /api/sessions/{liveSessionId}/clues/release` + affected-team board push. |
| Live updates | `ClueReleasedEvent` → re-project the affected team board → push over the reused `team:{teamId}` SignalR group. |
| Frontend | Operator control to release a clue to one/all teams; the participant board reveals the released clue live (web + participant surface — see Steps 9/9b). |

## Touched surfaces

- `backend/services/session-operations-service` (Domain, Application, Infrastructure, Api)
- `frontend/` operator clue-release control + participant board clue reveal
- `backend/frontend` API contract boundary: `POST /api/sessions/{id}/clues/release`
  request/response + the `team:{teamId}` board-update payload carrying the newly-visible clue
- **Out of scope (do not build):** publishing `ClueReleased` to RabbitMQ/MassTransit for
  async audit — split to the enabler **DES-92** (gated by #164), consumed by the session
  event history **DES-56 (HU-40A)**. This HU delivers the release + its **local** record only.

## Committed phases

| Commit | Phase | Description |
|---|---|---|
| _(none yet)_ | | |

## Known quirks / gotchas

- **`historial` = the `ClueReleaseRecord`, not a `SessionEvent` table.** Canon models a
  `SessionEvent` append-only log, but the code never built one (HU-21A used domain
  events + outbox). Do **not** introduce a `SessionEvent` table here — the append-only
  per-team `ClueReleaseRecord` (carrying `releasedAt` / `releasedByUserId` / `teamId` =
  time/actor/context) is the local trace that satisfies the AC. The consolidated,
  queryable event **history** is DES-56/HU-40A, fed asynchronously via DES-92 — not this HU.
- **The clue is keyed by `TargetId`.** For treasure-hunt substages the clue is embedded
  on `TargetSnapshot.ClueText` / `.ClueVisibilityPolicy`, so the release naturally
  identifies the **target** whose optional clue becomes visible (canon
  `ClueReleaseRecord.targetId`). Store `clueId` too **when** the snapshot carries a
  distinct clue node id, else leave it null. Substage-level / trivia clue release is
  **canon-silent** (`ClueReleaseRecord.targetId` assumes a target) — keep this HU's
  primary path to treasure-hunt target clues and flag any trivia-clue release as a
  build decision if a reviewer asks for it.
- **Session validity gate = `Active`.** Canon does **not** name an explicit state for
  release (`bd_umbral_entity_spec.md`/grilling are silent), but a `Target` "inside the
  active substage" exists only after `Preparing → Active` starts the first substage
  (grilling §Session Lifecycle). Gate release on `State == Active`. `Paused` is
  canon-silent (submissions are blocked while paused, clue release is not mentioned) —
  default to Active-only; treat allowing `Paused` as a build decision, not an assumption.
- **Do not re-guard existing endpoints or build evidence/QR/score.** This HU adds one
  write action + its live read reveal. No score ledger, no target resolution (HU-31),
  no evidence intake (HU-29), no conditional/auto release (HU-27).
- Namespace root is `umbral_backend.*`; result DTOs live in `Application/Dtos/Sessions/`,
  **not** inside the slice folder (ADR-0011). No `Handlers/`/`Dtos/` type-buckets, no
  `*CommandHandlerBase`.

## Per-phase derivation — authoritative for implementation

> Primary source for the phase subagent (per `backend-agent.md` "Read first"). Derived
> from `bd_umbral_entity_spec.md` §Clue/§Target/§ClueReleaseRecord/§Team/§LiveSession,
> `ddd_solution_model.md` §6/§8/§9 SessionOperations, `grilling-session-mission-restructure.md`
> §Clues/§Session Lifecycle, ADR-0009/0012/0013, and the resolved code map (2026-07-13).
> Open a cited canon section only to fill a gap a block leaves open.

### Phase X.1 — Domain
**Derive** (`bd_umbral_entity_spec.md` §ClueReleaseRecord L669–698, §Clue L136–161,
§Target L104–134, §Team L379–380, §LiveSession L317–323;
`grilling-session-mission-restructure.md` §Clues L46–52; §Session Lifecycle L73–98):
- `ClueReleaseRecord` — **new child entity of `LiveSession`**: `clueReleaseRecordId`,
  `teamId`, `targetId`, `clueId?`, `releaseMode` (`ReleaseMode` enum; this HU uses
  `Manual`), `releasedByUserId?`, `releasedAt`. **Append-only.** "Clue visibility does
  not unlock, resolve, or advance targets by itself" (spec L698).
- `ReleaseMode` enum — `Manual` (+ `Automatic`/`Policy` reserved per canon; only
  `Manual` used here).
- `LiveSession.ReleaseClue(targetId, teamId, operatorUserId, now)` domain method:
  requires `State == Active`; resolves the target's optional clue in the **active
  treasure-hunt substage** of the runtime snapshot (must exist and be
  `HiddenUntilOperatorRelease`); enforces **uniqueness on `(teamId, targetId)`** (spec
  L696: "the same clue should not be released twice to the same `Team`"); appends a
  `ClueReleaseRecord`; increments that `Team.ReleasedClueCount`; **does not** advance
  the substage or resolve the target; raises `ClueReleasedEvent` (canonical
  `ClueReleasedToTeam`, `ddd_solution_model.md §6 L325`).
- `LiveSession.ReleaseClueToAllTeams(targetId, operatorUserId, now)` — fan-out over
  runtime `Teams`, one record + event per team; all-team release "creates or implies
  visibility for every team" (spec L697).
- Update `CollectTargetVisibleClues(...)` so a `HiddenUntilOperatorRelease` target clue
  surfaces **for a team once a `ClueReleaseRecord` for `(teamId, targetId)` exists** —
  per team, so a release to one team does not leak to others (the "no leak" property is
  realized here at the projection).
- New exceptions: `ClueAlreadyReleasedToTeamException`, `ClueNotReleasableException`
  (no hidden clue on the target / not in the active substage),
  `SessionNotActiveForClueReleaseException` (or reuse the existing invalid-state guard).

**Target files** (create | edit — file to mirror):
- create `src/Domain/Entities/ClueReleaseRecord.cs` — mirror `src/Domain/Entities/Team.cs` (LiveSession child entity)
- create `src/Domain/Enums/ReleaseMode.cs` — mirror `src/Domain/Enums/SubstagePlayMode.cs`
- create `src/Domain/Events/ClueReleasedEvent.cs` — mirror `src/Domain/Events/SessionStateChangedEvent.cs`
- edit `src/Domain/Entities/LiveSession.cs` — add `ReleaseClue`/`ReleaseClueToAllTeams`; wire the release into `CollectTargetVisibleClues` (replace the `// HU-26/HU-28` withhold TODO)
- create `src/Domain/Exceptions/{ClueAlreadyReleasedToTeamException,ClueNotReleasableException,SessionNotActiveForClueReleaseException}.cs` — mirror existing `src/Domain/Exceptions/*`

**Pattern this phase owns:** none (Facade/Proxy are realized in X.2/X.4; the
`ClueReleasePolicy` release invariants are enforced inside the aggregate method, as
`MoveTo` enforces transition rules inline).
**Gate:** unit test per new domain type; `ReleaseClue` enforces Active-state,
hidden-clue-exists, and per-team `(teamId, targetId)` uniqueness (duplicate rejected);
all-teams fan-out writes one record/event per team; release **does not** advance the
substage or resolve the target; the released clue surfaces **only** for the released
team (no leak); `ClueReleasedEvent` raised per released team.

### Phase X.2 — Application
**Derive** (`ddd_solution_model.md` §9 `ReleaseClue` L529, §8 `ClueReleasePolicy`
L438–439 + Facade L453–454 + Proxy L461–462; ADR-0013; ADR-0009):
- `ReleaseClueCommand(Guid LiveSessionId, Guid TargetId, Guid? TeamId)` : `IRequest<ReleaseClueResultDto>`,
  `[Authorize(Roles = "Operator")]`. `TeamId == null` ⇒ release to all teams.
- `ReleaseClueCommandHandler` — delegates to `IClueReleaseFacade`; returns the result DTO.
- `ClueReleaseFacade` / `IClueReleaseFacade` (**Facade**) — `ReleaseCluesAsync(command, ct)`:
  `resolver.GetAuthorizedSessionAsync(liveSessionId, ct)` (**Proxy** — Administrator
  all / Operator owns-else-`ForbiddenAccessException`) → apply
  `liveSession.ReleaseClue(...)` or `ReleaseClueToAllTeams(...)` (fan-out) →
  `_liveSessionRepository.UpdateAsync(...)` → `ReleaseClueResultDto` (targetId +
  releasedTeamIds). Mirror `SessionTeamAssociationFacade`.
- `ReleaseClueCommandValidator` — `LiveSessionId`/`TargetId` `NotEmpty()`; `TeamId` optional.
- `ReleaseClueResultDto` in `Dtos/Sessions/`.

**Target files** (create | edit — file to mirror):
- create `src/Application/Sessions/Commands/ReleaseClue/{ReleaseClueCommand,ReleaseClueCommandHandler,ReleaseClueCommandValidator}.cs` — mirror `src/Application/Sessions/Commands/AssignOperatorToSession/*`
- create `src/Application/Sessions/Common/{IClueReleaseFacade,ClueReleaseFacade}.cs` — mirror `src/Application/Sessions/Common/SessionTeamAssociationFacade.cs`
- create `src/Application/Dtos/Sessions/ReleaseClueResultDto.cs` — mirror `src/Application/Dtos/Sessions/AssignOperatorToSessionResultDto.cs`
- edit `src/Application/DependencyInjection.cs` — register `IClueReleaseFacade`

**Pattern this phase owns:** `Facade` (`ClueReleaseFacade` orchestration entry point)
**and** `Proxy` (ownership resolver in the facade).
**Gate:** handler + facade tests — assigned-operator releases to one team (200 path) and
to all teams; non-owning operator → `ForbiddenAccessException` (Proxy, **no ad-hoc
role/owner `if`**); duplicate release rejected; release does not advance the substage;
`[Authorize(Roles="Operator")]` present. Gate (pattern): orchestration behind the
single `ClueReleaseFacade` entry point (Facade); access via the resource-ownership
resolver (Proxy).

### Phase X.3 — Infrastructure
**Derive:** persist `ClueReleaseRecord` as an EF **`OwnsMany`** collection on
`LiveSession` (table `live_session_clue_releases`, FK `live_session_id`, key
`ClueReleaseRecordId`, columns for teamId/targetId/clueId/releaseMode/releasedByUserId/
releasedAt); ensure the runtime `Teams.ReleasedClueCount` column round-trips the new
increment. No stale schema.

**Target files** (create | edit — file to mirror):
- edit `src/Infrastructure/Persistence/Configurations/LiveSessionConfiguration.cs` — add
  `OwnsMany(ClueReleaseRecords)` mirroring the runtime `Teams` `OwnsMany` (≈ L406)
- create a new migration under `src/Infrastructure/Persistence/Migrations/` (`ef migrations add AddClueReleaseRecords`) — none exists for this collection yet
- edit `tests/IntegrationTests/Persistence/LiveSessionRepositoryIntegrationTests.cs` (or a
  sibling) — round-trip a `LiveSession` carrying clue-release records
- grep `ClueRelease`/`clue_release` in `ApplicationDbContextModelSnapshot.cs` to confirm
  the table — **do not full-read** the snapshot

**Pattern this phase owns:** none.
**Gate:** `ef migrations add` succeeds and represents the new owned collection; the
repository integration test proves round-trip of `LiveSession` + `ClueReleaseRecord`
rows (team/target/releasedAt persisted, uniqueness respected); no regression to the
existing snapshot/team schema.

### Phase X.4 — Api
**Derive:** `POST /api/sessions/{liveSessionId}/clues/release` operator endpoint
(`[Authorize(Policy = AuthorizationPolicies.Operator)]` — **Proxy** endpoint policy),
request record `ReleaseClueRequest(Guid TargetId, Guid? TeamId)` → `ReleaseClueCommand`.
Map the new domain exceptions in `ProblemDetailsExceptionHandler` (duplicate →
409/appropriate RFC 7807; not-releasable/not-active → 409/422). **SignalR live push**:
`ClueReleasedEvent` → an Application notification handler re-projects the **affected
team's** board and pushes via `ITeamBoardBroadcaster` to `team:{teamId}` (mirror
`BroadcastTeamBoardNotificationHandler`); an all-teams release pushes to each released
team only.

**Target files** (create | edit — file to mirror):
- edit `src/Api/Controllers/SessionsController.cs` — add `ReleaseCluesAsync` + inline
  `ReleaseClueRequest` record; mirror `AssignOperatorAsync` / `AssociateTeamAsync`
- create `src/Application/Sessions/EventHandlers/BroadcastClueReleasedNotificationHandler.cs`
  — mirror `src/Application/Sessions/EventHandlers/BroadcastTeamBoardNotificationHandler.cs`
- edit `src/Api/Services/ProblemDetailsExceptionHandler.cs` — map the new exceptions
- add endpoint + hub integration tests mirroring the HU-24A / HU-23 test style

**Pattern this phase owns:** `Proxy` (endpoint Operator policy + the ownership resolver
in the handler/facade). SignalR transport gate verified here.
**Gate:** endpoint integration tests — assigned operator releasing to one team **200**
and all-teams **200**; non-owning operator **403** (RFC 7807); duplicate release →
409/ProblemDetails; SignalR test proves the released clue reaches **only** the target
team's board (`team:{teamId}`) and **not** other teams' connections, and that release
does **not** advance the substage; `backend/docs/adr/0005-coverlet-msbuild-for-aggregate-coverage.md`
coverage gate passes. Gate (pattern): endpoint authorized via the Operator policy AND
the ownership resolver (Proxy); no ad-hoc role/owner checks in controller/hub.

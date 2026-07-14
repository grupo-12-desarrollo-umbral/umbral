# HU-28 Context — Pistas operativas ad-hoc durante sesión en vivo

> Paste this section into any agent session that needs context for HU-28.
> Last updated: 2026-07-13 | Branch: `feature/hu-28-operative-clues`

## State

- DES-38 (HU-28): **In Progress** (moved off Todo 2026-07-14), labels:
  `svc:session-operations-service`, `Feature`, `canon-realign`, `ready-for-agent`.
- **Resolved mode:** **feature flow** with a **comment-only canon reword**
  (`canon-realign` **without** `needs-rebuild` → not a rebuild). The AC checklist in the
  ticket body was **rewritten 2026-07-13** to the reinterpretation below; the authoritative
  scope lives in that reworded AC + the ticket's `⚠️ Nota de canon` comment (2026-07-13).
- **Canon reinterpretation (the scope decision).** The mission-runtime rewrite (`c35f6c2`)
  makes the `LiveSession`'s `MissionRuntimeSnapshot` **immutable**, which conflicts with the
  ticket's original "agregar una pista nueva." The realignment map
  (`canon-realignment-after-mission-runtime-rewrite.md:106,113`) offered two exits;
  **the decision taken is option (a), resolved by layering:** an operator-authored
  operative clue is **session runtime state on `LiveSession`** (a new `OperativeClue` child
  entity) — **never** injected into the immutable `MissionRuntimeSnapshot` nor the source
  `Mission`. So "sin modificar la misión original" is literally true, and the immutability
  tension dissolves. This is exactly how `required_patterns_matrix.md:121` already frames
  HU-28: *"Operator adds operational clues to a live session without modifying the source
  mission … re-scoped to runtime clue addition."*
- **Distinct from HU-26 (DES-36, Done):** HU-26 **releases** a *planned* clue already in the
  snapshot (`ClueReleaseRecord`); HU-28 **authors** a *new ad-hoc* clue (`OperativeClue`).
  Author vs reveal. HU-28 mirrors HU-26's structure but is a sibling surface, not a
  dependency of it.
- **Supersession check:** DES-38 is **not** in the realignment map's superseded ("old")
  column — it appears only as a *dependent* of the superseded rows DES-28→DES-76 and
  DES-30→DES-77 (map lines 196–197). Safe to build. No predecessor ids were dropped or
  substituted beyond the standard superseded-originals exclusion.
- PRD: DES-70 (`session-operations-service`), local file authoritative
  (`backend/docs/prd/DES-70-primera-implementacion-de-session-operations-service-hu-15-a-hu-36.md`).
  The PRD frames the clue cluster around *release*; HU-28's ad-hoc *authoring* is the
  reworded scope (matrix line 121 is the authoritative HU-28 framing).
- Predecessor DES ids (build-on): **DES-36 (HU-26, Done)** — the direct sibling whose clue
  surface HU-28 mirrors; DES-22/DES-24 (HU-15/HU-16 `LiveSession` + immutable snapshot),
  DES-25 (HU-18 team association), DES-26 (HU-19 operator assignment + ownership seam),
  DES-76 (HU-21A session state machine), DES-77 (HU-22 timer — not consumed), DES-31
  (HU-23 participant team board), DES-32 (HU-24A operator panel + ownership Proxy).
- **Branch:** `feature/hu-28-operative-clues`, **base `develop`**. All build-on
  predecessors (incl. HU-26) are **Done** on `develop`. HU-30 (DES-95) and HU-31 (DES-42)
  are **In Progress** but are the evidence/QR aggregate — **not** built on here, so the base
  stays `develop`.

## Required design patterns

- **No mandated pattern.** `required_patterns_matrix.md` places HU-28 in the **no-pattern
  row** (line 45) and the **no-SignalR row** (line 59); the HU→pattern table row (line 121)
  is `HU-28 | — | —`. Do **not** force a `Facade` or `Proxy` gate onto this HU — the single
  operator write (author clue + assign teams on one aggregate) is a plain CQRS handler, and
  a Facade here is ceremony (ADR-0012 genuine-vs-ceremony test).
- **Applies-where `Proxy` (note, not a gate).** Matrix line 121 reads "no mandated pattern,
  `Proxy` guards the operator action." This is **informational** — the operator action
  inherits the **existing** session-ownership guard already established by HU-24A/HU-26
  (`ISessionAdministrationAccessResolver.GetAuthorizedSessionAsync` /
  `SessionAdministrationAuthorizationProxy`, ADR-0009) plus the endpoint
  `[Authorize(Policy = AuthorizationPolicies.Operator)]` it would get anyway (ADR-0001/0002).
  Reuse that guard; do **not** introduce a new pattern gate, and do **not** re-implement an
  ownership `if` inline. Verified as behaviour (non-owner → `ForbiddenAccessException`/403),
  not as a mandated-pattern gate line.
- **No SignalR mandate.** Unlike HU-26, HU-28 is not a SignalR HU (matrix line 59). The
  operative clue surfaces on the participant board through the existing read projection
  (`CollectVisibleClues`). A live push is **optional and non-gated** — see the X.4 block.

## What predecessors have already landed

`DES-38` is live, not superseded, and carries both required labels. Feature flow with a
canon-reword. Build-on predecessors (same `LiveSession` aggregate / runtime clue model /
team board / operator-ownership seam):

**Domain**
- **DES-36 (HU-26) — PRIMARY mirror (Done/merged).** Added the runtime clue-write pattern
  HU-28 copies: `ClueReleaseRecord` (append-only `LiveSession` child), `LiveSession.ReleaseClue`
  / `ReleaseClueToAllTeams` (fan-out, one record + `ClueReleasedEvent` per team), the
  `_clueReleaseRecords` collection + `GetClueReleaseRecords()` accessor, and the
  `CollectVisibleClues(teamId)` → `CollectTargetVisibleClues` per-team projection
  (`LiveSession.cs:408,418,435,810,832,929`). HU-28 mirrors this shape for `OperativeClue`.
- **DES-22/DES-24 (HU-15/HU-16)** — `LiveSession` aggregate root + **immutable**
  `MissionRuntimeSnapshot`. The board projection `CollectVisibleClues(teamId)`
  (`LiveSession.cs:810`) is the join point HU-28 extends to surface operative clues per team.
  Runtime `Team` (child of `LiveSession`) carries `ReleasedClueCount` / `CurrentClueNodeId`.
- **DES-76 (HU-21A)** — `LiveSession.State` (`Scheduled → Preparing → Active → Paused →
  Finished → Cancelled`) + `MoveTo(...)`. HU-28 gates authoring on `State ∈ {Active, Paused}`.
- **DES-26 (HU-19)** — operator assignment + `AssignedOperatorUserId` (the ownership seam the
  inherited Proxy checks). `AssignOperatorToSession` command slice is the closest
  operator-scoped single-aggregate **write** to mirror (no facade).
- **DES-32 (HU-24A) / DES-31 (HU-23)** — `SessionAdministrationAuthorizationProxy` +
  `ISessionAdministrationAccessResolver.GetAuthorizedSessionAsync` (the inherited guard), and
  the participant team-board projection HU-28's operative clues ride on.

**Application**
- Existing operator-scoped write slice to mirror: `Application/Sessions/Commands/AssignOperatorToSession/*`.
  HU-26's `Commands/ReleaseClue/*` is the closest clue-write slice for shape (command +
  handler + validator), but **omit its `ClueReleaseFacade`** — HU-28 has no mandated Facade.
- Result DTOs live in `Application/Dtos/Sessions/` (ADR-0011), not inside the slice folder.

**Infrastructure / API**
- `LiveSessionConfiguration.cs` owns the snapshot and the runtime children via `OwnsMany`
  (e.g. the `ClueReleaseRecord` collection HU-26 added, and runtime `Teams`).
  `LiveSessionRepository` + `LiveSessionRepositoryIntegrationTests` are the round-trip
  precedent. API is **MVC controllers** (`SessionsController`, `[Route("api/sessions")]`).
- **`SessionEvent` now EXISTS** (`Domain/Entities/SessionEvent.cs`, added post-HU-26,
  **specialized to state-changes** via `SessionEvent.ForStateChange`) — this **corrects** the
  stale HU-26 note that "no `SessionEvent` table exists." See the gotcha below: HU-28 does
  **not** extend it.

**Landed, untouched by this HU:** HU-33A/33B/34/36A (trivia runtime), HU-20 (assigned session
reads), HU-07A/07B (membership/reconnect). **In Progress, untouched (different aggregate —
evidence/QR):** HU-30 (DES-95 evidence validation), HU-31 (DES-42 QR target scan) — neither is
a base dependency.

**Coverage:** no predecessor context records a stable aggregate % for
`session-operations-service`; verify the real ADR-0005 gate during phase X.4.

## What this HU adds

| Concern | New work |
|---|---|
| Operator authors ad-hoc clue | An operator **creates** an operative clue (authored text that did **not** exist in the mission) during an **Active or Paused** session. |
| Session-scoped, mission untouched | The clue is stored as **`LiveSession` runtime state** (new `OperativeClue` child) — never in the immutable `MissionRuntimeSnapshot` nor the source `Mission`. |
| Assign to one/several teams | The clue may be assigned to **one team or several**; visible **only** to the assigned teams (no leak). Modeled as one `OperativeClue` record per (clue, team), mirroring HU-26's per-team fan-out. |
| Session history | The append-only `OperativeClue` record itself is the local *historial* (`createdByUserId`, `createdAt`, `teamId`). |
| Does not advance | Authoring/assigning changes visibility only — it does **not** advance the substage or resolve a target. |
| Access guard | Inherits the existing operator-ownership guard (`ISessionAdministrationAccessResolver`) — **no** new pattern. |
| Backend contract | `POST /api/sessions/{liveSessionId}/operative-clues` (operator-guarded). |
| Frontend | Operator control to author + assign an operative clue; participant board reveals assigned operative clues (web + participant surface — Steps 9/9b). |

## Touched surfaces

- `backend/services/session-operations-service` (Domain, Application, Infrastructure, Api)
- `frontend/` operator operative-clue authoring control + participant board operative-clue reveal
- `backend/frontend` API contract boundary: `POST /api/sessions/{id}/operative-clues`
  request/response + the operative clue's shape in the participant board payload
- **Out of scope (do not build):** authoring `Target`s or touching mission structure
  (canonical Target-leaf modeling is HU-10, matrix line 121); mutating the
  `MissionRuntimeSnapshot`/`Mission`; a consolidated queryable `SessionEvent` history
  (DES-56/HU-40A); RabbitMQ/MassTransit publication (DES-92); conditional/auto release
  (HU-27); manual release of *planned* snapshot clues (HU-26, already shipped); evidence/QR
  (HU-29/30/31); re-guarding existing endpoints.

## Committed phases

| Commit | Phase | Description |
|---|---|---|
| _(none yet)_ | | |

## Known quirks / gotchas

- **`OperativeClue` is canon-silent — derived by mirror + the Nota de canon.** `grep` over
  `bd_umbral_entity_spec.md` / `ddd_solution_model.md` / `grilling-session-mission-restructure.md`
  returns **no** `OperativeClue` / "pista operativa" concept. It is the *new* runtime concept
  the scope decision introduces. Its authority is the reworded DES-38 AC + `⚠️ Nota de canon`
  + matrix line 121, and its **shape** mirrors HU-26's merged `ClueReleaseRecord`. Do not
  invent canon citations for the entity itself; the base concepts it composes (`Clue`,
  `Target`, `Team`, `LiveSession`) are cited below.
- **Immutability is respected by layering, not by exception code.** Store `OperativeClue` on
  `LiveSession` (mutable session runtime state, exactly like `ClueReleaseRecord`/scores/team
  progress). **Never** add to `MissionRuntimeSnapshot` or `Mission`. There is no "snapshot
  mutation" path to build — if a design puts the clue in the snapshot, it is wrong.
- **`historial` = the `OperativeClue` record, not `SessionEvent`.** `SessionEvent` now exists
  but is specialized to state-changes (`SessionEvent.ForStateChange`, `EventType =
  "SessionStateChanged"`). The AC ("registrada en el historial de la sesión") is satisfied by
  the append-only `OperativeClue` (carrying actor/time/team). Do **not** extend `SessionEvent`
  with an `OperativeClueAdded` type here — the consolidated queryable history is
  DES-56/HU-40A. Extending it is a deliberate build decision, not required by the AC.
- **State gate = `Active` OR `Paused`.** Unlike HU-26 (Active-only), HU-28's AC explicitly
  allows authoring during a **Paused** session. Gate on `State == Active || State == Paused`.
- **No mandated Facade / Proxy / SignalR.** See Required design patterns. The ownership guard
  is inherited (existing resolver + endpoint policy), the Facade is omitted (ceremony), and
  the live push is optional/non-gated. Do not copy HU-26's mandated-pattern gates.
- **Assign-to-all = per-team fan-out.** "uno o varios equipos" → one `OperativeClue` record
  per assigned team (mirror `ReleaseClueToAllTeams`). This makes per-team visibility / no-leak
  structural. An optional shared correlation id may group a single authoring action, but is
  not required by the AC.
- Namespace root is `umbral_backend.*`; result DTOs live in `Application/Dtos/Sessions/`
  (ADR-0011). No `Handlers/`/`Dtos/` type-buckets, no `*CommandHandlerBase`.

## Per-phase derivation — authoritative for implementation

> Primary source for the phase subagent (per `backend-agent.md` "Read first"). The
> `OperativeClue` concept is **canon-silent** and derived by mirroring HU-26's merged
> `ClueReleaseRecord` surface; base concepts cite `bd_umbral_entity_spec.md` §Clue L136–161 /
> §Target L104–134 / §Team L379–380 / §LiveSession L317–323 and the resolved code map
> (2026-07-13). Authority for the *scope*: reworded DES-38 AC + `⚠️ Nota de canon` +
> `required_patterns_matrix.md:121`. Open a cited canon section only to fill a gap a block
> leaves open.

### Phase X.1 — Domain
**Derive** (canon-silent entity — mirror merged `ClueReleaseRecord`; base concepts
`bd_umbral_entity_spec.md` §Clue L136–161, §Team L379–380, §LiveSession L317–323):
- `OperativeClue` — **new append-only child entity of `LiveSession`**, one record per
  (clue, team): `operativeClueId`, `liveSessionId`, `teamId`, `clueText`, `createdByUserId`,
  `createdAt`. Static factory `Create(...)`. Mirror `ClueReleaseRecord` exactly in shape
  (private ctors, `internal static` factory, `Guid.NewGuid()` id). **No `ReleaseMode`-style
  enum** — an operative clue is always operator-authored.
- `LiveSession.AddOperativeClue(string clueText, IReadOnlyCollection<Guid> teamIds, int
  operatorUserId, DateTimeOffset now)` domain method:
  - requires `State == Active || State == Paused` — else
    `SessionNotLiveForOperativeClueException`.
  - `clueText` required (non-empty, trimmed) — else `OperativeClueTextRequiredException`.
  - `teamIds` non-empty **and** each must be a runtime `Team` of this session — else
    `OperativeClueRequiresAtLeastOneTeamException` (empty) / reuse `TeamNotFoundException`
    (unknown team).
  - **fan-out:** append one `OperativeClue` per teamId (mirror `ReleaseClueToAllTeams`).
  - **does not** advance the substage or resolve any target.
  - raises `OperativeClueAddedEvent` per assigned team (mirror `ClueReleasedEvent`) — carried
    for optional board re-projection; **no SignalR gate** (see X.4).
- Private `List<OperativeClue> _operativeClues` + `GetOperativeClues()` accessor (mirror
  `_clueReleaseRecords` / `GetClueReleaseRecords`, `LiveSession.cs:17,435`).
- Extend `CollectVisibleClues(Guid teamId)` (`LiveSession.cs:810`) to **append** the operative
  clues whose `TeamId == teamId` to the projected visible clues — per team, so an operative
  clue assigned to one team never leaks to another (the no-leak property is realized here).
- New exceptions: `OperativeClueTextRequiredException`,
  `OperativeClueRequiresAtLeastOneTeamException`, `SessionNotLiveForOperativeClueException`
  — mirror `LiveSessionTitleRequiredException` / `SessionNotActiveForClueReleaseException`.

**Target files** (create | edit — file to mirror):
- create `src/Domain/Entities/OperativeClue.cs` — mirror `src/Domain/Entities/ClueReleaseRecord.cs`
- create `src/Domain/Events/OperativeClueAddedEvent.cs` — mirror `src/Domain/Events/ClueReleasedEvent.cs`
- edit `src/Domain/Entities/LiveSession.cs` — add `_operativeClues`, `AddOperativeClue`,
  `GetOperativeClues`; extend `CollectVisibleClues` to include operative clues per team
- create `src/Domain/Exceptions/{OperativeClueTextRequiredException,OperativeClueRequiresAtLeastOneTeamException,SessionNotLiveForOperativeClueException}.cs`
  — mirror existing `src/Domain/Exceptions/*`

**Pattern this phase owns:** none.
**Gate:** unit test per new domain type; `AddOperativeClue` enforces `State ∈ {Active, Paused}`
(else rejected), non-empty `clueText`, non-empty/known `teamIds`; fan-out writes one
`OperativeClue` + one `OperativeClueAddedEvent` per assigned team; authoring **does not**
advance the substage or resolve a target; an operative clue surfaces via `CollectVisibleClues`
**only** for its assigned team (no leak).

### Phase X.2 — Application
**Derive** (mirror `AssignOperatorToSession` write slice; ownership guard = existing
`ISessionAdministrationAccessResolver`, ADR-0009 — inherited, not a new pattern):
- `AddOperativeClueCommand(Guid LiveSessionId, string ClueText, IReadOnlyList<Guid> TeamIds)`
  : `IRequest<AddOperativeClueResultDto>`, `[Authorize(Roles = "Operator")]`.
- `AddOperativeClueCommandHandler` — resolve the authorized session via
  `resolver.GetAuthorizedSessionAsync(liveSessionId, ct)` (existing Proxy — Administrator all /
  Operator owns-else-`ForbiddenAccessException`), apply
  `liveSession.AddOperativeClue(command.ClueText, command.TeamIds, actor.UserId, now)`,
  `_liveSessionRepository.UpdateAsync(...)`, return `AddOperativeClueResultDto`. **Plain
  handler — no Facade** (mirror `AssignOperatorToSessionCommandHandler`, *not*
  `ClueReleaseFacade`).
- `AddOperativeClueCommandValidator` — `LiveSessionId` `NotEmpty()`; `ClueText` `NotEmpty()`
  + a sane `MaximumLength(...)`; `TeamIds` `NotEmpty()`.
- `AddOperativeClueResultDto` — `operativeClueIds` + `assignedTeamIds` + `clueText`, in
  `Dtos/Sessions/`.

**Target files** (create | edit — file to mirror):
- create `src/Application/Sessions/Commands/AddOperativeClue/{AddOperativeClueCommand,AddOperativeClueCommandHandler,AddOperativeClueCommandValidator}.cs`
  — mirror `src/Application/Sessions/Commands/AssignOperatorToSession/*`
- create `src/Application/Dtos/Sessions/AddOperativeClueResultDto.cs` — mirror
  `src/Application/Dtos/Sessions/AssignOperatorToSessionResultDto.cs`
- (no facade, no new DI registration — the handler is discovered by MediatR like the
  `AssignOperatorToSession` handler)

**Pattern this phase owns:** none. (Access uses the **existing** ownership resolver — inherited
guard, not a new gate.)
**Gate:** handler tests — assigned operator authors a clue for one team and for several teams
(fan-out); non-owning operator → `ForbiddenAccessException` (via the existing resolver, **no
ad-hoc role/owner `if`**); authoring rejected when the session is not Active/Paused; authoring
does not advance the substage; `[Authorize(Roles="Operator")]` present.

### Phase X.3 — Infrastructure
**Derive:** persist `OperativeClue` as an EF **`OwnsMany`** collection on `LiveSession` (table
`live_session_operative_clues`, FK `live_session_id`, key `OperativeClueId`, columns
team_id / clue_text / created_by_user_id / created_at). Mirror the `ClueReleaseRecord`
`OwnsMany` configuration. No stale schema.

**Target files** (create | edit — file to mirror):
- edit `src/Infrastructure/Persistence/Configurations/LiveSessionConfiguration.cs` — add
  `OwnsMany(OperativeClues)` mirroring the `ClueReleaseRecord` `OwnsMany`
- create a migration under `src/Infrastructure/Persistence/Migrations/`
  (`ef migrations add AddOperativeClues`) — none exists for this collection yet
- edit `tests/IntegrationTests/Persistence/LiveSessionRepositoryIntegrationTests.cs` (or a
  sibling) — round-trip a `LiveSession` carrying operative clues
- grep `OperativeClue`/`operative_clue` in `ApplicationDbContextModelSnapshot.cs` to confirm
  the table — **do not full-read** the snapshot

**Pattern this phase owns:** none.
**Gate:** `ef migrations add` succeeds and represents the new owned collection; the repository
integration test proves round-trip of `LiveSession` + `OperativeClue` rows (team/clueText/
createdBy/createdAt persisted); no regression to the existing snapshot/team/clue-release schema.

### Phase X.4 — Api
**Derive:** `POST /api/sessions/{liveSessionId}/operative-clues` operator endpoint
(`[Authorize(Policy = AuthorizationPolicies.Operator)]` — the standard endpoint policy, not a
new pattern), request record `AddOperativeClueRequest(string ClueText, IReadOnlyList<Guid>
TeamIds)` → `AddOperativeClueCommand`. Map the new domain exceptions in
`ProblemDetailsExceptionHandler` (text-required / no-teams → 400 or 422; not-live → 409;
unknown team → 404 or 409, matching the existing `TeamNotFoundException` mapping).
**SignalR: not mandated** (matrix line 59). The operative clue reaches the participant board
through the existing `CollectVisibleClues` projection on the next board read; **optionally**,
an Application notification handler for `OperativeClueAddedEvent` may re-project + push the
assigned team's board via the existing `ITeamBoardBroadcaster` (`team:{teamId}`) mirroring
`BroadcastTeamBoardNotificationHandler` — build it only if the live reveal is wanted, and do
**not** gate the phase on it.

**Target files** (create | edit — file to mirror):
- edit `src/Api/Controllers/SessionsController.cs` — add `AddOperativeCluesAsync` + inline
  `AddOperativeClueRequest` record; mirror `AssignOperatorAsync` / `ReleaseCluesAsync`
- edit `src/Api/Services/ProblemDetailsExceptionHandler.cs` — map the new exceptions
- _(optional, non-gated)_ create
  `src/Application/Sessions/EventHandlers/BroadcastOperativeClueAddedNotificationHandler.cs`
  — mirror `BroadcastTeamBoardNotificationHandler.cs`, only if the live reveal is built
- add endpoint integration tests mirroring the HU-26 / HU-24A endpoint test style

**Pattern this phase owns:** none. (Endpoint uses the standard Operator policy + the existing
ownership resolver — inherited guard, not a mandated-pattern gate.)
**Gate:** endpoint integration tests — assigned operator authoring for one team **200** and for
several teams **200**; non-owning operator **403** (RFC 7807); empty clueText / empty teamIds →
400 or 422 (ProblemDetails); authoring on a non-Active/Paused session → 409; authoring does
**not** advance the substage; the operative clue appears on an **assigned** team's board and
**not** on a non-assigned team's (no leak);
`backend/docs/adr/0005-coverlet-msbuild-for-aggregate-coverage.md` coverage gate passes.

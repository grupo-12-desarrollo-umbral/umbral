# HU-23 Context — Tablero de equipo en vivo

> Paste this section into any agent session that needs context for HU-23 (DES-31).
> Last updated: 2026-07-11 | Branch: `feature/hu-23-live-team-board`

## State

- DES-31 (HU-23): **Todo**, labels: `canon-realign`, `svc:session-operations-service`, `Feature`, `ready-for-agent`. Both required labels are present.
- **Resolved mode: feature flow with comment-only realignment.** DES-31 carries `canon-realign` but not `needs-rebuild`, and it is not in the realignment map's superseded column. It is Bucket 2b: new build, but the issue-body AC is stale where it says progress is based on "pistas habilitadas". The realignment comment says the board must show **target-based active-substage progress + optional visible clues**, and the timer comes from DES-77/HU-22.
- **Superseded handling applied:** DES-23, DES-28, DES-30, and DES-44 are superseded by DES-75, DES-76, DES-77, and DES-78. DES-47 is merged into DES-46. Do not cite the superseded originals as predecessors.
- Predecessor DES ids (build-on, Done/merged): **DES-22 (HU-15)**, **DES-24 (HU-17)**, **DES-25 (HU-18)**, **DES-76 (HU-21A)**, **DES-77 (HU-22)**, **DES-78 (HU-33A)**, **DES-86 (per-target score refactor)**, **DES-11/DES-12 (HU-07A/HU-07B)**. Light build-on / mirror only: **DES-49 (HU-36A)** for read-projection shape, **DES-46 (HU-34)** for operator-only live-update precedent. Landed-untouched: DES-26 (HU-19), DES-27 (HU-20), DES-45 (HU-33B).
- PRD DES id: **DES-70** -> `backend/docs/prd/DES-70-primera-implementacion-de-session-operations-service-hu-15-a-hu-36.md` (local file is authoritative; never re-fetch PRD scope from Linear).
- Realignment inputs: `backend/docs/canon-realignment-after-mission-runtime-rewrite.md` lines 78, 102, 109, 206-208; DES-31 comment `⚠️ Deuda de canon menor`; `backend/services/session-operations-service/CONTEXT.md` §TargetProgression, §TreasureHuntTargetScore, §TriviaQuestionTimer, §Runtime Authority.
- Blocked by in Linear: DES-77 (HU-22) is Done. No same-service In Progress predecessor -> branch base **`develop`**.
- Branch: `feature/hu-23-live-team-board`, base **`develop`**.

## Required design patterns

No mandated pattern from `backend/docs/adr/0004-required-domain-patterns.md` for HU-23.

- Matrix row: `required_patterns_matrix.md` lines 105-113 marks `HU-23` as `—` with `SignalR` transport and explains: "Live team board — real-time read projection; `Proxy` scopes it to the team." This is **not** a new pattern mandate under generator-agent's Proxy nuance; it is a protected read that inherits the standard participant authorization/runtime guard.
- Transport: **SignalR / WebSockets** is a hard transport gate (`required_patterns_matrix.md:57,111`). The board must update without manual reload over the existing `SessionsHub` groups.
- Applies-where note (no new gate): the participant board endpoint and team SignalR updates expose team-only resources. They must pass through the existing participant runtime guard / gateway authorization (`IRuntimeParticipationGuard`, `[Authorize(Participant)]`, hub team group membership), but do **not** introduce a new `Proxy` phase gate.

## What predecessors have already landed

**Build-on (read for derivation):**

- **DES-22 / HU-15 and DES-24 / HU-17 — session creation + mission-only source.** `LiveSession.Create(...)` owns the immutable `MissionRuntimeSnapshot`; `MissionRuntimeSnapshot` includes ordered stages/substages, `TargetSnapshot`s, `TriviaQuestionSnapshot`s, and no session-level `SessionMode`. HU-23 reads from the frozen snapshot; it never re-derives runtime content from `MissionDesign`.
- **DES-25 / HU-18 — runtime teams.** `LiveSession.Teams` and `Team` are the runtime team state. `Team.CurrentScore`, `CurrentProgressNodeId`, `CurrentClueNodeId`, and `ReleasedClueCount` exist as early board fields, but current score is not an authoritative scoring ledger. Treat them as reuse candidates for snapshot shape, not as proof that scoring is complete.
- **DES-76 / HU-21A — lifecycle.** The canonical `Scheduled -> Preparing -> Active -> Paused -> Finished -> Cancelled` state machine and `SessionStateChanged` SignalR broadcast are landed. HU-23 board reads are valid for `Active` and `Paused`; terminal states can be read for last-known board state.
- **DES-77 / HU-22 — timer.** The authoritative timer is already exposed through `SessionTimerSnapshotDto` and participant/operator timer queries. HU-23 reuses that DTO/factory semantics rather than building a second timer.
- **DES-78 / HU-33A — active substage pointer and synchronized trivia.** `LiveSession.ActiveSubstageId` identifies the active substage, `ActiveQuestionIndex` identifies the active trivia question, and SignalR broadcasts already target `live-session:{id}`. HU-23 consumes the pointer to compute active-substage progress and active content context.
- **DES-86 — per-target score refactor.** `TargetSnapshot.Score` is now the per-target `ScoreValue`; `SubstageSnapshot.WinnerScore` is gone. HU-23's score/progress wording must not reintroduce winner-takes-all substage score.
- **DES-11/DES-12 — participant admission/reconnect.** Participant-callable runtime paths use participant authorization and reconnection seams. HU-23 mirrors `GetParticipantSessionTimerSnapshot` and `IRuntimeParticipationGuard` so a participant sees only their own team board.

**Light build-on / mirror only:** DES-49 (HU-36A) landed a CQRS read projection over `LiveSession` with DTOs in `Application/Dtos/Sessions/`, query files under `Application/Sessions/Queries/<UseCase>/`, and an Api controller route; use it as the shape for a board snapshot. DES-46 (HU-34) landed `TeamAnswered` as an operator-only live pulse; HU-23 should add/reuse a participant-team board pulse, not leak operator-only signals.

**Landed, untouched by this HU:** DES-26 (HU-19) operator assignment, DES-27 (HU-20) assigned-session reads, DES-45 (HU-33B) RabbitMQ publication. They are not board dependencies except for existing common authorization/read infrastructure.

**Superseded / not predecessors:** DES-23, DES-28, DES-30, DES-44, DES-47. Never anchor implementation on their pre-canon AC or code shape.

**Coverage:** measure the real `session-operations-service` percentage against `backend/docs/adr/0005-coverlet-msbuild-for-aggregate-coverage.md` at X.4; do not assume a carried-forward value.

## What this HU adds

| Concern | New work |
|---|---|
| Participant team board read | New team-scoped read projection for one participant's runtime team in one `LiveSession`. |
| Score display | Show accumulated team score as available from current session-owned team state, with an explicit caveat: full score ledger/ranking belongs to `ScoringMonitoring` (HU-37/HU-39). Before that ledger exists, the board may report zero or session-owned accumulated fields only. |
| Target-based progress | For active treasure-hunt substages, progress is targets resolved / total active targets in the active substage, not clues completed. If target-resolution persistence is absent, add the minimal session-owned projection seam needed for later HU-31 to feed; do not fake progress from clue visibility. |
| Optional visible clues | Show optional clue guidance that is already visible/released to the team; clues help but do not advance progress or resolve targets. |
| Timer reuse | Include the existing participant timer snapshot (`SessionTimerSnapshotDto`) so trivia shows active-question remaining time and treasure-hunt has no invented countdown. |
| Real-time updates | Add/reuse SignalR delivery so board state updates without manual reload when timer/session/team/progress/clue facts change. |
| Backend contract | New participant-only `GET /api/sessions/{liveSessionId}/participants/team-board?teamId=...&token=...` returning the board snapshot, plus a team-scoped SignalR board update payload. |
| Frontend/mobile contract | Participant board UI consumes the snapshot + live board updates; both backend and frontend must preserve team scoping. |

## Touched surfaces

- `backend/services/session-operations-service/` — domain (board projection method / value objects only, no new aggregate), application (participant query slice + DTO/factory + optional broadcaster port), infrastructure (repository include/read verification; migration only if minimal board state is genuinely missing), api (participant GET route + SignalR board broadcaster/hub test).
- `frontend/` or mobile participant board surface — live team board UI with score, timer, active target progress, visible clues.
- API/runtime contract boundary: new participant board HTTP response and SignalR update payload; timer semantics reused from HU-22; score is session-owned/current until `ScoringMonitoring` ledger is available.

## Committed phases

| Commit | Phase | Description |
|---|---|---|
| — | X.1 Domain | No commits yet |
| — | X.2 Application | No commits yet |
| — | X.3 Infrastructure | No commits yet |
| — | X.4 Api | No commits yet |

## Known quirks / gotchas

- **Canon-debt correction:** do not implement "progress = enabled clues". DES-31's comment redefines the board to **target-based active-substage progress + optional visible clues**.
- **Score ambiguity:** the AC says accumulated team score, but authoritative score ledger/ranking is `ScoringMonitoring` scope (HU-37/HU-39). The Linear cycle break explicitly says DES-31 can show zero before the ledger exists. Record this in API docs and frontend plan; do not build `ScoringMonitoring` inside HU-23.
- **Do not invent a treasure-hunt countdown.** HU-22 resolved no treasure-hunt authoritative countdown. The board includes the timer snapshot; for treasure-hunt it may be frozen/zero/no active question rather than a fabricated remaining time.
- **Participant scoping is mandatory.** Mirror `GetParticipantSessionTimerSnapshotQueryHandler`: use `IRuntimeParticipationGuard.EnsureAllowedAsync(liveSessionId, teamId, token, ct)` before loading/returning board data.
- **Board updates must be team-scoped.** Participant connections join `team:{teamId}` in `SessionsHub.ReconnectAsync`; send board updates to that group, not to `live-session:{id}` if the payload contains team-only score/progress/clues.
- **No clue-authoring scope.** HU-26/HU-28 own clue release/runtime clue changes. HU-23 only reads visible optional clues already represented in session state/snapshot.
- **No target-resolution implementation unless only the board projection seam is missing.** HU-31 owns QR target validation/resolution. HU-23 may define/read a `TargetProgress` projection seam, but must not build QR scan validation or evidence intake.
- **Application structure:** request/query files under `Application/Sessions/Queries/GetParticipantTeamBoard/`; response DTOs under `Application/Dtos/Sessions/`; shared-by-2+ mappers/broadcasters under `Application/Sessions/Common/`. No `Handlers/`, `DTOs/`, or `Facades/` buckets.

## Per-phase derivation — authoritative for implementation

> Primary source for the phase subagent (per `backend-agent.md` "Read first"). Do not re-read the full canon; open a cited section only to fill a gap this block leaves open.
> Canon: DES-31 comment (`⚠️ Deuda de canon menor`) says board = target-based progress + optional visible clues; `canon-realignment-after-mission-runtime-rewrite.md:27-30,78,109`; `CONTEXT.md:75-88,117-130,133-146,177-178,195-196`; PRD DES-70 US17/18 (`:107-110`) and operational read decisions (`:220-223`). Pattern: none mandated; transport SignalR (`required_patterns_matrix.md:57,111`). Structure: `backend/structure.md:67-92`.

### Phase X.1 — Domain
**Derive** (`CONTEXT.md:75-88,117-130,133-146,177-178`; `bd_umbral_entity_spec.md` §Target/§Clue lines 104-161; DES-31 canon comment):
- Add a read-only team-board projection on `LiveSession` for `(teamId, observedAt)`: resolves the runtime `Team`, active substage (`ActiveSubstageId`), ordered active-substage targets from `MissionRuntimeSnapshot.TargetSnapshots`, optional visible clues from target/clue snapshot fields plus any release state that exists, and the current authoritative timer snapshot.
- For treasure-hunt active substage, progress = target-resolution facts for that team over active targets / total active targets. If no target-resolution fact collection exists yet, return 0 resolved and expose total active targets; do **not** infer progress from `CurrentClueNodeId` or `ReleasedClueCount`.
- For trivia active substage, include active question identity from the existing timer snapshot; target progress can be absent/zero because trivia progress rides question timer/answer flows.
- Score = current session-owned team score field when present, otherwise 0. Do not compute score ledger, ranking, penalties, or `SessionTeamWinner`.

**Target files** (create | edit — file to mirror):
- edit `src/Domain/Entities/LiveSession.cs` — add `ProjectParticipantTeamBoard(Guid teamId, DateTimeOffset observedAt)`; mirror `ProjectActiveQuestionAnsweredStatus()` shape but team-scoped and option-free
- create `src/Domain/ValueObjects/ParticipantTeamBoardSnapshot.cs` and small nested value objects if needed — mirror `TriviaAnsweredMonitorSnapshot.cs` / `TeamAnsweredStatus.cs`
- edit `tests/UnitTests/Domain/Entities/LiveSessionTests.cs` or create `LiveSessionTeamBoardTests.cs` — mirror HU-36A projection tests

**Pattern this phase owns:** none.
**Gate:** Domain build passes; unit tests prove: board resolves only the requested team; score defaults to 0/current team score; treasure-hunt progress counts active targets and never clues; optional visible clues are shown as guidance only; trivia board includes active-question/timer context without target progress; no score ledger/ranking calculation is introduced.

### Phase X.2 — Application
**Derive** (`PRD DES-70:107-110,220-223`; `GetParticipantSessionTimerSnapshotQueryHandler.cs`; `IRuntimeParticipationGuard.cs`; `SessionTimerSnapshotDtoFactory.cs`):
- Add `GetParticipantTeamBoardQuery(liveSessionId, teamId, token)` under `Application/Sessions/Queries/GetParticipantTeamBoard/`.
- Handler first calls `IRuntimeParticipationGuard.EnsureAllowedAsync(liveSessionId, teamId, token, ct)`, then loads the session, calls the domain projection, maps to `ParticipantTeamBoardDto`, and includes/reuses `SessionTimerSnapshotDto` for timer context.
- Add a board-update broadcaster port only if X.4 needs one for SignalR; keep the port in `Application/Common/Interfaces` or `Application/Sessions/Common/` following existing broadcaster interfaces.

**Target files** (create | edit — file to mirror):
- create `src/Application/Sessions/Queries/GetParticipantTeamBoard/GetParticipantTeamBoardQuery.cs`
- create `src/Application/Sessions/Queries/GetParticipantTeamBoard/GetParticipantTeamBoardQueryHandler.cs` — mirror `GetParticipantSessionTimerSnapshotQueryHandler.cs`
- create `src/Application/Dtos/Sessions/ParticipantTeamBoardDto.cs` — central DTO root, mirror `TriviaAnsweredMonitorDto.cs` / `SessionTimerSnapshotDto.cs`
- create `src/Application/Sessions/Common/ParticipantTeamBoardDtoFactory.cs` — mirror `TriviaAnsweredMonitorDtoFactory.cs`
- edit `src/Application/Common/Interfaces/ITeamBoardBroadcaster.cs` only if needed for X.4 live push
- create `tests/Application.UnitTests/Sessions/Queries/GetParticipantTeamBoard/GetParticipantTeamBoardQueryHandlerTests.cs`

**Pattern this phase owns:** none; participant scoping inherits existing runtime guard.
**Gate:** Application build passes; handler tests assert runtime guard is called before data is returned, NotFound maps through existing `NotFoundException`, DTO maps score/progress/visible clues/timer, and no ad-hoc participant/team authorization check appears in the handler.

### Phase X.3 — Infrastructure
**Derive** (`LiveSessionRepository.cs:18-35`; `LiveSessionConfiguration.cs`; DES-31 comment; DES-86 per-target score handoff):
- Ensure repository reads for the board hydrate teams, members, mission snapshot stages/substages/targets/questions, and whatever existing clue/target progress state the domain projection uses.
- If the domain projection only reads already-persisted fields, no migration. If implementation discovers there is no persisted session-owned state for target progress or visible clue release, add only the minimal session-owned projection storage needed for HU-23 to read; do not implement QR validation/evidence intake.
- Keep `TargetSnapshot.Score` as per-target score; do not resurrect `WinnerScore` or substage-level scoring columns.

**Target files** (create | edit — file to mirror):
- edit `src/Infrastructure/Persistence/Repositories/LiveSessionRepository.cs` — add `GetBoardSessionByIdAsync` or reuse `GetByIdAsync` if sufficient; mirror include style in `GetByIdAsync`
- edit `src/Application/Common/Interfaces/ILiveSessionRepository.cs` if a board-specific read method is added
- edit `src/Infrastructure/Persistence/Configurations/LiveSessionConfiguration.cs` only if new minimal board state is required
- create migration under `src/Infrastructure/Migrations/` **only if** new persisted board state is required; otherwise explicitly no-op/no migration
- add/extend integration tests under `tests/IntegrationTests/Infrastructure/` or API factory tests to prove board read hydrates target score/progress inputs

**Pattern this phase owns:** none.
**Gate:** Infrastructure build passes; repository/integration test proves board read loads teams + active-substage target/question snapshot data + persisted progress/clue state; model snapshot has no `winner_score` / substage winner-score regression; migration is absent unless justified by missing minimal board state.

### Phase X.4 — Api
**Derive** (`SessionsController.cs` participant timer endpoint lines 183-196; `SessionsHub.cs` team group lines 35-53, 117; `required_patterns_matrix.md:57,111`; ADR coverage path `backend/docs/adr/0005-coverlet-msbuild-for-aggregate-coverage.md`):
- Add participant-only endpoint `GET /api/sessions/{liveSessionId}/participants/team-board?teamId={teamId}&token={token}` returning `ParticipantTeamBoardDto`.
- Add/verify SignalR board updates over the existing team group `team:{teamId}`. If a new broadcaster is introduced, implement it in `Api/Hubs/` like existing SignalR broadcasters and keep payload team-scoped.
- Keep hub membership in `SessionsHub.ReconnectAsync` as the authorization boundary; do not let operators or other teams receive participant board payloads.

**Target files** (create | edit — file to mirror):
- edit `src/Api/Controllers/SessionsController.cs` — mirror `GetParticipantTimerSnapshotAsync`
- create/edit `src/Api/Hubs/SignalRTeamBoardBroadcaster.cs` if a broadcaster port was added — mirror `SignalRSessionTimerBroadcaster.cs` / `SignalRTeamAnsweredBroadcaster.cs`
- keep `src/Api/Hubs/SessionsHub.cs` group names; only edit if a join change is strictly needed
- create `tests/IntegrationTests/Api/ParticipantTeamBoardEndpointTests.cs` — mirror `ParticipantSessionTimerSnapshotEndpointTests.cs`
- create/extend hub test for team-scoped board update delivery

**Pattern this phase owns:** none new; standard `[Authorize(Participant)]` + runtime guard. **SignalR transport gate verified here.**
**Gate:** endpoint integration tests cover 200 authorized participant, 403/ProblemDetails for unauthorized team/token, and response shape (score, timer, target progress, visible clues); SignalR hub test proves board update reaches `team:{teamId}` and not a different team; `backend/docs/adr/0005-coverlet-msbuild-for-aggregate-coverage.md` coverage gate passes.

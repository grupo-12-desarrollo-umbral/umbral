# HU-36A Context — Monitoreo restringido de respondido/no respondido en trivia

> Paste this section into any agent session that needs context for HU-36A.
> Last updated: 2026-07-10 | Branch: `feature/hu-36a-trivia-answered-monitor`

## State

- DES-49 (HU-36A): **Todo**, labels: `svc:session-operations-service`, `Feature`, `ready-for-agent`
- **Resolved mode: feature flow.** The service went through the mission-runtime
  realignment cycle, but DES-49 is **not** in the supersession table's "old" column
  — it is forward, un-superseded work re-pointed off canceled DES-44 onto the
  rebuild DES-78 (`canon-realignment-after-mission-runtime-rewrite.md` line 198).
  No `canon-realign` / `needs-rebuild` label → not a rebuild ticket.
- **Superseded predecessors dropped / substituted** (realignment map): DES-44 (HU-33A)
  → **DES-78**; DES-30 (HU-22) → **DES-77**; DES-28 (HU-21A) → **DES-76**; DES-23
  (HU-16) → **DES-75**; DES-47 (HU-34B) → merged into **DES-46**. Predecessor scope
  below is anchored on the rebuild ids only — never on the canceled originals.
- Build-on predecessor DES ids: **DES-46 (HU-34)**, **DES-78 (HU-33A)**, **DES-77 (HU-22)**,
  session-creation/operator-assignment reads (ADR-0009 ownership seam)
- PRD DES id: **DES-70** (local file authoritative:
  `backend/docs/prd/DES-70-primera-implementacion-de-session-operations-service-hu-15-a-hu-36.md`)
- Branch: `feature/hu-36a-trivia-answered-monitor` — base `develop` (no same-service
  predecessor is currently In Progress; DES-46 and DES-78 are Done)

## Required design patterns

- `Proxy` **(mandated — `required_patterns_matrix.md` HU-36 row, line 134)**
  - Why: "Restricted answer monitoring (answered-or-not before close, full review
    after) is a **guarded projection** updated live." The pre-close monitor exposes
    protected supervision data (which teams answered) that must be gated to the
    session's assigned operator and must never carry the chosen option.
  - Phase owner: **X.2 Application** (application-slice resolver proxy) **and
    X.4 Api** (endpoint authorization policy + hub-join ownership guard).
  - Concrete obligation: the monitoring read passes through the existing
    resource-ownership resolver Proxy
    (`ISessionAdministrationAccessResolver` / `SessionAdministrationAuthorizationProxy`,
    ADR-0009 + ADR-0012 §"resource-ownership guard") — Administrator sees all,
    Operator only where `LiveSession.AssignedOperatorUserId == actor.UserId`, else
    `ForbiddenAccessException`. **No ad-hoc role/owner `if` checks** in the handler,
    controller, or hub. Coarse role gate stays `[Authorize(Roles="Operator")]` via
    `AuthorizationBehaviour`.
- Transport: **SignalR** (reused, not re-added). The operator-only live pulse already
  exists from HU-34 (`live-session-operators:{id}` group + `TeamAnswered` event). HU-36A
  adds the **initial-snapshot query** so an operator connecting mid-question or refreshing
  sees the full roster; the event keeps it live thereafter. No participant-group broadcast.

## What predecessors have already landed

HU-36A is a **CQRS read surface over the one aggregate `LiveSession`** (PRD lines 173–174,
222). Nearly all runtime + transport it needs already landed; it adds a projection + read
slice, not new runtime state.

**DES-46 / HU-34 — trivia answer registration + operator-only answered transport (build-on, PRIMARY)**
- Domain: `Domain/Entities/EvidenceSubmission.cs` (umbrella) + `Domain/Entities/TriviaAnswerSubmission.cs`
  (trivia specialization: `QuestionSequenceOrder`, `SelectedOptionSequenceOrder`, `IsCorrect`, `ScoreValue`);
  built via `TriviaAnswerSubmission.Accept(...)`. A snapshotted question has **no Guid** — it is keyed by
  the pair `(ActiveSubstageId, QuestionSequenceOrder)`.
- `Domain/Entities/LiveSession.cs`: read-only `TriviaAnswerSubmissions` collection (the accepted-answer
  store HU-36A projects); `RegisterTriviaAnswer(...)`; fact `Domain/Events/AnswerRegisteredEvent.cs`.
- Transport **already operator-only and option-free**: `Application/Common/Interfaces/ITeamAnsweredBroadcaster.cs`,
  bridge `Application/Sessions/EventHandlers/TeamAnsweredNotificationHandler.cs`, DTO
  `Application/Sessions/Common/Notifications/TeamAnsweredNotificationDto.cs` = `(LiveSessionId, TeamId,
  TriviaSubstageSnapshotId, QuestionSequenceOrder, AnsweredAt)` — **drops** `IsCorrect`/`ScoreValue`/option
  by design. Hub impl `Api/Hubs/SignalRTeamAnsweredBroadcaster.cs`: client event `"TeamAnswered"`,
  operator-only group `live-session-operators:{id}` via `BuildOperatorGroup(Guid)`.
- **Boundary (hu34-context.md:122): HU-34 owns the answered *fact* + its transport; HU-36A owns the full
  pre-close answered/not-answered *dashboard projection*.** HU-34 emits only the transient live pulse — there
  is no query for the current per-team answered/not-answered snapshot. That is HU-36A's gap.

**DES-78 / HU-33A — synchronized trivia substage orchestration (build-on)**
- Active-question model on `Domain/Entities/LiveSession.cs`: `ActiveSubstageId` (nullable Guid),
  `ActiveQuestionIndex` (nullable int), `ResolveActiveTriviaQuestion()` (rejects non-trivia substage /
  no active question). `SynchronizedTriviaQuestion` = the single active snapshotted `TriviaQuestion` in the
  authoritative timer window (CONTEXT.md:141–143).
- Snapshot value objects: `Domain/ValueObjects/TriviaQuestionSnapshot.cs` (keyed to substage by
  `SubstageSnapshotId`; `SequenceOrder`, `Options`, `TimeLimitSeconds`, `ScoreValue`),
  `Domain/ValueObjects/SubstageSnapshot.cs`, off immutable `Domain/Entities/MissionRuntimeSnapshot.cs`.
- Orchestration facade `Application/Sessions/Common/TriviaRoundOrchestratorFacade.cs`; question broadcasts
  `QuestionActivated`/`QuestionClosed`/`SubstageAdvanced` to the participant-shared `live-session:{id}` group.

**DES-77 / HU-22 + operator reads — operator-ownership guard & the Query to mirror (build-on)**
- Ownership resolver Proxy (ADR-0009): `Application/Common/Interfaces/ISessionAdministrationAccessResolver.cs`
  + impl `Application/Sessions/Common/Authorization/SessionAdministrationAuthorizationProxy.cs` — Administrator
  all / Operator `AssignedOperatorUserId == actor.UserId` else `ForbiddenAccessException`. Actor resolved via
  `IAuthenticatedActorProfileAccessClient` (`/api/users/me`).
- **Query to mirror**: `Application/Sessions/Queries/GetOperatorSessionTimerSnapshot/` (`Query` +
  `Handler`), `[Authorize(Roles="Operator")]`, injects the resolver, calls `GetAuthorizedTimerSessionAsync`,
  projects via a static DTO factory; result DTO in `Application/Dtos/Sessions/`. Controller
  `Api/Controllers/SessionsController.cs` `[HttpGet("{liveSessionId:guid}/timer")]`
  `[Authorize(Policy = AuthorizationPolicies.Operator)]`.

**SignalR infrastructure (reused as-is)** — single hub `Api/Hubs/SessionsHub.cs` at `/hubs/sessions`;
`JoinLiveSessionAsOperatorAsync(Guid)` enforces `EnsureOperatorCaller()` + the ownership resolver **at join**
then joins `live-session-operators:{id}`. Token via trusted gateway headers (ADR-0001/0002).

**Other landed session-ops slices — untouched by this HU**: session lifecycle transitions (HU-21A/DES-76),
session creation/snapshot (HU-16/DES-75), team lobby/assignment reads. Landed; HU-36A reads none of their
state beyond the `AssignedOperatorUserId` ownership seam.

**Coverage:** verify real `session-operations-service` aggregate coverage against the repo gate (ADR-0005)
during phase X.4; no predecessor doc records a stable %.

## What this HU adds

| Concern | New work |
|---|---|
| Answered projection | A domain read over the active `(ActiveSubstageId, ActiveQuestionIndex)` question that marks each associated `Team` answered vs not-answered. |
| No-leak invariant | The projection type structurally carries **no** `SelectedOptionSequenceOrder`, `IsCorrect`, or `ScoreValue` — the option cannot be revealed before close. |
| Operator-guarded read | New CQRS query `GetOperatorTriviaAnsweredMonitor`, gated by the existing ownership resolver Proxy — operator sees only their assigned session. |
| Live without reload | Initial-snapshot query on connect/refresh; the existing operator-only `TeamAnswered` event keeps the board live (no new broadcaster). |
| Read verification | No new persisted state or migration — the read rides HU-34's persistence; X.3 verifies the aggregate read hydrates teams + accepted answers. |
| Backend contract | New operator-only `GET /api/sessions/{liveSessionId}/answered-monitor` returning per-team answered/not-answered for the active question. |
| Frontend flow | Operator answered/not-answered board (web) subscribing to the snapshot + `TeamAnswered` event; never shows the chosen option pre-close. |

## Touched surfaces

- `backend/services/session-operations-service` (Domain read method + VO, Application query slice, Api endpoint)
- `frontend/` operator monitoring board (web)
- `backend/frontend` API contract boundary: `GET /api/sessions/{liveSessionId}/answered-monitor`
  response shape + the reused operator-only `TeamAnswered` SignalR event shape
- No migration; no RabbitMQ; no new SignalR group or broadcaster

## Committed phases

| Commit | Phase | Description |
|---|---|---|
| — | X.1 Domain | (no commits yet) |
| — | X.2 Application | (no commits yet) |
| — | X.3 Infrastructure | (no commits yet) |
| — | X.4 Api | (no commits yet) |

## Known quirks / gotchas

- **Snapshotted questions have no Guid** — identity is the pair `(ActiveSubstageId, QuestionSequenceOrder)`.
  The projection must filter `LiveSession.TriviaAnswerSubmissions` on that pair, not on a question id.
- **Answered = an accepted `TriviaAnswerSubmission` exists** for the team on the active question. Teams with
  none are "not answered yet" — derive not-answered by enumerating `LiveSession`'s teams, not by absence in a
  list.
- **Reuse the transport; do not add one.** The operator-only group `live-session-operators:{id}`, the
  `TeamAnswered` event, and `SignalRTeamAnsweredBroadcaster` already exist and are already option-free. Adding
  a second broadcaster or sending to `live-session:{id}` re-introduces the participant-leak HU-34 closed.
- **Proxy, not inline checks.** Ownership goes through `ISessionAdministrationAccessResolver` (mirror
  `GetOperatorSessionTimerSnapshotQueryHandler`); a bare `if (role == Operator …)` in the handler/controller
  is the ceremony ADR-0012 forbids.
- **Paused sessions**: trivia answers are not accepted while `Paused`; the monitor reflects the frozen active
  question and resumes on the same question (CONTEXT.md:59–61). The read is valid during `Paused`.
- **Pre-close only.** Correctness/right-answer reveal is HU-35; post-close full answer + points review is
  HU-36B. HU-36A must expose neither.
- Applies-where note: HU-36A's endpoint inherits the standard gateway + `AuthorizationBehaviour` guard like any
  protected read (ADR-0001/0002); the **mandated** `Proxy` here is the resource-ownership resolver, which is a
  design obligation beyond that inherited coarse gate.

## Per-phase derivation — authoritative for implementation

> Primary source for the phase subagent (per `backend-agent.md` "Read first").
> Derived from `bd_umbral_entity_spec.md` §LiveSession/§Team/§TriviaAnswerSubmission,
> `ddd_solution_model.md` §SessionOperations (Proxy responsibility, read surfaces),
> `services/session-operations-service/CONTEXT.md` (`SynchronizedTriviaQuestion`, `Proxy`),
> ADR-0009 (operator ownership), ADR-0012 (Proxy placement), and predecessor context
> `hu34-context.md` / `hu33a-context.md`. Open a cited canon section only to fill a gap a block leaves open.

### Phase X.1 — Domain
**Derive** (`bd_umbral_entity_spec.md` §LiveSession 274–326, §Team 362–401, §TriviaAnswerSubmission 601–639;
CONTEXT.md `SynchronizedTriviaQuestion` 141–143):
- `LiveSession.ProjectActiveQuestionAnsweredStatus()` — domain read method returning a
  `TriviaAnsweredMonitorView`. For the active `(ActiveSubstageId, ActiveQuestionIndex)` question it enumerates
  the session's `Team`s and marks each answered iff an **accepted** `TriviaAnswerSubmission` exists for that
  `(TeamId, ActiveSubstageId, QuestionSequenceOrder)`. Rejects when there is no active trivia question / the
  active substage is not `Trivia` (mirror the guard in `ResolveActiveTriviaQuestion()`) →
  `NoActiveTriviaQuestionException` (or reuse the existing active-question guard exception if one exists).
- `TriviaAnsweredMonitorView` (value object): active question identity `(SubstageSnapshotId,
  QuestionSequenceOrder)` + ordered `IReadOnlyList<TeamAnsweredStatus>`.
- `TeamAnsweredStatus` (value object): `(TeamId, TeamCode|DisplayName, bool Answered, DateTimeOffset? AnsweredAt)`.
  **Deliberately no** `SelectedOptionSequenceOrder`, `IsCorrect`, or `ScoreValue` — the no-leak invariant is
  enforced by the type's shape, not by a mapping step.

**Target files** (create | edit — file to mirror):
- create `src/Domain/ValueObjects/TriviaAnsweredMonitorView.cs` — mirror `src/Domain/ValueObjects/TriviaQuestionSnapshot.cs` (VO style)
- create `src/Domain/ValueObjects/TeamAnsweredStatus.cs` — mirror `src/Domain/ValueObjects/TriviaOptionSnapshot.cs`
- edit `src/Domain/Entities/LiveSession.cs` — add `ProjectActiveQuestionAnsweredStatus()` beside `ResolveActiveTriviaQuestion()` (~line 386)
- create (only if no reusable guard exists) `src/Domain/Exceptions/NoActiveTriviaQuestionException.cs` — mirror an existing trivia guard exception

**Pattern this phase owns:** none.
**Gate:** unit tests on `LiveSession` — answered iff an accepted submission for the active
`(ActiveSubstageId, QuestionSequenceOrder)`; teams with none appear as not-answered; the view/status types
expose no option/correctness/score; method throws when there is no active trivia question. Domain build green.

### Phase X.2 — Application
**Derive** (`ddd_solution_model.md` §SessionOperations Proxy 461–462 + read surfaces 538–540; ADR-0009; ADR-0012 §resource-ownership guard):
- `GetOperatorTriviaAnsweredMonitorQuery(Guid LiveSessionId)` — `[Authorize(Roles="Operator")]` CQRS read.
- Handler injects `ISessionAdministrationAccessResolver`, calls `GetAuthorizedSessionAsync(liveSessionId, actor)`
  (**the Proxy** — loads + owner-checks + returns the authorized `LiveSession`, throwing `ForbiddenAccessException`
  on non-owner), then calls `session.ProjectActiveQuestionAnsweredStatus()` and maps to the result DTO. No
  ownership/role `if` in the handler.
- `TriviaAnsweredMonitorDto` (+ per-team item DTO) in `Application/Dtos/Sessions/`, mapped by a static factory —
  carries answered/not-answered + `AnsweredAt` + active-question identity, never the option.

**Target files** (create — mirror):
- create `src/Application/Sessions/Queries/GetOperatorTriviaAnsweredMonitor/GetOperatorTriviaAnsweredMonitorQuery.cs`
  — mirror `Queries/GetOperatorSessionTimerSnapshot/GetOperatorSessionTimerSnapshotQuery.cs`
- create `.../GetOperatorTriviaAnsweredMonitor/GetOperatorTriviaAnsweredMonitorQueryHandler.cs`
  — mirror `GetOperatorSessionTimerSnapshotQueryHandler.cs`
- create `src/Application/Dtos/Sessions/TriviaAnsweredMonitorDto.cs` — mirror `src/Application/Dtos/Sessions/SessionTimerSnapshotDto.cs`
- create `src/Application/Sessions/Common/TriviaAnsweredMonitorDtoFactory.cs` — mirror `Sessions/Common/SessionTimerSnapshotDtoFactory.cs`

**Pattern this phase owns:** `Proxy` (resource-ownership resolver — application side).
**Gate:** handler + unit tests — returns the projection for the assigned operator; **`ForbiddenAccessException`
for a non-owning operator**; the ownership decision runs through `ISessionAdministrationAccessResolver`
(no ad-hoc `if`); result DTO carries no option/correctness/score. App build green.

### Phase X.3 — Infrastructure
**Derive:** the projection reads `LiveSession.Teams` + `TriviaAnswerSubmissions` already persisted by HU-34; the
existing operator read path (mirror how `GetOperatorSessionTimerSnapshot` loads the aggregate) already hydrates
them. **No new persisted state → no migration.**
**Target files** (verify | edit):
- verify `src/Infrastructure/Persistence/` — the session read/repository used by the timer query hydrates
  `Team`s and accepted `TriviaAnswerSubmission`s for the active question; extend the read include only if a gap is proven.
- Confirm no migration: grep `ApplicationDbContextModelSnapshot.cs` for `TriviaAnswerSubmission` — the HU-34 model already exists; do **not** add a migration.

**Pattern this phase owns:** none.
**Gate:** infra build green; repository/integration test proves the answered/not-answered derivation
round-trips for a session with some teams answered and some not on the active question; **`ef migrations add`
is a no-op / not run** (no schema change); no new persisted field leaks the option.

### Phase X.4 — Api
**Derive** (ADR-0009 hub-join parity; ADR-0012 §Proxy Api note; ADR-0005 coverage):
- `GET /api/sessions/{liveSessionId:guid}/answered-monitor` on `Api/Controllers/SessionsController.cs`,
  `[Authorize(Policy = AuthorizationPolicies.Operator)]`, `sender.Send(new GetOperatorTriviaAnsweredMonitorQuery(liveSessionId))`.
- Map `NoActiveTriviaQuestionException` (and reuse `ForbiddenAccessException` mapping) in the ProblemDetails handler.
- Live updates ride the **existing** operator-only `TeamAnswered` event + `SessionsHub.JoinLiveSessionAsOperatorAsync`
  (whose ownership guard already gates the hub group) — add no broadcaster.

**Target files** (edit — mirror):
- edit `src/Api/Controllers/SessionsController.cs` — add the endpoint beside `[HttpGet("{liveSessionId:guid}/timer")]`
- edit `src/Api/Services/ProblemDetailsExceptionHandler.cs` (or the service's exception→ProblemDetails map) for the new exception

**Pattern this phase owns:** `Proxy` (endpoint authorization policy + the ownership resolver enforced in the
handler; hub-join ownership guard reused).
**Gate:** endpoint returns **200** with per-team answered/not-answered for the assigned operator; **403 (RFC 7807)**
for a non-owning operator; the payload never contains the chosen option, correctness, or points; the operator-only
monitor does not reach participant connections; service coverage meets the repo gate (ADR-0005).

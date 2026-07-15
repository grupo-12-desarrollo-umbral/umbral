# HU-38 Context - Aplicación de penalizaciones justificadas

> Paste this section into any agent session that needs context for HU-38.
> Last updated: 2026-07-15 | Branch: `feature/hu-38-justified-penalties`

## State

- DES-53 (HU-38): **Todo**, labels: `svc:scoring-monitoring-service`, `svc:session-operations-service`, `Feature`, `ready-for-agent`.
  - The **double `svc:` label is intentional and not co-ownership.** Per the ticket's 2026-07-12 clarification, the write is owned **only** by `scoring-monitoring-service` (canonical owner of `Penalty` + the `ScoreEntry` ledger, PRD DES-85 / `bd_umbral_entity_spec.md` §`Penalty`). `svc:session-operations-service` marks **only** the `Proxy` guard: the operator may penalize a team of a session **assigned to him**, and that assignment lives in session-operations. HU-38 does **not** write session-operations.
- **Resolved mode: feature flow.** DES-53 carries neither `canon-realign` nor `needs-rebuild`. No realignment map exists for `scoring-monitoring-service`; DES-53 is **not** superseded (not folded, not cancelled). No superseded predecessor ids were dropped or substituted.
- PRD: DES-85 (**Backlog**, `ready-for-agent`, `svc:scoring-monitoring-service`) — local file `backend/docs/prd/DES-85-primera-implementacion-de-scoring-monitoring-service-hu-37-a-hu-40.md` is authoritative.
- **Build-on predecessor (same service): DES-99 (HU-37 + HU-39) — Done (2026-07-15).** DES-99 landed the append-only `ScoreEntry` ledger, `ScoreValue`/`ResolutionTime`, `IScorePolicy`/`SnapshotScorePolicy` (**Strategy already present**), the `Ranking` projection, `ScoringMonitoringDbContext` + migration, the MassTransit registration, and the Api composition root (`Program.cs`, controllers, `AuthorizationPolicies`, `ProblemDetailsExceptionHandler`, SignalR). **The service is no longer greenfield.** DES-51 (HU-37) and DES-54 (HU-39) are cancelled — folded into DES-99; they are not predecessors. Its context file is `backend/docs/hu37-39-context.md` (full read done).
- Cross-service blockers declared on the ticket — **both Done**: DES-26 (HU-19, operator↔session assignment) Done 2026-06-04; DES-76 (HU-21A, session state machine + `LiveSession.AssignedOperatorUserId`) Done 2026-07-05. These own the assigned-operator fact the `Proxy` consumes.
- Branch: `feature/hu-38-justified-penalties` — base `develop` (DES-99 is merged to `develop`; no same-service predecessor is In Progress). **The "land HU-37 first" gate from the earlier draft is satisfied** — the ledger exists on `develop`.

## Required design patterns

- `Strategy`
  - Why: a justified penalty is a **score-policy outcome** — `PenaltyPolicy` decides eligibility/justification and `ScorePolicy` decides the deduction magnitude; scoring variation must not become handler-level branching (`required_patterns_matrix.md` HU-38 row; ADR-0004; PRD DES-85 Implementation Decisions; `ddd_solution_model.md` §ScoringMonitoring L470-471).
  - Phase owner: X.1 Domain (policy interface + concrete `sealed` strategy in `Domain/Services/`), selected at runtime in X.2 Application.
  - Concrete obligation: **add `IPenaltyPolicy` + one `sealed` `DefaultPenaltyPolicy`** in `Domain/Services/` (eligibility/justification). **Reuse the landed `IScorePolicy`/`SnapshotScorePolicy`** for the deduction magnitude — do **not** recreate it (HU-37 landed it). Eligibility and impact live in the strategies, never as `if`/`switch` in the handler. Single-impl, no selector until a second variant lands (per the overengineering checklist).
- `Proxy`
  - Why: a penalty may only be applied by the operator **assigned to that session**; access must be guarded at the application boundary, not with ad-hoc role checks (`required_patterns_matrix.md` HU-38 row; ADR-0004 role/policy access guards; ADR-0012 resource-ownership resolver home).
  - Phase owner: X.2 Application (guarded access resolver) + X.4 Api (endpoint authorization policy).
  - Concrete obligation: `IScoringSessionAccessResolver` + `ScoringSessionAuthorizationProxy` (mirror session-operations `ISessionAdministrationAccessResolver` + `SessionAdministrationAuthorizationProxy`): blank actor → `UnauthorizedAccessException`; Administrator → unrestricted; non-Operator → `ForbiddenAccessException`; Operator whose id ≠ the session's assigned operator → `ForbiddenAccessException`. No ad-hoc role/owner `if` in handler, controller, or DI. The endpoint carries the coarse `[Authorize(Policy = AdministratorOrOperator)]`; the Proxy adds the fine per-session ownership check. **The resolver reads a local session-assignment projection (see Known quirks), not an HTTP client.**

_Not applies-where:_ HU-38's `Proxy` is a **mandated** obligation (it appears in HU-38's matrix pattern cell), not the informational applies-where tag reserved for HU-04/05/36B.

## What predecessors have already landed

`scoring-monitoring-service` is **populated by DES-99 (HU-37 + HU-39)** — the append-only ledger, the ranking projection, persistence, messaging, and the Api host all exist on `develop`. HU-38 is the operator-facing **penalty layer** on top of the landed ledger: it adds the `Penalty` child, the eligibility policy, the apply command, the authorization Proxy, and the endpoint; it **mirrors/extends** the landed scoring code and does **not** recreate the ledger, ranking, DbContext, or Api host. Types tagged _(from HU-37)_ below are build-on surface.

**Domain (landed — build-on)**
- `ScoreEntry` (`sealed : BaseAuditableEntity`) — append-only ledger root with a **`Grant` factory only**; fields `ScoreEntryId, LiveSessionId, TeamId, EntryType (ScoreEntryType), ReasonCode, ScoreValue, RecordedAt, SourceEntityType (ScoreSourceType), SourceEntityId, RecordedByUserId`; the `Grant` factory raises `ScoreEntryRegistered`. HU-38 **adds a `Penalty` factory** to it.
- `ScoreValue` VO — **`Create(int)` rejects negatives** (`InvalidScoreValueException`). A penalty deduction is therefore a **non-negative magnitude tagged `EntryType.Penalty`**, not a negative `ScoreValue`; the derived team total **subtracts** Penalty entries.
- `ScoreEntryType { Grant, Penalty, Correction }` and `ScoreSourceType { TargetResolution, TriviaAnswerSubmission, Penalty }` — **both already carry `Penalty`**; X.1 does **not** add enum members.
- `IScorePolicy.Award(ScoreValue)` + `SnapshotScorePolicy` (`sealed`) — **Strategy already present**; HU-38 reuses it for the deduction magnitude.
- `Domain/Events/ScoreEntryRegistered.cs`, `Domain/Common/{BaseEntity,BaseAuditableEntity,BaseEvent,ValueObject}.cs`, `Domain/Exceptions/{DomainException,InvalidScoreValueException,ScoreEntryIsAppendOnlyException}.cs` — mirror anchors for the new penalty types.

**Application (landed — build-on)**
- `RecordScoreEntry/` command slice (Command + Handler + Validator) — the mirror shape for `ApplyPenalty/`. Handler injects `IScoreEntryRepository` + `IScorePolicy`, checks idempotency via `IScoreEntryRepository.ExistsForSourceAsync`, awards via the policy, calls the factory, `AddAsync`.
- MassTransit consume/publish convention: `AnswerRegisteredConsumer : IConsumer<…>` + `Application/Scores/Common/*IntegrationEvent.cs` (`[EntityName]`) + `PublishScoreEntryRegisteredIntegrationEventHandler : INotificationHandler<ScoreEntryRegistered>` (post-commit `IPublishEndpoint.Publish` with a 5 s timeout, failures logged + swallowed). **This publish handler already fires for the Penalty deduction's `ScoreEntryRegistered`** — the ledger-impact publish is already wired.
- `AuthorizationBehaviour` + `Application/Common/Security/AuthorizeAttribute` — coarse **role** gate applied when a request carries `[Authorize(Roles=…)]`. Exceptions `ForbiddenAccessException`, `NotFoundException`, `ValidationException` exist.
- Interfaces `IScoreEntryRepository`, `IRankingRepository`, `IRankingBroadcaster`, `ICurrentUser` exist; DI in `Application/DependencyInjection.cs`.

**Infrastructure (landed — build-on)**
- `ScoringMonitoringDbContext` (**not** `ApplicationDbContext`) + `ScoringMonitoringDbContextFactory`; `ScoreEntryConfiguration` (table `score_entries`, VO conversions, index `(LiveSessionId, TeamId)`, **unique index `(SourceEntityType, SourceEntityId)`**), `RankingConfiguration`; interceptors `AuditableEntityInterceptor`, `DispatchDomainEventsInterceptor`; repos `ScoreEntryRepository`, `RankingRepository`; migration `20260714153000_AddScoringLedgerAndRanking` + `ScoringMonitoringDbContextModelSnapshot.cs`.
- `Infrastructure/Messaging/{MassTransitMessagingRegistration,RabbitMqOptions}.cs` — the per-type-exchange MassTransit registration to extend with a new consumer.

**Api (landed — build-on)**
- `Program.cs` composition root, `RankingController` (MVC, `[Route("api/sessions")]`, `[Authorize(Policy = …)]`, `[HttpGet("{liveSessionId:guid}/ranking")]`), `HealthController`, `Api/Services/AuthorizationPolicies.cs` (**`AdministratorOrOperator` and `ParticipantOrOperator` constants already exist**), `ProblemDetailsExceptionHandler.cs`, `CurrentUser`/`CurrentUserContext`, SignalR `ScoringHub`. HU-38's Api phase is small: a new controller + exception mappings + policy wiring.

**Cross-service (session-operations, both blockers Done)**
- HU-19 (DES-26) assigns operators to sessions; HU-21A (DES-76) owns `LiveSession.AssignedOperatorUserId`. `SessionAdministrationAuthorizationProxy` + `ISessionAdministrationAccessResolver` are the **mirror anchors for the Proxy shape** (in-process there; HU-38 reads a projection instead). `LiveSessionOperatorAssignedEvent` exists as a **domain** event but is **not yet published as an integration event** — see Known quirks.

**Coverage:** DES-99 established the service's ADR-0005 aggregate gate; HU-38 must not regress it and is measured fresh at X.4.

## What this HU adds

| Concern | New work |
|---|---|
| Justified penalty | Operator applies a `Penalty` to a team in a session they supervise, with a mandatory `PenaltyReason`, recorded actor (`appliedByUserId`) and timestamp (`appliedAt`). |
| Ledger deduction | The penalty impacts the score **only** through an append-only `ScoreEntry` of `EntryType.Penalty` (non-negative magnitude; total subtracts it) — no separate mutable total. |
| Eligibility Strategy | `IPenaltyPolicy` validates whether a penalty may be applied / what justification is required, as an interchangeable strategy (new). |
| Impact Strategy | `IScorePolicy` (reused from HU-37) computes the deduction magnitude — no new impact policy. |
| Authorization Proxy | Application-boundary guard restricting the operation to the session's assigned operator (Administrator unrestricted). |
| Assignment projection | scoring-monitoring **consumes** `session-operator-assigned` into a local session→operator read-model the Proxy reads (chosen mechanism; see Known quirks). |
| Domain event | On transactional success, raise `PenaltyApplied` (+ the ledger's `ScoreEntryRegistered`) → published post-commit for secondary recalc/audit; the apply flow must **not** depend on RabbitMQ (ticket AC). |
| Backend contract | `POST /api/sessions/{liveSessionId}/penalties` (operator-guarded) returning the applied-penalty result. |
| Frontend flow | Operator UI to apply a justified penalty and see it reflected in score (human-driven; Steps 9/9b). |

## Touched surfaces

- `backend/services/scoring-monitoring-service` (all four layers — extending the DES-99 baseline).
- `frontend/` operator penalty-application surface (web).
- backend/frontend API contract boundary: the apply-penalty request/response shape.
- Cross-service boundary: `ScoringMonitoring` **consumes** the operator-assignment fact from `SessionOperations` (projection); it does not own session progression. It **publishes** scoring-side `PenaltyApplied` / `ScoreEntryRegistered` facts only.

## Committed phases

| Commit | Phase | Description |
|---|---|---|
| _(none yet)_ | | |

## Known quirks / gotchas

- **Cross-service assignment fact — consume→projection (chosen 2026-07-15).** The assigned-operator fact lives only in `session-operations` (`LiveSession.AssignedOperatorUserId`). HU-38 does **not** call session-operations synchronously and does **not** introduce an HTTP access client (the earlier draft's `ISessionAssignmentAccessClient` / `IAuthenticatedActorProfileAccessClient` mirror **does not exist in the repo** — discard it). Instead scoring-monitoring **consumes `LiveSessionOperatorAssignedIntegrationEvent`** (`[EntityName("session-operator-assigned")]`) into a local `SessionOperatorAssignment` read-model, and the `Proxy` reads it in-process — consistent with the service's consume-facts boundary and its existing `IConsumer` stack.
- **PRIMARY OPEN DEPENDENCY — the assignment integration event is not published yet.** `session-operations` emits `LiveSessionOperatorAssignedEvent` only as a **domain** event; there is **no** outbound integration event / publisher (`grep`: none). Until session-operations publishes `session-operator-assigned` (a **cross-service contract item, gated by GH #164**), the projection stays empty and the `Proxy` denies **all Operators** (Administrator still passes). The scoring side declares its **own** structurally-identical copy of the contract decorated `[EntityName("session-operator-assigned")]` (separate deployable — no shared type). **Flag the producer gap at Stop 2; do not fix session-operations from this service.** This is directly analogous to the `AnswerRegisteredIntegrationEvent [EntityName]` producer gap noted in `hu37-39-context.md`.
- **Messaging is gated by GH #164** (MassTransit bus bootstrap + RabbitMQ), per the ticket. Both the inbound assignment consume and the outbound `PenaltyApplied` publish depend on it. The **core apply + persist path must not depend on RabbitMQ** (ticket AC): a broker outage never fails or rolls back the ledger write.
- **`ScoreValue` cannot be negative.** `ScoreValue.Create` throws on `value < 0`. Model the deduction as a **non-negative magnitude** on a `ScoreEntry` with `EntryType.Penalty` + `ScoreSourceType.Penalty`; the derived total subtracts Penalty entries. Do not attempt a negative `ScoreValue`.
- **`ScoreEntry` is `sealed` with a private ctor and a `Grant` factory only.** HU-38 **edits `ScoreEntry.cs`** to add a sibling **`Penalty(...)` factory** (mirror `Grant`): `EntryType.Penalty`, `ScoreSourceType.Penalty`, `SourceEntityId` = the new `Penalty`'s id, raising `ScoreEntryRegistered` **and** `PenaltyApplied`. Do not fork or subclass the ledger.
- **The ledger-impact publish is already wired.** `PublishScoreEntryRegisteredIntegrationEventHandler` publishes `ScoreEntryRegisteredIntegrationEvent` for **every** `ScoreEntryRegistered`, so the penalty deduction's secondary-recalc publish is inherited. `PenaltyApplied` is the penalty-specific fact (ddd `§domain events L343`); add its own post-commit publish handler mirroring the score one.
- **Enums + impact policy already exist — do not recreate.** `ScoreEntryType.Penalty`, `ScoreSourceType.Penalty`, and `IScorePolicy`/`SnapshotScorePolicy` all landed with HU-37. Recreating them is a defect.
- **DbContext name.** It is `ScoringMonitoringDbContext`, not `ApplicationDbContext`. Register `Penalty` + the projection through it.
- **Endpoint path (fixed).** `POST /api/sessions/{liveSessionId}/penalties`, body `{ teamId, reason }` — route-embedding `liveSessionId` so the Proxy resolves ownership from the route. Mirror `RankingController` (`[Route("api/sessions")]`).
- **Structure guard.** Vertical-slice layout is CI-enforced. Command slice under `Application/Scores/Commands/ApplyPenalty/` (Command + Handler + Validator co-located); output DTO in `Application/Dtos/Scores/`; the Proxy + resolver in `Application/Scores/Common/Authorization/`; consumers in `Application/Scores/Consumers/`. No `Handlers/`/`DTOs/`/`Facades/` buckets, no handler base class, no `UseCases/` wrapper (ADR-0011).

## Per-phase derivation — authoritative for implementation

> Primary source for the phase subagent (per `backend-agent.md` "Read first").
> Derived from `bd_umbral_entity_spec.md` §ScoringMonitoring (§ScoreEntry L700-733, §Penalty L735-759, VO/policy catalogs L897-915, RF-11/RB-06 L984/L1002), `ddd_solution_model.md` §ScoringMonitoring (domain events L343-344, repositories L386, domain services L470-471, use cases L549-550), `services/scoring-monitoring-service/CONTEXT.md`, `hu37-39-context.md` (landed build-on surface), ADR-0004/0011/0012, and DES-53 acceptance criteria.
> Types tagged _(from HU-37)_ are landed build-on surface — mirror/extend, do not recreate. Open a canonical section only to fill a gap a block leaves open.

### Phase X.1 — Domain
**Derive** (`bd_umbral_entity_spec.md` §ScoreEntry L700-733 / §Penalty L735-759; `ddd_solution_model.md` events L343-344, services L470-471; service `CONTEXT.md`):
- `Penalty` — entity (`sealed`, mirror `ScoreEntry`): `PenaltyId` (PK), `ScoreEntryId` (FK — **one `Penalty` ↔ exactly one `ScoreEntry`**, spec L755), `LiveSessionId`, `TeamId`, `PenaltyReason` (VO), `AppliedAt`, `AppliedByUserId`. Private ctor + a static factory. Invariant (spec L759): **must record `PenaltyReason` and `AppliedAt`** — reject blank/missing via `PenaltyRequiresReasonException`; no public setters.
- `PenaltyReason` — value object (`sealed : ValueObject`, `Create` + `GetEqualityComponents`, mirror `ScoreValue`): non-empty justification text; blank → `PenaltyRequiresReasonException`.
- `ScoreEntry` _(from HU-37 — **edit**, do not recreate)_ — add a `Penalty(...)` static factory mirroring `Grant`: builds a `ScoreEntry` with `EntryType.Penalty`, a **non-negative** deduction `ScoreValue`, `ScoreSourceType.Penalty`, `SourceEntityId` = the `Penalty`'s id, and raises `ScoreEntryRegistered` **and** `PenaltyApplied`.
- `IScorePolicy` _(from HU-37 — **consume**)_ — reused to compute the deduction magnitude; do not redefine.
- `IPenaltyPolicy` (domain service, **Strategy** — new) — validates penalty eligibility + justification before a deduction is persisted; rejection → `PenaltyNotEligibleException`. One `sealed` `DefaultPenaltyPolicy`.
- Domain event `PenaltyApplied` (`BaseEvent`, mirror `ScoreEntryRegistered`): `PenaltyId, ScoreEntryId, LiveSessionId, TeamId, appliedAt/appliedBy, magnitude`. (`PenaltyReverted` is ddd L344 but **out of scope** — HU-38 is apply-only.)
- Exceptions: `PenaltyRequiresReasonException`, `PenaltyNotEligibleException` (mirror `InvalidScoreValueException`).

**Target files** (create | edit — file to mirror):
- edit `Domain/Entities/ScoreEntry.cs` _(from HU-37)_ — add the `Penalty(...)` factory beside `Grant`
- create `Domain/Entities/Penalty.cs` — mirror `Domain/Entities/ScoreEntry.cs`
- create `Domain/ValueObjects/PenaltyReason.cs` — mirror `Domain/ValueObjects/ScoreValue.cs`
- create `Domain/Services/IPenaltyPolicy.cs` + `DefaultPenaltyPolicy.cs` — mirror `Domain/Services/IScorePolicy.cs` + `SnapshotScorePolicy.cs`
- create `Domain/Events/PenaltyApplied.cs` — mirror `Domain/Events/ScoreEntryRegistered.cs`
- create `Domain/Exceptions/{PenaltyRequiresReasonException,PenaltyNotEligibleException}.cs` — mirror `Domain/Exceptions/InvalidScoreValueException.cs`
- _(no enum changes — `ScoreEntryType.Penalty` / `ScoreSourceType.Penalty` already exist)_

**Pattern this phase owns:** `Strategy` — `IPenaltyPolicy` + `sealed DefaultPenaltyPolicy` in `Domain/Services/` (new); `IScorePolicy` reused. Single-impl, no selector until a second variant lands.
**Gate:** domain build passes; a unit test per **new/changed** type — `ScoreEntry.Penalty` raises `ScoreEntryRegistered` **and** `PenaltyApplied` as an append-only deduction (`EntryType.Penalty`, non-negative magnitude, no mutator); `Penalty` requires non-blank `PenaltyReason` + `AppliedAt`/`AppliedByUserId`; `PenaltyReason` blank rejected; `DefaultPenaltyPolicy` eligibility (accept + reject branches); each new exception; a penalty impacts score **only** through a `ScoreEntry` `Penalty` entry — no mutable total. `IScorePolicy`/enums reused, not recreated.

### Phase X.2 — Application
**Derive** (`ddd_solution_model.md` use cases L549-550, repositories L386; ADR-0012 Proxy Application home; `backend-agent.md` Application rules; landed `RecordScoreEntry/` slice):
- `ApplyPenalty` command slice: `ApplyPenaltyCommand` (`LiveSessionId`, `TeamId`, `Reason`) carrying `[Authorize(Roles = "Administrator,Operator")]` (coarse role gate via the landed `AuthorizationBehaviour`); `ApplyPenaltyCommandHandler`; `ApplyPenaltyCommandValidator` (FluentValidation — `Reason` required/non-blank, ids present).
- Handler flow (mirror `RecordScoreEntryCommandHandler`): (1) resolve the **authorized** session via `IScoringSessionAccessResolver` (operator must own it — fine ownership check); (2) evaluate `IPenaltyPolicy` eligibility (reject → `PenaltyNotEligibleException`); (3) compute the deduction magnitude via `IScorePolicy` (reused); (4) create `Penalty` + `ScoreEntry.Penalty(...)`, persist both in one unit of work via `IScoreEntryRepository.AddAsync` + `IPenaltyRepository.AddAsync` (raises `PenaltyApplied` + `ScoreEntryRegistered`); (5) return `AppliedPenaltyDto`. Policies **injected + selected at runtime** — no branching.
- **Proxy:** `IScoringSessionAccessResolver` + `ScoringSessionAuthorizationProxy` (`Application/Scores/Common/Authorization/`) — mirror `session-operations-service/.../Application/Sessions/Common/Authorization/SessionAdministrationAuthorizationProxy.cs` + `Application/Common/Interfaces/ISessionAdministrationAccessResolver.cs`: blank actor → `UnauthorizedAccessException`; Administrator → unrestricted; non-Operator → `ForbiddenAccessException`; Operator id ≠ assigned operator → `ForbiddenAccessException`. Reads ownership from `ISessionAssignmentReadRepository` (local projection) — **not** an HTTP client.
- **Assignment consume path:** local contract copy `LiveSessionOperatorAssignedIntegrationEvent` (`[EntityName("session-operator-assigned")]`, structurally identical, `Application/Scores/Common/`) + `LiveSessionOperatorAssignedConsumer : IConsumer<…>` (`Application/Scores/Consumers/`) → **idempotent upsert** of a `SessionOperatorAssignment` read-model via `ISessionAssignmentProjectionRepository`. Mirror `AnswerRegisteredConsumer`.
- **Outbound publish:** `PublishPenaltyAppliedIntegrationEventHandler : INotificationHandler<PenaltyApplied>` (`Application/Scores/EventHandlers/`) + `PenaltyAppliedIntegrationEvent` (`[EntityName("scoring-penalty-applied")]`, `Application/Scores/Common/`) — mirror `PublishScoreEntryRegisteredIntegrationEventHandler` (5 s timeout, failures logged + swallowed). The ledger's `ScoreEntryRegistered` publish is already inherited.
- DTO: output-only `AppliedPenaltyDto` (`Application/Dtos/Scores/`) — no domain types leak.
- Interfaces (`Application/Common/Interfaces/`): `IPenaltyRepository` (new), `ISessionAssignmentReadRepository` + `ISessionAssignmentProjectionRepository` (new). `IScoreEntryRepository` reused.
- Wire in `Application/DependencyInjection.cs`: `IPenaltyPolicy` → `DefaultPenaltyPolicy` (1:1), the Proxy/resolver, the consumer.

**Target files** (create | edit — file to mirror):
- create `Application/Scores/Commands/ApplyPenalty/{ApplyPenaltyCommand,ApplyPenaltyCommandHandler,ApplyPenaltyCommandValidator}.cs` — mirror `Application/Scores/Commands/RecordScoreEntry/*`
- create `Application/Scores/Common/Authorization/{IScoringSessionAccessResolver,ScoringSessionAuthorizationProxy}.cs` — mirror `session-operations-service/.../Authorization/SessionAdministrationAuthorizationProxy.cs` + `ISessionAdministrationAccessResolver.cs`
- create `Application/Scores/Common/{LiveSessionOperatorAssignedIntegrationEvent,PenaltyAppliedIntegrationEvent}.cs` — mirror `Application/Scores/Common/AnswerRegisteredIntegrationEvent.cs` (`[EntityName]` shape)
- create `Application/Scores/Consumers/LiveSessionOperatorAssignedConsumer.cs` — mirror `Application/Scores/Consumers/AnswerRegisteredConsumer.cs`
- create `Application/Scores/EventHandlers/PublishPenaltyAppliedIntegrationEventHandler.cs` — mirror `Application/Scores/EventHandlers/PublishScoreEntryRegisteredIntegrationEventHandler.cs`
- create `Application/Common/Interfaces/{IPenaltyRepository,ISessionAssignmentReadRepository,ISessionAssignmentProjectionRepository}.cs`
- create `Application/Dtos/Scores/AppliedPenaltyDto.cs`
- edit `Application/DependencyInjection.cs` — register policy (1:1) + proxy/resolver + consumer

**Pattern this phase owns:** `Proxy` (operator→assigned-session guard as access resolver, ADR-0012 Application home) + `Strategy` consumed (`IPenaltyPolicy` + reused `IScorePolicy`, runtime selection).
**Gate:** app build; handler tests — valid apply path; **non-owning operator → `ForbiddenAccessException`**; Administrator unrestricted; ineligible penalty → `PenaltyNotEligibleException`; missing/blank reason → validation failure; validator tests (reason required, ids present); access enforced through the Proxy resolver — **no ad-hoc role/owner `if`** in the handler; assignment consumer performs an idempotent projection upsert; `PenaltyApplied` raised for post-commit publish; no infrastructure leak.

### Phase X.3 — Infrastructure
**Derive** (`ddd_solution_model.md` repositories L386; `backend-agent.md` X.3 rules; landed persistence + MassTransit registration):
- EF Core: add `PenaltyConfiguration.cs` (table `penalties`; key `PenaltyId`; **one-to-one FK to `ScoreEntry`** via `ScoreEntryId`; `PenaltyReason` value conversion; `AppliedAt`/`AppliedByUserId`) — mirror `ScoreEntryConfiguration.cs`. Add `SessionOperatorAssignmentConfiguration.cs` (projection table `session_operator_assignments`; key `LiveSessionId`; `AssignedOperatorUserId`, `UpdatedAt`). Register both `DbSet`s in `ScoringMonitoringDbContext` (**edit**).
- Repositories: `PenaltyRepository : IPenaltyRepository`; `SessionAssignmentRepository : ISessionAssignmentReadRepository, ISessionAssignmentProjectionRepository` (read for the Proxy + upsert for the consumer). `ScoreEntryRepository` reused (`AddAsync` exists; append-only). Mirror `Infrastructure/Persistence/Repositories/ScoreEntryRepository.cs`.
- MassTransit: register `LiveSessionOperatorAssignedConsumer` in `MassTransitMessagingRegistration.cs` (**edit**); the per-type exchange `session-operator-assigned` is bound via `[EntityName]`. The outbound `PenaltyApplied`/`ScoreEntryRegistered` publish is handled by the Application event handlers over the existing `IPublishEndpoint`.
- **New EF migration** `AddPenaltyAndSessionAssignmentProjection`. Grep `ScoringMonitoringDbContextModelSnapshot.cs` for `Penalty`/`SessionOperatorAssignment` before reading it.

**Target files** (create | edit — file to mirror):
- create `Infrastructure/Persistence/Configurations/{PenaltyConfiguration,SessionOperatorAssignmentConfiguration}.cs` — mirror `Infrastructure/Persistence/Configurations/ScoreEntryConfiguration.cs`
- create `Infrastructure/Persistence/Repositories/{PenaltyRepository,SessionAssignmentRepository}.cs` — mirror `Infrastructure/Persistence/Repositories/ScoreEntryRepository.cs`
- edit `Infrastructure/Persistence/ScoringMonitoringDbContext.cs` — add `DbSet<Penalty>` + `DbSet<SessionOperatorAssignment>` + apply configs
- edit `Infrastructure/Messaging/MassTransitMessagingRegistration.cs` — register `LiveSessionOperatorAssignedConsumer`
- create migration under `Infrastructure/Migrations/` via `dotnet ef migrations add AddPenaltyAndSessionAssignmentProjection`

**Pattern this phase owns:** none (persistence + messaging wiring).
**Gate:** infra build; `dotnet ef migrations add` succeeds and represents `Penalty` (one-to-one under `ScoreEntry`) + the `session_operator_assignments` projection; repository integration test round-trips an append-only `ScoreEntry` `Penalty` + its `Penalty` child; the assignment consumer upserts the projection idempotently; `PenaltyApplied` (+ `ScoreEntryRegistered`) published **after commit** and a broker outage does **not** fail the apply path; no mutable score total persisted.

### Phase X.4 — Api
**Derive** (`backend-agent.md` X.4 rules; ADR-0001 header auth; ADR-0005 coverage; landed `RankingController`):
- Endpoint: `POST /api/sessions/{liveSessionId}/penalties`, body `{ teamId, reason }`, dispatching `ApplyPenaltyCommand`; returns `AppliedPenaltyDto` (201/200).
- `PenaltiesController` — MVC, `[ApiController]`, `[Route("api/sessions")]`, `[Authorize(Policy = AuthorizationPolicies.AdministratorOrOperator)]`, `[HttpPost("{liveSessionId:guid}/penalties")]` — mirror `Api/Controllers/RankingController.cs`. The coarse policy gates role; the Proxy (X.2) enforces per-session ownership.
- `AdministratorOrOperator` policy constant **already exists** — ensure it is **registered** in `Program.cs`/DI (add the `AddAuthorizationBuilder().AddPolicy(...)` wiring if HU-37 only wired `ParticipantOrOperator`).
- Map `PenaltyRequiresReasonException` (400) / `PenaltyNotEligibleException` (409/422) / `ForbiddenAccessException` (403) in `Api/Services/ProblemDetailsExceptionHandler.cs` (**edit** — `ForbiddenAccessException` may already map to 403; add the two domain exceptions, RFC 7807).

**Target files** (create | edit — file to mirror):
- create `Api/Controllers/PenaltiesController.cs` — mirror `Api/Controllers/RankingController.cs`
- edit `Api/Services/ProblemDetailsExceptionHandler.cs` — map the new domain exceptions
- edit `Api/Program.cs` — register the `AdministratorOrOperator` policy if not already wired (no new composition root — it exists)

**Pattern this phase owns:** `Proxy` (endpoint `[Authorize]` policy + the X.2 access resolver enforcing assigned-session ownership; no ad-hoc role `if`).
**Gate:** endpoint returns success for the **assigned** operator applying a justified penalty; **403 RFC 7807** for a non-owning operator; **400** for a missing/blank reason; the penalty is reflected as a `ScoreEntry` `Penalty` deduction; service meets the ADR-0005 coverage gate. _(Assigned-operator success is only observable once the `session-operator-assigned` producer exists — see Known quirks; until then verify Administrator success + the 403/400 branches, and flag the producer gap at Stop 2.)_

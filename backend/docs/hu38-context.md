# HU-38 Context - Aplicación de penalizaciones justificadas

> Paste this section into any agent session that needs context for HU-38.
> Last updated: 2026-07-12 | Branch: `feature/hu-38-justified-penalties`

## State

- DES-53 (HU-38): **Backlog**, labels: `svc:scoring-monitoring-service`, `Feature`, `ready-for-agent`
- **Resolved mode: feature flow** — DES-53 carries neither `canon-realign` nor `needs-rebuild`. No realignment map exists for `scoring-monitoring-service`; DES-53 is **not** superseded (not folded, not canceled). No superseded predecessor ids were dropped or substituted.
- PRD: DES-85 (**Backlog**, `ready-for-agent`, `svc:scoring-monitoring-service`) — local file `backend/docs/prd/DES-85-primera-implementacion-de-scoring-monitoring-service-hu-37-a-hu-40.md` is authoritative.
- Predecessors (same `svc:scoring-monitoring-service`): none Done or In Progress yet. **DES-51 (HU-37, the `ScoreEntry` ledger) is HU-38's build-on foundation and must merge before HU-38 is built** (resolved: land HU-37 first — see Known quirks). It is currently `Todo`. All other scoring tickets are Backlog/Todo. DES-52/DES-55 are Canceled (folded into DES-54), untouched by this HU.
- Cross-service blockers declared on the ticket: DES-26 (HU-19, operator↔session assignment) and DES-76 (HU-21A, session state machine) — both `session-operations-service`, both unmerged. These supply the operator-identity / assigned-session facts the Proxy authorization needs.
- Branch: `feature/hu-38-justified-penalties` — base `develop`. **Do not start X.1 until DES-51/HU-37 has merged to `develop`**; if HU-37 is instead mid-flight on its own feature branch when HU-38 starts, branch from that branch.

## Required design patterns

- `Strategy`
  - Why: justified penalties are a **score-policy outcome** — `PenaltyPolicy` decides eligibility/justification and `ScorePolicy` decides the deduction impact; scoring variation must not become handler-level branching (`required_patterns_matrix.md` HU-38 row; ADR-0004; `ddd_solution_model.md` §ScoringMonitoring L402-404).
  - Phase owner: X.1 Domain (policy interfaces + concrete strategies in `Domain/Services/`), selected at runtime in X.2 Application.
  - Concrete obligation: `IPenaltyPolicy` and `IScorePolicy` as interchangeable Strategy abstractions with `sealed` concrete implementations in `Domain/Services/`; eligibility and score-impact logic live in the strategies, never as `if`/`switch` branches in the handler. Follow the established single-impl convention (one `sealed` impl wired 1:1 in `Application/DependencyInjection.cs`; add a selector/factory only when a second policy variant lands).
- `Proxy`
  - Why: a penalty may only be applied by the operator **assigned to that session**; access must be guarded at the application boundary, not with ad-hoc role checks (`required_patterns_matrix.md` HU-38 row; ADR-0004 role/policy access guards; ADR-0012 resource-ownership resolver home).
  - Phase owner: X.2 Application (guarded resolver/decorator) + X.4 Api (endpoint authorization policy).
  - Concrete obligation: a `ScoringSessionAuthorizationProxy` implementing a small access-resolver interface (mirror `SessionAdministrationAuthorizationProxy`/`ISessionAdministrationAccessResolver`): Administrator unrestricted, Operator only when the actor owns the target session (`AssignedOperatorUserId == actor.UserId`) else `ForbiddenAccessException`. No ad-hoc role/owner `if` in handler, controller, or DI. Endpoint carries `[Authorize(Policy=...)]`.

_Not applies-where:_ HU-38's `Proxy` is a **mandated** obligation (it appears in HU-38's matrix pattern cell), not the informational applies-where tag reserved for HU-04/05/36B.

## What predecessors have already landed

`scoring-monitoring-service` is **greenfield** — the service tree is scaffolded but empty. Every `src/` and `tests/` folder holds only a `.gitkeep`; there are **zero `.cs` files** today. The Application layer is pre-seeded with slice-area folders `Scores/`, `Metrics/`, `Alerts/`, and `Common/`. **HU-38 lands after HU-37**, so its `ScoreEntry`/`ScoreValue` ledger core is the build-on surface (mirror/extend, do not recreate); everything else HU-38 needs is net-new, with mirror targets from sibling services (session-operations, mission-design, identity-access).

- **Domain / Application / Infrastructure / API:** none landed for scoring. `ScoringMonitoring` owns `ScoreEntry` and `Penalty` (aggregate roots), with `Ranking`/`AuditHistory`/monitoring as derived models (out of scope for HU-38).
- **DES-51 (HU-37) — the build-on foundation (must land first):** HU-37 "establishes the `ScoreEntry` ledger from validations, answers, and penalties." Canon models `Penalty` as a **child entity of `ScoreEntry`**, so HU-38 builds on the ledger rather than recreating it: `ScoreEntry` and `ScoreValue` come from HU-37, and HU-38 adds the `Penalty` child, the penalty policies, and the penalty-recording behavior. HU-38 X.1 must not begin until HU-37 has landed the ledger.
- **Cross-service (session-operations, unmerged):** HU-19 assigns operators to sessions; HU-21A owns the session state machine + `LiveSession.AssignedOperatorUserId`. These are the operator-identity/assigned-session facts the Proxy consumes. `session-operations-service` provides the mirror anchors: `SessionAdministrationAuthorizationProxy`, `ISessionAdministrationAccessResolver`, `IAuthenticatedActorProfileAccessClient`, `IQuestionActivationStrategy`/`SequentialQuestionActivationStrategy` (Strategy convention).

**Coverage:** greenfield — no prior aggregate percentage; the ADR-0005 gate is measured fresh at X.4.

## What this HU adds

| Concern | New work |
|---|---|
| Justified penalty | Operator applies a `Penalty` to a team in a session they supervise, with a mandatory `PenaltyReason` and recorded actor + timestamp. |
| Ledger deduction | The penalty impacts the score **only** through an append-only `ScoreEntry` deduction — no separate mutable session total. |
| Eligibility Strategy | `PenaltyPolicy` validates whether a penalty may be applied / what justification is required, as an interchangeable strategy. |
| Impact Strategy | `ScorePolicy` computes the deduction magnitude of an accepted penalty, as an interchangeable strategy. |
| Authorization Proxy | Application-boundary guard restricting the operation to the session's assigned operator (Administrator unrestricted). |
| Domain event | On transactional success, raise/publish `PenaltyApplied` (and the underlying `ScoreEntryRecorded`) to RabbitMQ for secondary recalculation/audit — the main apply flow must **not** depend on RabbitMQ (ticket AC). |
| Backend contract | `POST` apply-penalty endpoint (operator-guarded) returning the applied penalty/ledger result. |
| Frontend flow | Operator UI to apply a justified penalty to a team and see it reflected in score (human-driven; Steps 9/9b). |

## Touched surfaces

- `backend/services/scoring-monitoring-service` (all four layers — greenfield)
- `frontend/` operator penalty-application surface (web)
- backend/frontend API contract boundary: the apply-penalty request/response shape
- Cross-service boundary: `ScoringMonitoring` **consumes** operator-identity/assigned-session facts from `SessionOperations`; it does not own session progression. It **publishes** scoring-side `PenaltyApplied` facts only.

## Committed phases

| Commit | Phase | Description |
|---|---|---|
| _(none yet)_ | | |

## Known quirks / gotchas

- **HU-37 (`ScoreEntry` ledger) is the build-on foundation — land it first (resolved).** Canon makes `Penalty` a child entity of `ScoreEntry`, and the PRD names HU-37 the foundation "before richer surfaces", so **HU-38 is the penalty layer on top of HU-37's landed ledger**: `ScoreEntry` and `ScoreValue` are consumed from HU-37 (build-on), and HU-38 adds only the `Penalty` child, the policies, the penalty-recording behavior, the authorization, and the endpoint. HU-38 X.1 must not begin until DES-51/HU-37 has merged. In the per-phase blocks, types tagged _(from HU-37)_ are build-on surface to mirror/extend, **not** to recreate — do not expand HU-38 to rebuild the ledger.
- **Cross-service authorization data (resolved — HTTP access client).** The assigned-operator fact lives in `session-operations-service` (`LiveSession.AssignedOperatorUserId`), a different deployable. The scoring Proxy resolves operator↔session ownership through an `ISessionAssignmentAccessClient` abstraction (Application), implemented as a **synchronous HTTP access client** to session-operations in Infrastructure — mirroring the established `IAuthenticatedActorProfileAccessClient` convention. The Application depends only on the interface.
- **Event names.** Canon (`ddd_solution_model.md` §ScoringMonitoring domain events) defines `PenaltyApplied` and `ScoreEntryRecorded` verbatim — these are the events HU-38 raises (the ticket AC's "PenaltyApplied/ScoreEntryRegistered" maps onto them). The PRD itself names no concrete event; do not invent others.
- **Endpoint path (fixed).** `POST /api/sessions/{liveSessionId}/penalties` — route-embedding `liveSessionId` so the Proxy resolves ownership from the route; body `{ teamId, reason }`.
- **Append-only ledger.** No mutation/deletion of `ScoreEntry`; the team total is always derived from entries. A penalty is a deduction entry, never a hidden mutable total (PRD Out-of-Scope).
- **Structure guard.** Vertical-slice layout is CI-enforced (`scripts/structure-guard.sh`): command slice under `Application/Scores/Commands/ApplyPenalty/` (Command + Handler + Validator co-located, no DTO in the slice folder); DTOs in central `Application/Dtos/Scores/`; the Proxy in `Application/Scores/Common/Authorization/`. No `Handlers/`/`DTOs/`/`Facades/` buckets, no handler base class, no `UseCases/` wrapper.

## Per-phase derivation — authoritative for implementation

> Primary source for the phase subagent (per `backend-agent.md` "Read first").
> Derived from `bd_umbral_entity_spec.md` §ScoringMonitoring (§ScoreEntry L700-733, §Penalty L735-759, VO/policy catalogs L897-915), `ddd_solution_model.md` §ScoringMonitoring (domain events L339-348, repositories L383-389, domain services L464-471, use cases L546-557, Strategy note L402-404), `backend/services/scoring-monitoring-service/CONTEXT.md`, ADR-0004/0011/0012, and DES-53 acceptance criteria.
> Types tagged _(from HU-37)_ are build-on ledger surface from DES-51 — mirror/extend, do not recreate (HU-38 lands after HU-37). Open a canonical section only to fill a gap a block leaves open.

### Phase X.1 — Domain
**Derive** (`bd_umbral_entity_spec.md` §ScoreEntry L700-733 / §Penalty L735-759; `ddd_solution_model.md` §ScoringMonitoring events L339-348, services L464-471; service `CONTEXT.md`):
- `Penalty` — **child entity of `ScoreEntry`** (`BaseEntity`). Fields: `penaltyId` (PK), `scoreEntryId` (FK → parent), `liveSessionId`, `teamId`, `penaltyReason` (`PenaltyReason` VO), `appliedAt`, `appliedByUserId`. Invariant: **must record `penaltyReason` and `appliedAt`** — enforce via `PenaltyRequiresReasonException`; no public setters.
- `PenaltyReason` — value object (`ValueObject`, `GetEqualityComponents`): non-empty justification text; reject blank (`PenaltyRequiresReasonException`).
- `ScoreEntry` _(from HU-37 — extend, do not recreate)_ — append-only ledger aggregate root (`BaseAuditableEntity`) landed by HU-37 with fields `scoreEntryId` (PK), `liveSessionId`, `teamId`, `entryType` (`ScoreEntryType`: Grant/Penalty/Correction), `reasonCode`, `scoreValue` (`ScoreValue` VO), `recordedAt`, `sourceEntityType`, `sourceEntityId`, `recordedByUserId`. HU-38 **adds the penalty-recording behavior** to it: a method that records a penalty deduction, producing the `Penalty` child and raising `PenaltyApplied` + `ScoreEntryRecorded`. Mirror HU-37's actual `entryType`/factory surface — do not fork the ledger.
- `ScoreValue` _(from HU-37 — consume)_ — value object for a score quantity landed by HU-37; HU-38 uses it to express the deduction magnitude. Do not redefine.
- `IPenaltyPolicy` (domain service, **Strategy**) — validates penalty eligibility + justification requirement before a deduction is persisted (`PenaltyNotEligibleException` on rejection). One `sealed` concrete impl.
- `IScorePolicy` (domain service, **Strategy**) — computes the deduction impact of an accepted penalty on the ledger. One `sealed` concrete impl.
- Domain events (`BaseEvent`, raised via `AddDomainEvent`): `PenaltyApplied`, `ScoreEntryRecorded`.
- Exceptions: one per invariant — `PenaltyRequiresReasonException`, `PenaltyNotEligibleException`.

**Target files** (create — mirror):
- edit `Domain/Entities/ScoreEntry.cs` _(from HU-37)_ — add the penalty-recording method; do not recreate the aggregate
- create `Domain/Entities/Penalty.cs` — child entity; mirror a `BaseEntity` child in a sibling aggregate
- create `Domain/ValueObjects/PenaltyReason.cs` — mirror an existing `ValueObject` (`ScoreValue` is consumed from HU-37, not recreated)
- `Domain/Enums/ScoreEntryType.cs` _(from HU-37 — ensure a `Penalty` member exists; add it if HU-37 left it out)_
- create `Domain/Services/IPenaltyPolicy.cs` + `DefaultPenaltyPolicy.cs`, `Domain/Services/IScorePolicy.cs` + `DefaultScorePolicy.cs` — mirror `session-operations-service/.../Domain/Services/IQuestionActivationStrategy.cs` + `SequentialQuestionActivationStrategy.cs`
- create `Domain/Events/PenaltyApplied.cs`, `Domain/Events/ScoreEntryRecorded.cs`
- create `Domain/Exceptions/{PenaltyRequiresReasonException,PenaltyNotEligibleException}.cs`

**Pattern this phase owns:** `Strategy` (`IPenaltyPolicy` + `IScorePolicy` as interface + `sealed` concrete impl in `Domain/Services/`; single-impl, no selector until a second variant lands).
**Gate:** domain build passes; a unit test per **new/changed** domain type — the `ScoreEntry` penalty-recording behavior (raises `PenaltyApplied`/`ScoreEntryRecorded` as an append-only deduction), `Penalty` reason/appliedAt invariants, `PenaltyReason`, `DefaultPenaltyPolicy`, `DefaultScorePolicy`, each exception (HU-37 already covers base `ScoreEntry`/`ScoreValue`); Strategy realized as interface + `sealed` impl in `Domain/Services/` (no eligibility/impact branching elsewhere); a penalty impacts score only through a `ScoreEntry` deduction — no mutable total.

### Phase X.2 — Application
**Derive** (`ddd_solution_model.md` §ScoringMonitoring use cases L546-557, repositories L383-389; ADR-0012 Proxy Application home; `backend-agent.md` Application rules):
- `ApplyPenalty` command slice: `ApplyPenaltyCommand` (`liveSessionId`, `teamId`, `reason`), `ApplyPenaltyCommandHandler`, `ApplyPenaltyCommandValidator` (FluentValidation — `reason` required/non-blank, ids present).
- Handler flow: (1) resolve the **authorized** session via the Proxy resolver (operator must own it); (2) evaluate `IPenaltyPolicy` eligibility (reject → rejection branch); (3) compute impact via `IScorePolicy`; (4) append a `ScoreEntry` penalty deduction + `Penalty` child through the repository (raises `PenaltyApplied`); (5) return the result DTO. Policies are **injected and selected at runtime** — no branching.
- Repository interfaces (`Application/Common/Interfaces/`): `IPenaltyRepository` (new); `IScoreEntryRepository` is consumed from HU-37 (extend only if the penalty append needs a method it lacks).
- **Proxy**: `IScoringSessionAccessResolver` + `ScoringSessionAuthorizationProxy` (`Application/Scores/Common/Authorization/`) — mirror `ISessionAdministrationAccessResolver` + `SessionAdministrationAuthorizationProxy`: blank actor → `UnauthorizedAccessException`; load session ownership fact; Administrator → unrestricted; non-Operator → `ForbiddenAccessException`; Operator whose id ≠ assigned operator → `ForbiddenAccessException`. Depends on `ISessionAssignmentAccessClient` (`Application/Common/Interfaces/`) — the HTTP access client to session-operations (impl in X.3); the Application depends only on the interface.
- DTO: output-only `AppliedPenaltyDto` (`Application/Dtos/Scores/`) — no domain types leak.
- Wire policies + proxy in `Application/DependencyInjection.cs`.

**Target files** (create — mirror):
- create `Application/Scores/Commands/ApplyPenalty/{ApplyPenaltyCommand,ApplyPenaltyCommandHandler,ApplyPenaltyCommandValidator}.cs` — mirror `mission-design-service/.../Application/Missions/Commands/CreateMission/`
- create `Application/Scores/Common/Authorization/{IScoringSessionAccessResolver,ScoringSessionAuthorizationProxy}.cs` — mirror `session-operations-service/.../Application/Sessions/Common/Authorization/SessionAdministrationAuthorizationProxy.cs` + `Application/Common/Interfaces/ISessionAdministrationAccessResolver.cs`
- create `Application/Common/Interfaces/{IPenaltyRepository,ISessionAssignmentAccessClient}.cs` (consume/extend `IScoreEntryRepository` from HU-37)
- create `Application/Dtos/Scores/AppliedPenaltyDto.cs`
- edit `Application/DependencyInjection.cs` — register policies (1:1) + proxy

**Pattern this phase owns:** `Proxy` (operator→assigned-session guard as resolver/decorator, ADR-0012 Application home) + `Strategy` policies consumed here (runtime selection).
**Gate:** app build; handler tests — valid apply path; **non-owning operator → `ForbiddenAccessException`**; ineligible penalty → rejection; missing/blank reason → validation failure; validator tests (reason required, ids present); access enforced through the Proxy resolver — **no ad-hoc role/owner `if`** in the handler; `PenaltyApplied` raised for post-commit publish; no infrastructure leak.

### Phase X.3 — Infrastructure
**Derive** (`ddd_solution_model.md` repositories L383-389; `backend-agent.md` X.3 rules; `rabbitmq-events-dotnet` convention):
- EF Core config: add `Penalty` as an owned/child collection under the existing `ScoreEntry` mapping — edit `ScoreEntryConfiguration.cs` (from HU-37) and add `PenaltyConfiguration.cs`.
- Repositories: add `PenaltyRepository`; `ScoreEntryRepository` exists from HU-37 (extend for the penalty append if needed). Append-only semantics.
- `ApplicationDbContext : IApplicationDbContext` (from HU-37) exposes `DbSet<ScoreEntry>` with `Penalty` reachable through it; interceptors `AuditableEntityInterceptor`, `DispatchDomainEventsInterceptor`.
- RabbitMQ publisher for `PenaltyApplied` (scoring-monitoring publishes outbound facts) — mirror the session-operations publisher; **publish after commit, best-effort**: a broker failure must not roll back or block the ledger write (ticket AC — main flow independent of RabbitMQ).
- `ISessionAssignmentAccessClient` implementation — a synchronous HTTP access client to session-operations resolving the assigned-operator fact (mirror `IAuthenticatedActorProfileAccessClient`); Application depends only on the interface.
- **New EF migration for the `Penalty` child** (HU-37's ledger migration already exists). Grep `ApplicationDbContextModelSnapshot.cs` for `ScoreEntry`/`Penalty` before reading.

**Target files** (create — mirror):
- edit `Infrastructure/Persistence/Configurations/ScoreEntryConfiguration.cs` (from HU-37) + create `PenaltyConfiguration.cs` — mirror a sibling owned-type config
- create `Infrastructure/Persistence/Repositories/PenaltyRepository.cs` (extend `ScoreEntryRepository` from HU-37 if needed)
- edit `Infrastructure/Persistence/ApplicationDbContext.cs` + interceptors (from HU-37)
- create RabbitMQ publisher — mirror `session-operations-service/.../Infrastructure/...` event publisher
- create `ISessionAssignmentAccessClient` impl
- create new migration under `Infrastructure/Persistence/Migrations/`

**Pattern this phase owns:** none.
**Gate:** `ef migrations add` succeeds and represents `ScoreEntry` + `Penalty`; repository integration test round-trips an append-only `ScoreEntry` + `Penalty`; `PenaltyApplied` published to RabbitMQ **after commit** and a broker outage does not fail the apply path; no mutable score total persisted.

### Phase X.4 — Api
**Derive** (`backend-agent.md` X.4 rules; ADR-0001 header auth; ADR-0005 coverage):
- Endpoint: `POST /api/sessions/{liveSessionId}/penalties`, body `{ teamId, reason }`, dispatching `ApplyPenaltyCommand`; returns the applied-penalty result (201/200).
- MVC controller (`Api/Controllers/PenaltiesController.cs`), `[ApiController]` + attribute routing; `[Authorize(Policy=...)]` operator policy (constant in `Api/Services/AuthorizationPolicies.cs`).
- `CurrentUser : ICurrentUser` from gateway headers (`X-User-Id`/`X-User-Role`/`X-User-Email`); `Program.cs` wires Application + Infrastructure + controllers.
- Map `PenaltyRequiresReasonException`/`PenaltyNotEligibleException`/`ForbiddenAccessException` in `Api/Services/ProblemDetailsExceptionHandler.cs` (RFC 7807).

**Target files** (create — mirror):
- create `Api/Controllers/PenaltiesController.cs` — mirror a sibling MVC controller
- create `Api/Services/{AuthorizationPolicies,ProblemDetailsExceptionHandler}.cs`, `Api/CurrentUser.cs`, `Api/Program.cs` (greenfield Api composition root)

**Pattern this phase owns:** `Proxy` (endpoint `[Authorize]` policy + the access resolver enforcing assigned-session ownership; no ad-hoc role `if`).
**Gate:** endpoint returns success for the **assigned** operator applying a justified penalty; **403 RFC 7807** for a non-owning operator; **400** for a missing/blank reason; the penalty is reflected as a `ScoreEntry` deduction; service reaches the ADR-0005 coverage gate.

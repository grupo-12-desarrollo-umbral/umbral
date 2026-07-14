# HU-29 Context — Common evidence-submission intake (umbrella)

> Paste this section into any agent session that needs context for HU-29 (DES-39).
> Last updated: 2026-07-13 | Branch: `feature/hu-29-evidence-submission-intake`
>
> **Nature of this HU: extraction / generalization, not greenfield.** HU-34 (DES-46,
> Done) already shipped the `EvidenceSubmission` umbrella base, the `TriviaAnswerSubmission`
> specialization, the `LiveSession.RegisterTriviaAnswer` skeleton, the `TriviaAnswerValidation`
> Chain of Responsibility, and the MassTransit-outbox event bridge — explicitly "shaped so
> later HU-29/HU-30A can extract into the shared umbrella pipeline" (`hu34-context.md:86,111`,
> `prompt_example_feature_hu34.md:405-408`). HU-29 **lifts** that shared intake substrate into a
> reusable Facade + generic Chain, adds the generic `EvidenceSubmissionRegistered` intake event,
> and publishes it through the existing outbox. It does **not** rebuild what HU-34 shipped, and it
> does **not** build the QR form (that is HU-31 / DES-42) or the deeper pre-acceptance validation
> (that is HU-30A / DES-40).

## State

- DES-39 (HU-29): **In Progress**, labels: `svc:session-operations-service`, `Feature`, `ready-for-agent`. Both required labels present (the `ready-for-agent` gate was satisfied at generation time; the earlier `Todo`/unlabeled snapshot is superseded).
- **Resolved mode: feature flow** — DES-39 carries neither `canon-realign` nor `needs-rebuild`. But it is an **extraction feature** over Done HU-34 code; several phases refactor/verify rather than create (see per-phase keep/refactor notes).
- **Supersession check applied:** DES-39 was Canceled twice (2026-06-16, "QR-only") and **re-activated by ADR-0010 (Accepted 2026-07-10)**, which explicitly lists "DES-39 (HU-29) … active backlog items again as the generic evidence intake … under the umbrella model" (`adr/0010-evidence-qr-only-first-delivery.md:61-63`). The stale QR-only cancellation comment is exactly what ADR-0010 overturned. **DES-39 is live and is not in any supersession column.**
- Same-service **build-on** predecessors (Done/merged):
  - **DES-46 (HU-34)** — the substrate this HU extracts (base entity, skeleton, trivia chain, outbox bridge).
  - **DES-76 (HU-21A)** — session state machine; the "session admits reception" gate (`Paused`/`Finished`/`Cancelled` reject gameplay).
  - **DES-78 (HU-33A)** — active-substage / active-question runtime seam (the `active_substage` pointer intake resolves against).
  - **DES-11 / DES-12 (HU-07A / HU-07B)** — participant admission/reconnect; the `RuntimeParticipationGuard` intake reuses.
  - Outbox infra: the MassTransit EF-Core transactional bus outbox (`20260713020055_AddMassTransitTransactionalOutbox`, ADR-0017 amended 2026-07-12).
- Same-service **landed, untouched by this HU:** DES-77 (HU-22 timer — relevant only inside the trivia specialization, not generic intake), DES-22/24/25/26/27 (HU-15/17/18/19/20 session baseline; HU-18 team association is consumed for team resolution but not modified), DES-75 (HU-16 snapshot).
- **Not predecessors — do NOT build here:** DES-40 (HU-30A, `EvidenceValidationPolicy` deeper context validation — sibling, downstream), DES-42 (HU-31, QR `TreasureEvidenceSubmission` + `TargetResolved` — the first consumer of this substrate), DES-41 (HU-30B, rejection reasons), DES-49/DES-84 (client tickets).
- PRD DES id: **DES-70** → `backend/docs/prd/DES-70-primera-implementacion-de-session-operations-service-hu-15-a-hu-36.md` (authoritative; never re-fetch PRD scope from Linear). Umbrella terminology overlaid by `adr/0010-evidence-qr-only-first-delivery.md`.
- Branch: `feature/hu-29-evidence-submission-intake`, base **`develop`** (all build-on predecessors Done/merged; no *other* same-service build-on predecessor is In Progress).

## Required design patterns

| Pattern | Owning phase | Why (from patterns matrix) | Concrete obligation |
|---|---|---|---|
| `Facade` (mandated) | X.2 Application | `required_patterns_matrix.md:40,122`: "on transactional success the **Facade** publishes `EvidenceSubmissionRegistered` (canonical RabbitMQ workflow)." | A single orchestration entry point over the shared intake subsystem: run the intake Chain → invoke the domain registration core → persist → the raised `EvidenceSubmissionRegistered` fact bridges to the outbox. Because it is **shared by ≥2 consumers** (the trivia command now, the QR command in HU-31), it is a **discrete `*Facade.cs` class in `Sessions/Common/`** per ADR-0013 §Decision (not handler-inlined — the single-consumer inline rule does not apply). Mirror the existing shared facades `SessionTeamAssociationFacade` / `TriviaRoundOrchestratorFacade`. |
| `Chain of Responsibility` (mandated) | X.2 Application | `required_patterns_matrix.md:42,122`: "Evidence submission runs the composable validation pipeline." | Ordered, composable **generic intake-admission** links — runtime participation, session-admits-reception, active-substage-present/team-in-session — that short-circuit on first failure and are extended (not edited) by each concrete form. The trivia chain is refactored to compose these shared links + its trivia-specific links. **Boundary:** HU-29 owns only the generic intake-admission links; target/question-specific validators stay with HU-30A/HU-31. |

Transport note: HU-29 carries **RabbitMQ** (`required_patterns_matrix.md:58,61-64,122`). This is *the* canonical end-to-end workflow: after transactional success the intake publishes `EvidenceSubmissionRegistered`, consumed by audit/history, notification, and secondary recalc/projection. Reuse the existing MassTransit EF-Core **transactional bus outbox** (ADR-0017 as amended 2026-07-12): `IPublishEndpoint.Publish` is a pre-commit `OutboxMessage` insert that rides the business `SaveChanges`; `BusOutboxDeliveryService` drains it asynchronously. Do **not** add a second publisher stack, a five-second-bounded post-commit publish, or a hand-rolled AMQP client.

Applies-where note (no new gate): HU-29 is **not** in the matrix's applies-where `Proxy` set (HU-04/05/36B), and it adds no new participant endpoint. The existing concrete-form endpoints inherit the standard gateway + `AuthorizationBehaviour` guard (ADR-0001/0002) — note only, **no** new `Proxy` gate.

## What predecessors have already landed

**Build-on (read for derivation):**

- **DES-46 (HU-34) — Done. THE substrate this HU extracts.**
  - Domain: `EvidenceSubmission` (**abstract** umbrella base, `Domain/Entities/EvidenceSubmission.cs`) with fields `EvidenceSubmissionId, LiveSessionId, TeamId, ActiveSubstageId, SubmissionType, SubmittedByParticipantId?, SubmittedAt, ValidationState`; ctor throws `EvidenceSubmissionContextRequiredException` if session/team/substage is empty. `EvidenceSubmissionType { TreasureHuntQrScan=1, TriviaAnswer=2 }`. `EvidenceValidationState { Accepted=1, Rejected=2 }` — **no `Pending`** (HU-34 only ever persists `Accepted`; rejections throw). `TriviaAnswerSubmission : EvidenceSubmission` (inheritance/TPH) hardcodes type=TriviaAnswer, state=Accepted, adds trivia fields.
  - `LiveSession.RegisterTriviaAnswer(...)` is the fixed ordered skeleton (state gate → team resolve → active question resolve → window → option resolve → first-write-wins → create entity → raise `AnswerRegisteredEvent`) — the comment cites HU-29/HU-30A as the intended extractor.
  - Application: `SubmitTriviaAnswer` command slice; `Sessions/Common/TriviaAnswerValidation/` chain (`TriviaAnswerValidationChain` + `TriviaAnswerValidationLink` base + `Validators/{RuntimeParticipationLink, ActiveTriviaQuestionLink, TriviaAnswerWindowLink, DuplicateTriviaAnswerLink}`), DI-ordered, short-circuit-on-throw.
  - Outbox bridge: `AnswerRegisteredEvent` → (`OutboxDomainEventDispatcher` switch, invoked pre-commit by `DispatchDomainEventsInterceptor`) → `PublishAnswerRegisteredIntegrationEventHandler` → `IPublishEndpoint.Publish(AnswerRegisteredIntegrationEvent)`. Contracts live in `Sessions/Common/*IntegrationEvent.cs` with `[EntityName(...)]`.
  - Persistence: `LiveSessionConfiguration.OwnsMany(session => session.TriviaAnswerSubmissions, …)` → table `live_session_trivia_answer_submissions` (base + specialization columns flattened; enums `HasConversion<string>()` maxlen 32; unique index `(LiveSessionId, TeamId, ActiveSubstageId, QuestionSequenceOrder)`); migration `20260710001735_AddTriviaAnswerRegistration`.
  - Api: `POST /api/sessions/{liveSessionId}/participants/answers` (`[Authorize(Policy = Participant)]`), acceptance-metadata-only DTO.
- **DES-76 (HU-21A) — Done.** The `State`-pattern lifecycle: `Paused`/`Finished`/`Cancelled` reject gameplay. The generic intake state gate delegates to `LiveSessionStateFactory.For(State)` (the trivia skeleton already calls `EnsureCanRegisterTriviaAnswer`). AC #3 ("bloquea cuando la sesión no admite recepción") builds directly on this.
- **DES-78 (HU-33A) — Done.** The active-substage pointer / active-question runtime; migration `20260706043854_AddActiveSubstagePointer`. Intake resolves `ActiveSubstageId` against this seam.
- **DES-11 / DES-12 (HU-07A/07B) — Done.** `RuntimeParticipationGuard` (`IRuntimeParticipationGuard.EnsureAllowedAsync`, fail-closed, Participation Block #91). The generic intake chain's first link reuses it.

**Landed, untouched by this HU:** DES-77 (HU-22 timer — trivia-specialization-only), DES-22/24/25/26/27 (session baseline; HU-18 team association consumed for `GetTeam`, not modified), DES-75 (HU-16 snapshot shape). Do not anchor derivation on these beyond the seams named above.

**Coverage:** no stable carried-forward aggregate percentage is recorded for this seam; verify the real service percentage at X.4 against the ADR-0005 repo gate.

## What this HU adds

| Concern | New work |
|---|---|
| Generic intake event | Add `EvidenceSubmissionRegisteredEvent` (base intake fact) — the canonical `ddd_solution_model.md:326` domain event, absent from code today. Raised by the shared registration core on every successful base-evidence registration. |
| Initial `pending` state | Add `Pending` to `EvidenceValidationState` (canon: pending/accepted/rejected — `CONTEXT.md:101-102`; PRD AC "estado de validación inicial `pending`"). HU-34 skipped it because trivia is instantly `Accepted`. |
| Shared registration core (domain) | Extract the common intake steps of `RegisterTriviaAnswer` into a reusable `LiveSession` registration core (session-admits-reception → team resolve → active-substage resolve → create base evidence → raise `EvidenceSubmissionRegistered`), which the trivia flow (and future QR flow) build on. No mandated pattern in X.1. |
| Intake Facade (application) | `EvidenceIntakeFacade` (`Sessions/Common/`, shared ≥2 consumers) orchestrating chain → domain core → persist → outbox. The trivia command delegates its shared-intake portion to it. |
| Generic intake Chain (application) | `EvidenceIntakeValidation/` — generic ordered links (runtime participation, session-admits-reception, active-substage-present). The trivia chain composes these + its trivia-specific links. |
| RabbitMQ workflow | `EvidenceSubmissionRegisteredIntegrationEvent` + publish handler + `OutboxDomainEventDispatcher` case, published via `IPublishEndpoint` (pre-commit outbox insert). The trivia submission path now demonstrates `EvidenceSubmissionRegistered` **end-to-end**. |
| Backend contract | **No new participant endpoint** (`EvidenceSubmission` is abstract; concrete forms own endpoints — trivia Done, QR = HU-31). New **async contract**: `EvidenceSubmissionRegistered` on the session-operations exchange for audit/history/notification/projection consumers. |

## Touched surfaces

- `backend/services/session-operations-service/` — domain (event + enum + registration core), application (Facade + generic Chain + outbox bridge; refactor of the trivia intake to route through the substrate), infrastructure (outbox contract registration; migration only if persistence changes), api (verification + coverage; no new endpoint).
- API contract boundary: **unchanged** for clients (no new endpoint / request shape).
- Async contract boundary: **new** `EvidenceSubmissionRegistered` RabbitMQ message consumed downstream by audit/history, notification, and secondary recalc/projection (ScoringMonitoring / HU-40).
- Frontend: **none** — HU-29 adds no client-facing surface (see prompt Step 9 contract hand-off).

## Committed phases

| Commit | Phase | Description |
|---|---|---|
| | | |

## Known quirks / gotchas

- **Do NOT rebuild HU-34.** `EvidenceSubmission`, `TriviaAnswerSubmission`, the trivia skeleton, the trivia chain, and the outbox bridge exist and are canon-aligned. HU-29 extracts and generalizes; the gate for every refactored span is **HU-34 behavior unchanged** (all existing trivia tests stay green). Reject any plan that recreates the base entity or a second publisher stack.
- **`EvidenceSubmission` is abstract — there is no generic thing to POST.** Every real submission is a concrete form (QR or trivia). This is why HU-29 adds **no** generic participant endpoint. The umbrella is an internal substrate + an async event, not a client contract. Surface this at Stop 1 (see rationale).
- **Two facts, per form (ADR-0010:38-39).** The trivia form keeps its specialized `AnswerRegistered` (scoring consumer) **and** now also raises the umbrella `EvidenceSubmissionRegistered` (audit/history/notification/projection consumers) — additive, not a replacement. The QR form (HU-31) will raise `EvidenceSubmissionRegistered` then `TargetResolved`. Do not delete or rename `AnswerRegistered`.
- **Chain boundary.** HU-29 owns only generic *intake-admission* links. The deeper pre-acceptance context validation (`EvidenceValidationPolicy`, target/question specifics) is HU-30A (DES-40) / HU-31 (DES-42). Do not pull those validators forward.
- **Outbox semantics (ADR-0017 amended).** Publish = pre-commit local `OutboxMessage` insert on the tracked `ApplicationDbContext`, atomic with the business write; a publish failure rolls the transaction back (no swallow, no five-second timeout). Broker delivery is async via `BusOutboxDeliveryService`. Wire the new event through `OutboxDomainEventDispatcher` (pre-commit), not a post-commit MediatR path.
- **`Pending` + persisted enum.** `ValidationState` is stored as a string (`HasConversion<string>()`, maxlen 32). Adding the `Pending` value is string-compatible and needs **no schema migration**; a migration is required in X.3 only if some other persistence change forces it — verify, don't assume.
- **Namespace is `umbral_backend.*`** across all session-ops layers (`umbral_backend.Domain.*`, `umbral_backend.Application.Sessions.*`); the Application DI file uses `Microsoft.Extensions.DependencyInjection` by convention. Match existing files.
- **Facade placement.** ADR-0013 inlines *single-consumer* mandated Facades into their handler, but this Facade is **shared by ≥2 consumers** → it stays a discrete class in `Sessions/Common/` (the ADR's explicit exception, alongside `SessionTeamAssociationFacade`/`TriviaRoundOrchestratorFacade`). Naming a Facade only in prose without a gate line is a defect — the X.2 gate carries it.

## Per-phase derivation — authoritative for implementation

> Primary source for the phase subagent (per `backend-agent.md` "Read first").
> Mode = **feature flow (extraction-dominant)**. Canon precedence: `ddd_solution_model.md` → service
> `CONTEXT.md` → `structure.md` → `bd_umbral_entity_spec.md` → plan docs; umbrella terminology overlay
> `adr/0010-evidence-qr-only-first-delivery.md`. Target-file layout is ADR-0011 vertical slices
> (`Commands/<UseCase>/`, `<Area>/Common/`, central `Application/Dtos/`) — the hu09 exemplar's
> `Handlers/`/`DTOs/` buckets are pre-ADR-0011 and must not be reproduced.

### Phase X.1 — Domain
**Derive** (`bd_umbral_entity_spec.md:402-451` §EvidenceSubmission; `ddd_solution_model.md:245-250,326,530`; `CONTEXT.md:95-106,196`; `adr/0010-evidence-qr-only-first-delivery.md:31-49`):
- **`EvidenceValidationState` gains `Pending`** — canonical states are pending/accepted/rejected (`CONTEXT.md:101-102`); the umbrella intake registers with `Pending` (PRD AC "estado de validación inicial `pending`"). Keep `Accepted`/`Rejected`; the trivia specialization continues to construct as `Accepted` (its resolution is instantaneous). Persisted as string — no numeric-ordinal dependency.
- **`EvidenceSubmissionRegisteredEvent` (new, `Domain/Events/`)** — the base intake fact (`ddd_solution_model.md:326`): `LiveSessionId, TeamId, EvidenceSubmissionId, ActiveSubstageId, SubmissionType, SubmittedAt, ValidationState`. Raised on every successful base-evidence registration, success path only. Mirror `AnswerRegisteredEvent.cs` (`: BaseEvent`, get-only props).
- **Shared registration core on `LiveSession`** — extract the common prefix of `RegisterTriviaAnswer` into a reusable protected/internal method (e.g. `RegisterEvidenceCore` / a `protected EvidenceSubmission BeginEvidenceRegistration(...)`): (1) `EnsureSessionAdmitsEvidence()` via `LiveSessionStateFactory.For(State)` (generalize the existing `EnsureCanRegisterTriviaAnswer` state gate), (2) resolve/verify the team belongs to the session (`GetTeam`), (3) resolve/verify the `ActiveSubstageId` from the runtime snapshot, (4) construct the base-evidence context + set `ValidationState = Pending`, (5) `AddDomainEvent(new EvidenceSubmissionRegisteredEvent(...))`. `RegisterTriviaAnswer` is refactored to call this core, then run its trivia-specific steps (option resolve → first-write-wins → `TriviaAnswerSubmission.Accept` flips state to `Accepted`) and additionally raise `AnswerRegisteredEvent`. **Behavior of the trivia path must not change** beyond the added `EvidenceSubmissionRegistered` fact.
- Rejections stay typed domain exceptions (reuse `EvidenceSubmissionContextRequiredException`, `TeamNotFoundException`, and the session-state rejection from the lifecycle). Do not add QR/target or question exceptions here.

**Target files** (create | edit — file to mirror):
- edit `src/Domain/Enums/EvidenceValidationState.cs` — add `Pending` (mirror the existing enum members)
- create `src/Domain/Events/EvidenceSubmissionRegisteredEvent.cs` — mirror `src/Domain/Events/AnswerRegisteredEvent.cs`
- edit `src/Domain/Entities/EvidenceSubmission.cs` — allow the base to be constructed in `Pending` and expose the umbrella-safe construction the core needs (keep `abstract`; do not add a concrete instantiable base)
- edit `src/Domain/Entities/LiveSession.cs` — extract the shared registration core; refactor `RegisterTriviaAnswer` to build on it and additionally raise `EvidenceSubmissionRegisteredEvent`; mirror the existing skeleton (do not duplicate steps)
- edit `tests/UnitTests/Domain/Entities/LiveSessionTests.cs` — assert the core raises `EvidenceSubmissionRegisteredEvent` with `Pending` on a valid registration, rejects when the session does not admit reception / team absent / active substage absent, and that **every existing trivia assertion still passes** (first-write-wins, late/duplicate rejection, `AnswerRegisteredEvent` on success)

**Pattern this phase owns:** none mandated (the two mandated patterns land in X.2). The registration core is domain support for the X.2 Facade, not a gated pattern — do not label it `Template Method` (not mandated for HU-29; Constraint 2).
**Gate:** Domain build passes; a unit test proves the shared core registers a base `EvidenceSubmission` in `Pending`, raises `EvidenceSubmissionRegisteredEvent` on success only, and rejects the three generic intake failures (session not admitting reception, unknown team, missing active substage); **all pre-existing HU-34 trivia domain tests remain green** (no behavior regression); no QR/target logic introduced.

### Phase X.2 — Application
**Derive** (`required_patterns_matrix.md:40,42,122`; `adr/0013-facades-in-application-command-slices.md` §Decision; `adr/0017-masstransit-abstractions-in-application.md` as amended; `ddd_solution_model.md:530`; existing `Sessions/Common/{TriviaAnswerValidation/*, SessionTeamAssociationFacade.cs, AnswerRegisteredIntegrationEvent.cs}`, `Sessions/EventHandlers/{OutboxDomainEventDispatcher.cs, PublishAnswerRegisteredIntegrationEventHandler.cs}`, `DependencyInjection.cs`):
- **`Chain of Responsibility` — generic intake chain.** Create `src/Application/Sessions/Common/EvidenceIntakeValidation/` with `EvidenceIntakeValidationChain` + `EvidenceIntakeValidationLink` (base, `SetNext` + short-circuit-on-throw) + `EvidenceIntakeValidationContext` (immutable: `LiveSession Session, Guid TeamId, Guid ActiveSubstageId, string? Token, DateTimeOffset SubmittedAt`) + `Validators/{RuntimeParticipationLink, SessionAdmitsReceptionLink, ActiveSubstagePresentLink}`. Mirror `TriviaAnswerValidation/` exactly (same base-class shape, DI-ordered, first-throw short-circuits). Refactor `TriviaAnswerValidationChain` to compose the shared generic links **then** its trivia-specific links (`ActiveTriviaQuestionLink → TriviaAnswerWindowLink → DuplicateTriviaAnswerLink`) — reuse `RuntimeParticipationLink` from the shared set, do not duplicate it.
- **`Facade` — shared intake orchestrator.** Create `src/Application/Sessions/Common/EvidenceIntakeFacade.cs` + `IEvidenceIntakeFacade.cs` (discrete class — shared ≥2 consumers, ADR-0013). It: loads/receives the session aggregate, runs the `EvidenceIntakeValidationChain`, invokes the domain registration core, and persists via `ILiveSessionRepository`; the raised `EvidenceSubmissionRegisteredEvent` is dispatched to the outbox by the existing interceptor. Mirror `SessionTeamAssociationFacade.cs`. Refactor `SubmitTriviaAnswerCommandHandler` to delegate its shared-intake portion to `IEvidenceIntakeFacade`, keeping its trivia-specific result mapping; **the participant-facing DTO and its no-leak guarantee are unchanged**.
- **RabbitMQ bridge (outbox).** Create `src/Application/Sessions/Common/EvidenceSubmissionRegisteredIntegrationEvent.cs` (`[EntityName("session-evidence-submission-registered")]`, fields mapped from the domain event) — mirror `AnswerRegisteredIntegrationEvent.cs`. Create `src/Application/Sessions/EventHandlers/PublishEvidenceSubmissionRegisteredIntegrationEventHandler.cs` (`IPublishEndpoint.Publish`, try/catch-log-rethrow) — mirror `PublishAnswerRegisteredIntegrationEventHandler.cs`. Add an `EvidenceSubmissionRegisteredEvent` case to `OutboxDomainEventDispatcher`. Register the facade, the generic links (in run order), and the new publisher in `DependencyInjection.cs` (the single DI touch-point — do not edit chain classes to add links).

**Target files** (create | edit — file to mirror):
- create `src/Application/Sessions/Common/EvidenceIntakeValidation/{EvidenceIntakeValidationChain.cs, EvidenceIntakeValidationLink.cs, EvidenceIntakeValidationContext.cs}` + `Validators/{RuntimeParticipationLink.cs, SessionAdmitsReceptionLink.cs, ActiveSubstagePresentLink.cs}` — mirror `Sessions/Common/TriviaAnswerValidation/*`
- create `src/Application/Sessions/Common/{EvidenceIntakeFacade.cs, IEvidenceIntakeFacade.cs}` — mirror `Sessions/Common/{SessionTeamAssociationFacade.cs, ISessionTeamAssociationFacade.cs}`
- create `src/Application/Sessions/Common/EvidenceSubmissionRegisteredIntegrationEvent.cs` — mirror `Sessions/Common/AnswerRegisteredIntegrationEvent.cs`
- create `src/Application/Sessions/EventHandlers/PublishEvidenceSubmissionRegisteredIntegrationEventHandler.cs` — mirror `Sessions/EventHandlers/PublishAnswerRegisteredIntegrationEventHandler.cs`
- edit `src/Application/Sessions/EventHandlers/OutboxDomainEventDispatcher.cs` — add the `EvidenceSubmissionRegisteredEvent` routing case
- edit `src/Application/Sessions/Common/TriviaAnswerValidation/TriviaAnswerValidationChain.cs` — compose the shared generic links before the trivia-specific links (reuse, do not duplicate, the shared `RuntimeParticipationLink`)
- edit `src/Application/Sessions/Commands/SubmitTriviaAnswer/SubmitTriviaAnswerCommandHandler.cs` — delegate shared intake to `IEvidenceIntakeFacade`; keep trivia result mapping
- edit `src/Application/DependencyInjection.cs` — register the facade, the generic links (run order), and the publisher
- add tests under `tests/Application.UnitTests/Sessions/...` — generic chain order + short-circuit; facade orchestration (chain → core → persist); publisher maps the domain event; and the existing `SubmitTriviaAnswer` tests still pass

**Pattern this phase owns:** `Facade` (mandated) — `EvidenceIntakeFacade` (discrete, shared) + `Chain of Responsibility` (mandated) — `EvidenceIntakeValidationChain`.
**Gate:** Application build passes; the generic intake links run in stable DI order and short-circuit on first failure (runtime participation → session-admits-reception → active-substage-present); `EvidenceIntakeFacade` is a discrete `Sessions/Common/` class that orchestrates chain → domain core → persist (no ad-hoc `if` intake logic in the handler); the trivia command delegates to it with **no change to its participant response / no-leak guarantee**; `EvidenceSubmissionRegisteredIntegrationEvent` publishes through `IPublishEndpoint` off the domain fact and is wired via `OutboxDomainEventDispatcher`. **`Facade` verified — single shared orchestration entry point over the intake subsystem; `Chain of Responsibility` verified — ordered composable links, not a collapsed handler.**

### Phase X.3 — Infrastructure
**Derive** (`bd_umbral_entity_spec.md:419-451`; existing `src/Infrastructure/Persistence/Configurations/LiveSessionConfiguration.cs`, `src/Infrastructure/Messaging/MassTransitMessagingRegistration.cs`, `src/Infrastructure/Persistence/Interceptors/DispatchDomainEventsInterceptor.cs`; ADR-0008 shared Postgres testcontainer; ADR-0017 amended):
- Persistence: the base-evidence columns already round-trip via the HU-34 owned mapping (`live_session_trivia_answer_submissions`, `validation_state` as string maxlen 32). Adding `Pending` is string-compatible — **verify no schema change is needed** (model-snapshot grep / `ef migrations add` dry-check). Add a migration **only** if the tooling reports a real model diff (e.g. if a check constraint or default on `validation_state` exists). Do not fabricate an empty migration.
- Messaging: the MassTransit EF-Core outbox and RabbitMQ topology already exist (`AddEntityFrameworkOutbox<ApplicationDbContext>` + `UsingRabbitMq` + `ConfigureEndpoints`). The new `[EntityName]` contract is auto-registered by convention — no topology edit. Confirm the pre-commit dispatch path (`DispatchDomainEventsInterceptor` → `OutboxDomainEventDispatcher` → publisher) carries `EvidenceSubmissionRegisteredEvent`.
- Add an integration test proving the outbox path: registering evidence (through the trivia submission seam) inserts an `OutboxMessage` for `EvidenceSubmissionRegistered` in the same transaction as the business write.

**Target files** (create | edit — file to mirror):
- edit (only if the model actually diffs) `src/Infrastructure/Persistence/Configurations/LiveSessionConfiguration.cs` — no change expected for the enum value; touch only if a constraint/default references the enum set
- create (conditional) `src/Infrastructure/Migrations/<timestamp>_AddEvidencePendingState.cs` — **only** if `ef migrations add` reports a real diff; otherwise record "no migration — `Pending` is string-compatible, no schema change"
- add integration test under `tests/IntegrationTests/Messaging/` — mirror the existing outbox/publish integration test for `AnswerRegistered`; assert the pre-commit `OutboxMessage` insert for `EvidenceSubmissionRegistered`

**Pattern this phase owns:** none.
**Gate:** Infrastructure build passes; **no new migration** unless `ef migrations add` reports a real model diff (assert the check and record the outcome); the base-evidence row round-trips with `ValidationState = Pending`; an integration test proves `EvidenceSubmissionRegistered` is inserted into the transactional outbox atomically with the business write and drains through the existing RabbitMQ registration — no second publisher stack or exchange bootstrap.

### Phase X.4 — Api
**Derive** (`src/Api/Controllers/SessionsController.cs`; `src/Api/Services/ProblemDetailsExceptionHandler.cs`; ADR-0001/0002 gateway auth; ADR-0005 coverage):
- **No new participant endpoint.** `EvidenceSubmission` is abstract; the participant-facing write paths are the concrete forms — trivia's `POST …/participants/answers` (Done) and QR's endpoint (HU-31). HU-29's api-layer work is verification + coverage: the existing trivia submission path, exercised end-to-end through the gateway, now also emits `EvidenceSubmissionRegistered` on the RabbitMQ exchange after transactional success; generic intake rejections (session not admitting reception) surface as consistent RFC-7807 ProblemDetails through the existing handler (already classified via `IErrorMetadata`). Confirm no new exception mapping is required beyond what the reused domain exceptions already carry.
- If Stop-1 review decides HU-29 should also expose a generic intake endpoint (see rationale — *not* the default interpretation), that is an additive change to be scoped then; the default ships no endpoint.

**Target files** (create | edit — file to mirror):
- add/extend integration test under `tests/IntegrationTests/Api/` (or `.../Messaging/`) — the trivia submission path publishes `EvidenceSubmissionRegistered` observably after transactional success; a session-not-admitting-reception submission returns consistent ProblemDetails
- verify (do not reshape) `src/Api/Controllers/SessionsController.cs`, `src/Api/Services/ProblemDetailsExceptionHandler.cs`

**Pattern this phase owns:** none new — `Facade`/`Chain of Responsibility` are realized in X.2; the reused endpoints inherit the standard gateway + `AuthorizationBehaviour` guard (ADR-0001/0002). HU-29 is not in the applies-where `Proxy` set → no new gate.
**Gate:** endpoint/messaging integration test proves the intake publishes `EvidenceSubmissionRegistered` end-to-end after transactional success and that a non-admitting-session submission returns consistent RFC-7807 ProblemDetails; no new client endpoint added (or, if Stop 1 explicitly opts in, the additive endpoint is scoped separately); **ADR-0005 coverage** (service ≥ repo gate).

# HU-32 Context — Trazabilidad de evidencias

> Paste this section into any agent session that needs context for HU-32 (DES-43).
> Last updated: 2026-07-14 | Branch: `feature/hu-32-evidence-traceability`
>
> **Nature of this HU: the read half of the `EvidenceSubmission` umbrella, fed asynchronously.**
> HU-29/30/31/34 built the write half — intake, contextual validation, the two concrete forms, and
> the `EvidenceSubmissionRegistered` outbox bridge. All are **Done and merged**. HU-32 adds three
> things: the **outcome facts** the umbrella never published (`EvidenceSubmissionAccepted` /
> `EvidenceSubmissionRejected` — canon `ddd_solution_model.md:327-328`, deliberately deferred by
> HU-30 "no in-scope consumer — result query is HU-32"), the **first consumer in this service**,
> which projects those facts into an evidence-trace read model, and the **operator read surface**
> over it.
>
> It does **not** rebuild the base, the facade, either chain, or either concrete form, and it adds
> **no** operator-mediated review (ADR-0010 — both forms are system-resolved).

## State

- DES-43 (HU-32): **Todo**, labels: `svc:session-operations-service`, `Feature`, `ready-for-agent`.
  Both required labels present.
- **Resolved mode: feature flow.** DES-43 carries **no** `canon-realign` and no `needs-rebuild`
  label. Its body was already written against the `EvidenceSubmission` umbrella (it names both
  `TreasureEvidenceSubmission` and `TriviaAnswerSubmission` and cites ADR-0010), so there is no
  canon overlay to apply and no existing HU-32 code to keep/delete/decide — `EvidenceTraceEntry`
  and the outcome events appear in **zero** `.cs` files today.
- **Supersession check (clear).** DES-43 appears in the realignment map's ✅ **living** row
  (`canon-realignment-after-mission-runtime-rewrite.md:89` — "DES-39/40/41/42/43/46/47/56 …
  ✅ already realigned to `EvidenceSubmission` umbrella (#28)"), **not** in any superseded column
  (the superseded set is DES-23/28/30/44, plus the DES-47→DES-46 merge at `:181`). Safe to generate.
- **Superseded handling applied to predecessors:** none of this HU's build-on predecessors are
  superseded. The service's superseded/rebuilt pairs (DES-28→DES-76, DES-30→DES-77, DES-44→DES-78,
  DES-23→DES-75) are session-lifecycle/trivia-orchestration tickets that HU-32 does not build on;
  they are listed under *landed, untouched*.
- Same-service **build-on** predecessors (Done/merged):
  **DES-39 (HU-29)**, **DES-95 (HU-30)**, **DES-42 (HU-31)**, **DES-46 (HU-34)**,
  **DES-29 (HU-21)**, and the **MassTransit EF-Core transactional bus outbox** (GH #164→#166 +
  outbox, on `develop`).
- Same-service **landed, untouched by this HU:** DES-22 (HU-15), DES-24 (HU-17), DES-25 (HU-18),
  DES-26 (HU-19), DES-27 (HU-20), DES-31 (HU-23), DES-32 (HU-24A), DES-36 (HU-26), DES-45 (HU-33B),
  DES-49 (HU-36A), DES-75 (HU-16), DES-76 (HU-21A), DES-77 (HU-22), DES-78 (HU-33A), DES-86/87
  (per-target scoring), DES-93 (TreasureHunt timer). **DES-33 (HU-24B — operator panel of events,
  evidence and ranking)** is a **downstream consumer** of this HU's read surface (`blocks` edge on
  DES-43), not a predecessor.
- **In Progress, but not build-on:** DES-13 (HU-08 multi-device sync) and DES-38 (HU-28 live clue
  authoring) are In Progress but touch aggregates HU-32 does not consume → branch base stays
  `develop` (resolution step 4).
- PRD DES id: **DES-70** → `backend/docs/prd/DES-70-primera-implementacion-de-session-operations-service-hu-15-a-hu-36.md`
  (authoritative; never re-fetch PRD scope from Linear). Anchors: **US-33** (`:141-142` — "every
  evidence item to retain validation state, origin, and rejection reason, so runtime audit is
  preserved") and **US-34** (`:143-144` — "detailed evidence review by team and session, so I can
  inspect what happened"); scope `:50` ("accept, validate, and **trace** `EvidenceSubmission`");
  `:222-223` ("Operational reads (`HU-20`, `HU-23`, `HU-24`, `HU-25`, **`HU-32`**, `HU-36`) are
  separate read surfaces aligned with CQRS").
- Terminology overlay: `adr/0010-evidence-qr-only-first-delivery.md` (umbrella model; **operator
  review out of scope**).
- Branch: `feature/hu-32-evidence-traceability`, base **`develop`** (all build-on predecessors are
  Done/merged; no build-on predecessor is In Progress).

## Required design patterns

**None mandated.** `required_patterns_matrix.md:45` lists `HU-32` in the "— (no mandated pattern)"
row, and `:125` reads: "`HU-32` | — | — | Evidence traceability — audit read; `Proxy` guards
operator access." Do **not** invent a pattern to fill the gap (resolution step 5 / Constraint 2).

**Applies-where `Proxy` (no new gate).** `:125` tags HU-32 with the same *applies-where* phrasing the
matrix uses for HU-04/05 ("No mandated pattern; `Proxy` guards its access endpoints"). The operator
read inherits the **existing** ADR-0009 ownership resolver Proxy —
`ISessionAdministrationAccessResolver.GetAuthorizedSessionAsync` (`Sessions/Common/Authorization/SessionAdministrationAuthorizationProxy.cs`)
— plus `[Authorize(Roles = "Operator")]` on the query and `[Authorize(Policy = AuthorizationPolicies.Operator)]`
on the endpoint, exactly as `GetOperatorTriviaAnsweredMonitorQueryHandler` does. That is the standard
inherited guard (ADR-0001/0002/0009), **not** a new pattern gate. No ad-hoc role/owner `if` in the
handler.

> Note on `generator-agent.md`'s applies-where list: it enumerates "HU-04/05/36B only", but the
> governing rule is "carry the note only for the HUs the **matrix actually tags**" — and the matrix
> tags HU-32 at `:125`. The note is carried; no gate is added. Flagged so the divergence is visible
> at Stop 1 rather than silently resolved either way.

**Transport note — RabbitMQ, and HU-32 is the first *consumer* in this service.** The matrix
transport table (`:59`) lists `HU-32` under "neither", but that table is **stale on this point** and
is overridden by two authoritative sources (same call HU-31 made at `hu31-context.md:67-72`):

- **DES-43 AC #5** — "La trazabilidad/historial de evidencias se alimenta de forma asíncrona
  consumiendo los eventos de dominio publicados en RabbitMQ, sin que el flujo principal dependa de
  RabbitMQ."
- **Canon** — `ddd_solution_model.md:615`: "the minimum explicit RabbitMQ workflow is
  `EvidenceSubmissionRegistered` published after successful evidence registration, **then consumed by
  audit/history, notification, and secondary recalculation or projection-support flows**";
  `bd_umbral_entity_spec.md:942` repeats it; `required_patterns_matrix.md:61-64` names the same
  canonical workflow. HU-32 **is** the audit/history projection-support consumer.

The matrix's transport table tracks **publishing**; HU-32's obligation is **consuming** (plus
publishing the two outcome facts it adds). AC #5's "sin que el flujo principal dependa de RabbitMQ"
is already satisfied structurally by the shipped EF-Core **bus outbox**: `IPublishEndpoint.Publish` is
a local `OutboxMessage` insert riding the business `SaveChanges`, drained to the broker asynchronously
by `BusOutboxDeliveryService` (ADR-0017 as amended 2026-07-12). The write path never touches the
broker — do **not** add a timeout, a swallow-and-forget `catch`, or a bespoke outbox.
**(Stop-1 closed — resolved on the record.** Same call HU-31 made at `hu31-context.md:67-72`; the
consistency is its own justification.) Leave a one-line comment on each of the three `IConsumer<>`
adapters (e.g. `// AC#5 / ddd_solution_model.md:615 — first audit/history consumer in this service;
override of matrix transport table :59 "neither"`) so the override is discoverable at the code site.

## What predecessors have already landed

**Build-on (read for derivation):**

- **DES-39 (HU-29) — Done/merged. The intake substrate.** Abstract `EvidenceSubmission : BaseEntity`
  (`Domain/Entities/EvidenceSubmission.cs`) with the context fields (`EvidenceSubmissionId`,
  `LiveSessionId`, `TeamId`, `ActiveSubstageId`, `SubmissionType`, `SubmittedByParticipantId?`,
  `SubmittedAt`, `ValidationState`) — AC #1 (timestamp) and AC #2 (team/session/substage) are
  **already persisted**; HU-32 surfaces them, it does not re-model them. Also
  `LiveSession.RegisterEvidenceCore<TSubmission>` (`LiveSession.cs:618`) which raises
  `EvidenceSubmissionRegisteredEvent`, the `EvidenceIntakeFacade` + generic
  `EvidenceIntakeValidationChain`, `EvidenceSubmissionRegisteredIntegrationEvent`
  (`[EntityName("session-evidence-submission-registered")]`) +
  `PublishEvidenceSubmissionRegisteredIntegrationEventHandler` + its
  `OutboxDomainEventDispatcher` arm.
- **DES-95 (HU-30) — Done/merged. The rejection reason (AC #4).** `EvidenceSubmission.RejectionReason`
  (`EvidenceRejectionReason?`, `private set`) + the `Reject(reason)` transition (`Pending → Rejected`,
  throws `EvidenceAlreadyResolvedException` if already resolved); the contextual
  `EvidenceValidationChain` (`Sessions/Common/EvidenceValidation/`) with reasons
  `{SubstageBindingMismatch, OutsideSubmissionWindow, UnauthorizedOrigin}`; the facade's
  `RegisterPendingAsync(...)` seam that catches `EvidenceContextRejectedException` → `Reject(reason)`
  → persist; the `rejection_reason` column (migration `20260713221512_AddEvidenceRejectionReason`).
- **DES-42 (HU-31) — Done/merged. The QR form + its own rejection reason.**
  `TreasureEvidenceSubmission : EvidenceSubmission` with `ScannedValue`, `TargetSnapshotId?`, and
  **`ResolutionRejectionReason` (`TargetResolutionRejectionReason?`)** — a *second, disjoint* reason
  model (`{ScannedValueDoesNotResolveToTarget, TargetOutsideActiveSubstage, TargetAlreadyResolvedByTeam}`
  + a `ToMessage()` extension). Internal transitions `AcceptRegisteredTarget()` /
  `RejectRegisteredTarget(reason)`; `LiveSession.RegisterTargetScan` (`:520-570`) registers, resolves
  against the snapshot, then accepts + raises `TargetResolvedEvent` **or** rejects and retains.
  Migration `20260714022538_AddTreasureEvidenceSubmission`.
- **DES-46 (HU-34) — Done/merged. The trivia form.** `TriviaAnswerSubmission : EvidenceSubmission`
  with `QuestionSequenceOrder`, `SelectedOptionSequenceOrder`, `IsCorrect`, `ScoreValue`;
  `AcceptRegisteredAnswer()`; `LiveSession.RegisterTriviaAnswer` (`:489+`) which **auto-accepts**
  every registered answer. Late/duplicate answers **throw** (`LateTriviaAnswerException`,
  first-write-wins) and are therefore **never persisted** — see *Known quirks*.
- **DES-29 (HU-21) — Done/merged. The audit precedent + the producer template.** The append-only
  `SessionEvent` child of `LiveSession` (`live_session_events`), `SessionStateChangedIntegrationEvent`
  (`[EntityName("session-state-changed")]`) + `PublishSessionStateChangedIntegrationEventHandler`, and
  the two-handler `SessionStateChangedEvent` arm in `OutboxDomainEventDispatcher`
  (`DispatchSessionStateChangedAsync` — sequential awaits on the shared scoped `DbContext`, **not**
  `Task.WhenAll`). HU-32 mirrors this publish-handler shape for its two new outcome facts.
- **MassTransit EF-Core transactional bus outbox — Done/merged on `develop`.**
  `Infrastructure/Messaging/MassTransitMessagingRegistration.cs` configures
  `AddEntityFrameworkOutbox<ApplicationDbContext>` + `UseBusOutbox()` + `UsingRabbitMq` with
  **`cfg.ConfigureEndpoints(context)` already present** — so adding `bus.AddConsumer<T>()` is
  sufficient to create the receive endpoint (no topology hand-wiring).
  `DispatchDomainEventsInterceptor` dispatches the outbox publishers **pre-commit**
  (`SavingChanges` → `IOutboxDomainEventDispatcher`) and the MediatR/SignalR fan-out **post-commit**
  (`SavedChanges`), collecting events via `ChangeTracker.Entries<BaseEntity>()`. Outbox tables exist
  (migration `20260713020055_AddMassTransitTransactionalOutbox`).

**Consumer exemplar (cross-service, mirror its shape):**
`scoring-monitoring-service/src/Infrastructure/Messaging/Consumers/AnswerRegisteredConsumer.cs` — the
only consumer in the backend today. `IConsumer<TIntegrationEvent>` in `Infrastructure/Messaging/Consumers/`,
injecting `ISender` + `ILogger`, whose `Consume` maps the message to an Application command and
dispatches through MediatR. **No business logic in the consumer body** (ADR-0017 §Decision 5). Its
service registers it with `bus.AddConsumer<AnswerRegisteredConsumer>()` and re-declares the contract
locally (`Scores/Common/AnswerRegisteredIntegrationEvent.cs`) — contracts are duplicated per service
by design (`ddd_solution_model.md:617` "no shared domain library can collapse the bounded contexts").

**Landed, untouched by this HU:** DES-22/24/25/26/27 (session setup/reads), DES-31/32/49 (live board,
operator panel, answered monitor — HU-32 adds a *separate* read surface, per PRD `:222-223`; do not
fold it into `OperatorSessionPanelDtoFactory`), DES-36 (clue release), DES-45/75/76/77/78/93 (trivia
runtime, lifecycle, timers), DES-86/87 (per-target scoring).

**Coverage:** no stable carried-forward aggregate percentage is recorded for this seam; the ADR-0005
gate (`coverlet.msbuild`, **at least 95% aggregate branch coverage**) is enforced by `dotnet test` —
verify the real service percentage at X.4.

## What this HU adds

| Concern | New work |
|---|---|
| Outcome facts (domain) | `EvidenceSubmissionAcceptedEvent` + `EvidenceSubmissionRejectedEvent` — canon `ddd_solution_model.md:327-328`, deferred by HU-30 (`hu30-context.md:85` "**no in-scope consumer** — result query is HU-32"). Raised on the base's resolution transitions so **every** resolution path (contextual reject, QR accept, QR reject, trivia accept) emits exactly one outcome fact. This is the gap that makes AC #3/#4 reachable at all — see *Known quirks*. **Scope nuance (Stop-1 closed):** HU-30 deferred the *event types*; the **publish** side (contracts + publish handlers + `OutboxDomainEventDispatcher` arms) is **new scope added by this HU**, not inherited. Do not conflate "this HU inherits HU-30's parked events" with "it inherits HU-30's parked publishers" — HU-30 parked none. |
| Normalized rejection reason | One `RejectionReason` string on the rejected fact, normalized from the **two disjoint** reason models (`EvidenceRejectionReason` base/contextual + `TargetResolutionRejectionReason` QR). AC #4. **Stop-1 closed:** do **not** backfill `EvidenceSubmission.RejectionReason` from `TreasureEvidenceSubmission.RejectRegisteredTarget` — `EvidenceSubmission.cs:81-84` documents HU-31's invariant ("concrete forms do not set this; they flip state via `MarkRejectedByConcreteForm` and keep their own reason"), and mutating it from HU-31's path is a stealth contract change against merged predecessor code (Constraint 2). A `RejectionSource` discriminator (contextual vs QR-resolution) is **deferred** — AC #4 only requires the reason text and the panel doesn't currently distinguish them; it's an additive nullable later if the frontend UX names the need. |
| Origin reference (AC #2) | `OriginReference` (nullable string) added to `EvidenceSubmissionRegisteredEvent` + its contract, populated by each concrete form (trivia → question sequence order; QR → resolved target / scanned value) so the trace identifies the *"nodo de misión o pregunta de trivia"* grain, not just the substage. Additive — the contract has no consumer today. |
| Transport bridge (application) | `EvidenceSubmissionAcceptedIntegrationEvent` + `EvidenceSubmissionRejectedIntegrationEvent` (`[EntityName]`-named) + two publish handlers + two new `OutboxDomainEventDispatcher` arms. Mirror the shipped `PublishEvidenceSubmissionRegisteredIntegrationEventHandler`. |
| Evidence-trace read model | New `EvidenceTraceEntry` projection (own table + `DbSet` + `IEvidenceTraceRepository`), keyed by `EvidenceSubmissionId`, carrying submittedAt (AC #1), session/team/substage/origin (AC #2), validation state (AC #3), rejection reason + resolvedAt (AC #4). **Derived name** — see *Known quirks*. |
| First consumer in this service (infra) | Three thin `IConsumer<>` adapters (registered, accepted, rejected) in `Infrastructure/Messaging/Consumers/` → MediatR commands that upsert the trace. This is what makes the history *"se alimenta de forma asíncrona consumiendo los eventos … de RabbitMQ"* (AC #5). |
| Operator read surface | `GET /api/sessions/{liveSessionId}/evidence-submissions` (Operator-guarded, optional `?teamId=`) → the trace list with state + reason. PRD US-34 / `:222-223`. |
| Frontend | Operator evidence-traceability read panel (DES-43 has **no** `backend-only` label). Small, single-endpoint, read-only surface → Step 9 uses the **hu-03** exemplar shape. |

## Touched surfaces

- `backend/services/session-operations-service/` — Domain (two outcome events + origin on the
  registered event + the trace projection entity), Application (two contracts + two publish handlers +
  dispatcher arms, two trace-write command slices, the operator query slice + DTO/factory, the
  repository port), Infrastructure (trace table + `DbSet` + configuration + migration + repository,
  three consumers + `AddConsumer` registration), Api (one operator read endpoint).
- **Frontend: yes** — operator evidence-traceability panel (one endpoint, read-only).
- API contract boundary: **new** operator read endpoint + trace item response shape.
- Async contract boundary: **new** `session-evidence-submission-accepted` / `-rejected` exchanges
  (published *and* consumed by this service); `session-evidence-submission-registered` gains an
  additive `OriginReference` field and its **first** consumer.

## Committed phases

| Commit | Phase | Description |
|---|---|---|
| — | — | No commits yet |

## Known quirks / gotchas

- **The load-bearing gap: `EvidenceSubmissionRegistered` carries `Pending`, always.**
  `LiveSession.RegisterEvidenceCore` raises it with a **hardcoded** `EvidenceValidationState.Pending`
  (`LiveSession.cs` — `AddDomainEvent(new EvidenceSubmissionRegisteredEvent(…, EvidenceValidationState.Pending))`),
  *before* any accept/reject runs. So a projection fed **only** by that fact can never satisfy AC #3
  (validation state) or AC #4 (rejection reason) — it would show every evidence as `Pending` forever.
  This is why HU-32 must add the canonical outcome facts (`ddd_solution_model.md:327-328`), which HU-30
  explicitly deferred *to this HU* (`hu30-context.md:85,104`). **Do not** "fix" this by mutating the
  registered event's payload to the final state — the registration fact is the *registration* fact
  (HU-30's rule), and the state is not yet known when it is raised.
- **Two disjoint rejection-reason models must be normalized into one.** The base carries
  `RejectionReason` (`EvidenceRejectionReason` — HU-30 contextual: substage-binding / window / origin);
  `TreasureEvidenceSubmission` carries `ResolutionRejectionReason` (`TargetResolutionRejectionReason` —
  HU-31 QR match: no-target / outside-substage / already-resolved) and deliberately **does not** set the
  base field (see the comment on `EvidenceSubmission.RejectionReason`). A QR rejection therefore leaves
  the base `RejectionReason` **null**. The rejected outcome fact must carry a single normalized reason
  string sourced from whichever model applies, or AC #4 silently reports "no reason" for every rejected
  QR scan — the most likely defect in this HU.
- **Where the outcome events are raised — Stop-1 closed: raise on the base's three resolution
  transitions, gated by a targeted `OwnsMany` probe.** All four resolution paths funnel through
  exactly three base methods: `Reject(reason)` (public, called by the facade),
  `MarkAcceptedByConcreteForm()` and `MarkRejectedByConcreteForm()` (protected, called by the
  concrete forms). Raising the outcome event **inside those three methods** covers every path exactly
  once with no missed caller, and `EvidenceSubmission : BaseEntity` already has `AddDomainEvent`.
  Raising on `LiveSession` instead would force the aggregate root to know each concrete form's typed
  reason (breaking HU-31's "concrete forms own their reason" invariant at `EvidenceSubmission.cs:81-84`)
  and would require adding a `LiveSession.RejectEvidence(submission, reason)` just for the facade's
  contextual-reject path — extra surface for one event. **The load-bearing assumption:** every event in
  this service today is raised on the aggregate root (`LiveSession`), and submissions are mapped as
  **owned collections**, so this depends on `DispatchDomainEventsInterceptor.CollectDomainEvents`'
  `ChangeTracker.Entries<BaseEntity>()` (`DispatchDomainEventsInterceptor.cs:104,:126`) also returning
  owned-entity entries. EF Core should give owned types their own `EntityEntry` (so the assumption
  holds), **but no test in the repo proves it today** — `DispatchDomainEventsInterceptorBranchTests.cs`
  and the cross-service `DispatchDomainEventsInterceptorTests.cs` both exercise a `TestEntity`
  attached **directly** as a `DbSet`; neither uses an `OwnsMany` parent/child relationship. So the gap
  is a *test gap*, not a *no submission raises events today* observation. **X.1 gate: add a targeted
  probe** mirroring `DispatchDomainEventsInterceptorBranchTests.cs` but with a `ParentEntity :
  BaseEntity` that `OwnsMany(ChildEntity)` and a `ChildEntity : BaseEntity` that raises a domain event
  on save — assert the event reaches the dispatcher/publish endpoint exactly once. ~30 lines; this
  converts the Stop-1 EF assumption into a green check. **X.3 gate** still adds an integration test
  that the event reaches the outbox. **Fallback (contingency, not a parallel path):** if the probe
  surprisingly fails, raise from `LiveSession` (QR/trivia paths) + a `LiveSession.RejectEvidence(
  submission, reason)` method the facade calls instead of `submission.Reject(...)` — the cost of being
  wrong is small and localized (three method bodies + one new aggregate method). Do not leave the
  contextual-reject path eventless.
- **`MarkRejectedByConcreteForm()` needs the reason to raise a useful fact.** It is currently
  parameterless and `RejectRegisteredTarget` sets `ResolutionRejectionReason` *before* calling it.
  Change it to take the normalized reason (e.g. `MarkRejectedByConcreteForm(string reason)`); the base
  cannot read the subclass's typed reason otherwise. Keep `TreasureEvidenceSubmission.ResolutionRejectionReason`
  as-is (persisted, HU-31's) — the parameter is for the event payload, not a second store.
- **Trivia late/duplicate rejections are throws and will never appear in the trace.** HU-34 rejects
  late/repeated answers with `LateTriviaAnswerException` — the attempt is **never persisted** and no
  evidence record exists (`hu30-context.md:82`). Likewise HU-29's structural intake links
  (session-admits / team / participation / active-substage-present) **throw** and block registration.
  So the trace covers *registered* evidence only — which is exactly what AC #1 says ("Cada evidencia
  **registrada**"). Do **not** widen HU-32 to persist blocked attempts: that would change HU-29/HU-34's
  shipped behaviour and is out of scope (Constraint 2). If the reviewer wants blocked attempts traced,
  that is a Stop-1 scope call, not an implementation detail.
- **No reviewer fields. `reviewedByUserId` / `reviewedAt` are OUT of scope.** `bd_umbral_entity_spec.md:432-433`
  models them and `:982` (RF-09) lists them, **but** ADR-0010 §Decisions in scope is explicit:
  "Operator-mediated human review is out of scope for both current forms — the canonical traceability
  and rejection fields remain modeled, but both current evidence forms are system-resolved in first
  delivery." DES-43's own body repeats it ("Trazabilidad/auditoría sí; revisión mediada por operador
  **no**"). **`hu30-context.md:26,87` says the reviewer fields are "HU-32's" — that note is stale**
  (written before/against the pre-ADR-0010 framing) and is **overridden** by ADR-0010 + the DES-43 body.
  Do not add them, and do not add an approve/reject-by-operator endpoint. **(Stop-1 closed — resolved on the record.)** Leave a one-line comment on `EvidenceTraceEntry` (e.g. `// reviewer fields excluded per ADR-0010; override of hu30-context.md:26,87`) so the override is discoverable at the code site and doesn't have to be re-derived by a future reader. Do **not** update `hu30-context.md` — its inaccuracy stays as historical record; the code comment + ADR-0010 are the governed artifacts.
- **The trace read model's name is derived, not canonical.** Canon's nearest artifact is
  `EvidenceReviewQueueProjection` (`bd_umbral_entity_spec.md:931`, "makes **pending validation work**
  explicit for operators", RF-09/RF-18) — but that framing is a *review queue* for operator-mediated
  decisions, which ADR-0010 puts out of scope. Canon names **no** artifact for a system-resolved
  evidence *traceability/history* projection. `EvidenceTraceEntry` / `evidence_trace_entries` /
  `IEvidenceTraceRepository` are therefore **derived** here (canon-silent, flagged per Constraint 7),
  keeping the canonical *shape* (`:925-931` read-models-and-projections: a projection derived from
  `EvidenceSubmission` + `Team` + `MissionNode`, not an aggregate root) and the canonical repository
  rule (`ddd_solution_model.md:366` — "Repository interfaces are defined around aggregate roots **and
  clearly owned projections**"). If the reviewer prefers the canonical name, rename at Stop 1.
- **This service publishes and consumes its own events — that is intentional, and it is new here.**
  Session-operations has **zero** production consumers today (only test-fixture `IConsumer<>`s under
  `tests/IntegrationTests/Messaging/`). AC #5 requires the history to be fed by *consuming RabbitMQ
  events*, so the trace is fed through the broker (publish → exchange → this service's own receive
  endpoint), **not** by an in-process notification handler. A same-process shortcut (e.g. projecting
  straight from the domain event) would be simpler but would **fail AC #5** and defeat the "sin que el
  flujo principal dependa de RabbitMQ" decoupling. `cfg.ConfigureEndpoints(context)` is already wired,
  so `bus.AddConsumer<T>()` is all that is needed.
- **Consumers must be idempotent and order-tolerant.** Outbox delivery is **at-least-once**
  (`DuplicateDetectionWindow = 30 min` narrows, but does not eliminate, redelivery), and the accepted /
  rejected fact can arrive **before** its registered fact (separate messages, separate deliveries).
  Upsert by `EvidenceSubmissionId`, and let a resolution message create the row if absent (or leave the
  registration to fill the context fields on arrival). Never assume ordering; never throw on a duplicate.
- **The read is eventually consistent — by AC.** The endpoint reads the projection, so evidence just
  submitted may not appear until the outbox drains (`QueryDelay = 1s`) and the consumer runs. That is
  inherent to AC #5, not a bug. Integration/E2E tests must **await** the projection (poll with a
  timeout, mirroring `tests/IntegrationTests/Messaging/EvidenceSubmissionRegisteredDeliveryE2ETests.cs`),
  not assert synchronously after the write.
- **Publish handlers are plain classes dispatched pre-commit — NOT MediatR `INotificationHandler`s.**
  Mirror `PublishEvidenceSubmissionRegisteredIntegrationEventHandler`: a `sealed class` with
  `(IPublishEndpoint, ILogger)` and `Handle(TDomainEvent, ct)`, invoked from
  `OutboxDomainEventDispatcher.DispatchAsync`, `try/catch` that logs and **rethrows** (a publish failure
  is a failed outbox INSERT = a DB fault that must roll the write back). Do **not** swallow, do **not**
  add a timeout, do **not** reintroduce `IIntegrationEventPublisher` or a routing-key switch (deleted
  by #166; ADR-0017 §3).
- **`[EntityName]` names the exchange — there is no routing key.** MassTransit resolves topology by
  message type; mirror `session-evidence-submission-registered` / `session-state-changed`.
- **Namespace is `umbral_backend.*`**; ADR-0011 vertical slices (`Commands/<UseCase>/`,
  `Queries/<UseCase>/`, `Sessions/Common/`, `Sessions/EventHandlers/`). **No** `Handlers/` / `DTOs/` /
  `Facades/` type-buckets and no `*CommandHandlerBase` (the `hu09-*` exemplars predate ADR-0011 —
  mirror them for section structure only). Apply
  `docs/refactors/application-layer-overengineering-checklist.md`: no `Id > 0` marker validators, no
  dead interfaces. This HU has **no mandated pattern** to preserve — keep it plain.
- **Test projects:** domain → `tests/UnitTests/`; application → `tests/Application.UnitTests/`
  (publish-handler exemplar: `Sessions/EventHandlers/PublishEvidenceSubmissionRegisteredIntegrationEventHandlerTests.cs`
  — `Mock<IPublishEndpoint>`, publish-once + **propagates-on-failure**); integration →
  `tests/IntegrationTests/` (`Api/`, `Persistence/`, `Messaging/`; delivery exemplars
  `Messaging/EvidenceSubmissionRegisteredDeliveryE2ETests.cs`, `Messaging/EvidenceSubmissionRegisteredOutboxTests.cs`,
  `Messaging/OutboxDeliveryOnRecoveryTests.cs`). ADR-0008 shared Postgres testcontainer.
- **`ef migrations add` needs Docker** — run it with the sandbox disabled
  (`session-operations-masstransit-outbox-implementation-handoff-2026-07-12.md`). **grep**
  `ApplicationDbContextModelSnapshot.cs` for the new table rather than full-reading it.

## Per-phase derivation — authoritative for implementation

> Primary source for the phase subagent (per `backend-agent.md` "Read first").
> Mode = **feature flow**. Canon precedence per `backend-agent.md`: `ddd_solution_model.md` →
> service `CONTEXT.md` → `structure.md` → `bd_umbral_entity_spec.md` → plan docs; terminology overlay
> `adr/0010-evidence-qr-only-first-delivery.md` (umbrella; operator review out of scope).
> Every block cites its canon section; where canon is silent it says so. Read a cited section only to
> fill a gap a block leaves open — do not re-read the canon wholesale.

### Phase X.1 — Domain

**Derive** (`bd_umbral_entity_spec.md:419-451` §EvidenceSubmission — fields/relationships/allowed
`validationState` transitions; `:925-931` §Read models and projections; `ddd_solution_model.md:326-329`
outbound events (`EvidenceSubmissionRegistered`, **`EvidenceSubmissionAccepted`**,
**`EvidenceSubmissionRejected`**), `:366` repositories around "aggregate roots and clearly owned
projections"; `CONTEXT.md:95-106` §Runtime Evidence; `adr/0010` §Decisions in scope; PRD DES-70:141-144):

- **`EvidenceSubmissionAcceptedEvent`** (`Domain/Events/`) — canon `ddd_solution_model.md:327`. Fields
  (derived from the `EvidenceSubmissionRegisteredEvent` precedent; canon does not enumerate them):
  `LiveSessionId`, `TeamId`, `EvidenceSubmissionId`, `ActiveSubstageId`, `SubmissionType`,
  `SubmittedAt`, `ResolvedAt`.
- **`EvidenceSubmissionRejectedEvent`** (`Domain/Events/`) — canon `ddd_solution_model.md:328`. Same
  fields **plus `string RejectionReason`** — the **normalized** reason (AC #4), sourced from either
  `EvidenceRejectionReason` (base/contextual) or `TargetResolutionRejectionReason` (QR). Mirror
  `EvidenceSubmissionRegisteredEvent.cs`.
- **Raise both from the base's three resolution transitions** (`Domain/Entities/EvidenceSubmission.cs`)
  so every path emits exactly one outcome fact:
  - `Reject(EvidenceRejectionReason reason)` → `AddDomainEvent(new EvidenceSubmissionRejectedEvent(…, reason.ToString()))`
  - `MarkAcceptedByConcreteForm()` → `AddDomainEvent(new EvidenceSubmissionAcceptedEvent(…))`
  - `MarkRejectedByConcreteForm(string reason)` (**signature change** — was parameterless) →
    `AddDomainEvent(new EvidenceSubmissionRejectedEvent(…, reason))`; update the single caller
    `TreasureEvidenceSubmission.RejectRegisteredTarget(reason)` to pass
    `reason.ToString()` (keep it setting `ResolutionRejectionReason` first — that field stays).
  - The transitions need a resolution timestamp: pass `resolvedAt` in, or accept the submission's
    `SubmittedAt` as the resolution instant for these system-resolved forms (both forms resolve
    synchronously within the same registration call) — **prefer an explicit `resolvedAt` parameter**;
    do not reach for `DateTimeOffset.UtcNow` inside the domain.
  - See *Known quirks — where the outcome events are raised*: this is the flagged derived decision;
    the fallback (raise from `LiveSession` + a `LiveSession.RejectEvidence(...)` for the facade path)
    applies only if the interceptor does not collect owned-entity events.
- **`OriginReference` (AC #2)** — add a nullable `string? OriginReference` to
  `EvidenceSubmissionRegisteredEvent` (defaulted ctor param, so no existing call site breaks) and
  populate it in `LiveSession.RegisterEvidenceCore` from the concrete submission. Source it through a
  small `protected abstract string? DescribeOrigin()` on the base, overridden by each form: trivia →
  the question grain (e.g. `$"question:{QuestionSequenceOrder}"`), QR → the target grain (e.g.
  `$"target:{TargetSnapshotId}"`, or the scanned value when unresolved). Canon models the base context
  as session/team/**activeSubstage** only (`bd_umbral_entity_spec.md:424-427`) — the finer
  *"nodo de misión o pregunta de trivia"* grain AC #2 names is **not** canonically enumerated, so this
  field is derived to satisfy the AC. **Do not** call `DescribeOrigin` a Template Method — HU-32 has
  no mandated pattern (`required_patterns_matrix.md:45`) and naming it one is ceremony (`adr/0012:64`).
- **`EvidenceTraceEntry`** (`Domain/Entities/`) — the traceability **projection** (derived name; canon
  silent, see *Known quirks*). Not an aggregate root, not a child of `LiveSession`: a standalone
  read-model row keyed by the natural key `EvidenceSubmissionId`. Fields: `EvidenceSubmissionId`
  (natural key), `LiveSessionId`, `TeamId`, `ActiveSubstageId`, `SubmissionType`,
  `SubmittedByParticipantId?`, `OriginReference?`, `SubmittedAt` (AC #1), `ValidationState` (AC #3),
  `RejectionReason?` (string, AC #4), `ResolvedAt?`. Constructed via a factory
  (`EvidenceTraceEntry.ForRegistration(...)`) + two idempotent transitions
  (`MarkAccepted(resolvedAt)` / `MarkRejected(reason, resolvedAt)`) that are **safe to re-apply**
  (at-least-once delivery) and tolerate arriving before the registration. No mutators beyond those.

**Target files** (create | edit — file to mirror):
- create `src/Domain/Events/EvidenceSubmissionAcceptedEvent.cs` — mirror `src/Domain/Events/EvidenceSubmissionRegisteredEvent.cs`
- create `src/Domain/Events/EvidenceSubmissionRejectedEvent.cs` — mirror the same file (+ `RejectionReason`)
- edit `src/Domain/Events/EvidenceSubmissionRegisteredEvent.cs` — add defaulted `string? OriginReference`
- edit `src/Domain/Entities/EvidenceSubmission.cs` — raise the two outcome events from `Reject` /
  `MarkAcceptedByConcreteForm` / `MarkRejectedByConcreteForm(string reason)`; add
  `protected abstract string? DescribeOrigin()`
- edit `src/Domain/Entities/TreasureEvidenceSubmission.cs` — `RejectRegisteredTarget` passes the
  normalized reason to the base; override `DescribeOrigin()`
- edit `src/Domain/Entities/TriviaAnswerSubmission.cs` — override `DescribeOrigin()`
- edit `src/Domain/Entities/LiveSession.cs` — `RegisterEvidenceCore` passes `submission.DescribeOrigin()`
  into `EvidenceSubmissionRegisteredEvent`
- create `src/Domain/Entities/EvidenceTraceEntry.cs` — mirror `src/Domain/Entities/SessionEvent.cs`
  (the append-only projection-ish child added by HU-21) for the factory + no-mutator shape
- create `tests/UnitTests/Domain/Entities/EvidenceTraceEntryTests.cs` — mirror `SessionEventTests.cs` /
  `EvidenceSubmissionTests.cs`
- edit `tests/UnitTests/Domain/Entities/EvidenceSubmissionTests.cs`,
  `TreasureEvidenceSubmissionTests.cs`, `LiveSessionTests.cs` — outcome-event assertions; **every
  existing assertion still passes**

**Pattern this phase owns:** none — HU-32 has **no mandated pattern** (`required_patterns_matrix.md:45`).
Do not introduce one.
**Gate:** Domain build passes; unit tests prove — (a) a contextual `Reject(reason)` raises exactly one
`EvidenceSubmissionRejectedEvent` carrying the normalized base reason; (b) a QR
`RejectRegisteredTarget(reason)` raises exactly one `EvidenceSubmissionRejectedEvent` carrying the
**`TargetResolutionRejectionReason`** (not null — the two-reason-model trap); (c) an accepted QR scan
and an accepted trivia answer each raise exactly one `EvidenceSubmissionAcceptedEvent`; (d)
`EvidenceSubmissionRegisteredEvent` now carries an `OriginReference` identifying the trivia question /
QR target, and still fires on every registered submission; (e) `EvidenceTraceEntry` records submittedAt
+ session/team/substage/origin + state + reason, and its `MarkAccepted`/`MarkRejected` are idempotent
(re-applying the same resolution is a no-op, not a throw); (f) **all pre-existing HU-29/30/31/34 domain
tests remain green** — no observable change to intake, validation, or either form's behaviour; (g) **the
`OwnsMany` interceptor probe passes** — a `ParentEntity : BaseEntity` that `OwnsMany(ChildEntity)`
with a `ChildEntity : BaseEntity` raising a domain event on save, asserting that the event is collected
by `DispatchDomainEventsInterceptor`'s `ChangeTracker.Entries<BaseEntity>()` and reaches the dispatcher
exactly once. This converts the "raise on owned-entity entries" EF assumption (see *Known quirks — where
the outcome events are raised*) into a green check before X.2/X.3 build on it. Mirror
`tests/IntegrationTests/Infrastructure/DispatchDomainEventsInterceptorBranchTests.cs` for the shape
(in-memory DbContext, mocked dispatcher); ~30 lines.

### Phase X.2 — Application

**Derive** (`ddd_solution_model.md:327-328,615` (the audit/history consumption workflow), `:366`
(repositories around owned projections); `required_patterns_matrix.md:61-64` (canonical RabbitMQ
workflow), `:125` (applies-where `Proxy`); `adr/0017:Decision 1,3,5` (Application may reference
`MassTransit.Abstractions`/`IPublishEndpoint`; no `IIntegrationEventPublisher` wrapper; consumers stay
thin adapters that dispatch an Application command); `adr/0009` (ownership resolver Proxy);
`adr/0011` + `structure.md` (vertical slices); PRD DES-70:143-144,222-223; existing
`PublishEvidenceSubmissionRegisteredIntegrationEventHandler`, `EvidenceSubmissionRegisteredIntegrationEvent`,
`OutboxDomainEventDispatcher`, `GetOperatorTriviaAnsweredMonitorQuery(+Handler)`,
`TriviaAnsweredMonitorDtoFactory`, `Common/Interfaces/ILiveSessionRepository`):

- **Two integration contracts** (`Sessions/Common/`), mirroring
  `EvidenceSubmissionRegisteredIntegrationEvent.cs`:
  `EvidenceSubmissionAcceptedIntegrationEvent` `[EntityName("session-evidence-submission-accepted")]`
  and `EvidenceSubmissionRejectedIntegrationEvent` `[EntityName("session-evidence-submission-rejected")]`
  (the latter carries `RejectionReason`). Add `string? OriginReference` to the existing
  `EvidenceSubmissionRegisteredIntegrationEvent` record (additive; it has no consumer today).
- **Two publish handlers** (`Sessions/EventHandlers/`) mirroring
  `PublishEvidenceSubmissionRegisteredIntegrationEventHandler.cs` exactly — plain `sealed class`,
  `(IPublishEndpoint, ILogger)`, `Handle(TDomainEvent, ct)`, log + **rethrow** on failure.
- **Route them:** inject both into `OutboxDomainEventDispatcher` and add two arms to `DispatchAsync`'s
  switch (`EvidenceSubmissionAcceptedEvent` → …, `EvidenceSubmissionRejectedEvent` → …). Follow the
  existing one-event-one-handler arms; only `SessionStateChangedEvent` fans out to two (sequential
  awaits — never `Task.WhenAll`; shared scoped `DbContext`).
- **Trace-write slices** (the commands the consumers dispatch — ADR-0017 §5 keeps logic out of consumer
  bodies), ADR-0011 vertical slices under `Sessions/Commands/`:
  - `RecordEvidenceTraceRegistration/` — `{Command, CommandHandler, CommandValidator}`: upsert the
    `EvidenceTraceEntry` for `EvidenceSubmissionId` with the context + `SubmittedAt` + `OriginReference`
    (creating it, or filling the context if a resolution already created a stub). Idempotent.
  - `RecordEvidenceTraceResolution/` — `{Command, CommandHandler, CommandValidator}`: apply
    `MarkAccepted(resolvedAt)` / `MarkRejected(reason, resolvedAt)` to the entry (creating it if the
    resolution arrived first). Idempotent. One slice for both outcomes, discriminated by the command's
    state field — do not author two near-identical slices.
  - Both are `IRequest` (no `[Authorize]` — they are system/transport-driven, not user-invoked; the
    existing `AuthorizationBehaviour` only gates requests carrying the attribute).
- **`IEvidenceTraceRepository`** (`Application/Common/Interfaces/`) — `GetByEvidenceSubmissionIdAsync`,
  `UpsertAsync`, `ListBySessionAsync(liveSessionId, teamId?, ct)`. Canon `ddd_solution_model.md:366`
  sanctions a repository around a "clearly owned projection". Mirror `ILiveSessionRepository`.
- **Operator read slice** `Sessions/Queries/GetOperatorEvidenceTrace/` — mirror
  `Queries/GetOperatorTriviaAnsweredMonitor/` **exactly**:
  - `GetOperatorEvidenceTraceQuery(Guid LiveSessionId, Guid? TeamId) : IRequest<EvidenceTraceDto>`
    decorated `[Authorize(Roles = "Operator")]`.
  - Handler injects `ISessionAdministrationAccessResolver` + `IEvidenceTraceRepository`; calls
    `_accessResolver.GetAuthorizedSessionAsync(request.LiveSessionId, ct)` **first** (the ADR-0009
    resolver Proxy: Administrator sees all, Operator only their assigned session, else
    `ForbiddenAccessException`) — **no ad-hoc role/owner `if` in the handler** — then reads the trace
    and maps via a `EvidenceTraceDtoFactory` (`Sessions/Common/`, mirror `TriviaAnsweredMonitorDtoFactory`).
  - DTO in `Application/Dtos/Sessions/` (verified — the sibling operator reads keep theirs there, e.g.
    `TriviaAnsweredMonitorDto.cs`; do not create a new bucket): per-item `evidenceSubmissionId`, `teamId`,
    `activeSubstageId`, `submissionType`, `originReference`, `submittedAt`, `validationState`,
    `rejectionReason`, `resolvedAt`.
- **DI:** register the two publish handlers, the two command slices (MediatR assembly scan already
  covers handlers), and the repository (Infrastructure DI, X.3). Follow the existing
  `DependencyInjection.cs` blocks.

**Target files** (create | edit — file to mirror):
- create `src/Application/Sessions/Common/EvidenceSubmissionAcceptedIntegrationEvent.cs`,
  `.../EvidenceSubmissionRejectedIntegrationEvent.cs` — mirror `EvidenceSubmissionRegisteredIntegrationEvent.cs`
- edit `src/Application/Sessions/Common/EvidenceSubmissionRegisteredIntegrationEvent.cs` — add `string? OriginReference`
- create `src/Application/Sessions/EventHandlers/PublishEvidenceSubmissionAcceptedIntegrationEventHandler.cs`,
  `.../PublishEvidenceSubmissionRejectedIntegrationEventHandler.cs` — mirror `PublishEvidenceSubmissionRegisteredIntegrationEventHandler.cs`
- edit `src/Application/Sessions/EventHandlers/OutboxDomainEventDispatcher.cs` — inject + add the two switch arms
- create `src/Application/Sessions/Commands/RecordEvidenceTraceRegistration/{…Command.cs,…CommandHandler.cs,…CommandValidator.cs}`
  and `.../Commands/RecordEvidenceTraceResolution/{…}` — mirror an existing command slice
  (`Commands/SubmitTriviaAnswer/`) minus the `[Authorize]`
- create `src/Application/Common/Interfaces/IEvidenceTraceRepository.cs` — mirror `ILiveSessionRepository.cs`
- create `src/Application/Sessions/Queries/GetOperatorEvidenceTrace/{GetOperatorEvidenceTraceQuery.cs,GetOperatorEvidenceTraceQueryHandler.cs}`
  — mirror `Queries/GetOperatorTriviaAnsweredMonitor/`
- create `src/Application/Sessions/Common/EvidenceTraceDtoFactory.cs` — mirror `TriviaAnsweredMonitorDtoFactory.cs`
- create `src/Application/Dtos/Sessions/EvidenceTraceDto.cs` — mirror `src/Application/Dtos/Sessions/TriviaAnsweredMonitorDto.cs`
- add tests under `tests/Application.UnitTests/Sessions/EventHandlers/` (mirror
  `PublishEvidenceSubmissionRegisteredIntegrationEventHandlerTests.cs` — publish-once +
  **propagates-on-failure** with `Mock<IPublishEndpoint>`), `.../Sessions/Commands/RecordEvidenceTrace*/`,
  and `.../Sessions/Queries/GetOperatorEvidenceTrace/`

**Pattern this phase owns:** none mandated. The operator read **reuses** the ADR-0009 resolver Proxy
(applies-where, `required_patterns_matrix.md:125`) — no new gate, no new guard type.
**Gate:** Application build passes; both publish handlers map their domain event → contract and publish
via `IPublishEndpoint`, and a publish failure **propagates** (`act.Should().ThrowAsync()`, rolling the
transaction back — no swallow, no timeout); `OutboxDomainEventDispatcher` routes
`EvidenceSubmissionAcceptedEvent`/`EvidenceSubmissionRejectedEvent` to them while every existing arm is
unchanged; the two trace-write handlers are **idempotent** (re-applying the same message is a no-op)
and **order-tolerant** (a resolution arriving before its registration still yields a correct row);
`GetOperatorEvidenceTraceQuery` carries `[Authorize(Roles = "Operator")]` and its handler resolves
access **only** through `ISessionAdministrationAccessResolver` (**no ad-hoc role/owner check**), returns
each registered evidence with submittedAt + team/session/substage/origin + validation state + rejection
reason, and **a QR-rejected item surfaces a non-null reason** (the two-reason-model trap); optional
`teamId` filters. No new facade, no second publisher stack, no `IIntegrationEventPublisher`.

### Phase X.3 — Infrastructure

**Derive** (`bd_umbral_entity_spec.md:925-931` (projection derived from `EvidenceSubmission`/`Team`/
`MissionNode`); `adr/0017:Decision 2,5` (transport/topology stays in Infrastructure; consumers are thin
adapters); `adr/0008` (shared Postgres testcontainer); existing
`Infrastructure/Messaging/MassTransitMessagingRegistration.cs` (bus outbox +
**`cfg.ConfigureEndpoints(context)` already present**), `Infrastructure/Persistence/ApplicationDbContext.cs`
(today `LiveSessions` is the **only** `DbSet`), `Infrastructure/Persistence/Configurations/LiveSessionConfiguration.cs`,
`Infrastructure/Persistence/Repositories/LiveSessionRepository.cs`; the consumer exemplar
`scoring-monitoring-service/src/Infrastructure/Messaging/Consumers/AnswerRegisteredConsumer.cs`):

- **Persist the projection as its own table** — `EvidenceTraceEntry` is **not** an owned child of
  `LiveSession` (it is fed asynchronously and must be writable without loading the aggregate). Add
  `public DbSet<EvidenceTraceEntry> EvidenceTraceEntries => Set<EvidenceTraceEntry>();` to
  `ApplicationDbContext` (the first `DbSet` besides `LiveSessions`) and a **separate**
  `EvidenceTraceEntryConfiguration : IEntityTypeConfiguration<EvidenceTraceEntry>` mapping
  `evidence_trace_entries`: unique index on `evidence_submission_id` (the natural key / upsert key),
  index on `(live_session_id, team_id)` for the read, `validation_state` and `submission_type`
  `HasConversion<string>()` (mirror `LiveSessionConfiguration`'s `validation_state` mapping),
  `rejection_reason` nullable, `resolved_at` nullable, `origin_reference` nullable. **Do not** add it
  inside `LiveSessionConfiguration.cs` — that file holds the aggregate's `OwnsMany` children only.
- **`EvidenceTraceRepository : IEvidenceTraceRepository`** (`Infrastructure/Persistence/Repositories/`)
  — mirror `LiveSessionRepository`; the list read is `AsNoTracking()`. Register it in Infrastructure DI
  alongside the existing repositories.
- **EF migration** — `dotnet ef migrations add AddEvidenceTraceEntries` (adds the new table only; **no**
  change to `live_sessions`, its owned-child tables, or the outbox tables). Needs Docker → run with the
  sandbox disabled. **grep** `ApplicationDbContextModelSnapshot.cs` for the new table; do not full-read it.
- **The three consumers** (`Infrastructure/Messaging/Consumers/`) — mirror `AnswerRegisteredConsumer`
  1:1: `EvidenceSubmissionRegisteredConsumer : IConsumer<EvidenceSubmissionRegisteredIntegrationEvent>`,
  `EvidenceSubmissionAcceptedConsumer`, `EvidenceSubmissionRejectedConsumer`. Each injects
  `(ISender, ILogger)` and its `Consume` **only** logs + maps the message to
  `RecordEvidenceTraceRegistrationCommand` / `RecordEvidenceTraceResolutionCommand` and
  `await _sender.Send(…, context.CancellationToken)`. **No projection logic, no DbContext, no
  repository in the consumer body** (ADR-0017 §5).
- **Register them** in `MassTransitMessagingRegistration.AddMassTransitMessaging`:
  `bus.AddConsumer<EvidenceSubmissionRegisteredConsumer>();` (+ the other two) **before**
  `bus.UsingRabbitMq(...)`. `cfg.ConfigureEndpoints(context)` is already there and creates the receive
  endpoints bound to the `[EntityName]` exchanges — **no** topology hand-wiring, no exchange bootstrap,
  and **do not** touch the `AddEntityFrameworkOutbox` block. This service now both publishes and
  consumes these three exchanges (see *Known quirks*).

**Target files** (create | edit — file to mirror):
- edit `src/Infrastructure/Persistence/ApplicationDbContext.cs` — add the `EvidenceTraceEntries` `DbSet`
- create `src/Infrastructure/Persistence/Configurations/EvidenceTraceEntryConfiguration.cs` — mirror
  `LiveSessionConfiguration.cs` for column/conversion conventions (**not** its `OwnsMany` shape)
- create `src/Infrastructure/Persistence/Repositories/EvidenceTraceRepository.cs` — mirror `LiveSessionRepository.cs`
- create `src/Infrastructure/Messaging/Consumers/{EvidenceSubmissionRegisteredConsumer.cs,EvidenceSubmissionAcceptedConsumer.cs,EvidenceSubmissionRejectedConsumer.cs}`
  — mirror `scoring-monitoring-service/src/Infrastructure/Messaging/Consumers/AnswerRegisteredConsumer.cs`
- edit `src/Infrastructure/Messaging/MassTransitMessagingRegistration.cs` — `bus.AddConsumer<…>()` ×3
- edit `src/Infrastructure/DependencyInjection.cs` — register `IEvidenceTraceRepository`
- create `src/Infrastructure/Migrations/<timestamp>_AddEvidenceTraceEntries.cs` (+ `.Designer.cs`) — via `ef migrations add`
- add integration tests under `tests/IntegrationTests/Persistence/` (round-trip + upsert idempotency)
  and `tests/IntegrationTests/Messaging/` — mirror `Messaging/EvidenceSubmissionRegisteredDeliveryE2ETests.cs`
  (broker-backed delivery; **poll** for the projection) and `Messaging/EvidenceSubmissionRegisteredOutboxTests.cs`

**Pattern this phase owns:** none. (RabbitMQ transport reuses the existing bus outbox; the consumers are
transport adapters, not a pattern.)
**Gate:** Infrastructure build passes; `ef migrations add` produces **only** the `evidence_trace_entries`
table (assert the diff — no change to `live_sessions`, its child tables, or the outbox tables); a trace
entry round-trips with state + reason + resolvedAt, and upserting the same `EvidenceSubmissionId` twice
yields **one** row (at-least-once safety); an integration test proves the three domain facts are inserted
into the transactional outbox atomically with the evidence write, and a broker-backed delivery test
proves that submitting evidence end-to-end results in a trace row reaching its **terminal** state
(`Accepted`, or `Rejected` **with a non-null reason** for a wrong QR scan) once the outbox drains —
polled, not asserted synchronously; consumers contain no projection logic; no second publisher stack and
no exchange bootstrap.

### Phase X.4 — Api

**Derive** (PRD DES-70:143-144 (US-34 detailed evidence review by team and session), `:222-223`
(HU-32 is a separate CQRS read surface); `required_patterns_matrix.md:125` (applies-where `Proxy`);
`adr/0001`/`0002`/`0009` (gateway auth + ownership resolver); `adr/0018` (centralized ProblemDetails —
no controller `try/catch`); `adr/0005` (coverage gate); existing `Api/Controllers/SessionsController.cs`
operator read actions (e.g. the `AuthorizationPolicies.Operator`-guarded `[HttpGet("{liveSessionId:guid}/teams")]`)
and `Api/Services/ProblemDetailsExceptionHandler.cs`):

- Add the operator read endpoint
  **`GET /api/sessions/{liveSessionId:guid}/evidence-submissions`** with
  `[Authorize(Policy = AuthorizationPolicies.Operator)]`, an optional `[FromQuery] Guid? teamId`,
  forwarding to `GetOperatorEvidenceTraceQuery` via `ISender`. Mirror the existing operator `HttpGet`
  actions — thin: translate transport, delegate, return. No `try/catch` (ADR-0018); a
  `ForbiddenAccessException` from the resolver Proxy surfaces as RFC-7807 through the global handler.
- The endpoint inherits the standard gateway + `AuthorizationBehaviour` + ADR-0009 ownership resolver
  guard. HU-32 is matrix-tagged *applies-where* `Proxy` (`:125`) → **note only, no new gate**.
- Response: the trace list — each item carrying submittedAt (AC #1), team/session/substage/origin
  (AC #2), validation state (AC #3), rejection reason when rejected (AC #4).

**Target files** (create | edit — file to mirror):
- edit `src/Api/Controllers/SessionsController.cs` — add the `evidence-submissions` operator action;
  mirror the existing `[HttpGet("{liveSessionId:guid}/teams")]` operator read action
- create/extend `tests/IntegrationTests/Api/` — endpoint tests (mirror an existing operator endpoint test)
- extend the end-to-end messaging test from X.3 to assert through the endpoint

**Pattern this phase owns:** none new — the endpoint inherits `[Authorize(Policy = Operator)]` +
`AuthorizationBehaviour` + the ADR-0009 resolver Proxy (applies-where, no new gate).
**Gate:** endpoint integration tests prove — the assigned operator gets the session's registered
evidence with date/time, team, session, substage + origin, validation state, and (when rejected) the
rejection reason; `?teamId=` filters to one team; a **non-assigned** operator gets RFC-7807 403 and an
unauthenticated/participant caller is rejected (resolver Proxy inherited, no ad-hoc check); an
end-to-end test proves a QR scan submitted through the participant endpoint appears in the trace with
its **terminal** state after the outbox drains and the consumer runs (**polled** — the read is
eventually consistent by AC #5), including a rejected scan carrying its reason; the write path is
unaffected when the broker is down (bus outbox off the critical path — the evidence write still
commits and the trace catches up on recovery); **ADR-0005 coverage** (service aggregate branch coverage
≥ 93).

# HU-31 Context — Server-side QR Target scan, validation & resolution

> Paste this section into any agent session that needs context for HU-31 (DES-42).
> Last updated: 2026-07-13 | Branch: `feature/hu-31-qr-target-scan-resolution`
>
> HU-31 implements the **treasure-hunt QR form** (`TreasureEvidenceSubmission`) of the
> `EvidenceSubmission` umbrella — the sibling of the trivia form (`TriviaAnswerSubmission`,
> HU-34, Done). It specializes the generic intake (HU-29) + context validation for the QR
> case and adds the second ordered fact of the QR flow: `TargetResolved`.
>
> **The shared substrate already exists.** HU-29 (DES-39, Done) landed the abstract
> `EvidenceSubmission` base, the `IEvidenceIntakeFacade`, the generic `EvidenceIntakeValidationChain`,
> the `EvidenceSubmissionRegistered` domain→integration→outbox bridge, and reserved
> `EvidenceSubmissionType.TreasureHuntQrScan`. HU-34 (DES-46, Done) landed the concrete-form
> template (`TriviaAnswerSubmission` + `AnswerRegisteredEvent`). HU-31 **reuses** both and mirrors
> the trivia form; it does **not** rebuild the base, the facade, or the generic chain.

## State

- DES-42 (HU-31): **Todo**, labels: `svc:session-operations-service`, `Feature`, `backend-only`,
  `canon-realign`, `ready-for-agent`
- **Resolved mode: feature flow.** DES-42 carries `canon-realign` **without** `needs-rebuild`, so
  it is a comment-only reword, **already applied** — the `⚠️ Deuda de canon` comment (2026-06-17)
  reworded the AC to canon: `Target` is the QR-validated object, `Clue` is optional guidance, never
  the validation object. The current AC is canon-tight; build from it. This is **not** a
  realignment-rebuild — there is no existing HU-31 code to keep/delete/decide (`TreasureEvidenceSubmission`
  and `TargetResolved` appear in **zero** `.cs` files today).
- **Supersession check (clear).** DES-42 is in the realignment map's ✅ living row
  (`canon-realignment-after-mission-runtime-rewrite.md:89`), not the superseded column — safe to
  generate.
- **Superseded handling applied to predecessors:** the session-lifecycle predecessors were rebuilt.
  HU-21A `DES-28` → **`DES-76`** (Done) and HU-22 `DES-30` → **`DES-77`** (Done); the canceled
  `DES-28`/`DES-30` are dropped and their rebuilds substituted (map §"Stale-blocker repair").
- Same-service **build-on** predecessors (Done/merged):
  **DES-39 (HU-29)**, **DES-46 (HU-34)**, **DES-76 (HU-21A)**, **DES-77 (HU-22)**,
  **DES-86 (per-target scoring refactor)**, **DES-87 (TargetSnapshot.Score hardening)**,
  **DES-22 (HU-15, runtime snapshot substrate)**
- Same-service **landed but untouched** by this HU: DES-24 (HU-17), DES-25 (HU-18), DES-26 (HU-19),
  DES-27 (HU-20), DES-29 (HU-21 audit), DES-36 (HU-26 clue release), DES-32 (HU-24A), DES-49 (HU-36A),
  DES-45 (HU-33B), DES-75 (HU-16), DES-78 (HU-33A), DES-93 (TreasureHunt timer). **DES-31 (HU-23)** is
  a **downstream consumer** (its live board reads the `resolvedTargets` counter HU-31 fills), not a
  predecessor to build on.
- PRD DES id: **DES-70** → `backend/docs/prd/DES-70-primera-implementacion-de-session-operations-service-hu-15-a-hu-36.md`
  (US-32 "QR/token target scans validated by the server, so target resolution is authoritative", :139-140;
  scope :50-51, :251-252). Read the **local** file, not Linear.
- Canon overlay still relevant (service was realigned):
  `backend/docs/canon-realignment-after-mission-runtime-rewrite.md`
- Branch: `feature/hu-31-qr-target-scan-resolution`, base **`develop`** (no same-service predecessor
  In Progress; all build-on predecessors are Done/merged)

## Required design patterns

| Pattern | Owning phase | Why (from patterns matrix) | Concrete obligation |
|---|---|---|---|
| `Chain of Responsibility` (mandated) | X.2 Application | `required_patterns_matrix.md:42,124`: "Server-side QR target validation (belongs to active substage, not duplicate/out-of-context) is the QR-submission validator chain (`TargetResolutionPolicy`)". `adr/0004-required-domain-patterns.md:3`; `ddd_solution_model.md:444` names `TargetResolutionPolicy` ("prevents duplicate or invalid target resolution"). | Realize `TargetResolutionPolicy` as an ordered, composable validator chain (builder + abstract link + concrete links in `Validators/`) under `Application/Sessions/Common/TargetResolution/`. Ordered links — QR resolves to a Target → Target belongs to the active treasure-hunt substage → not already resolved by this team — decide handle-or-pass-on via `SetNext`/`Next`; the first failing link supplies the rejection reason. **Not** one handler with sequential `if` blocks (`adr/0012:64` ceremony test). |

**Facade — reused, no new obligation.** The matrix mandates `Facade` for HU-29, not HU-31.
HU-31 orchestrates through the **existing** `IEvidenceIntakeFacade` (`Application/Sessions/Common/`,
shipped by HU-29). Do **not** author a second facade; reuse `RegisterAsync<TSubmission>`.

**Applies-where `Proxy` (no new gate).** The participant write endpoint is
`[Authorize(Policy = Participant)]` + the MediatR `AuthorizationBehaviour` (`[Authorize(Roles="Participant")]`
on the command) + the reused `RuntimeParticipationGuard`. HU-31 is **not** in the matrix's
applies-where `Proxy` set — this is the standard inherited gateway/authorization guard (ADR-0001/0002),
not a new pattern gate.

Transport note: HU-31 publishes to **RabbitMQ** (both `EvidenceSubmissionRegistered` — inherited —
and the new `TargetResolved`) via the existing MassTransit EF-Core transactional outbox
(ADR-0017). No SignalR obligation in this HU's AC. The matrix transport table (`:59`) lists HU-31
under "neither", but the AC (intake + `TargetResolved` to RabbitMQ) and `ddd_solution_model.md:326-329`
(events `EvidenceSubmissionRegistered`, `TargetResolved`) are authoritative — the coarse table is
stale on this point; carry RabbitMQ.

## What predecessors have already landed

**Build-on (read for derivation):**

- **DES-39 (HU-29) — Done. The intake substrate — reuse, do not rebuild.** Abstract
  `EvidenceSubmission` base (`Pending`/`Accepted`/`Rejected` state, context fields), the
  `IEvidenceIntakeFacade.RegisterAsync<TSubmission>(context, validateConcreteForm, registerConcreteForm, ct)`
  order (generic chain → concrete-form validation → aggregate register → persist), the generic
  `EvidenceIntakeValidationChain` (CoR: `RuntimeParticipationLink` → `SessionAdmitsReceptionLink` →
  `ActiveSubstagePresentLink`), the `EvidenceSubmissionRegisteredEvent` → integration event → MassTransit
  bus-outbox bridge, and the reserved `EvidenceSubmissionType.TreasureHuntQrScan = 1`. The generic chain's
  `ActiveSubstagePresentLink` deliberately leaves target/question ownership to the concrete form
  ("Concrete forms decide whether the caller's declared target or question belongs to that active
  substage") — **that is HU-31's `TargetResolutionPolicy` seam.**
- **DES-46 (HU-34) — Done. The concrete-form template to mirror.** `TriviaAnswerSubmission :
  EvidenceSubmission` (private ctor + `Begin`/`Accept` factories + `internal` accept hook calling base
  `MarkAcceptedByConcreteForm()`), `LiveSession.RegisterTriviaAnswer(...)` going through
  `RegisterEvidenceCore` (which raises `EvidenceSubmissionRegisteredEvent`), the `AnswerRegisteredEvent`
  domain event (carries `IsCorrect` + `ScoreValue` — the payload precedent HU-31's AC cites), the
  `TriviaAnswerValidation/` chain (parallel structure for the CoR), `SubmitTriviaAnswer` command slice,
  `AnswerRegisteredIntegrationEvent` + `PublishAnswerRegisteredIntegrationEventHandler` + the
  `OutboxDomainEventDispatcher` switch. HU-31 mirrors every one of these for the QR/target form.
- **DES-76 (HU-21A) — Done.** `SessionState` machine `Scheduled→Preparing→Active→Paused→Finished→Cancelled`
  and the `State` pattern (`Domain/Services/SessionStates/`): `ILiveSessionState.EnsureCanRegisterEvidence`
  is a no-op only in `ActiveLiveSessionState`; every other state throws. `RegisterEvidenceCore` calls it
  first, so HU-31's QR registration inherits AC#4 (no scans when paused/finished/cancelled) for free.
- **DES-77 (HU-22) — Done.** Authoritative timer keyed to the active `SubstagePlayMode`. Not directly
  consumed by HU-31 (target resolution has no timer window, unlike trivia's answer window) — noted so the
  driver does not attach a timer gate to QR scans.
- **DES-86 (per-target scoring refactor) — Done (no context file — refactor).** `Target.Score` is a
  non-nullable `ScoreValue`; `WinnerScore` is removed. This is the per-target model HU-31's `TargetResolved`
  payload reads.
- **DES-87 (TargetSnapshot.Score hardening) — Done (no context file — hardening).** In session-operations,
  `TargetSnapshot.Score` is now a **non-nullable `int`** with a `score > 0` ctor guard (`int?` was
  vestigial). HU-31 relays this value into `TargetResolved` as-is.
- **DES-22 (HU-15) — Done.** Session creation freezes the immutable `MissionRuntimeSnapshot`
  (`StageSnapshot`/`SubstageSnapshot`/`TargetSnapshot`/`ClueSnapshot`) HU-31 reads to resolve the scanned
  QR against the active substage's targets. HU-31 changes **no** snapshot shape.

**Landed, untouched by this HU:** DES-24/25/26/27 (session setup/reads), DES-29 (HU-21 state-change audit),
DES-36 (HU-26 manual clue release — a `Clue` is optional guidance; HU-31 validates the `Target`, never the
clue), DES-32/49 (operator/monitor projections), DES-45/75/78 (trivia runtime), DES-93 (TreasureHunt timer).
DES-31 (HU-23 live board) is a **downstream consumer** of HU-31's `resolvedTargets` counter, not a build-on.

**Coverage:** no stable carried-forward aggregate percentage is recorded for this seam; the ADR-0005
gate (`coverlet.msbuild`, threshold **93** line+branch, aggregate) is enforced by `dotnet test` — verify
the real service percentage at X.4.

## What this HU adds

| Concern | New work |
|---|---|
| QR evidence specialization | Add `TreasureEvidenceSubmission : EvidenceSubmission` (concrete QR form, reserved `EvidenceSubmissionType.TreasureHuntQrScan`) recording the scanned value + the resolved target id; mirror `TriviaAnswerSubmission`. |
| Unconditional intake | Every context-valid scan registers a `TreasureEvidenceSubmission` (`Pending`) and publishes `EvidenceSubmissionRegistered` after transactional success — including scans that later reject (AC#5). Reuse the inherited `EvidenceSubmissionRegisteredEvent`. |
| Server-side target resolution | Resolve the scanned QR/token against the **active treasure-hunt substage's** `Target` (the QR-validated objective) — never a `Stage`, never a `Clue`. Correct match → accept the evidence + resolve the target for the team. |
| `TargetResolved` fact | New `TargetResolvedEvent` domain event, raised **only** on a correct match, carrying the resolved target's `ScoreValue`; published to RabbitMQ via the existing outbox (second ordered fact after `EvidenceSubmissionRegistered`). Mirror `AnswerRegisteredEvent`. |
| Retained auto-rejection | Wrong / invalid / duplicate / out-of-context scans auto-reject: the submission is persisted as `Rejected` with a consistent reason (retained for audit, ADR-0010) and **no** `TargetResolved` is published. Adds a `MarkRejected(reason)` transition to the base (the `Rejected` state is currently unused). |
| `TargetResolutionPolicy` chain | New Chain-of-Responsibility validator chain deciding accept-vs-reject (QR resolves to a Target → Target in active substage → not already resolved by the team), yielding the rejection reason on first failure. |
| `resolvedTargets` counter | Fill the per-team resolved-target count in `LiveSession.BuildTreasureHuntContext` (currently hardcoded `0` with the comment "HU-31 owns it"); HU-23's board consumes it. |

## Touched surfaces

- `backend/services/session-operations-service/` — Domain, Application, Infrastructure, Api
- **Frontend: none** — DES-42 is `backend-only`. `TargetResolved` and `EvidenceSubmissionRegistered` are
  consumed by backend services (scoring-monitoring/HU-37 ledger, audit/history/HU-40A, HU-23 board via the
  read model). No client-facing contract in this slice — see the prompt's Step 9 (downstream contract
  hand-off), no Step 9b.
- API contract boundary: new participant QR write endpoint + acceptance/rejection response shapes
- Async contract boundary: new `TargetResolved` RabbitMQ message consumed downstream by scoring/audit;
  `EvidenceSubmissionRegistered` (inherited) continues on the QR path

## Committed phases

| Commit | Phase | Description |
|---|---|---|
| — | — | No commits yet |

## Known quirks / gotchas

- **QR rejection is *retained*, unlike trivia's *throw*.** HU-34 rejects late/duplicate answers by
  throwing a typed exception — the attempt never persists. HU-31 is the opposite: intake is
  **unconditional** (AC#5), so a wrong/duplicate/out-of-context scan is **registered** as `Rejected`
  (retained for audit history — ADR-0010 §Decisions in scope, `0010:43-46`) with a consistent reason,
  and `EvidenceSubmissionRegistered` still fires. Consequence: `TargetResolutionPolicy` **yields a
  verdict** (accept + reason-on-fail), it does **not** throw; and the base `EvidenceSubmission` needs a
  new `MarkRejected(string reason)` transition (the `Rejected` enum value is presently unused). Only the
  **pre-intake** context guards (the reused generic chain: session-admits-reception, participation,
  active-substage-present) throw and block registration — those cover AC#2/AC#4 ("no acepta escaneos"
  when paused/finished/cancelled).
- **Boundary: pre-intake block vs post-intake retained-reject.** The AC lists "fuera de contexto" in
  both the pre-accept guard (AC#2/#4) and the auto-reject list (AC#9). Interpretation used here:
  session/team/active-substage-present failures → **pre-intake throw, not registered**; QR-does-not-match /
  target-not-in-active-substage / duplicate → **post-intake retained `Rejected`**. This split is noted in
  the prompt rationale; confirm at Stop 1 if the reviewer wants a different cut.
- **`Clue` is never the validation object.** Validate the scanned QR against the `Target`
  (`bd_umbral_entity_spec.md:104-134`); a `Clue` is optional guidance attached to at most one `Target`
  (`:136-161`) and releasing it **never** advances the substage (`:161`). The 2026-06-17 `⚠️ Deuda de
  canon` comment fixed an earlier AC that inverted this — do not reintroduce a "validate against the
  active clue" reading.
- **Relay the score; do not re-bound it.** `TargetResolved` carries the `TargetSnapshot.Score` verbatim
  (a positive non-nullable `int` after DES-87). Session-operations **relays**, it does not author or
  re-validate the range (`adr/0015:64-72` ownership split — MissionDesign authors, SessionOperations
  relays, ScoringMonitoring accumulates). The AC's "entero 1-100" is the **pre-amendment** framing:
  `adr/0015:19-29` (2026-07-10) supersedes it — authored score is now derived `{50, 100, 150}` and
  `ScoreValue.MaximumPoints` rose to 150. So do **not** hardcode a `1..100` check in session-operations;
  read whatever positive int the snapshot carries. Recorded on the ticket as the `⚠️ Nota de canon`
  (DES-42, 2026-07-13) so the AC's "1-100" wording and this spec no longer disagree.
- **`TargetResolved` field list is not canonically enumerated.** `adr/0015:56-62` mandates the payload
  carries the resolution fact + the authored `ScoreValue`, but no doc lists the fields. Derived here from
  the `AnswerRegisteredEvent` precedent + the `TargetResolution` entity (`bd_umbral_entity_spec.md:567-599`):
  `LiveSessionId`, `TeamId`, `EvidenceSubmissionId`, `ActiveSubstageId` (the MissionNode/substage),
  `TargetSnapshotId`, `ScoreValue` (int), `ResolvedAt`. Flagged as derived, not spec'd.
- **Substage advancement is out of scope.** `ddd_solution_model.md:326-329,447` names
  `SubstageAdvancementPolicy` / `TreasureHuntSubstageWon` / `SubstageAdvanced` ("first team to resolve
  all targets wins the substage"). HU-31's AC covers **per-target** resolution + `TargetResolved` only —
  it does not mention winning/advancing. Do **not** invent substage-advancement scope here (Constraint 2);
  it belongs to a later HU. HU-31 only fills the per-team `resolvedTargets` count.
- **One resolution per target per team.** `bd_umbral_entity_spec.md:565` — a team may successfully
  resolve each target at most once; the duplicate guard is the third `TargetResolutionPolicy` link and,
  on hit, yields a retained `Rejected` (not a throw).
- **Reuse the outbox, add one contract + one switch arm.** Both events flow through the existing
  MassTransit EF-Core bus-outbox (atomic with the write). New: `TargetResolvedIntegrationEvent` contract +
  `PublishTargetResolvedIntegrationEventHandler` + a `TargetResolvedEvent` arm in
  `OutboxDomainEventDispatcher`. No second publisher stack, no new AMQP bootstrap.

## Per-phase derivation — authoritative for implementation

> Primary source for the phase subagent (per `backend-agent.md` "Read first").
> Mode = **feature flow** (canon-realign reword already applied). Canon precedence:
> `ddd_solution_model.md` → service `CONTEXT.md` → `structure.md` → `bd_umbral_entity_spec.md` →
> plan docs. Realignment overlay applies where it tightened terms (`Target` is the validation object;
> `Clue` is optional guidance). Every derivation cites its canon section; where canon is silent it says so.

### Phase X.1 — Domain
**Derive** (`bd_umbral_entity_spec.md:104-134` §Target, `:136-161` §Clue, `:402-451` §EvidenceSubmission,
`:537-565` §TreasureEvidenceSubmission, `:567-599` §TargetResolution, `:905` EvidenceValidationState;
`ddd_solution_model.md:233-235,326-329,442-447`; `adr/0010:37-46`; `adr/0015:56-62,99-101`;
`adr/0004:3`):
- `TreasureEvidenceSubmission : EvidenceSubmission` (sealed, concrete QR form) — uses reserved
  `EvidenceSubmissionType.TreasureHuntQrScan`; adds the scanned value (QR/token) and the resolved
  `TargetSnapshotId`. Private ctor + `Begin(...)` (Pending) / `Accept(...)` factories + an `internal`
  accept hook calling base `MarkAcceptedByConcreteForm()`; mirror `TriviaAnswerSubmission`. On a failed
  match the form is registered then rejected via the new base transition below.
- `EvidenceSubmission` (base, **edit**): add `MarkRejected(string reason)` → sets
  `ValidationState = Rejected` + stores the reason (the `Rejected` value is currently unused; canon
  `:449,905` allows `pending → accepted or rejected`). This is the retained-rejection transition.
- `TargetResolvedEvent` (domain event) — raised **only** on a correct match. Fields (derived; canon
  silent on the exact list — `adr/0015:56-62` + `bd_umbral_entity_spec.md:567-599`): `LiveSessionId`,
  `TeamId`, `EvidenceSubmissionId`, `ActiveSubstageId`, `TargetSnapshotId`, `ScoreValue` (int, relayed
  from `TargetSnapshot.Score`), `ResolvedAt`. Mirror `AnswerRegisteredEvent`.
- `LiveSession.RegisterTargetScan(...)` — mirror `RegisterTriviaAnswer`: go through `RegisterEvidenceCore`
  (raises `EvidenceSubmissionRegisteredEvent`, inherits `EnsureSessionAdmitsEvidence` state guard),
  resolve the scanned value against `MissionRuntimeSnapshot.TargetSnapshots` where
  `SubstageSnapshotId == ActiveSubstageId` (active treasure-hunt substage only), then on match →
  accept + raise `TargetResolvedEvent`; on miss/duplicate/out-of-substage → `MarkRejected(reason)`, no
  event. Fill the `resolvedTargets` count in `BuildTreasureHuntContext` (currently hardcoded `0`).
- Rejection reasons: a typed reason (enum + message) carried on the rejected submission — consistent
  wording per AC#9. Not thrown for match failures (retained); thrown only for structural impossibility
  (no active treasure-hunt substage), which the pre-intake chain already blocks.

**Target files** (create | edit — file to mirror):
- create `src/Domain/Entities/TreasureEvidenceSubmission.cs` — mirror `src/Domain/Entities/TriviaAnswerSubmission.cs`
- edit `src/Domain/Entities/EvidenceSubmission.cs` — add `MarkRejected(string reason)` + rejection-reason
  field; mirror the existing `MarkAcceptedByConcreteForm()` transition
- create `src/Domain/Events/TargetResolvedEvent.cs` — mirror `src/Domain/Events/AnswerRegisteredEvent.cs`
- edit `src/Domain/Entities/LiveSession.cs` — add `RegisterTargetScan(...)` mirroring `RegisterTriviaAnswer`;
  resolve the target from the snapshot; fill `resolvedTargets` in `BuildTreasureHuntContext`
- create `src/Domain/Enums/TargetResolutionRejectionReason.cs` (or reuse an existing reason type if one
  exists) — mirror the trivia rejection-reason modeling
- create `tests/UnitTests/Domain/Entities/TreasureEvidenceSubmissionTests.cs` — mirror `TriviaAnswerSubmissionTests.cs`
- edit `tests/UnitTests/Domain/Entities/LiveSessionTests.cs` — add target-scan registration/resolution tests

**Pattern this phase owns:** none (CoR lands in X.2; the session-state guard is the inherited `State` pattern).
**Gate:** Domain build passes; unit tests cover every new public domain type and each branch of
`RegisterTargetScan`: correct QR → evidence `Accepted` + `TargetResolvedEvent` raised once carrying the
relayed `ScoreValue`; wrong QR → evidence persisted `Rejected` + reason + **no** `TargetResolvedEvent`;
duplicate (same team, already-resolved target) → `Rejected` + reason; QR matching a target outside the
active substage → `Rejected` + reason; `EvidenceSubmissionRegisteredEvent` raised on every registered scan;
paused/finished/cancelled rejected via the inherited state guard; `resolvedTargets` reflects the team's
resolved count. **No `1..100` score check in domain — the relayed value is used verbatim.**

### Phase X.2 — Application
**Derive** (`required_patterns_matrix.md:124`; `adr/0004:3`; `adr/0012:64`;
`adr-0012-pattern-realizations-by-layer.md:94-108`; `ddd_solution_model.md:444,453-458,611-616`;
`structure.md:282-294` + `adr/0011:40-62`; existing `IEvidenceIntakeFacade`, `EvidenceIntakeValidationChain`,
`SubmitTriviaAnswer` slice, `OutboxDomainEventDispatcher`, `AnswerRegisteredIntegrationEvent`):
- One vertical-slice command `RegisterTargetScan` in `Application/Sessions/Commands/RegisterTargetScan/`
  (`[Authorize(Roles="Participant")]`): the handler loads the aggregate, builds the
  `EvidenceIntakeValidationContext` (pre-intake) and a `TargetResolutionContext` (concrete-form), and
  calls the **existing** `_evidenceIntakeFacade.RegisterAsync(intakeContext, validateConcreteForm,
  session => session.RegisterTargetScan(...), ct)`. Mirror `SubmitTriviaAnswerCommandHandler` 1:1.
- **`TargetResolutionPolicy` = Chain of Responsibility** in
  `Application/Sessions/Common/TargetResolution/`: `TargetResolutionLink` (abstract, `SetNext`/`Next`/
  `ValidateAsync`→`CheckAsync`), `TargetResolutionChain` (builder wiring links in DI order), and
  `Validators/{TargetExistsForScanLink, TargetBelongsToActiveSubstageLink, TargetNotAlreadyResolvedLink}.cs`.
  Mirror `Application/Sessions/Common/TriviaAnswerValidation/` (which mirrors `StateTransitions/`). The
  chain evaluates in order and returns a verdict (first failing link → reason); it does not throw for a
  match failure (retained-reject, see quirks). Register links in DI order.
- Transport bridge: `TargetResolvedIntegrationEvent` (`[EntityName("session-target-resolved")]`) mapped
  from `TargetResolvedEvent` and published via `IPublishEndpoint`; `PublishTargetResolvedIntegrationEventHandler`;
  a `TargetResolvedEvent` arm added to the `OutboxDomainEventDispatcher` switch. `EvidenceSubmissionRegistered`
  is inherited (no new bridge). Mirror `AnswerRegisteredIntegrationEvent` + its publish handler.
- Result DTO in `Application/Dtos/Sessions/RegisterTargetScanResultDto.cs` — returns acceptance/outcome
  metadata (session, team, target id, resolved-or-rejected + reason). Mirror `SubmitTriviaAnswerResultDto`.

**Target files** (create | edit — file to mirror):
- create `src/Application/Sessions/Commands/RegisterTargetScan/{RegisterTargetScanCommand.cs,RegisterTargetScanCommandHandler.cs,RegisterTargetScanCommandValidator.cs}` — mirror `Commands/SubmitTriviaAnswer/`
- create `src/Application/Sessions/Common/TargetResolution/{TargetResolutionChain.cs,TargetResolutionLink.cs}` + `.../Validators/{TargetExistsForScanLink.cs,TargetBelongsToActiveSubstageLink.cs,TargetNotAlreadyResolvedLink.cs}` — mirror `Common/TriviaAnswerValidation/`
- create `src/Application/Dtos/Sessions/RegisterTargetScanResultDto.cs` — mirror `SubmitTriviaAnswerResultDto.cs`
- create `src/Application/Sessions/Common/TargetResolvedIntegrationEvent.cs` — mirror `AnswerRegisteredIntegrationEvent.cs`
- create `src/Application/Sessions/EventHandlers/PublishTargetResolvedIntegrationEventHandler.cs` — mirror `PublishAnswerRegisteredIntegrationEventHandler.cs`
- edit `src/Application/Sessions/EventHandlers/OutboxDomainEventDispatcher.cs` — add the `TargetResolvedEvent` switch arm
- edit `src/Application/DependencyInjection.cs` — register the `TargetResolution` links in order (mirror the generic-chain registration)
- reuse `src/Application/Sessions/Common/IEvidenceIntakeFacade.cs` + `RuntimeParticipationGuard.cs` + `IPublishEndpoint` — do **not** author a second facade or publisher
- add tests under `tests/Application.UnitTests/Sessions/Commands/RegisterTargetScan/` + `tests/Application.UnitTests/Sessions/Common/TargetResolution/`

**Pattern this phase owns:** `Chain of Responsibility` (`TargetResolutionPolicy`).
**Gate:** Application build passes; `RegisterTargetScan` is one vertical slice reusing the existing
facade (no ad-hoc intake in the handler, no second facade); handler + validator tests cover the accepted
path and each rejection branch; the result DTO surfaces a consistent rejection reason. **Chain of
Responsibility verified — `TargetResolution` links run in stable order, first-fail supplies the reason,
links are independently testable and reorderable; not one handler with sequential `if` blocks.**
`TargetResolvedIntegrationEvent` bridges off the resolved fact only; `EvidenceSubmissionRegistered` still
fires for every registered scan.

### Phase X.3 — Infrastructure
**Derive** (`bd_umbral_entity_spec.md:402-451,537-565`; existing `LiveSessionConfiguration` owned-child
mapping, `LiveSessionRepository` eager-load, `MassTransitMessagingRegistration` bus-outbox; ADR-0017):
- Persist `TreasureEvidenceSubmission` as an owned collection of `LiveSession`
  (table-per-concrete-form, since `EvidenceSubmission` is abstract): `OwnsMany(session =>
  session.TreasureEvidenceSubmissions, ...)` into e.g. `live_session_treasure_qr_submissions`, keyed by
  `EvidenceSubmissionId`. Mirror the existing trivia `OwnsMany` block.
- Add the collection to the repository eager-load `Include(...)` so a scan round-trips with the aggregate.
- Add the EF migration (`ef migrations add`). MassTransit bus-outbox is already configured — no new
  messaging infra; the new `TargetResolvedIntegrationEvent` is published through the same transactional
  outbox, atomic with the write. Verify both `EvidenceSubmissionRegistered` and `TargetResolved` land in
  the outbox in one transaction.

**Target files** (create | edit — file to mirror):
- edit `src/Infrastructure/Persistence/Configurations/LiveSessionConfiguration.cs` — add the QR-submission
  `OwnsMany` block; mirror the trivia-answer owned-collection mapping
- create `src/Infrastructure/Persistence/Migrations/<timestamp>_AddTreasureEvidenceSubmission.cs` — generate via `ef migrations add`
- edit `src/Infrastructure/Persistence/Repositories/LiveSessionRepository.cs` — add the new collection to the `Include(...)` chain
- add integration tests in `tests/IntegrationTests/Persistence/` + `tests/IntegrationTests/Messaging/` — mirror `EvidenceSubmissionRegisteredOutboxTests.cs`

**Pattern this phase owns:** none.
**Gate:** Infrastructure build passes; `ef migrations add` succeeds; a `TreasureEvidenceSubmission`
round-trips through `LiveSessionRepository` with `ValidationState` persisted (`Pending`→`Accepted`/`Rejected`
+ reason); an integration test proves that on a correct scan **both** `EvidenceSubmissionRegistered` and
`TargetResolved` are inserted into the transactional outbox atomically with the write, and on a rejected
scan only `EvidenceSubmissionRegistered` is; no second publisher stack.

### Phase X.4 — Api
**Derive** (`SessionsController` participant actions; `AuthorizationPolicies.Participant`;
`AuthorizationBehaviour`; global ProblemDetails handler):
- Add the participant write endpoint `POST /api/sessions/{liveSessionId:guid}/participants/target-scans`
  with a participant-authorized body carrying the runtime `teamId`, the scanned QR/token value, and the
  optional participation token. Forward to `RegisterTargetScanCommand` via `ISender`. Mirror
  `SubmitTriviaAnswerAsync` + its nested request record.
- On a correct scan return acceptance/outcome metadata (target resolved, score is **not** leaked to the
  participant response — it travels only on the RabbitMQ fact for scoring). A retained-`Rejected` scan
  returns a consistent RFC 7807 ProblemDetails reason; a pre-intake context failure (paused/finished/
  cancelled, non-member) surfaces as ProblemDetails through the global handler.

**Target files** (create | edit — file to mirror):
- edit `src/Api/Controllers/SessionsController.cs` — add the `target-scans` participant action +
  `RegisterTargetScanRequest` record; mirror `SubmitTriviaAnswerAsync` / `SubmitTriviaAnswerRequest`
- add API integration tests under `tests/IntegrationTests/Api/` + an end-to-end test under
  `tests/EndToEndTests/` mirroring the evidence-delivery E2E

**Pattern this phase owns:** none new — the endpoint inherits `[Authorize(Policy = Participant)]` +
`AuthorizationBehaviour` (`[Authorize(Roles="Participant")]` on the command) + `RuntimeParticipationGuard`
(applies-where `Proxy`, no new gate).
**Gate:** endpoint integration tests prove a correct QR scan returns acceptance metadata (no score leak),
a wrong/duplicate/out-of-context scan returns consistent RFC 7807 ProblemDetails, and a scan on a
non-admitting session (Paused/Finished/Cancelled) is rejected; an E2E test proves the correct path
publishes `EvidenceSubmissionRegistered` then `TargetResolved` end-to-end after transactional success;
service coverage passes the **ADR-0005** gate (`coverlet.msbuild`, aggregate line+branch ≥ 93).

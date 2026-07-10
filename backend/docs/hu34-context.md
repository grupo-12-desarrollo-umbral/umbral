# HU-34 Context — Team trivia answer first-write-wins

> Paste this section into any agent session that needs context for HU-34 (DES-46).
> Last updated: 2026-07-09 | Branch: `feature/hu-34-trivia-team-answer-first-write-wins`
>
> DES-46 is the merged ticket for the old HU-34A/HU-34B split: acceptance of the first valid
> answer and rejection of late/repeated attempts are one invariant, one command, one endpoint,
> and one test surface. `DES-47` is **Canceled** and must not be cited as live scope.

## State

- DES-46 (HU-34): **Todo**, labels: `svc:session-operations-service`, `Feature`, `ready-for-agent`
- **Resolved mode: feature flow** — DES-46 carries neither `canon-realign` nor `needs-rebuild`
- **Superseded handling applied:** `DES-47` (HU-34B) is in the realignment map's superseded column
  and was merged into `DES-46` on 2026-07-09; do not cite it as a predecessor or separate scope
- Same-service build-on predecessors (Done/merged): **DES-78 (HU-33A)**, **DES-45 (HU-33B)**,
  **DES-77 (HU-22)**, **DES-76 (HU-21A)**; light build-on participant-admission seams:
  **DES-11 (HU-07A)**, **DES-12 (HU-07B)**
- Same-service landed but untouched by this HU: **DES-22 (HU-15)**, **DES-24 (HU-17)**,
  **DES-25 (HU-18)**, **DES-26 (HU-19)**, **DES-27 (HU-20)**, **DES-75 (HU-16)**
- PRD DES id: **DES-70** → `backend/docs/prd/DES-70-primera-implementacion-de-session-operations-service-hu-15-a-hu-36.md`
- Canon overlay still relevant because the service was realigned: `backend/docs/canon-realignment-after-mission-runtime-rewrite.md`
- Branch: `feature/hu-34-trivia-team-answer-first-write-wins`, base **`develop`** (no same-service predecessor In Progress)

## Required design patterns

| Pattern | Owning phase | Why (from patterns matrix) | Concrete obligation |
|---|---|---|---|
| `Template Method` (mandated) | X.1 Domain + X.2 Application | `required_patterns_matrix.md:132,183`: accept-first and reject-late/repeat must reuse one stable ordered validation workflow under the trivia timer window | One fixed answer-registration skeleton governs the flow: session/runtime admission → active trivia question → timer window → first-write-wins → persist base evidence + trivia specialization → raise facts. Do not fork "accept" and "reject" into separate handlers or ad-hoc copies of the same checks. |
| `Chain of Responsibility` (mandated) | X.2 Application | `required_patterns_matrix.md:132,183`; `CONTEXT.md:217`: trivia answers must compose ordered validators instead of one monolithic handler | The acceptance/rejection checks run as ordered, composable validators/links. No single handler with a long `if` chain deciding every branch. |

Transport note: HU-34 carries **SignalR + RabbitMQ** (`required_patterns_matrix.md:57-58,132`).
The accepted answer broadcasts an **operator-only answered indicator** in real time and publishes
`AnswerRegistered` after transactional success. RabbitMQ infrastructure already exists from HU-33B;
HU-34 reuses that publisher and adds the answer contract only.

Applies-where note (no new `Proxy` gate): the participant write endpoint is `[Authorize(Policy = Participant)]`
and the runtime admission check reuses the existing `RuntimeParticipationGuard`; HU-34 is not in the
matrix's applies-where `Proxy` set.

## What predecessors have already landed

**Build-on (read for derivation):**

- **DES-78 (HU-33A) — Done.** The trivia runtime already exposes one synchronized active question
  per active trivia substage, advances by timer, and broadcasts `QuestionActivated` /
  `QuestionClosed` / `SubstageAdvanced`. HU-34 attaches answer intake to that seam: the active
  question is authoritative and shared for all teams; there is no participant-paced question flow.
- **DES-45 (HU-33B) — Done.** RabbitMQ producer infrastructure already exists:
  `IIntegrationEventPublisher`, `RabbitMqIntegrationEventPublisher`, durable service-owned
  exchange wiring, and the "domain fact → app bridge → RabbitMQ contract" pattern. HU-34 must
  reuse that seam instead of inventing a second publisher stack.
- **DES-77 (HU-22) — Done.** The timer is authoritative for the active trivia question only; a
  substage with no active question has no countdown. HU-34's "late answer" rule keys off that
  question window, not wall-clock heuristics or client-side timers.
- **DES-76 (HU-21A) — Done.** `Paused`, `Finished`, and `Cancelled` sessions reject gameplay
  actions by canon. HU-34 builds on that lifecycle: accepted answers exist only while the session
  is effectively admitting the active trivia question.
- **DES-11 / DES-12 (HU-07A / HU-07B) — build-on participant path.** Membership and reconnect
  already establish the participant-side team/session context. HU-34 must reuse those access facts
  and team identity seams; it must not invent a parallel participant identity flow.

**Landed, untouched by this HU:**

- DES-22 / DES-24 / DES-75 — mission-only snapshot/source canon already frozen; HU-34 consumes the
  snapshotted trivia question ids/options/timers but changes no snapshot shape
- DES-25 / DES-26 / DES-27 — team association, operator assignment, and operator session lists are
  already landed and not modified here

**Coverage:** no stable carried-forward aggregate percentage is recorded for this combined seam;
verify the real service percentage at X.4 against the repo coverage gate.

## What this HU adds

| Concern | New work |
|---|---|
| Trivia evidence specialization | Add `EvidenceSubmission` as the umbrella base inside `LiveSession` and `TriviaAnswerSubmission` as its trivia specialization for one team / one active question / one accepted answer snapshot. |
| First-write-wins rule | Accept exactly one in-time answer per team per snapshotted trivia question; reject repeats and late attempts with consistent ProblemDetails reasons. |
| Fixed answer-registration workflow | Inline the trivia-specific intake now, but shape it as the canonical evidence-registration skeleton that later HU-29 / HU-30A can extract into the shared umbrella pipeline. |
| Ordered validator chain | Reuse one ordered chain for runtime participation, active question presence, timer window, and duplicate-answer checks; the handler orchestrates, the links decide. |
| Accepted-answer domain fact | Raise `AnswerRegistered` only after the accepted answer is persisted. No event on rejected attempts. |
| Operator-only answered signal | Broadcast "team answered" without option/correctness leakage to an operator-only SignalR group; do not send it to the participant-visible live-session group. |
| RabbitMQ answer contract | Publish the accepted-answer fact after transactional success through the existing RabbitMQ publisher seam so `ScoringMonitoring` can consume it. |
| Frontend slice | Add the participant answer-submission surface on top of the existing trivia round/timer runtime and wire the new backend contract into the frontend plan. |

## Touched surfaces

- `backend/services/session-operations-service/` — domain, application, infrastructure, and api
- `frontend/` — participant trivia-answer submission flow and its contract plumbing
- API contract boundary: new participant answer-write endpoint + accepted-answer response shape
- Real-time contract boundary: new operator-only `TeamAnswered` SignalR notification
- Async contract boundary: new `AnswerRegistered` RabbitMQ message consumed downstream by scoring

## Committed phases

| Commit | Phase | Description |
|---|---|---|
| — | — | No commits yet |

## Known quirks / gotchas

- **DES-47 is canceled, not live scope.** Cite `DES-46` only. The merged ticket already absorbed
  the rejection ACs.
- **Inline the trivia intake now; do not wait for HU-29/HU-30A.** `workflow_refactor.md` flags
  the ambiguity explicitly: HU-29/HU-30A carry no blocker edge to DES-46. The canonical decision
  for generation is: HU-34 lands the trivia-specific path now, using the same naming and workflow
  shape the later shared evidence pipeline can extract.
- **Do not leak answered-state to participants.** `SessionsHub` currently puts both participants and
  operators in `live-session:{id}`. A new answered indicator sent to that group would leak
  supervision state. HU-34 must introduce an operator-only group / broadcaster seam for this
  notification.
- **Do not reveal correctness or points in the participant response or operator signal.** Fairness
  says those belong after close (HU-35) or downstream scoring (HU-37). The accepted-answer write
  returns acceptance metadata only; correctness/points stay internal / RabbitMQ-only.
- **Reuse the existing RabbitMQ infrastructure.** HU-33B already introduced the service-owned
  exchange and `IIntegrationEventPublisher`. HU-34 adds the answer contract; it does not bootstrap
  a second AMQP stack.
- **Transport naming follows the current backend seam, not the older sprint note.** The historical
  sprint handoff froze `domain.events` / `trivia.answer.registered`, but the as-built publisher in
  this service now owns a service-scoped exchange and `session.*` routing keys. Keep HU-34 aligned
  with the current transport precedent.
- **HU-36A still owns the operator monitoring UI.** HU-34 owns the answered-state fact and its
  transport, not the full pre-close answered/not-answered dashboard projection.

## Per-phase derivation — authoritative for implementation

> Primary source for the phase subagent (per `backend-agent.md` "Read first").
> Mode = **feature flow**. Canon precedence: `ddd_solution_model.md` → service `CONTEXT.md` →
> `structure.md` → `bd_umbral_entity_spec.md` → plan docs. Realignment overlay still applies where
> it tightened terms (`EvidenceSubmission` umbrella; DES-47 merged into DES-46).

### Phase X.1 — Domain
**Derive** (`ddd_solution_model.md:220-250,432-461`; `CONTEXT.md:149-159,217-225`; `bd_umbral_entity_spec.md:401-447,600-638`; `adr/0004-required-domain-patterns.md`):
- Add the umbrella base `EvidenceSubmission` plus the trivia specialization `TriviaAnswerSubmission`
  under `LiveSession`. `EvidenceSubmission` records the generic submission identity/context
  (`liveSessionId`, `teamId`, active substage, participant/timestamp, validation state); the
  trivia specialization records the snapshotted question id, selected option id, accepted/correct
  flags, and snapshotted score value.
- `LiveSession` owns the fixed answer-registration skeleton (`Template Method`): verify the
  session/runtime permits trivia answers, resolve the active trivia question from the synchronized
  runtime seam, verify the timer window is open, enforce one accepted answer per team/question,
  create the base evidence + trivia specialization, snapshot correctness/score from the question
  option, and raise `AnswerRegisteredEvent` on success only.
- Rejected attempts become typed domain exceptions: no active question, wrong substage mode,
  late answer, duplicate team answer, invalid selected option, and any session-state rejection
  inherited from the lifecycle/timer canon.
- The merged A/B decision is structural: there is **one** accepted-answer flow and the rejection
  branches are the same invariant seen from the failure side. Do not create a second entity/event
  for rejected answers.

**Target files** (create | edit — file to mirror):
- create `src/Domain/Entities/EvidenceSubmission.cs` — mirror the aggregate-child style of `JoinContext.cs`
- create `src/Domain/Entities/TriviaAnswerSubmission.cs` — mirror `Team.cs` / `JoinContext.cs` child-entity shape
- edit `src/Domain/Entities/LiveSession.cs` — add the fixed answer-registration workflow and
  accepted-answer collection checks; mirror the existing question-runtime methods for authoritative
  question lookup and event raising
- create `src/Domain/Events/AnswerRegisteredEvent.cs` — mirror `QuestionClosedEvent.cs`
- create `src/Domain/Exceptions/{LateTriviaAnswerException,DuplicateTriviaAnswerException,InvalidTriviaAnswerOptionException,TriviaAnswerRequiresActiveQuestionException,TriviaAnswerRequiresTriviaSubstageException}.cs` — mirror the existing question/timer exception files
- edit `tests/UnitTests/Domain/Entities/LiveSessionTests.cs` — add answer-registration tests

**Pattern this phase owns:** `Template Method` — one fixed answer-registration skeleton with the
same ordered steps for accept/reject branches.
**Gate:** Domain build passes; unit tests cover each new public domain type and the fixed skeleton:
first valid answer accepted once, duplicate answer rejected, late answer rejected, no active
question rejected, paused/finished/cancelled rejected, invalid option rejected, correctness/score
snapshotted on the accepted answer, and `AnswerRegisteredEvent` raised only on success. **Template
Method verified — one invariant workflow, not split accept/reject implementations.**

### Phase X.2 — Application
**Derive** (`CONTEXT.md:217-225`; `required_patterns_matrix.md:132,183`; `structure.md` ADR-0011 vertical slices; existing `RuntimeParticipationGuard`, `PublishQuestionClosedIntegrationEventHandler`, and `SessionStateChangedNotificationHandler`):
- Add one vertical-slice command `SubmitTriviaAnswer` in `Application/Sessions/Commands/SubmitTriviaAnswer/`.
  The handler loads the session aggregate, runs the ordered validation chain, delegates to the
  domain answer-registration skeleton, persists, and returns acceptance metadata only.
- Realize `Chain of Responsibility` in `Application/Sessions/Common/TriviaAnswerValidation/`:
  ordered links for runtime participation, active-question presence/substage mode, timer window,
  and duplicate-team-answer. These links must be independently testable and reusable by the same
  command's success and rejection paths.
- Add the accepted-answer transport bridges:
  - `AnswerRegisteredIntegrationEvent` mapped from `AnswerRegisteredEvent` and published via the
    existing `IIntegrationEventPublisher`
  - `TeamAnsweredNotificationDto` mapped from `AnswerRegisteredEvent` and broadcast to operators
    only, with no option/correctness payload
- The command result DTO belongs in `Application/Dtos/Sessions/` and returns acceptance metadata
  only (`liveSessionId`, `teamId`, accepted question identity/order, `answeredAt`).

**Target files** (create | edit — file to mirror):
- create `src/Application/Sessions/Commands/SubmitTriviaAnswer/{SubmitTriviaAnswerCommand.cs,SubmitTriviaAnswerCommandHandler.cs,SubmitTriviaAnswerCommandValidator.cs}` — mirror `ReconnectAuthenticatedParticipant` / `SelectTeam` command slices
- create `src/Application/Sessions/Common/TriviaAnswerValidation/{TriviaAnswerValidationChain.cs,TriviaAnswerValidationLink.cs}` and `.../Validators/*.cs` — mirror `Application/Sessions/StateTransitions/`
- create `src/Application/Dtos/Sessions/SubmitTriviaAnswerResultDto.cs` — mirror `SelectTeamResultDto.cs`
- create `src/Application/Sessions/Common/AnswerRegisteredIntegrationEvent.cs` — mirror `QuestionClosedIntegrationEvent.cs`
- create `src/Application/Sessions/Common/Notifications/TeamAnsweredNotificationDto.cs` — mirror `QuestionClosedNotificationDto.cs`
- create `src/Application/Sessions/EventHandlers/{PublishAnswerRegisteredIntegrationEventHandler.cs,TeamAnsweredNotificationHandler.cs}` — mirror the existing question-close / session-state bridges
- edit `src/Application/Common/Interfaces/ISessionQuestionBroadcaster.cs` or add a new operator-only broadcaster seam if the answered notification must target a distinct group
- reuse `src/Application/Common/Interfaces/IIntegrationEventPublisher.cs` and `src/Application/Sessions/Common/RuntimeParticipationGuard.cs`
- add application unit tests under `tests/Application.UnitTests/Sessions/...`

**Pattern this phase owns:** `Chain of Responsibility` (mandated) + the application side of the
`Template Method` realization.
**Gate:** Application build passes; the validation links run in stable order and short-circuit on
the first rejection; the handler delegates to the single domain workflow instead of re-encoding the
same checks inline; the result DTO leaks no correctness/points; `AnswerRegisteredIntegrationEvent`
and `TeamAnsweredNotificationDto` publish off the accepted-answer fact only. **Chain of
Responsibility verified — ordered links, no monolithic handler branch chain.**

### Phase X.3 — Infrastructure
**Derive** (`bd_umbral_entity_spec.md:401-447,600-638`; existing `LiveSessionConfiguration`, `RabbitMqIntegrationEventPublisher`, and integration-test style):
- Persist the new `EvidenceSubmission` / `TriviaAnswerSubmission` child entities under `LiveSession`
  with the uniqueness guarantee required by canon: one accepted trivia answer per team per
  snapshotted question in the same session.
- Reuse the existing RabbitMQ publisher infrastructure from HU-33B: add the answer contract and
  routing-key mapping to the current service-owned exchange, rather than introducing a second
  publisher or exchange abstraction.
- No new repository abstraction is needed if the aggregate round-trips through the existing
  `LiveSessionRepository`; extend configuration/mapping and add integration tests around the new
  child collections / uniqueness rule.

**Target files** (create | edit — file to mirror):
- edit `src/Infrastructure/Persistence/Configurations/LiveSessionConfiguration.cs` — mirror the
  existing owned-child mapping style already used for runtime snapshot / team state
- create `src/Infrastructure/Persistence/Migrations/<timestamp>_AddTriviaAnswerEvidence.cs` — add
  the umbrella + specialization persistence
- edit `src/Infrastructure/Messaging/RabbitMqIntegrationEventPublisher.cs` — add the answer
  routing-key mapping; mirror the existing `QuestionClosedIntegrationEvent` / `SessionResultsFinalizedIntegrationEvent` handling
- add integration tests in `tests/IntegrationTests/Persistence/` and `tests/IntegrationTests/Messaging/`

**Pattern this phase owns:** none.
**Gate:** Infrastructure build passes; `ef migrations add` succeeds; the aggregate round-trips
accepted trivia answers and their base evidence through persistence; the uniqueness rule is enforced
at the persisted model boundary; the existing RabbitMQ transport can publish `AnswerRegistered`
through the current service-owned exchange without new infrastructure. 

### Phase X.4 — Api
**Derive** (`frontend contract boundary from existing participant timer/read endpoints; `SessionsController.cs`; `SessionsHub.cs`; global ProblemDetails handler):
- Add the participant write endpoint:
  `POST /api/sessions/{liveSessionId}/participants/answers`
  with a participant-authorized request body carrying the runtime `teamId`, selected trivia option
  id, and optional token reuse for runtime participation validation.
- Return an acceptance-only DTO on success; rejected attempts surface as RFC 7807 ProblemDetails
  through the global exception handler with a consistent reason contract.
- Add the operator-only SignalR answered-state transport. Because participants and operators both
  currently join `live-session:{id}`, this phase must add a distinct operator group membership /
  broadcaster path so the answered indicator is visible to operator monitoring only.

**Target files** (create | edit — file to mirror):
- edit `src/Api/Controllers/SessionsController.cs` — add the participant answer route, mirroring the
  participant reconnect/select-team/timer endpoints
- edit `src/Api/Hubs/SessionsHub.cs` — add operator-only group membership alongside the existing
  operator live-session join/leave flow
- edit `src/Api/Hubs/SignalRSessionQuestionBroadcaster.cs` or create a distinct answered-state
  broadcaster targeting the operator-only group
- add API / hub integration tests under `tests/IntegrationTests/Api/`

**Pattern this phase owns:** none new. The participant endpoint inherits `[Authorize(Policy = Participant)]`
plus the runtime participation guard; the answered signal is a transport/privacy gate, not a new
design pattern.
**Gate:** endpoint integration tests prove a first valid participant answer succeeds, repeat/late
attempts return consistent ProblemDetails, and no correctness/option data leaks in the response;
hub tests prove `TeamAnswered` reaches the operator-only group and does **not** leak to participant
connections; service coverage passes the repo gate. **SignalR + RabbitMQ transport obligations
verified here.**

# HU-29 — Driver brief
_Driver reads only this file. The full prompt + context are for the subagent
and human review; the driver never loads them._

**Nature of this HU:** _extraction / generalization (not greenfield)._ HU-34 (DES-46, Done) already
shipped the `EvidenceSubmission` base, the trivia skeleton, the trivia Chain, and the outbox bridge.
HU-29 **extracts** that shared intake substrate into a Facade + generic Chain, adds the generic
`EvidenceSubmissionRegistered` event, and publishes it via the existing outbox. X.1/X.2 refactor
HU-34 code (gate: trivia behavior unchanged); X.3/X.4 are verification-dominant. **Do not authorize
rebuilding the base entity, the QR form (HU-31), or the deep validation (HU-30A).**

## Slice
| HU | DES | PRD | Service | Branch | Base |
|----|-----|-----|---------|--------|------|
| HU-29 — Envío de evidencias por parte del equipo | DES-39 | DES-70 | session-operations-service | feature/hu-29-evidence-submission-intake | develop |

## Required pattern(s) → owning phase
- `Facade` (phase X.2) — the Facade publishes `EvidenceSubmissionRegistered` over the shared intake subsystem — obligation: a **discrete** `EvidenceIntakeFacade` in `Sessions/Common/` (shared ≥2 consumers, ADR-0013 → not handler-inlined) orchestrating chain → domain core → persist → outbox
- `Chain of Responsibility` (phase X.2) — evidence submission runs a composable validation pipeline — obligation: ordered generic intake links (runtime participation → session-admits-reception → active-substage-present) short-circuit on first failure; the trivia chain composes them
- Transport: RabbitMQ — `EvidenceSubmissionRegistered` after transactional success via the MassTransit EF-Core transactional outbox (ADR-0017 amended); no second publisher stack

## Per phase — gate + owned pattern
| Phase | Gate | Pattern |
|-------|------|---------|
| X.1 Domain | Domain build; shared registration core registers a base `EvidenceSubmission` in `Pending` and raises `EvidenceSubmissionRegisteredEvent` on success only; generic rejections fire (session not admitting reception, unknown team, missing active substage); **all HU-34 trivia domain tests stay green**; no QR/target logic | — (no mandated pattern in X.1) |
| X.2 Application | App build; `EvidenceIntakeFacade` is a discrete `Sessions/Common/` class orchestrating chain → core → persist (no ad-hoc intake in the handler); generic intake links run in order + short-circuit; trivia chain composes the shared links; trivia command delegates to the facade with no response/no-leak change; `EvidenceSubmissionRegisteredIntegrationEvent` bridges via `OutboxDomainEventDispatcher` + `IPublishEndpoint` | `Facade` + `Chain of Responsibility` |
| X.3 Infrastructure | Infra build; **no new migration** unless `ef migrations add` reports a real diff (`Pending` is string-compatible — assert + record); base-evidence row round-trips with `ValidationState = Pending`; integration test proves `EvidenceSubmissionRegistered` is inserted into the transactional outbox atomically with the write; no second publisher stack | — |
| X.4 Api | **No new participant endpoint** (base is abstract; unless Stop 1 opted in); integration test proves the trivia path publishes `EvidenceSubmissionRegistered` end-to-end after transactional success; a non-admitting-session submission returns consistent RFC 7807 ProblemDetails; ADR-0005 coverage (service ≥ repo gate) | — |

Commit subjects — copy each phase's exact subject from the prompt's Steps 5–8 verbatim:
- X.1 `feat(session-operations): phase X.1 - domain layer (HU-29)`
- X.2 `feat(session-operations): phase X.2 - application layer (HU-29)`
- X.3 `feat(session-operations): phase X.3 - infrastructure layer (HU-29)`
- X.4 `feat(session-operations): phase X.4 - api layer (HU-29)`

Trailer (every phase): `Ref: HU-29` / `Ref: DES-39` / `Ref: DES-70`

## Acceptance criteria
- a participant can register an `EvidenceSubmission` tied to the active valid substage (mission node in treasure hunt; active question in trivia)
- each evidence is associated with exactly one team, one session, and the originating active substage
- the system blocks submission when the session does not admit reception (paused, finished, cancelled)
- the evidence is recorded with the info needed for later validation/traceability (initial validation state `pending`)
- after transactional success, `EvidenceSubmissionRegistered` is published to RabbitMQ for async consumption, without the main flow depending on RabbitMQ

## Endpoints + smoke (driver verifies at Stop 2)
_HU-29 adds **no new endpoint** — the umbrella base is abstract; registration happens through the existing concrete-form endpoint (trivia). Smoke the umbrella behavior on that path plus the new async fact._
- `POST /api/sessions/{liveSessionId}/participants/answers` — existing trivia participant write; first valid answer → **200** (unchanged HU-34 behavior); request/response shape unchanged (acceptance metadata only)
- same path while the session does not admit reception (Paused/Finished/Cancelled) → **RFC 7807 rejection**, consistent reason
- RabbitMQ `EvidenceSubmissionRegistered` — observable on the session-operations exchange after transactional success (fields: liveSessionId, teamId, evidenceSubmissionId, activeSubstageId, submissionType, submittedAt, validationState); `AnswerRegistered` still published alongside it

## Frontend slice
**None** — HU-29 adds no client-facing contract; the async `EvidenceSubmissionRegistered` event is consumed by backend services (audit/history, notification, projection). See Step 9 (downstream consumer contract hand-off) of `prompt_example_feature_hu29.md`. There is no Step 9 frontend plan / Step 9b implementation for this slice.

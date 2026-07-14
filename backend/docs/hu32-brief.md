# HU-32 — Driver brief

_Driver reads only this file. The full prompt + context are for the subagent
and human review; the driver never loads them._

**Nature of this HU:** feature build, but an **unusual shape** — the write path
(HU-29/30/31/34) is already Done and merged, so **no phase rebuilds intake, the facade, either
validation chain, or either evidence form**. X.1 adds the two *outcome* facts the umbrella never
published; X.2–X.3 publish them, consume them back, and project them; X.4 exposes the operator read.
If a subagent proposes rebuilding evidence intake, validation, the QR form, or the trivia form, that
is out of scope — stop it. Likewise, **operator-mediated review is out of scope** (ADR-0010): no
`reviewedByUserId`/`reviewedAt`, no approve/reject endpoint or control.

## Slice

| HU | DES | PRD | Service | Branch | Base |
|----|-----|-----|---------|--------|------|
| HU-32 — Trazabilidad de evidencias | DES-43 | DES-70 | session-operations-service | feature/hu-32-evidence-traceability | develop |

## Required pattern(s) → owning phase

- **None mandated** (`required_patterns_matrix.md:45`) — say so explicitly; do not let a subagent
  introduce one.
- `Proxy` (applies-where, **no new gate**) — matrix `:125` tags HU-32 "audit read; `Proxy` guards
  operator access". The operator read reuses the **existing** ADR-0009 ownership resolver
  (`ISessionAdministrationAccessResolver`) + `[Authorize(Roles = "Operator")]`. Obligation: no ad-hoc
  role/owner `if` in the handler — that is all.

## Per phase — gate + owned pattern

| Phase | Gate | Pattern |
|-------|------|---------|
| X.1 Domain | Domain build + unit tests per new type: contextual `Reject` and QR `RejectRegisteredTarget` each raise one `EvidenceSubmissionRejectedEvent` with a **non-null** reason (QR's lives on `ResolutionRejectionReason`, **not** the base field — the key trap; do **not** backfill `EvidenceSubmission.RejectionReason` from QR — HU-31's invariant at `EvidenceSubmission.cs:81-84`); accepted QR + accepted trivia each raise one `EvidenceSubmissionAcceptedEvent`; `EvidenceSubmissionRegisteredEvent` carries `OriginReference` and still fires on every registered submission; `EvidenceTraceEntry`'s `MarkAccepted`/`MarkRejected` are idempotent; **all pre-existing HU-29/30/31/34 domain tests green**; **`OwnsMany` interceptor probe passes** (a `ParentEntity : BaseEntity` `OwnsMany<ChildEntity>` raising a domain event on save reaches the dispatcher exactly once — converts the Stop-1 EF assumption into a green check); no pattern introduced | — |
| X.2 Application | Application build; both publish handlers map event → `[EntityName]` contract via `IPublishEndpoint` and **propagate** on failure (no swallow/timeout/`IIntegrationEventPublisher`); dispatcher routes both new events, existing arms unchanged; trace-write handlers **idempotent + order-tolerant**; `GetOperatorEvidenceTraceQuery` is `[Authorize(Roles="Operator")]` and resolves access **only** via the resolver Proxy; read returns date/time + team/session/substage/origin + state + reason, QR-rejected item has a **non-null** reason; `teamId` filters; no new facade, no second publisher stack | — (reuses resolver Proxy) |
| X.3 Infrastructure | Infrastructure build; `ef migrations add` produces **only** `evidence_trace_entries` (no change to `live_sessions`, child tables, or outbox tables); trace round-trips; upserting the same `EvidenceSubmissionId` twice yields **one** row; facts land in the outbox atomically with the evidence write; broker-backed delivery test proves a submitted evidence reaches its **terminal** trace state (`Rejected` **with reason** for a wrong QR) — **polled**, not synchronous; the three consumers are thin adapters (`ISender`+`ILogger`, map, send — no projection logic/DbContext/repository); registered via `bus.AddConsumer<T>()`, no topology hand-wiring, no change to the outbox block | — |
| X.4 Api | Assigned operator gets the session's registered evidence with date/time, team, session, substage+origin, state, and reason when rejected; `?teamId=` filters; non-assigned operator → RFC 7807 403, participant/unauthenticated rejected (inherited resolver Proxy, no ad-hoc check, no controller `try/catch`); E2E proves a QR scan appears with its terminal state after the outbox drains (**polled** — eventually consistent by AC #5); write path unaffected when the broker is down; **ADR-0005 coverage** (aggregate line+branch ≥ 93) | — |

Commit subjects — copy each phase's exact subject from the prompt's Steps 5–8
verbatim (the driver presents what the human approved; do not paraphrase or
normalize punctuation):
- X.1 `feat(session-operations): phase X.1 - domain layer (HU-32)`
- X.2 `feat(session-operations): phase X.2 - application layer (HU-32)`
- X.3 `feat(session-operations): phase X.3 - infrastructure layer (HU-32)`
- X.4 `feat(session-operations): phase X.4 - api layer (HU-32)`

Trailer (every phase): `Ref: HU-32` / `Ref: DES-43` / `Ref: DES-70`

## Acceptance criteria

- Each registered evidence retains its submission date and time.
- Each evidence identifies team, session, and origin substage (mission node or trivia question).
- The system shows the evidence's validation state.
- A rejected evidence retains its rejection reason — **including QR rejections**, whose reason lives on
  `TreasureEvidenceSubmission.ResolutionRejectionReason`, not the base field.
- The traceability/history is fed asynchronously by **consuming** the domain events published on
  RabbitMQ, and the main flow does not depend on RabbitMQ (the bus outbox keeps the broker off the
  write path: the evidence write commits and the trace catches up when the broker recovers).

Boundary to hold: no operator-mediated review (ADR-0010); the trace covers **registered** evidence only
(trivia late/duplicate answers and structural intake failures throw and are never persisted —
HU-29/HU-34 behaviour unchanged); DES-33 (HU-24B operator panel) is a downstream consumer, not this PR.

## Endpoints + smoke (driver verifies at Stop 2)

- `GET /api/sessions/{liveSessionId:guid}/evidence-submissions` — **new**, Operator-guarded, optional
  `?teamId={guid}`. Submit a correct QR scan, then GET as the assigned operator (allow ~1s for the
  outbox to drain) — expect **200**; response is a list of trace items, each
  `{ evidenceSubmissionId, teamId, activeSubstageId, submissionType, originReference, submittedAt,
  validationState, rejectionReason, resolvedAt }` (the frontend contract).
- Same endpoint, wrong/duplicate/out-of-context scan — expect the item to appear with
  `validationState = Rejected` and a **non-null** `rejectionReason`.
- Same endpoint with `?teamId=` — expect **200**, filtered to that team.
- Same endpoint as a **non-assigned** operator — expect **403** RFC 7807.
- Async: `session-evidence-submission-registered` / `-accepted` / `-rejected` exchanges are observable
  on RabbitMQ **and** their receive endpoints (queues) exist and are draining — this service now
  consumes as well as publishes.

## Frontend slice

Human-driven — see Steps 9 (generate plan) + 9b (implement it) of
`prompt_example_feature_hu32.md`. Small read-only operator traceability panel over the one new
endpoint (hu-03 exemplar shape). DES-43 has **no** `backend-only` label, so this slice is in scope.

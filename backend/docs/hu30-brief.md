# HU-30 — Driver brief
_Driver reads only this file. The full prompt + context are for the subagent
and human review; the driver never loads them._

**Nature of this HU:** _form-agnostic substrate over the Done HU-29 intake (not greenfield, not a
rebuild)._ HU-29 (DES-39, merged) shipped the shared `EvidenceIntakeFacade`, the generic
`EvidenceIntakeValidation` Chain, `Pending`, `EvidenceSubmissionRegistered`, and the outbox bridge.
HU-30 **adds** the form-agnostic `EvidenceValidationPolicy` contextual Chain (active-substage-binding
/ window / origin — membership & team stay HU-29 throws) + the explained-rejection outcome
(`EvidenceSubmission.Reject(reason)` + persisted `rejectionReason`), wired into the shared facade.
X.1 adds the domain rejection transition; X.2 adds the contextual chain + facade wiring; X.3 adds the
`rejection_reason` column (migration required); X.4 is verification. **Do not authorize rebuilding
HU-29's intake, the QR form (HU-31), trivia rules (HU-34), or the result query (HU-32). The trivia
path must stay observably unchanged.**

## Slice
| HU | DES | PRD | Service | Branch | Base |
|----|-----|-----|---------|--------|------|
| HU-30 — Validación contextual y rechazo explicado de evidencias | DES-95 | DES-70 | session-operations-service | feature/hu-30-evidence-contextual-validation | develop |

## Required pattern(s) → owning phase
- `Chain of Responsibility` (phase X.2) — pre-acceptance validation composed as ordered validators (`EvidenceValidationPolicy`, `required_patterns_matrix.md:123`; `ddd_solution_model.md:442-443`) — obligation: a new form-agnostic `EvidenceValidationChain` in `Sessions/Common/EvidenceValidation/` (active-substage-binding → submission-window → origin — three links) short-circuiting on first failure, each link yielding an explicit `EvidenceRejectionReason`; runs on the registered `Pending` record after HU-29's structural intake and before form-specific validation; mirrors `EvidenceIntakeValidation/*`. **Membership & team are NOT in this chain — they are HU-29 structural throws that short-circuit before this policy.**
- Transport: **none** — HU-30 adds no new RabbitMQ event; it reuses HU-29's `EvidenceSubmissionRegistered` (do not add `EvidenceSubmissionAccepted`/`Rejected` events)

## Per phase — gate + owned pattern
| Phase | Gate | Pattern |
|-------|------|---------|
| X.1 Domain | Domain build; base `EvidenceSubmission` transitions `Pending → Rejected` carrying an explicit `RejectionReason`; re-resolving a resolved record throws; acceptance path unchanged; **all HU-34/HU-29 domain tests green**; no QR/target/trivia-rule logic, no reviewer fields, no Accepted/Rejected events | — (no mandated pattern in X.1) |
| X.2 Application | App build; new `EvidenceValidationChain` (`Sessions/Common/EvidenceValidation/`) runs substage-binding → window → origin, short-circuits, each link a distinct reason; the shared `EvidenceIntakeFacade` runs it on the constructed `Pending` record after structural intake / before form validation and records `Reject(reason)` in the single commit on failure; recorded-rejection proven with a **non-trivia fixture** (trivia auto-accepts, never reaches `Reject`); `SubmissionWindowLink` must not turn a late trivia answer into a recorded rejection; trivia command delegates with **no response/no-leak change** (existing tests pass); links registered in DI run order | `Chain of Responsibility` |
| X.3 Infrastructure | Infra build; `ef migrations add` yields exactly the `rejection_reason` nullable column on `live_session_trivia_answer_submissions`; a contextually-rejected row round-trips `Rejected` + `rejection_reason` and the transaction **commits**; integration test proves `EvidenceSubmissionRegistered` outbox insert atomic with the committed write; no second publisher stack | — |
| X.4 Api | **No new participant endpoint** (base abstract; trivia = Done, QR = HU-31; unless Stop 1 opts in); integration test proves the trivia path is observably unchanged (200 accept; RFC-7807 on non-admitting session) and no contextual-rejection exception leaks to the client; recorded-rejection proven by X.1–X.3 tests; ADR-0005 coverage (service ≥ repo gate) | — |

Commit subjects — copy each phase's exact subject from the prompt's Steps 5–8 verbatim:
- X.1 `feat(session-operations): phase X.1 - domain layer (HU-30)`
- X.2 `feat(session-operations): phase X.2 - application layer (HU-30)`
- X.3 `feat(session-operations): phase X.3 - infrastructure layer (HU-30)`
- X.4 `feat(session-operations): phase X.4 - api layer (HU-30)`

Trailer (every phase): `Ref: HU-30` / `Ref: DES-95` / `Ref: DES-70`

## Design calls — resolved (verified against shipped HU-29 code + canon)
- **Throw-vs-record partition** (settled): session-admits / team-in-session / **participant membership** / active-substage-**pointer-resolves** stay HU-29 structural throws (all four already shipped; each is a precondition to construct the record). Recorded rejections are exactly **node-binding / temporal-window / origin**. Membership/team are throws, not recorded reasons — a membership link in the contextual chain would be dead code.
- **Where recorded-rejection is proven** (settled): at domain/application/integration level via a non-trivia fixture; the trivia path stays observably unchanged (it auto-accepts and never reaches `Reject`); end-to-end exercise is deferred to the QR form (HU-31). No generic endpoint, no trivia behavior change.
- **Session-state = throw** (default; RB-03's "cannot be accepted" is satisfied by a throw). Flip to recorded only if the product wants paused-session submissions persisted for the operator audit trail (RF-09) — out of first-delivery scope per ADR-0010. **One residual human call.**
- **Origin stays in scope** (AC #2 mandates validating "origen"): ship `SubmissionOriginLink` as substrate even though no concrete origin signal exists until HU-31 — it may be a pass-through proven only by a fixture until then.

## Acceptance criteria
- each submission registers an `EvidenceSubmission` in `pending` before its result is evaluated
- the system validates session, team, participant membership, active substage, temporal window, and origin
- the evidence is associated with exactly one `MissionNode` of the active substage — not a `Stage`, not a `Clue`
- if the common context rules pass, the flow continues toward form-specific validation (HU-31/HU-34)
- if a rule fails, the evidence is left `rejected` with an explicit reason, preserving its session, team, and substage
- `EvidenceSubmissionRegistered` is published after the transactional registration of every evidence, accepted or rejected; the main flow does not depend on RabbitMQ
- the result query/presentation is NOT built here (HU-32)

## Endpoints + smoke (driver verifies at Stop 2)
_HU-30 adds **no new endpoint** — the umbrella base is abstract; the recorded-rejection outcome is exercised end-to-end only when the QR form lands (HU-31). Smoke the substrate on the existing trivia path plus the persisted outcome fields._
- `POST /api/sessions/{liveSessionId}/participants/answers` — existing trivia participant write; first valid answer → **200** (unchanged HU-34 behavior; request/response shape unchanged)
- same path while the session does not admit reception (Paused/Finished/Cancelled) → **RFC 7807 rejection** (unchanged; structural intake throw) — confirm no contextual-rejection exception leaks to the client
- persisted evidence record now carries `validationState` (pending/accepted/rejected) + `rejectionReason` (the read model HU-32 presents); a contextually-rejected registration commits a `Rejected` row with a reason and still emits `EvidenceSubmissionRegistered` on the session-operations exchange
- `AnswerRegistered` + `EvidenceSubmissionRegistered` still published on a valid answer

## Frontend slice
**None** — HU-30 adds no client-facing contract; it is a form-agnostic internal validation + recorded-rejection substrate. The result presentation is HU-32 and the first end-to-end concrete consumer is HU-31 (QR). See Step 9 (substrate-contract hand-off) of `prompt_example_feature_hu30.md`. There is no Step 9 frontend plan / Step 9b implementation for this slice.

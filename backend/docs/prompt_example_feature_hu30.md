# Prompt Example — HU-30 Contextual validation + explained rejection of evidence

Concrete prompt sequence for driving `DES-95` / `HU-30` through the backend slice
on `feature/hu-30-evidence-contextual-validation`. Follows
[workflow_for_prompts.md](./workflow_for_prompts.md).

**Key difference for HU-30:** this is a **form-agnostic substrate** slice over the Done HU-29 intake,
not a greenfield feature and not a rebuild. HU-29 (DES-39, merged) shipped the shared
`EvidenceIntakeFacade`, the generic `EvidenceIntakeValidation` Chain (structural admission links), the
`Pending` state, the `EvidenceSubmissionRegisteredEvent`, and the transactional-outbox bridge — and
explicitly deferred "the deeper pre-acceptance context validation (`EvidenceValidationPolicy`) to
HU-30A/HU-31" (`hu29-context.md:93`). HU-30 **adds that layer**: the form-agnostic
`EvidenceValidationPolicy` contextual Chain of Responsibility and the **explained-rejection** outcome
(`EvidenceSubmission.Reject(reason)` + a persisted `rejectionReason`), wired into the shared facade so
evidence is registered `pending`, contextually validated, and left `accepted` or `rejected` with an
explicit reason. It does **not** rebuild HU-29's intake, **not** build the QR form (HU-31/DES-42) or
trivia rules (HU-34), and **not** build the result query/presentation (HU-32/DES-43). The invariant is
"add the shared contextual-validation + recorded-rejection substrate without regressing the trivia
path," not "add a new gameplay action."

When working from the monorepo root, make the target workload explicit in each prompt. For backend
steps, point to `@backend/.agents/backend-agent.md`.

---

## Required design patterns

- `Chain of Responsibility`
  - Why: `required_patterns_matrix.md:42,123` — "Pre-acceptance validation … composed as ordered
    validators (`EvidenceValidationPolicy`)"; canon `ddd_solution_model.md:442-443`.
  - Phase owner: X.2 Application.
  - Gate obligation: a **new** form-agnostic `EvidenceValidationChain` in `Sessions/Common/EvidenceValidation/`
    (active-substage-binding → submission-window → origin — three links) short-circuits on
    first failure, each link yielding an explicit `EvidenceRejectionReason`. Participant membership and
    team-in-session are **not** in this chain — they are HU-29 structural throws that short-circuit
    before the policy runs. It runs on the registered
    `Pending` record **after** HU-29's structural intake admission and **before** form-specific
    validation. Mirror `Sessions/Common/EvidenceIntakeValidation/*` — not one collapsed handler.

Transport obligations: **none.** HU-30 is in the matrix's "neither SignalR nor RabbitMQ" set
(`required_patterns_matrix.md:59`). The `EvidenceSubmissionRegistered` RabbitMQ fact already exists
from HU-29 and fires at registration — HU-30 adds no new integration event. Do **not** add the
canonical `EvidenceSubmissionAccepted`/`EvidenceSubmissionRejected` events (no transport, no in-scope
consumer — result query is HU-32).

---

## Pre-resolved orient (as of 2026-07-13)

> Step 1 has already been run. Paste this section into any agent session that needs context before
> picking up a phase; no need to re-run the orient prompt unless source or Linear state changed.

### What predecessors have already landed

`DES-95` is a **form-agnostic substrate** over the Done/merged HU-29 intake:

- **DES-39 / HU-29** — the shared `EvidenceIntakeFacade` (`IEvidenceIntakeFacade.RegisterAsync`), the
  generic `EvidenceIntakeValidation` Chain + links (`RuntimeParticipationLink`,
  `SessionAdmitsReceptionLink`, `ActiveSubstagePresentLink` — each **throws** to short-circuit),
  `LiveSession.RegisterEvidenceCore`, `EvidenceValidationState.Pending`,
  `EvidenceSubmissionRegisteredEvent` (raised at pending registration, success path only), and the
  `EvidenceSubmissionRegistered` transactional-outbox bridge. This is the substrate HU-30 extends.
- **The `EvidenceSubmission` abstract base** exposes only `MarkAcceptedByConcreteForm()` — **no**
  rejection transition and **no** `RejectionReason` yet (`Rejected` exists in the enum but is
  currently unreachable). Adding both is HU-30's domain work.
- Transitive seams through HU-29: HU-34 (trivia base/skeleton/chain), HU-21A (state machine /
  admits-reception), HU-33A (active-substage pointer), HU-07A/07B (`RuntimeParticipationGuard`), the
  MassTransit EF-Core transactional outbox.

`DES-95` is a fresh (2026-07-13) consolidation of the old HU-30A (DES-40, binding target) + HU-30B
(DES-41, generic rejection); its AC is born canon-aligned (the #28 umbrella + DES-40 binding reword).
It is **not** in any supersession column, and HU-29 (its blocker) is merged — so the branch base is
`develop`.

### What HU-30 adds on top

| Concern | New work |
|---|---|
| Explained-rejection transition | `RejectionReason` (nullable) + `Reject(reason)` (`Pending → Rejected`) on `EvidenceSubmission`; `EvidenceRejectionReason` enum (`bd_umbral_entity_spec.md:434,449`; `ddd_solution_model.md:532`) |
| Contextual-validation Chain | `EvidenceValidationPolicy` — new form-agnostic CoR (active-substage/node binding, window, origin — membership/team stay HU-29 throws) on the registered `Pending` record (`ddd_solution_model.md:442-443`; `CONTEXT.md:218`) |
| Register-then-validate outcome | Wire the policy into `EvidenceIntakeFacade` (register `Pending` → policy → `Reject(reason)` + persist on fail / form validation on pass) so `EvidenceSubmissionRegistered` fires for rejected evidence too |
| Persistence | New nullable `rejection_reason` column on `live_session_trivia_answer_submissions` — migration **required** |
| Contract | **No new endpoint** (base abstract; trivia = Done, QR = HU-31); **no new async contract** (reuses HU-29's `EvidenceSubmissionRegistered`) |

### Branch state and prerequisite

`feature/hu-30-evidence-contextual-validation` branches from `develop`. HU-29 is merged (PR #206) and
no *other* same-service build-on predecessor is In Progress, so there is no feature-branch dependency
to inherit first.

### Linear state (as of 2026-07-13)

- DES-95 (HU-30): **Todo**, labels: `svc:session-operations-service`, `Feature`, `canon-realign`, `ready-for-agent`, `backend-only`
- DES-70 (PRD): **Backlog**, labels: `svc:session-operations-service`, `canon-realign`, `ready-for-agent`
- Blocker DES-39 (HU-29): Done/merged. Downstream: DES-43 (HU-32), DES-60 (RabbitMQ enabler); related DES-42 (HU-31)

> Linear live state may have changed. Use the Linear MCP to verify DES-95 status and labels if needed,
> but do not re-fetch PRD scope from Linear — read the local PRD file at
> `@backend/docs/prd/DES-70-primera-implementacion-de-session-operations-service-hu-15-a-hu-36.md`.

---

## 1. Orient — read service state and the resolved HU-30 context

> **Skip this step if you have read the pre-resolved orient section above.** Run it only if the
> service source, README, or Linear state may have changed since 2026-07-13.

```text
Read the following files and summarize what has already landed and what HU-30 must add:
- @backend/services/session-operations-service/README.md
- @backend/services/session-operations-service/CONTEXT.md
- @backend/docs/prd/DES-70-primera-implementacion-de-session-operations-service-hu-15-a-hu-36.md
- @backend/docs/adr/0010-evidence-qr-only-first-delivery.md
- @backend/docs/hu29-context.md
- @backend/docs/hu30-context.md

Then inspect only the current session-operations evidence seams you need to anchor on:
- @backend/services/session-operations-service/src/Domain/Entities/EvidenceSubmission.cs
- @backend/services/session-operations-service/src/Domain/Entities/LiveSession.cs
- @backend/services/session-operations-service/src/Application/Sessions/Common/EvidenceIntakeValidation/EvidenceIntakeValidationChain.cs
- @backend/services/session-operations-service/src/Application/Sessions/Common/EvidenceIntakeFacade.cs
- @backend/services/session-operations-service/src/Application/Sessions/Commands/SubmitTriviaAnswer/SubmitTriviaAnswerCommandHandler.cs

Then use the Linear MCP to fetch only the current live state of:
- DES-95 (HU-30 - Validación contextual y rechazo explicado de evidencias)
- DES-70 (PRD - SessionOperations)

Output:
- the live DES-95 status and labels
- the direct build-on substrate (HU-29 intake facade + generic chain + Pending + registered event + outbox)
- the HU-30 scope: add the form-agnostic EvidenceValidationPolicy contextual chain + Reject(reason)/rejectionReason, wired into the shared facade
- the boundary: do NOT rebuild HU-29 intake, do NOT build the QR form (HU-31), trivia rules (HU-34), or result query (HU-32); trivia observable behavior stays unchanged

Do not start planning or implementing yet.
```

---

## 2. Label DES-95 as ready-for-agent

```text
Use the Linear MCP to confirm DES-95 still carries the label ready-for-agent.
If it is missing, add it.
Output the updated DES-95 ticket state and labels.
```

---

## 3. Confirm slice readiness

```text
Use the Linear MCP to confirm DES-95 carries both svc:session-operations-service
and ready-for-agent labels and output its current status and acceptance criteria.

The PRD scope is already in the local file at
@backend/docs/prd/DES-70-primera-implementacion-de-session-operations-service-hu-15-a-hu-36.md.
Do not re-fetch PRD scope from Linear.

Before planning, explicitly confirm:
- DES-95 is the live, canon-aligned consolidation of HU-30A (DES-40) + HU-30B (DES-41); it is not in any supersession column
- HU-30 builds the form-agnostic EvidenceValidationPolicy (Chain of Responsibility) + the Reject(reason)/rejectionReason explained-rejection outcome on the Done HU-29 substrate
- HU-30 does NOT rebuild HU-29's intake, and does NOT build the QR form (HU-31/DES-42), the trivia rules (HU-34), or the result query/presentation (HU-32/DES-43)
- the trivia path's observable behavior must stay unchanged; its late/duplicate rejections remain HU-34 form-specific throws
- HU-30 adds NO new endpoint and NO new RabbitMQ event (it reuses HU-29's EvidenceSubmissionRegistered)

Then confirm the two design calls this HU must settle (see the prompt Rationale), and surface them for human sign-off:
- the throw-vs-record partition of the six AC checks (which stay HU-29 structural throws vs. become HU-30 recorded rejections)
- that the recorded-rejection outcome is proven at domain/application/integration level and exercised end-to-end by the QR form (HU-31), with the trivia path staying observably unchanged

Output the confirmed HU id, title, acceptance criteria, labels, and the scope + Stop-1 design confirmations before planning the slice.
```

In the remaining examples below, `HU-30` and `DES-95` are the resolved values for this slice.
`DES-70` is the shared PRD reference for `session-operations-service`.

---

## 4. Start the slice

```text
Prepare the HU-30 slice on branch feature/hu-30-evidence-contextual-validation.
Use the HU id and DES id resolved from Linear in the previous step.
This slice affects backend `session-operations-service` only.

The pre-resolved orient at the top of this document lists what existing code has already landed and
what HU-30 adds. Do not re-read the PRD for scoping unless you need to resolve a precise
implementation detail.

Move DES-95 to In Progress if the team process requires it, and output the exact scope, branch name,
base branch, and touched surfaces. Note explicitly that HU-30 adds NO new participant endpoint
(EvidenceSubmission is abstract; concrete-form endpoints are trivia = Done, QR = HU-31) and NO new
RabbitMQ event (it reuses HU-29's EvidenceSubmissionRegistered), and that the trivia path's observable
behavior must stay unchanged.
```

---

## 5. Backend phase X.1 — Domain layer

```text
Use @backend/.agents/backend-agent.md.
Implement backend phase X.1 for HU-30 in session-operations-service, per the
**X.1 derivation block in @backend/docs/hu30-context.md** (your spec — do not
re-read the canon or re-inspect the tree; open a cited canon section only to
fill a gap the block leaves open).

Gate:
- Domain build passes; a unit test proves the base EvidenceSubmission transitions Pending → Rejected carrying an explicit RejectionReason, and that re-resolving an already-resolved record throws
- acceptance path (MarkAcceptedByConcreteForm) unchanged; the base stays Pending on construction
- ALL pre-existing HU-34/HU-29 domain tests remain green — no behavior regression
- no QR/target or trivia-rule logic introduced; no reviewer fields (HU-32); no Accepted/Rejected events (Constraint 2)
- no mandated pattern in this phase (Chain of Responsibility lands in X.2); do not label the Reject transition a pattern

Do not touch other backend layers or frontend.
```

Commit:

```text
feat(session-operations): phase X.1 - domain layer (HU-30)

Ref: HU-30
Ref: DES-95
Ref: DES-70
```

---

## 6. Backend phase X.2 — Application layer

```text
Use @backend/.agents/backend-agent.md.
Implement backend phase X.2 for HU-30 in session-operations-service, per the
**X.2 derivation block in @backend/docs/hu30-context.md** (your spec — do not
re-read the canon or re-inspect the tree; open a cited canon section only to
fill a gap the block leaves open).

Gate:
- clean build passes; tests cover the contextual chain order + short-circuit + per-link reason, the facade recording a rejection on contextual failure, and the trivia path staying green
- Chain of Responsibility verified: the new EvidenceValidationChain in Sessions/Common/EvidenceValidation/ runs active-substage-binding → submission-window → origin (three links; membership/team are HU-29 throws, not in this chain), short-circuits on first failure, each link yielding an explicit EvidenceRejectionReason — mirrors EvidenceIntakeValidation, not a collapsed handler
- recorded-rejection proven with a non-trivia fixture (trivia auto-accepts and never reaches Reject); SubmissionWindowLink must NOT convert a late trivia answer (LateTriviaAnswerException) into a recorded rejection — trivia late/duplicate behavior byte-unchanged
- the policy runs on the registered Pending record AFTER structural intake and BEFORE form-specific validation; on a contextual failure the shared EvidenceIntakeFacade records Reject(reason) + persists (commit), not a rolled-back throw; on pass it continues to form validation
- the trivia command delegates with NO change to its participant response / no-leak guarantee (existing SubmitTriviaAnswer tests still pass)
- links registered in DependencyInjection.cs in run order (registration order IS run order); chain classes not edited to add links

Do not touch Infrastructure, Api, or frontend.
```

Commit:

```text
feat(session-operations): phase X.2 - application layer (HU-30)

Ref: HU-30
Ref: DES-95
Ref: DES-70
```

---

## 7. Backend phase X.3 — Infrastructure layer

```text
Use @backend/.agents/backend-agent.md.
Implement backend phase X.3 for HU-30 in session-operations-service, per the
**X.3 derivation block in @backend/docs/hu30-context.md** (your spec — do not
re-read the canon or re-inspect the tree; open a cited canon section only to
fill a gap the block leaves open).

Gate:
- infrastructure build passes
- `ef migrations add` produces exactly the rejection_reason nullable column on live_session_trivia_answer_submissions (assert the diff is only that column; no data backfill)
- a contextually-rejected evidence row round-trips with ValidationState = Rejected + a persisted rejection_reason, and the transaction COMMITS (row present, not rolled back)
- an integration test proves EvidenceSubmissionRegistered is inserted into the transactional outbox atomically with the committed business write — no second publisher stack or exchange bootstrap

Do not touch Api.
```

Commit:

```text
feat(session-operations): phase X.3 - infrastructure layer (HU-30)

Ref: HU-30
Ref: DES-95
Ref: DES-70
```

---

## 8. Backend phase X.4 — Api layer

```text
Use @backend/.agents/backend-agent.md.
Implement backend phase X.4 for HU-30 in session-operations-service, per the
**X.4 derivation block in @backend/docs/hu30-context.md** (your spec — do not
re-read the canon or re-inspect the tree; open a cited canon section only to
fill a gap the block leaves open).

Gate:
- NO new participant endpoint (EvidenceSubmission is abstract; concrete-form endpoints are trivia = Done, QR = HU-31) — unless Stop 1 explicitly opted into a generic endpoint, in which case scope it separately
- an endpoint/messaging integration test proves the trivia submission path is observably unchanged (first valid answer → 200; non-admitting session → RFC 7807) and that no contextual-rejection exception escapes to the client
- the recorded-rejection substrate is proven by the X.1–X.3 tests (domain transition + application facade + integration persist)
- service coverage passes the repo gate (ADR-0005)

Do not touch frontend or mobile.
```

Commit:

```text
feat(session-operations): phase X.4 - api layer (HU-30)

Ref: HU-30
Ref: DES-95
Ref: DES-70
```

---

## 8.5. Docker rebuild

```text
Rebuild and restart the backend runtime for HU-30:

docker compose build session-operations-service api-gateway
docker compose up -d session-operations-service api-gateway

Then smoke that the contextual-validation substrate is in place without regressing the trivia path,
through the existing trivia submission path via the gateway:
- a first valid participant answer -> accepted response, 200 (unchanged HU-34 behavior)
- a submission while the session does not admit reception (Paused/Finished/Cancelled) -> RFC 7807 rejection (unchanged; structural intake throw)
- confirm AnswerRegistered and EvidenceSubmissionRegistered are still published on a valid answer
- (recorded-rejection is exercised end-to-end when the QR form lands in HU-31; here confirm no contextual-rejection exception leaks to the client)
```

---

## 9. Downstream / form-consumer contract hand-off

> HU-30 adds **no** client-facing surface — it is a form-agnostic internal validation + recorded-rejection
> substrate. The participant-facing presentation of a validation result is HU-32 (result query), and the
> first concrete form to route contextual failures into recorded rejections end-to-end is HU-31 (QR). So
> there is no frontend plan for this slice (the same posture the HU-29 prompt took). Instead, record the
> contract the next tickets consume.

```text
Do not implement any client (mobile/web) code as part of DES-95 — HU-30 has no client-facing contract.

Instead, record the verified substrate contract that the next tickets consume:

- the EvidenceSubmission record now carries ValidationState (pending/accepted/rejected) + RejectionReason — the read model HU-32 (DES-43) presents
- the EvidenceValidationPolicy contextual chain (active-substage-binding / window / origin — membership/team are HU-29 throws) and how a concrete form opts a contextual failure into Reject(reason) — the seam HU-31 (DES-42, QR) routes its target-context failures through
- confirmation that HU-29's EvidenceSubmissionRegistered fact still fires (now for rejected evidence too, because rejected records are registered Pending first and the transaction commits)
- confirmation that the trivia path (HU-34) is observably unchanged and its late/duplicate throws are untouched

Output:
- the verified substrate contract (record fields + policy seam)
- the explicit note that no client endpoint/UI is in scope for HU-30
- the two Stop-1 design confirmations (throw-vs-record partition; recorded-rejection proven at domain/integration level, exercised end-to-end by HU-31)
```

---

## 9b. Optional follow-up execution

```text
If the team explicitly chooses to continue after DES-95, start a new ticket-scoped session for the
first concrete consumer of this substrate — HU-31 (DES-42, QR TreasureEvidenceSubmission +
TargetResolved, which routes contextual failures into recorded rejections end-to-end) — or HU-32
(DES-43, result traceability/query over the persisted validationState/rejectionReason).

Do not continue that work under the DES-95 scope or branch, and do not re-open the extended trivia path.
```

---

## 10. Close-out

```text
Before opening the PR, confirm all DES-95 acceptance criteria are satisfied:
- each submission registers an EvidenceSubmission in Pending before its result is evaluated
- the system validates session, team, participant membership, active substage, temporal window, and origin
- the evidence is associated with exactly one MissionNode of the active substage — not a Stage, not a Clue
- if the common context rules pass, the flow continues toward the form-specific validation (HU-31/HU-34)
- if a rule fails, the evidence is left Rejected with an explicit reason, preserving its session, team, and substage
- EvidenceSubmissionRegistered is published after the transactional registration of every evidence, accepted or rejected; the main flow does not depend on RabbitMQ
- the result query/presentation is NOT built here (HU-32)

Also confirm the scope boundary:
- HU-29's intake substrate is not rebuilt; the trivia path (HU-34) is observably unchanged (all tests green; late/duplicate stay form-specific throws)
- no QR form (HU-31), trivia rules (HU-34), or result query (HU-32) was built
- no new client endpoint/UI and no new RabbitMQ event were added

Then open the PR:

gh pr create \
  --base develop \
  --head feature/hu-30-evidence-contextual-validation \
  --title "feat(session-operations): HU-30 contextual validation + explained rejection of evidence" \
  --body "Implements HU-30 / DES-95: adds the form-agnostic EvidenceValidationPolicy contextual Chain of Responsibility (active-substage/MissionNode binding, temporal window, origin; participant membership and team-in-session remain HU-29 structural throws) and the explained-rejection outcome (EvidenceSubmission.Reject(reason) + a persisted rejectionReason) on top of the Done HU-29 intake substrate. Evidence is registered pending, contextually validated, and left accepted or rejected with an explicit reason; EvidenceSubmissionRegistered now fires for rejected evidence too via the existing transactional outbox. The trivia path (HU-34) is observably unchanged. QR form (HU-31/DES-42), trivia rules (HU-34), and result query (HU-32/DES-43) remain out of scope."
```

---

## Rationale

- **Why this is a substrate slice, not a greenfield feature or a rebuild:** HU-29 shipped the shared
  intake substrate and *explicitly* deferred the deeper pre-acceptance context validation
  (`EvidenceValidationPolicy`) to HU-30/HU-31 (`hu29-context.md:93`, `required_patterns_matrix.md:123`).
  DES-95 is the live consolidation of HU-30A (DES-40, binding target) + HU-30B (DES-41, generic
  rejection); its AC is already canon-aligned to the #28 umbrella and the DES-40 binding reword
  (`canon-realignment-after-mission-runtime-rewrite.md:130`). The move is to *add* the contextual layer
  onto the canon-aligned base, not to re-model evidence or tear out HU-29 code.
- **Design question #1 — the throw-vs-record partition (resolved against shipped code).** HU-29's
  structural intake links **throw** (→ RFC-7807, no record). HU-30 introduces **recorded** rejections
  (persist a `Rejected` record with a reason). AC #5 requires a rejected record to "preserve session,
  team, substage" — only possible once that context has *resolved*, i.e. the structural checks that let
  a record be constructed must pass first. Verified partition: session-admits / team-in-session /
  **participant membership** / active-substage-**pointer-resolves** = HU-29 structural throws (all four
  already shipped — `SessionAdmitsReceptionLink`, `GetTeam`, `RuntimeParticipationLink`,
  `ActiveSubstagePresentLink` — each a precondition to construct the record); node-**binding** (declared
  node ∈ resolved active substage) / temporal-window / origin = HU-30 recorded rejections. **Membership
  and team are throws, not recorded rejections** — a membership/team link in the contextual chain would
  be dead code, since the facade runs the intake throw before the policy (`DependencyInjection.cs:70-73`,
  `EvidenceIntakeFacade.cs`). One residual product call remains: **session-state** — canon RB-03 calls it
  "rechazo" but the entity-spec wording is negative ("cannot be accepted while Paused/Finished/Cancelled",
  `bd_umbral_entity_spec.md:448`), which a throw satisfies; the default stays throw. It flips to a
  recorded rejection only if the product wants paused-session submissions persisted for the operator
  audit trail (RF-09) — out of first-delivery scope per ADR-0010:50-52. Flag for the human.
- **Open design question #2 surfaced for Stop 1 — where the recorded-rejection outcome is proven.** The
  only concrete form today (trivia) already enforces its membership/participation as throws and its
  late/duplicate rules as HU-34 form-specific throws, and there is **no generic evidence endpoint**
  (the base is abstract). So HU-30's recorded-rejection path is genuinely *exercised end-to-end by the
  QR form (HU-31)*, which will route its contextual failures into `Reject(reason)`. The **default**
  here is: HU-30 builds and proves the substrate at domain + application + integration level and keeps
  the trivia path observably unchanged (additive substrate); it does **not** convert any trivia throw
  into a recorded rejection and does **not** add a generic endpoint. The alternative (change trivia's
  observable behavior, or add a generic intake endpoint now) is flagged for the human; if chosen, X.4
  grows additively.
- **Why the Chain is form-agnostic and does not pull QR/trivia validators forward:** the QR
  target-resolution validators (`TargetResolutionPolicy`) are HU-31's and the trivia timer/duplicate
  rules are HU-34's (`required_patterns_matrix.md:124`, AC scope note). HU-30's chain is the shared
  *common-context* policy those forms compose onto — mirroring how HU-29's generic intake chain is the
  admission substrate the trivia chain composes.
- **Why `EvidenceSubmissionRegistered` is reused, not extended, and no outcome event is added:** HU-30
  carries **no transport** (`required_patterns_matrix.md:59`). The registration fact already fires at
  pending registration; AC #6 ("published for accepted *or* rejected") is satisfied because a rejected
  record is registered `Pending` first and the transaction **commits** (persisting the rejection) rather
  than rolling back. The canonical outcome events `EvidenceSubmissionAccepted`/`Rejected`
  (`ddd_solution_model.md:327-328`) have no in-scope consumer (result query = HU-32) — adding them would
  invent scope (generator Constraint 2).
- **Why `rejectionReason` needs a migration but HU-29's `Pending` did not:** `Pending` was a new value
  on the existing string `validation_state` column (string-compatible, no schema change). `rejection_reason`
  is a brand-new nullable column on `live_session_trivia_answer_submissions` — a real model diff.
- **Why there is no frontend step:** HU-30 produces no client contract — its record fields are read by
  HU-32 (result query) and its policy seam is consumed by HU-31 (QR). Forcing a frontend plan would
  invent scope (Constraint 2). Step 9 is therefore a substrate-contract hand-off, mirroring the HU-29
  prompt's posture.

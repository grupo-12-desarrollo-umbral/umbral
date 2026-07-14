# Prompt Example — HU-31 Server-side QR Target scan, validation & resolution

Concrete prompt sequence for driving `DES-42` / `HU-31` through the backend slice
on `feature/hu-31-qr-target-scan-resolution`. Follows
[workflow_for_prompts.md](./workflow_for_prompts.md).

**Key difference for HU-31:** this is the **treasure-hunt QR form** of the `EvidenceSubmission`
umbrella — the sibling of HU-34's trivia form. The shared intake substrate is **already built**
(HU-29 base + facade + generic chain + outbox; HU-34 concrete-form template), so HU-31 **mirrors**
`TriviaAnswerSubmission` / `AnswerRegisteredEvent` and adds the second ordered fact of the QR flow,
`TargetResolved`. The one behavior that is **not** a mirror of trivia: QR rejection is *retained*
(a wrong/duplicate/out-of-context scan is registered as `Rejected` and kept for audit), where trivia
*throws*. Do not rebuild the base entity, the facade, or the generic chain.

`HU-31` is **backend-only** (DES-42 label). Its consumers are backend services — scoring-monitoring
(HU-37 ledger, via `TargetResolved`), audit/history (HU-40A), and the HU-23 live board (via the
`resolvedTargets` read model). There is no client contract in this slice; do not ask for frontend
work in this branch.

When working from the monorepo root, make the target workload explicit in each prompt.
For backend steps, point to `@backend/.agents/backend-agent.md`.

---

## Required design patterns

- `Chain of Responsibility` (mandated)
  - Why: `required_patterns_matrix.md:124` — server-side QR target validation (belongs to the active
    substage, not duplicate/out-of-context) is the QR-submission validator chain `TargetResolutionPolicy`
    (`ddd_solution_model.md:444`, `adr/0004:3`).
  - Phase owner: X.2 Application.
  - Gate obligation: ordered composable links — QR resolves to a `Target` → `Target` belongs to the
    active treasure-hunt substage → not already resolved by this team — decide via `SetNext`/`Next`; the
    first failing link supplies the retained rejection reason. No single handler with sequential `if`
    blocks (`adr/0012:64` ceremony test).

- `Facade` — **reused, not owned.** HU-31 orchestrates through the **existing** `IEvidenceIntakeFacade`
  (shipped by HU-29). Do not author a second facade.

- `Proxy` — **applies-where, no new gate.** The participant endpoint inherits
  `[Authorize(Policy = Participant)]` + `AuthorizationBehaviour` + `RuntimeParticipationGuard`.

Transport obligation:
- **RabbitMQ** — publish `TargetResolved` (and the inherited `EvidenceSubmissionRegistered`) after
  transactional success through the existing MassTransit EF-Core bus-outbox (ADR-0017). No SignalR in
  this HU's AC. No second publisher stack.

---

## Pre-resolved orient (as of 2026-07-13)

> Step 1 has already been run. Paste this section into any agent session that needs context
> before picking up a phase; no need to re-run the orient prompt unless source or Linear state changed.

### What predecessors have already landed

`DES-42` is a feature build on the realigned session runtime, on top of the shared evidence substrate:

- **DES-39 / HU-29** — the intake substrate: abstract `EvidenceSubmission` base, `IEvidenceIntakeFacade`,
  the generic `EvidenceIntakeValidationChain` (runtime participation → session-admits-reception →
  active-substage-present), the `EvidenceSubmissionRegistered` domain→integration→outbox bridge, and
  reserved `EvidenceSubmissionType.TreasureHuntQrScan`. **Reuse, do not rebuild.**
- **DES-46 / HU-34** — the concrete-form template to mirror: `TriviaAnswerSubmission`,
  `AnswerRegisteredEvent` (carries `IsCorrect` + `ScoreValue` — the payload precedent this HU cites),
  `LiveSession.RegisterTriviaAnswer`, the `TriviaAnswerValidation/` chain, the `SubmitTriviaAnswer`
  command slice, `AnswerRegisteredIntegrationEvent` + its publish handler + the `OutboxDomainEventDispatcher`
  switch.
- **DES-76 / HU-21A** — session state machine + `State` pattern; `EnsureCanRegisterEvidence` is a no-op
  only in `Active`, so the QR path inherits the paused/finished/cancelled block for free.
- **DES-77 / HU-22** — authoritative timer (not consumed by QR — no answer window for scans).
- **DES-86 / DES-87** — per-target `ScoreValue` model + `TargetSnapshot.Score` now a non-nullable
  positive `int`; HU-31 relays this into `TargetResolved`.
- **DES-22 / HU-15** — the immutable `MissionRuntimeSnapshot` (targets/substages) HU-31 reads.

Superseded predecessors already substituted: HU-21A `DES-28`→`DES-76`, HU-22 `DES-30`→`DES-77` (the
canceled `DES-28`/`DES-30` are dropped). No same-service predecessor is currently In Progress, so the
branch base is `develop`.

### What HU-31 adds on top

| Concern | New work |
|---|---|
| QR evidence form | `TreasureEvidenceSubmission : EvidenceSubmission` (mirror `TriviaAnswerSubmission`) |
| Unconditional intake | every context-valid scan registers (`Pending`) + publishes inherited `EvidenceSubmissionRegistered` |
| Target resolution | resolve the scanned QR against the active treasure-hunt substage's `Target`; correct → accept + resolve |
| `TargetResolved` fact | new `TargetResolvedEvent` carrying the relayed `ScoreValue`, published via the existing outbox |
| Retained rejection | wrong/duplicate/out-of-context → persisted `Rejected` + reason (audit), no `TargetResolved` |
| `TargetResolutionPolicy` | CoR chain deciding accept-vs-reject, first-fail supplies the reason |
| `resolvedTargets` | fill the per-team counter in `BuildTreasureHuntContext` (HU-23 board consumes it) |

### Branch state and prerequisite

`feature/hu-31-qr-target-scan-resolution` branches from `develop`. No same-service predecessor is
In Progress, so there is no feature-branch dependency to inherit first.

### Linear state (as of 2026-07-13)

- DES-42 (HU-31): **Todo**, labels: `svc:session-operations-service`, `Feature`, `backend-only`,
  `canon-realign`, `ready-for-agent`
- DES-70 (PRD): **Backlog**, labels: `svc:session-operations-service`, `canon-realign`, `ready-for-agent`
- Same-service Done build-on predecessors: DES-39, DES-46, DES-76, DES-77, DES-86, DES-87, DES-22

> Linear live state may have changed. Use the Linear MCP to verify DES-42 status and labels if needed,
> but do not re-fetch PRD scope — read the local PRD file at
> `@backend/docs/prd/DES-70-primera-implementacion-de-session-operations-service-hu-15-a-hu-36.md`.

---

## 1. Orient — read service state and the resolved HU-31 context

> **Skip this step if you have read the pre-resolved orient section above.** Run it only if
> the service source, README, or Linear state may have changed since 2026-07-13.

```text
Read the following files and summarize what has already landed and what HU-31 must add:
- @backend/services/session-operations-service/README.md
- @backend/services/session-operations-service/CONTEXT.md
- @backend/docs/prd/DES-70-primera-implementacion-de-session-operations-service-hu-15-a-hu-36.md
- @backend/docs/canon-realignment-after-mission-runtime-rewrite.md
- @backend/docs/hu31-context.md

Then inspect only the current session-operations evidence/runtime seams you need to anchor on:
- @backend/services/session-operations-service/src/Domain/Entities/EvidenceSubmission.cs
- @backend/services/session-operations-service/src/Domain/Entities/TriviaAnswerSubmission.cs
- @backend/services/session-operations-service/src/Domain/Entities/LiveSession.cs
- @backend/services/session-operations-service/src/Application/Sessions/Common/EvidenceIntakeFacade.cs
- @backend/services/session-operations-service/src/Application/Sessions/Common/TriviaAnswerValidation/
- @backend/services/session-operations-service/src/Api/Controllers/SessionsController.cs

Then use the Linear MCP to fetch only the current live state of:
- DES-42 (HU-31 - Escaneo, validación y registro server-side de objetivo QR)
- DES-70 (PRD - SessionOperations)

Output:
- the live DES-42 status and labels
- confirmation that the canon-realign reword is already applied (Target is the validation object; Clue is optional guidance)
- the direct build-on seams (HU-29 substrate + HU-34 concrete-form template + HU-21A state guard)
- the accepted generation decision: QR rejection is retained (registered Rejected), unlike trivia's throw
- that HU-31 is backend-only (no client contract in this slice)

Do not start planning or implementing yet.
```

---

## 2. Label DES-42 as ready-for-agent

```text
Use the Linear MCP to confirm DES-42 still carries the label ready-for-agent.
If it is missing, add it.
Output the updated DES-42 ticket state and labels.
```

---

## 3. Confirm slice readiness

```text
Use the Linear MCP to confirm DES-42 carries both svc:session-operations-service
and ready-for-agent labels and output its current status and acceptance criteria.

The PRD scope is already in the local file at
@backend/docs/prd/DES-70-primera-implementacion-de-session-operations-service-hu-15-a-hu-36.md.
Do not re-fetch PRD scope from Linear.

Before planning, explicitly confirm:
- the canon-realign reword is applied — validate the scanned QR against the Target of the active
  treasure-hunt substage, never against a Clue or a Stage; releasing a clue never advances the substage
- the shared substrate (HU-29 base/facade/generic chain/outbox, HU-34 concrete-form template) is reused,
  not rebuilt
- intake is unconditional: every context-valid scan registers an EvidenceSubmission (Pending) and
  publishes EvidenceSubmissionRegistered, even scans that reject
- wrong/duplicate/out-of-context scans are retained as Rejected with a consistent reason and publish no
  TargetResolved
- TargetResolved relays the target's ScoreValue as-is (no 1..100 re-check in session-operations)
- substage advancement / "won" is out of scope for this HU

Output the confirmed HU id, title, acceptance criteria, labels, and the guard confirmation before planning the slice.
```

In the remaining examples below, `HU-31` and `DES-42` are the resolved values for this slice.
`DES-70` is the shared PRD reference for `session-operations-service`.

---

## 4. Start the slice

```text
Prepare the HU-31 slice on branch feature/hu-31-qr-target-scan-resolution.
Use the HU id and DES id resolved from Linear in the previous step.
This slice affects backend `session-operations-service` only.

The pre-resolved orient at the top of this document lists what existing code has already
landed and what HU-31 adds. Do not re-read the PRD for scoping unless you need to resolve
a precise implementation detail.

Move DES-42 to In Progress if the team process requires it, and output the exact scope,
branch name, base branch, and touched surfaces. Note explicitly that HU-31 is backend-only:
downstream consumers (scoring-monitoring, audit/history, HU-23 board) consume the async facts;
there is no client work in this branch.
```

---

## 5. Backend phase X.1 — Domain layer

```text
Use @backend/.agents/backend-agent.md.
Implement backend phase X.1 for HU-31 in session-operations-service, per the
**X.1 derivation block in @backend/docs/hu31-context.md** (your spec — do not
re-read the canon or re-inspect the tree; open a cited canon section only to
fill a gap the block leaves open).

Gate:
- Domain build passes; unit tests cover every new public domain type
- a correct QR scan accepts the evidence and raises TargetResolvedEvent once, carrying the relayed ScoreValue
- a wrong QR scan persists the evidence as Rejected with a consistent reason and raises NO TargetResolvedEvent
- a duplicate scan (same team, already-resolved target) and a scan matching a target outside the active
  substage both persist as Rejected with a reason
- EvidenceSubmissionRegisteredEvent is raised on every registered scan; paused/finished/cancelled sessions
  reject via the inherited session-state guard
- resolvedTargets reflects the team's resolved-target count
- no 1..100 score check in the domain — the relayed value is used verbatim

Do not touch other backend layers or frontend.
```

Commit:

```text
feat(session-operations): phase X.1 - domain layer (HU-31)

Ref: HU-31
Ref: DES-42
Ref: DES-70
```

---

## 6. Backend phase X.2 — Application layer

```text
Use @backend/.agents/backend-agent.md.
Implement backend phase X.2 for HU-31 in session-operations-service, per the
**X.2 derivation block in @backend/docs/hu31-context.md** (your spec — do not
re-read the canon or re-inspect the tree; open a cited canon section only to
fill a gap the block leaves open).

Gate:
- clean build passes; RegisterTargetScan is one vertical slice reusing the existing IEvidenceIntakeFacade
  (no ad-hoc intake in the handler, no second facade)
- handler + validator tests cover the accepted path and each rejection branch, and the result DTO surfaces
  a consistent rejection reason
- Chain of Responsibility verified: the TargetResolution links (QR-resolves-to-target,
  target-belongs-to-active-substage, not-already-resolved) run in stable order, first-fail supplies the
  reason, links are independently testable and reorderable; not one handler with sequential if blocks
- TargetResolvedIntegrationEvent bridges onto RabbitMQ off the resolved fact only; EvidenceSubmissionRegistered
  still fires for every registered scan through the existing outbox dispatcher

Do not touch Infrastructure, Api, or frontend.
```

Commit:

```text
feat(session-operations): phase X.2 - application layer (HU-31)

Ref: HU-31
Ref: DES-42
Ref: DES-70
```

---

## 7. Backend phase X.3 — Infrastructure layer

```text
Use @backend/.agents/backend-agent.md.
Implement backend phase X.3 for HU-31 in session-operations-service, per the
**X.3 derivation block in @backend/docs/hu31-context.md** (your spec — do not
re-read the canon or re-inspect the tree; open a cited canon section only to
fill a gap the block leaves open).

Gate:
- infrastructure build passes
- ef migrations add succeeds for the new TreasureEvidenceSubmission owned collection
- a TreasureEvidenceSubmission round-trips through LiveSessionRepository with ValidationState persisted
  (Pending -> Accepted/Rejected + reason)
- an integration test proves that on a correct scan BOTH EvidenceSubmissionRegistered and TargetResolved
  are inserted into the transactional outbox atomically with the write, and on a rejected scan only
  EvidenceSubmissionRegistered is
- RabbitMQ transport reuses the existing bus-outbox; no second publisher stack or exchange bootstrap

Do not touch Api.
```

Commit:

```text
feat(session-operations): phase X.3 - infrastructure layer (HU-31)

Ref: HU-31
Ref: DES-42
Ref: DES-70
```

---

## 8. Backend phase X.4 — Api layer

```text
Use @backend/.agents/backend-agent.md.
Implement backend phase X.4 for HU-31 in session-operations-service, per the
**X.4 derivation block in @backend/docs/hu31-context.md** (your spec — do not
re-read the canon or re-inspect the tree; open a cited canon section only to
fill a gap the block leaves open).

Gate:
- the participant target-scan endpoint returns acceptance/outcome metadata on a correct scan (no score
  leak in the response — score travels only on the RabbitMQ fact)
- wrong/duplicate/out-of-context scans return consistent RFC 7807 ProblemDetails
- a scan on a non-admitting session (Paused/Finished/Cancelled) is rejected
- an end-to-end test proves the correct path publishes EvidenceSubmissionRegistered then TargetResolved
  after transactional success
- service coverage passes the ADR-0005 gate (coverlet.msbuild, aggregate line+branch >= 93)

Do not touch frontend or mobile.
```

Commit:

```text
feat(session-operations): phase X.4 - api layer (HU-31)

Ref: HU-31
Ref: DES-42
Ref: DES-70
```

---

## 8.5. Docker rebuild

```text
Rebuild and restart the backend runtime for HU-31:

docker compose build session-operations-service api-gateway
docker compose up -d session-operations-service api-gateway

Then smoke the new write path and transport through the gateway:
- a correct QR scan for the active substage's target -> accepted response (target resolved)
- a wrong / duplicate / out-of-context scan -> RFC 7807 rejection with a consistent reason
- a scan while the session is Paused/Finished/Cancelled -> RFC 7807 rejection
- EvidenceSubmissionRegistered then TargetResolved are observable on the session-operations RabbitMQ exchange after transactional success
```

---

## 9. Downstream consumers — hand off the verified contract

```text
Do not implement client code as part of DES-42 — HU-31 is backend-only.

Instead, record the verified backend contract that downstream tickets must consume:

- participant target-scan endpoint shape (POST /api/sessions/{liveSessionId}/participants/target-scans)
- acceptance/outcome response shape (no score leak) and the RFC 7807 rejection reasons for
  wrong, duplicate, out-of-context, non-admitting-session, and forbidden cases
- the TargetResolved RabbitMQ message shape (liveSessionId, teamId, evidenceSubmissionId,
  activeSubstageId, targetSnapshotId, scoreValue, resolvedAt) and that EvidenceSubmissionRegistered
  still fires on the QR path
- the resolvedTargets read-model value now populated for the active team

Then hand off the contract to the correct downstream consumers:

- scoring-monitoring / HU-37 ledger (DES-51) — consumes TargetResolved.scoreValue into a ScoreEntry
- audit/history / HU-40A (DES-56) — consumes both facts
- HU-23 live board (DES-31) — reads the resolvedTargets read model

Output:
- the verified contract table
- the explicit consumer list above
- any open questions the backend contract still leaves for the downstream tickets
```

---

## 9b. (not applicable)

```text
HU-31 is backend-only: there is no frontend plan to generate or implement for this slice.
Any client surface that displays target-scan outcomes is owned by a separate client ticket and
consumes the verified contract from Step 9. Do not implement client code under the DES-42 scope or branch.
```

---

## 10. Close-out

```text
Before opening the PR, confirm all DES-42 acceptance criteria are satisfied:
- the system receives the scanned QR/token
- session, team, and mission context are validated before accepting the scan
- the Target is validated as belonging to the active treasure-hunt substage (a MissionNode), not a Stage or a Clue
- scans are not accepted when the session is Paused/Finished/Cancelled
- intake is unconditional: every scan registers an EvidenceSubmission (pending) and publishes
  EvidenceSubmissionRegistered after transactional success
- the scanned QR is validated against the current Target of the active substage; a Clue is optional
  guidance, never the validation object
- a correct match resolves the Target for the team, moves the evidence to accepted, and publishes
  TargetResolved (associated with team, session, and the valid MissionNode of the active substage)
- TargetResolved carries the resolved Target's ScoreValue
- an incorrect/invalid/duplicate/out-of-context scan auto-rejects (rejected) with a consistent reason and
  publishes no TargetResolved
- both events are published to RabbitMQ within the same transaction, without the main flow depending on RabbitMQ

Also confirm the ticket boundary:
- HU-31 is backend-only; no client UI is part of this PR
- substage advancement / "won" is not part of this HU

Then open the PR:

gh pr create \
  --base develop \
  --head feature/hu-31-qr-target-scan-resolution \
  --title "feat(session-operations): HU-31 server-side QR target scan and resolution" \
  --body "Implements HU-31 / DES-42: the treasure-hunt QR form (TreasureEvidenceSubmission) of the EvidenceSubmission umbrella. Unconditional intake registers each scan and publishes EvidenceSubmissionRegistered; the TargetResolutionPolicy chain resolves the scanned QR against the active treasure-hunt substage's Target, accepting on a correct match and publishing TargetResolved with the relayed ScoreValue, or retaining a Rejected submission with a consistent reason otherwise. Reuses the HU-29 intake facade/chain/outbox and mirrors the HU-34 trivia form. Backend-only; downstream consumers (scoring-monitoring, audit/history, HU-23 board) consume the async facts and read model."
```

---

## Rationale

- **Why the pattern set is a single `Chain of Responsibility`:** HU-31 is a synchronous
  intake-and-resolution slice. The `Facade` and the generic intake chain already exist (HU-29), so the
  only *mandated new* obligation is the QR-specific resolution chain `TargetResolutionPolicy`
  (`required_patterns_matrix.md:124`). It is a genuine chain — ordered, reorderable links, first-fail
  decides — not a handler with sequential `if` blocks.
- **Why QR rejection is retained, not thrown:** HU-34 throws for late/duplicate answers (never persists),
  but ADR-0010 (`:43-46`) and the AC's unconditional-intake rule require a wrong scan to be *registered
  and retained for audit history*. So the resolution chain yields a verdict (accept + reason-on-fail) and
  the base `EvidenceSubmission` gains a `MarkRejected(reason)` transition (the `Rejected` state was
  previously unused). Only pre-intake context guards (session/team/active-substage) throw and block
  registration.
- **Boundary — pre-intake block vs post-intake retained-reject:** the AC lists "fuera de contexto" in
  both the pre-accept guard (AC#2/#4) and the auto-reject list (AC#9). The cut used here:
  session/team/active-substage-present → pre-intake throw (reused generic chain); QR-does-not-match /
  target-not-in-active-substage / duplicate → post-intake retained `Rejected`. If the reviewer wants a
  different boundary (e.g. treat "wrong QR" as a pre-intake block too), flag it at Stop 1 — the
  derivation block is the single place to change it.
- **Why the score is relayed, not re-validated:** ADR-0015 (`:64-72`) splits ownership — MissionDesign
  authors the per-target score, SessionOperations relays it, ScoringMonitoring accumulates it. The AC's
  "1-100" is pre-amendment; `adr/0015:19-29` (2026-07-10) makes the authored score derived `{50,100,150}`
  (`ScoreValue.MaximumPoints` now 150). So session-operations relays `TargetSnapshot.Score` (a positive
  non-null int after DES-87) verbatim into `TargetResolved` — it does not impose a numeric range. Recorded
  on DES-42 as the `⚠️ Nota de canon` (2026-07-13) so the AC and this spec no longer disagree.
- **Why there is no frontend step:** DES-42 is `backend-only`. Following the HU-29 precedent, Step 9 is a
  downstream-contract hand-off (scoring/audit/board consumers), and there is no Step 9 frontend-plan
  generation or Step 9b implementation for this slice.
- **Why `TargetResolved`'s fields are defined here:** canon mandates the payload carries the resolution
  fact + the authored `ScoreValue` (`adr/0015:56-62`) but does not enumerate the fields; they are derived
  from the `AnswerRegisteredEvent` precedent + the `TargetResolution` entity
  (`bd_umbral_entity_spec.md:567-599`) and noted as derived in the context file.

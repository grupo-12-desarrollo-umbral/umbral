# Required Patterns Matrix — full backlog

Maps **every** user story in `umbral_user_stories.md` (`HU-01`–`HU-40`) to the
mandatory design patterns from `docs/adr/0004-required-domain-patterns.md`,
cross-checked against the per-context pattern mapping in
`docs/ddd_solution_model.md` §8.

This is the full-backlog superset. The sprint-scoped subset (with A/B ticket
splits) lives in `trivia_sprint_required_patterns_matrix.md`; **where** each
pattern physically lives across Domain/Application/Api is governed by
[ADR-0012](adr/0012-design-pattern-placement-convention.md).

> A pattern is listed for an HU only when the ADR/DDD model mandates it for that
> HU's responsibility. HUs marked `—` carry **no mandated pattern**; `Proxy`
> still applies wherever they expose protected data or actions (it guards
> access, it isn't an HU-specific design choice).

---

## ADR-0004 pattern → mandated responsibility

| Pattern | ADR-0004 responsibility |
| --- | --- |
| `Composite` | `Mission` + hierarchical `MissionNode` (Stage / Substage / Clue / Target) modeling |
| `Template Method` | stable validation workflows with mode-specific steps |
| `Facade` | session orchestration + outbound event publication in `SessionOperations` |
| `State` | `LiveSession` lifecycle transitions |
| `Chain of Responsibility` | composable validation of QR submissions, trivia answers, and session-state changes |
| `Proxy` | role- and policy-based access guards in service and presentation layers |
| `Strategy` | score calculation + mode-specific progression policies |

---

## Pattern → HU (full backlog)

| Pattern | HUs |
| --- | --- |
| `Composite` | `HU-10`, `HU-28` |
| `Template Method` | `HU-11`, `HU-12`, `HU-13`, `HU-14`, `HU-34` |
| `Facade` | `HU-15`, `HU-16`, `HU-18`, `HU-19`, `HU-26`, `HU-29`, `HU-33` |
| `State` | `HU-21`, `HU-22`, `HU-33` |
| `Chain of Responsibility` | `HU-21`, `HU-29`, `HU-30`, `HU-31`, `HU-34` |
| `Proxy` | `HU-01`, `HU-02`, `HU-03`, `HU-06`, `HU-07`, `HU-19`, `HU-20`, `HU-24`, `HU-25`, `HU-26`, `HU-36`, `HU-38` |
| `Strategy` | `HU-33`, `HU-37`, `HU-38`, `HU-39` |
| — (no mandated pattern) | `HU-04`, `HU-05`, `HU-08`, `HU-09`, `HU-17`, `HU-23`, `HU-27`, `HU-32`, `HU-35`, `HU-40` |

---

## Transport → HU

Derived from the backlog technical notes (real-time → SignalR/WebSockets; async
secondary processing → RabbitMQ, never on the critical path). Not from ADR-0004
— included for parity with the sprint matrix.

| Transport | HUs |
| --- | --- |
| `SignalR / WebSockets` | `HU-07`, `HU-08`, `HU-21`, `HU-22`, `HU-23`, `HU-24`, `HU-26`, `HU-33`, `HU-34`, `HU-35`, `HU-36`, `HU-39` |
| `RabbitMQ` | `HU-21`, `HU-29`, `HU-33`, `HU-34`, `HU-37`, `HU-40` |
| — (neither) | `HU-01`–`HU-06`, `HU-09`–`HU-20`, `HU-25`, `HU-27`, `HU-28`, `HU-30`, `HU-31`, `HU-32`, `HU-38` |

> **Canonical end-to-end RabbitMQ workflow:** `HU-29` publishes
> `EvidenceSubmissionRegistered` after transactional success, consumed by
> audit/history, notification, and secondary recalc/projection. The trivia
> analog is `HU-34` → `AnswerRegistered` → scoring (`HU-37`).

---

## HU → Pattern (by bounded context)

### `Identity` (HU-01–HU-08)

| HU | Required pattern(s) | Transport | Why |
| --- | --- | --- | --- |
| `HU-01` | `Proxy` | — | Login + role identification gated by access guards (`AccessPolicy`). |
| `HU-02` | `Proxy` | — | User-access management restricted to admin policy; deactivated users blocked from protected capabilities. |
| `HU-03` | `Proxy` | — | Role/permission assignment enforced through policy-aware guards, including direct-route/endpoint attempts. |
| `HU-04` | — | — | Team reference-data CRUD. No mandated pattern; `Proxy` guards its access endpoints. |
| `HU-05` | — | — | Participant↔team membership CRUD. No mandated pattern; `Proxy` protects team-context access. |
| `HU-06` | `Proxy` | — | Participant login restricted to participant role on the React Native client. |
| `HU-07` | `Proxy` | SignalR | Membership validation + authorized reconnection gate admission to the team's live context (`JoinPolicy`). |
| `HU-08` | — | SignalR | Multi-device team sync — real-time backbone enabler, not a pattern HU. |

### `MissionDesign` (HU-09–HU-14)

| HU | Required pattern(s) | Transport | Why |
| --- | --- | --- | --- |
| `HU-09` | — | — | Mission basic-data CRUD + lifecycle on the aggregate root; no Composite traversal of its own — the tree is authored in `HU-10`. |
| `HU-10` | `Composite` | — | **Canonical Composite** — `Mission` owns the Stage/Substage/Clue/Target tree; `MissionStructurePolicy` protects tree invariants (no substage-in-substage, one play mode per substage). |
| `HU-11` | `Template Method` | — | Quiz create/edit keeps one invariant validation workflow with quiz-specific steps. |
| `HU-12` | `Template Method` | — | Publication/archival requires a stable readiness-validation pipeline (`TriviaPublicationPolicy`). |
| `HU-13` | `Template Method` | — | Duplicate/retire reuse the same quiz lifecycle validation sequence. |
| `HU-14` | `Template Method` | — | Question authoring (2–4 options, exactly one correct, score/timer ranges) fits the shared validation template. |

### `SessionOperations` — preparation (HU-15–HU-20)

| HU | Required pattern(s) | Transport | Why |
| --- | --- | --- | --- |
| `HU-15` | `Facade` | — | Session creation orchestrates source checks (`SessionCreationPolicy`), snapshot, and side effects through one coordination service. |
| `HU-16` | `Facade` | — | Runtime snapshot orchestrates the immutable copy of the mission Composite tree (substages, targets, clues, questions) into `MissionRuntimeSnapshot`. |
| `HU-17` | — | — | Single-source invariant; enforced inside the `HU-15`/`HU-16` Facade + `SessionCreationPolicy`, not its own pattern. |
| `HU-18` | `Facade` | — | Team association coordinates runtime session state changes and persistence before start. |
| `HU-19` | `Facade`, `Proxy` | — | Operator assignment is orchestration **plus** guarded access to protected session actions. |
| `HU-20` | `Proxy` | — | Assigned-session reads restricted by role and authorization policy. |

### `SessionOperations` — live execution (HU-21–HU-25)

| HU | Required pattern(s) | Transport | Why |
| --- | --- | --- | --- |
| `HU-21` | `State`, `Chain of Responsibility` | SignalR + RabbitMQ | Lifecycle (`Scheduled`→…→`Finished`/`Cancelled`) needs an explicit state model plus ordered transition validators (`SessionStateTransitionPolicy`); valid transitions broadcast live, `SessionStateChanged` published for async audit. Facade publishes the event. |
| `HU-22` | `State` | SignalR | Timer behavior depends on session state (active vs paused); authoritative remaining time pushed and restored on resume/reconnect. |
| `HU-23` | — | SignalR | Live team board — real-time read projection; `Proxy` scopes it to the team. |
| `HU-24` | `Proxy` | SignalR | Operator real-time panel is a guarded projection — only assigned/authorized sessions, blocks unauthorized actions. |
| `HU-25` | `Proxy` | — | Cross-entity operational reads restricted to the authenticated role's permissions. |

### `SessionOperations` — mission / QR flow (HU-26–HU-32)

| HU | Required pattern(s) | Transport | Why |
| --- | --- | --- | --- |
| `HU-26` | `Facade`, `Proxy` | SignalR | Manual clue release orchestrates a runtime visibility change + history record (`ClueReleasePolicy`); `Proxy` guards access to restricted clues (ADR-0004 names restricted clues explicitly). |
| `HU-27` | — | — | Initial clue-visibility config copied to the snapshot. Authoring config on `Clue` Composite nodes; no own mandated pattern. |
| `HU-28` | `Composite` | — | Treasure-hunt `Target` objectives are leaf nodes owned by a treasure-hunt `Substage` in the Composite tree (unique QR ids, resolvable in any order). |
| `HU-29` | `Chain of Responsibility`, `Facade` | RabbitMQ | Evidence submission runs the composable validation pipeline; on transactional success the Facade publishes `EvidenceSubmissionRegistered` (**canonical RabbitMQ workflow**). |
| `HU-30` | `Chain of Responsibility` | — | Pre-acceptance validation (session, team, active substage, target/question) composed as ordered validators (`EvidenceValidationPolicy`). |
| `HU-31` | `Chain of Responsibility` | — | Server-side QR target validation (belongs to active substage, not duplicate/out-of-context) is the QR-submission validator chain (`TargetResolutionPolicy`). |
| `HU-32` | — | — | Evidence traceability — audit read; `Proxy` guards operator access. |

### `SessionOperations` — trivia flow (HU-33–HU-36)

| HU | Required pattern(s) | Transport | Why |
| --- | --- | --- | --- |
| `HU-33` | `Facade`, `State`, `Strategy` | SignalR + RabbitMQ | **Highest pattern density.** Automated trivia round combines lifecycle orchestration (Facade), session/question state (State), and mode-specific progression/scoring (Strategy); countdown/activation/close broadcast live, results published for history. |
| `HU-34` | `Template Method`, `Chain of Responsibility` | SignalR + RabbitMQ | First-valid-answer acceptance + late/duplicate rejection share one ordered validator sequence (`TriviaQuestionTimerPolicy`); "answered" indicator broadcast, `AnswerRegistered` published. |
| `HU-35` | — | SignalR | Result/explanation reveal after close — real-time push, no mandated pattern. |
| `HU-36` | `Proxy` | SignalR | Restricted answer monitoring (answered-or-not before close, full review after) is a guarded projection updated live. |

### `ScoringMonitoring` (HU-37–HU-40)

| HU | Required pattern(s) | Transport | Why |
| --- | --- | --- | --- |
| `HU-37` | `Strategy` | RabbitMQ | Score ledger entries come from interchangeable scoring policies (`ScorePolicy`); each entry records its origin and feeds secondary recalc/projections. |
| `HU-38` | `Strategy`, `Proxy` | — | Justified penalties are a score-policy outcome (`PenaltyPolicy` eligibility + `ScorePolicy` impact); `Proxy` restricts to the operator's assigned session. |
| `HU-39` | `Strategy` | SignalR | Real-time ranking depends on score-policy outcomes and tie-breaking (`RankingPolicy`, `ResolutionTime`); refreshed ranking pushed to clients. |
| `HU-40` | — | RabbitMQ | Session event history — audit read/consolidation; `Proxy` guards operator access, fed by consumed events. |

---

## Sprint cross-reference (A/B ticket splits)

The trivia sprint matrix splits some whole HUs into delivery tickets. For
cross-referencing:

| Backlog HU | Sprint tickets |
| --- | --- |
| `HU-07` | `HU-07A` (membership/admission), `HU-07B` (reconnection) |
| `HU-14` | `HU-14A` (authoring flow), `HU-14B` (trivia rules) |
| `HU-21` | `HU-21A` (transitions + broadcast), `HU-21B` (audit event) |
| `HU-33` | `HU-33A` (round open/activation), `HU-33B` (round close/results) |
| `HU-34` | `HU-34A` (accept first answer), `HU-34B` (reject late/repeat) |
| `HU-36` | `HU-36A` (pre-close monitor), `HU-36B` (post-close review) |
| `HU-37` | `HU-37A` (score ledger), `HU-37B` (ranking refresh) |
| `HU-39` | `HU-39B` (trivia ranking) |

> The sprint matrix also renumbers session-creation: backlog `HU-15`/`HU-16`
> map to the sprint's `HU-16` (session-from-source Facade), since the sprint
> folds single-source creation and snapshot into one ticket.

---

## Hardest HUs (full backlog)

### Tier 1 — Hardest

| HU | Patterns | Why |
| --- | --- | --- |
| `HU-33` | Facade + State + Strategy | Only HU requiring 3 patterns. Auto-executed trivia round needs lifecycle orchestration, state-driven question progression, mode-specific scoring, AND the full real-time + event stack. |
| `HU-21` | State + Chain of Responsibility | Session lifecycle is foundational — `HU-22`, `HU-33`, and the whole runtime depend on correct transitions, validators, and audit trail. |

### Tier 2 — Hard

| HU | Patterns | Why |
| --- | --- | --- |
| `HU-29` | Chain of Responsibility + Facade | The canonical evidence pipeline + the one demonstrated end-to-end RabbitMQ workflow; umbrella over both QR and trivia submissions. |
| `HU-34` | Template Method + Chain of Responsibility | Accept-first / reject-late must reuse one ordered validator sequence under tight trivia timing without collapsing into a single handler. |
| `HU-10` | Composite | Mission hierarchy invariants (bounded tree, one play mode per substage, no nesting) — every snapshot and runtime flow inherits this structure. |

### Tier 3 — Non-trivial

| HU | Why |
| --- | --- |
| `HU-22` | Authoritative timer surviving pause/resume/reconnect across all clients, tied to `HU-21` state. |
| `HU-37`/`HU-39` | Strategy-based scoring/ranking with tie-breaks feeding real-time and projection consumers. |

### Bottom line

`HU-21` and `HU-33` are the derisk-early targets (the runtime blocks on
`HU-21 → HU-22 → HU-33`); `HU-10` derisks all of MissionDesign + snapshotting;
`HU-29` derisks the evidence + RabbitMQ backbone. Everything else is single-
pattern or pattern-free.

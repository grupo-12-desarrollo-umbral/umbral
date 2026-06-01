# Trivia Sprint 1 — Required Patterns Matrix

Maps the **Sprint 1 (Trivia completo)** tickets from `tickets_trivia_v2.md` to the mandatory design patterns from `hu-required-design-patterns-matrix.md` (which derives from `docs/adr/0004-required-domain-patterns.md`, `docs/ddd_solution_model.md`, and the service `CONTEXT.md` files).

TreasureHunt/mission HUs are out of scope for this sprint and are omitted.

---

## Pattern → HU (sprint scope only)

| Pattern | Sprint HUs |
| --- | --- |
| `Composite` | — (mission authoring excluded this sprint) |
| `Template Method` | `HU-11`, `HU-12`, `HU-13`, `HU-14A`, `HU-14B`, `HU-34A`, `HU-34B` |
| `Facade` | `HU-16`, `HU-18`, `HU-19`, `HU-33A`, `HU-33B` |
| `State` | `HU-21A`, `HU-21B`, `HU-22`, `HU-33A`, `HU-33B` |
| `Chain of Responsibility` | `HU-21A`, `HU-34A`, `HU-34B` |
| `Proxy` | `HU-01`, `HU-02`, `HU-03`, `HU-06`, `HU-07A`, `HU-07B`, `HU-19`, `HU-20`, `HU-36A` |
| `Strategy` | `HU-33A`, `HU-33B`, `HU-37A`, `HU-37B`, `HU-39B` |

---

## HU → Pattern (sprint scope, by service)

### `identity-access-service`

| HU | Required pattern(s) | Why |
| --- | --- | --- |
| `HU-01` | `Proxy` | Login + role identification gated by access guards. |
| `HU-02` | `Proxy` | User-access management restricted to admin policy. |
| `HU-03` | `Proxy` | Role/permission assignment enforced through policy-aware guards. |
| `HU-04` | — (not pattern-mandated) | Team reference-data CRUD. No mandated pattern; `Proxy` applies to its access endpoints. |
| `HU-05` | — (not pattern-mandated) | Participant↔team membership CRUD. No mandated pattern; `Proxy` applies to its access endpoints. |
| `HU-06` | `Proxy` | Participant login restricted to participant role on mobile client. |
| `HU-07A` | `Proxy` | Membership validation is a policy-guarded admission check. |
| `HU-07B` | `Proxy` | Authorized reconnection guarded by the same access policy. |
| `HU-08` | — (real-time enabler) | Multi-device team sync. Not a pattern HU; realized via SignalR/WebSockets backbone. |
| `HU-18` | `Facade` | Team assignment coordinates runtime session state changes and persistence. |
| `HU-19` | `Facade`, `Proxy` | Operator assignment is orchestration plus guarded access to protected session actions. |
| `HU-20` | `Proxy` | Assigned-session reads restricted by role and authorization policy. |

### `mission-design-service` (quiz only)

| HU | Required pattern(s) | Why |
| --- | --- | --- |
| `HU-11` | `Template Method` | Quiz creation/edit validation keeps one invariant workflow. |
| `HU-14A` | `Template Method` | Question authoring uses a shared validation flow with question-specific steps. |
| `HU-14B` | `Template Method` | Explicit trivia-question validation rules fit the required validation template. |
| `HU-12` | `Template Method` | Publication/archival requires a stable readiness validation pipeline. |
| `HU-13` | `Template Method` | Duplicate/retire rules reuse the same quiz lifecycle validation sequence. |

### `session-operations-service`

| HU | Required pattern(s) | Why |
| --- | --- | --- |
| `HU-16` | `Facade` | Session creation from a quiz orchestrates source checks, fixed copy, and side effects. |
| `HU-21A` | `State`, `Chain of Responsibility` | Lifecycle transitions need an explicit state model plus ordered transition validators. |
| `HU-21B` | `State` | Audit trail derives from authoritative lifecycle state transitions. |
| `HU-22` | `State` | Timer behavior depends on session state (active vs paused). |
| `HU-33A` | `Facade`, `State`, `Strategy` | Trivia round orchestration combines lifecycle control, coordination, and mode-specific progression policies. |
| `HU-33B` | `Facade`, `State`, `Strategy` | Round closing/final results require orchestration, lifecycle transitions, and score/ranking policy variation. |
| `HU-34A` | `Template Method`, `Chain of Responsibility` | First valid answer acceptance uses ordered checks inside one stable validation workflow. |
| `HU-34B` | `Template Method`, `Chain of Responsibility` | Late/repeated answer rejection is the corresponding ordered validation pipeline. |
| `HU-35` | — (not pattern-mandated) | Result/explanation reveal after close. No mandated pattern; reads from closed-question state. |
| `HU-36A` | `Proxy` | Restricted operator monitoring before question close is a guarded projection. |
| `HU-36B` | — (not pattern-mandated) | Post-close answer/points review. Not in source matrix; `Proxy` applies as a guarded projection like HU-36A. |

### `scoring-monitoring-service`

| HU | Required pattern(s) | Why |
| --- | --- | --- |
| `HU-37A` | `Strategy` | Score ledger entries come from interchangeable scoring/evaluation policies. |
| `HU-37B` | `Strategy` | Ranking refresh depends on score-policy outcomes and tie-breaking behavior. |
| `HU-39B` | `Strategy` | Trivia ranking uses the strategy requirement with trivia-specific timing rules. |

> **Not mapped in the source matrix:** `HU-04`, `HU-05`, `HU-08`, `HU-35`, `HU-36B`. These carry no mandated pattern in `hu-required-design-patterns-matrix.md`. `Proxy` still applies wherever they expose protected data/actions (HU-04/05/36B), and HU-08 is a real-time-sync enabler rather than a pattern HU.

---

## Hardest HUs in this sprint

Filtered from `hardes_hu.md` to the HUs actually in Sprint 1.

### Tier 1 — Hardest

| HU | Patterns | Why it's brutal |
| --- | --- | --- |
| `HU-33A`/`HU-33B` | Facade + State + Strategy | Only HUs requiring 3 patterns. Trivia auto-execution needs lifecycle orchestration, mode-specific progression policies, AND real-time broadcasting. End-to-end round loop is the most moving-parts feature in the sprint. |
| `HU-21A` | State + Chain of Responsibility | Session lifecycle is foundational — HU-22, HU-33A/B and the whole runtime depend on it. Getting transitions, validators, and audit trail right here unlocks or blocks the rest of `session-operations`. |

### Tier 2 — Hard

| HU | Patterns | Why |
| --- | --- | --- |
| `HU-34A`/`HU-34B` | Template Method + Chain of Responsibility | Answer acceptance/rejection pipeline under trivia's tight timing constraints. Both flows must reuse the same ordered validators in a stable sequence without collapsing into one big handler. |

### Tier 3 — Non-trivial

| HU | Why |
| --- | --- |
| `HU-22` | Authoritative timer that survives pause/resume/reconnect across all clients requires careful state management tied to `HU-21A`. |

> The other Tier 2/Tier 3 entries in `hardes_hu.md` (HU-27, HU-31, HU-30A/30B, HU-10A/10B, HU-29) are TreasureHunt/mission/QR/evidence HUs and are **out of scope** for this sprint.

### Bottom line

Within Sprint 1, **HU-21A** and **HU-33A/33B** are the derisk-early targets: HU-21A because the session runtime blocks on it (critical path `HU-21A → HU-22` and `HU-21A → HU-33A`), and HU-33A/33B because it's the highest pattern density and pulls in the full real-time stack. HU-34A/34B follows immediately behind on the critical path.

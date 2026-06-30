# Plan: Application-layer vertical-slice refactor

## Context

A review of the four services' Application layers found they organize **by technical type**
(`Handlers/`, `DTOs/` buckets — the old `structure.md` baseline) instead of **by vertical slice**
(one folder per use case), plus accidental ceremony: a hand-rolled `Proxy → Service → Executor`
forwarding chain on top of MediatR, and authorization implemented two different ways. There is no
uniform structure across services, and `scoring-monitoring-service` is an empty scaffold.

This plan refactors the **three implemented** services to a single canonical convention and treats
`scoring-monitoring-service` as separate greenfield work. It is structured so the bulk of the work
runs **in parallel across services**.

> **Governance note (read first).** This refactor changes the documented baseline. Per `AGENTS.md`
> ("Structure Enforcement"), the docs change **before** any file moves. The baseline change is
> recorded in **ADR-0011** (vertical-slice organization), which **explicitly preserves ADR-0004's
> mandated patterns**. Nothing in this plan deletes a mandated pattern — see "What this plan does NOT
> do" below.

### What this plan does NOT do (guardrails against the earlier draft's mistakes)

- It does **not** ban `Proxy`/`Facade`/`State`/`Strategy`/`Template Method`/`Chain of Responsibility`/
  `Composite`. Those are **mandated** by `docs/adr/0004-required-domain-patterns.md`, the service
  `CONTEXT.md` files, and `docs/trivia_sprint_required_patterns_matrix.md`, and the phase gate fails if
  a named pattern is not realized. They are kept and, where loose, made into genuine structural patterns.
- It does **not** move authorization wholesale into `[Authorize]`. Only coarse role/policy gates become
  declarative; **resource-specific** access decisions stay as a `Proxy`/domain policy.
- It does **not** use `dotnet` directly. Every gate is `make -C backend <target> SVC=<service>`.

### What is and isn't safe to parallelize (verified against the code)

**Safe — across services (the real parallel axis).** Each `Application.csproj` references **only its own
`Domain`** (verified) — no sibling-service source references; services integrate over HTTP. Separate DI,
`GlobalUsings`, and test suites per service → safe to run concurrently.

**Not safe — by feature folder *within* a service.** Three chokepoints (all verified):
- **`DependencyInjection.cs` is one shared file per service.** identity-access and session-operations
  register ~10 `Proxy/Service/Executor/Facade` lines each. De-ceremony edits this one file from every
  feature → guaranteed conflicts.
- **Namespace follows folder.** Moving `Missions.DTOs` ripples to ~51 files and handlers to ~28,
  **including the `Api` layer** (controllers + identity-access's `Api/Services/` import the DTO
  namespaces directly). A "move" is not a local edit.
- **Cross-feature coupling exists.** `Trivias/ArchiveTriviaQuizCommandHandler` imports `Missions.Common`,
  so Missions and Trivias are not disjoint streams.

Controllers never reference *handler types* (they use `ISender`), and MediatR/validators register by
assembly scan — so moves stay mechanical, but their blast radius spans the `Api` layer.

---

## Target convention (the contract every stream implements)

Defined authoritatively in **ADR-0011** and `structure.md`. One folder per use case:

```
Application/<Area>/Commands/<UseCase>/
    <UseCase>Command.cs            // command record (+ nested Response/Vm if small)
    <UseCase>CommandHandler.cs     // the ONE handler, orchestration only
    <UseCase>CommandValidator.cs   // FluentValidation
Application/<Area>/Queries/<UseCase>/
    <UseCase>Query.cs
    <UseCase>QueryHandler.cs
    <UseCase>QueryValidator.cs     // only if the query has inputs to validate
    <UseCase>Dto.cs                // response model owned by the query that returns it
Application/<Area>/Common/         // shared-by-≥2-slices mappers/guards + mandated patterns for the area
Application/Common/                // cross-cutting: Behaviours, Interfaces, Exceptions, Security, Models
```

Rules (also enforced in code review + the Phase-4 guard):
- **No** `Handlers/`, `DTOs/`, or `Facades/` type-buckets. Handler + owned DTO sit in the use-case
  folder; a mandated Facade sits in the slice it orchestrates (single consumer) or `<Area>/Common/`
  (shared), grouped by concern — never collected in a `Facades/` bucket. (`EventHandlers/` and
  `StateTransitions/` are preserved — they are the mandated event-dispatch / `State`-machine units,
  not type-buckets.)
- **Keep mandated patterns** (ADR-0004). A `Proxy`/`Facade`/`Template Method`/`State`/`Strategy`/`CoR`
  implementation that is named in `trivia_sprint_required_patterns_matrix.md` or a phase gate **stays**.
- **Remove only un-mandated forwarding ceremony.** The thing to delete is the `IService`/`IExecutor`
  pass-through split that adds no behavior and is not a named deliverable — collapse it into the handler.
- **Authorization:** coarse role/policy gate → `[Authorize]` + `AuthorizationBehaviour`. Resource-specific
  decision → `Proxy`/domain policy. Business-rule guards live in Domain entities/domain services.
- One handler per request; `CancellationToken` threaded; queries never mutate.

**Before removing any `*Proxy`/`*Facade`/`*Executor`, check the patterns matrix and the use case's
phase scope.** If the pattern is named there, keep it (refactor it to a genuine structural pattern, not a
rename). If not, and it is pure forwarding, collapse it.

---

## Phase overview

```
Phase 0  Governance: ADR-0011 + structure.md baseline change   [SERIAL — blocks all file moves]
   │
Phase 1  Golden service end-to-end (mission-design)            [SERIAL — the reference]
   │
   ├───────────────────────┬───────────────────────────┐
   ▼                       ▼                           (scoring-monitoring is a SEPARATE epic,
Phase 2  Replicate to identity-access & session-operations      not part of this refactor)
   │        (PARALLEL × 2 services; serial within each)
   ▼
Phase 3  Cross-service consistency sweep + structural guard    [SERIAL — convergence]
```

---

## Phase 0 — Governance change  *(SERIAL, blocks all file moves)*

Per `AGENTS.md` structure enforcement, the documented baseline changes **before** any file moves.

1. Land **ADR-0011** (vertical-slice organization; explicitly preserves ADR-0004).
2. Update `structure.md`: replace the `Handlers/` + `DTOs/` buckets in the Application tree with
   co-located handlers/DTOs + an `<Area>/Common/`; update the "Commands and Queries" and "DTOs" notes.
3. Confirm ADR-0004, the service `CONTEXT.md` Proxy/Facade/State requirements, and the patterns matrix
   are untouched.

**Exit criteria:** ADR-0011 merged; `structure.md` reflects the slice baseline; ADR-0004 unchanged.
**Owner:** 1 person. **Est:** ~0.25 day.

---

## Phase 1 — Golden service end-to-end: mission-design  *(SERIAL — the reference)*

Take **one service all the way** (mechanical collapse **and** de-ceremony) so the other streams copy a
real end-state, not a half-way shape. mission-design is chosen: it holds the hardest patterns
(`MissionCommandHandlerBase`, `Trivias → Missions.Common` coupling) and is the freshest code.

1. **Slice-collapse** `Missions`, then `Trivias` (keep the `Missions.Common` ref valid):
   - Move each `Handlers/<X>Handler.cs` into its `Commands/<X>/` or `Queries/<X>/` folder.
   - Move each query-owned `DTOs/<X>Dto.cs` into the query folder; genuinely shared DTOs/mappers into
     `<Area>/Common/`.
   - Update namespaces, then fix **all** `using` lines across `src`, the `Api` layer, and
     `Application.UnitTests` — scripted find/replace per moved namespace, applied atomically.
   - Delete the now-empty `Handlers/` and `DTOs/` folders.
2. **De-ceremony (judgment):** replace the `MissionCommandHandlerBase` inheritance with a small injected
   mapper/guard or direct repository calls (no inheritance between handlers). This is *not* a mandated
   pattern, so it goes. Keep `MissionDtoMapper`, the `*Guard` domain guards, and any matrix-named pattern.
3. `make -C backend test SVC=mission-design-service` green.
4. Capture the before/after as the canonical diff; link it from ADR-0011.

**Exit criteria:** no `Handlers/`/`DTOs/` bucket in mission-design; no base-handler inheritance; mandated
patterns intact; build + mission-design tests green. **Est:** ~1 day.

---

## Phase 2 — Replicate to the other two services  *(PARALLEL × 2; serial within each)*

Parallel **across** identity-access and session-operations; **single-owner within** each (every item
edits that service's one `DependencyInjection.cs`). Each item is its own small, separately-reviewable
commit, sequenced on a green build.

Apply the keep/cut lens from `docs/refactors/application-layer-overengineering-checklist.md` to every
slice — it is the Phase-1 audit distilled (cut `Id > 0` marker/base-validator combos, dead interfaces,
handler base classes; keep mandated ADR-0004 patterns). The de-ceremony steps below are that checklist's
forwarding-triplet and type-bucket rows in context.

**Per service, in order:**
1. **Slice-collapse** (mechanical, behavior-preserving) — same procedure as Phase 1 step 1. Areas:
   - identity-access: `Users`, `Teams`, `Sessions`, `JoinTokens`, `Permissions`.
   - session-operations: `Sessions` (keep `StateTransitions/` and `EventHandlers/` — the mandated
     `State` machine and event-dispatch units). The `Facades/` folder is a type-bucket and is
     **dissolved**, not kept — see **Facade handling** below (the Facades themselves stay).
2. **Collapse un-mandated forwarding only.** For each `*AuthorizationProxy` / `I*Service` / `I*Executor`
   triplet, **check the patterns matrix first**:
   - If `Proxy` is named for that HU (identity-access auth guards are — `HU-01/02/03/06/07A/07B/19/20`,
     session `HU-19`), **keep the Proxy** as a genuine access-guard; collapse only the redundant
     `IService`/`IExecutor` pass-through beneath it into the handler.
   - If nothing is named and the class is pure forwarding, inline it into the handler.
3. **Authorization split.** Coarse role/policy checks → `[Authorize]` + `AuthorizationBehaviour`.
   Resource-specific checks (load actor, `IsActive`, `AccessPolicy.Evaluate`, "actor may assign this
   operator to this session") **stay** in the `Proxy`/domain policy.
4. session-operations specifics: keep `StateTransitions` (mandated `State`) and `EventHandlers`. Handle
   the six Facades per **Facade handling** below.

#### Facade handling (session-operations)

The earlier draft named only three Facades and left the `Sessions/Facades/` bucket and two Facades
unaddressed. The complete, verified verdict — every Facade is **kept** (each realizes a matrix-named or
genuinely-shared orchestration), and the `Sessions/Facades/` **type-bucket is dissolved**: each Facade
moves to the slice it orchestrates (single consumer) or `<Area>/Common/` (shared by ≥2 slices).
Verdicts grounded in `docs/trivia_sprint_required_patterns_matrix.md` + consumer counts.

| Facade | Today | Matrix | Consumers | Destination |
| --- | --- | --- | --- | --- |
| `AssignOperatorToSessionFacade` | `Commands/AssignOperatorToSession/` | HU-19 `Facade` | in slice | keep — already co-located ✓ |
| `TransitionSessionStateFacade` | `Commands/TransitionSessionState/` | HU-21A/33A/33B `State`+`Facade` | in slice | keep — already co-located ✓ |
| `CreateSessionFacade` | `Commands/CreateSession/` | **HU-16 `Facade`** | in slice | keep — already co-located ✓ (was missing from the old keep-list) |
| `TriviaRoundOrchestratorFacade` | `Sessions/Facades/` | HU-33A/33B `Facade` | **2** (`Sessions/EventHandlers/TriviaRoundStartedNotificationHandler` + `Infrastructure/Realtime/AuthoritativeSessionTimerWorker.cs:57`) | keep; **move to `Sessions/Common/`** (shared by ≥2 consumers) — do NOT co-locate in the event-handler slice |
| `SessionTeamAssociationFacade` | `Sessions/Facades/` | realizes HU-18's session-side team assignment (`Facade`) | **4** (`AssociateTeamToSession{,ByCode}`, `GetAssociatedTeamsForSession{,ByCode}`) | keep; **move to `Sessions/Common/`** (shared by ≥2 slices) — do NOT fold |

After the moves, delete the empty `Sessions/Facades/` folder and fix the `...Sessions.Facades` namespace
import + the five `DependencyInjection.cs` registrations. The structural guard forbids a `Facades/`
directory, so this is enforced (mandated Facade *files* in slices/`Common/` are never flagged).

**Genuine Facade vs forwarding ceremony (the test for §3 de-ceremony).** A Facade is **genuine** when it
coordinates **≥2 collaborators** behind one interface, hiding a multi-step workflow (transaction boundary,
orchestration, or event publication). It is **ceremony to collapse** when it is a 1-to-1 pass-through that
relays a single call to a single collaborator and adds no behavior — that is the un-mandated
`IService`/`IExecutor` layer, not a Facade.

> *Worked example — `CreateSessionFacade` (HU-16) is genuine, keep it.* Its `CreateAsync` coordinates four
> collaborators — `IMissionReadinessSource` (load + `SessionCreationPolicy.EnsureMissionEligible`),
> `IMissionRuntimeSource` (load + build the `MissionRuntimeSnapshot`), `LiveSession.Create`, and
> `ILiveSessionRepository` — behind one interface. The handler is a thin delegate
> (`return _facade.CreateAsync(request, ct)`), which is fine: the **Facade** is the mandated orchestration
> unit, not the handler.
> *Contrast — collapse.* A `JoinTeamAsParticipant` flow where the handler calls `IJoinTeamAsParticipantExecutor`
> which just forwards one call to one repository: no coordination, no added behavior → inline it into the
> handler and delete the `*Executor` (Rule D / §3).

**Behavior-preservation gate (hard):** before de-ceremonying a slice, confirm it has authorization/behavior
**integration** coverage. If coverage is thin, add a characterization test **first**. Never mix a
mechanical move and a semantic change in the same PR.

**Exit criteria per service:** no `Handlers/`/`DTOs/` bucket; no un-mandated forwarding triplet; mandated
patterns intact and realized as genuine structures; auth via pipeline + domain; build + all test suites
green. **Est:** ~1–2 days per service.

---

## Phase 3 — Cross-service consistency sweep + structural guard  *(SERIAL — convergence)*

Run once the three streams land.

1. Diff the three Application trees; confirm identical folder vocabulary and naming.
2. Structural guard — **implemented** as `scripts/structure-guard.sh` (run via
   `make -C backend structure-guard [SVC=<service>]`). It scans only `Application/` and fails on: a
   `Handlers/` or `DTOs/` type-bucket, a generic `UseCases/` wrapper, a missing `Commands/`/`Queries/`
   level (a `*CommandHandler.cs`/`*QueryHandler.cs` whose grandparent folder isn't `Commands`/`Queries`),
   or an un-mandated `*Executor` forwarding type. **It does NOT flag mandated patterns** — it keys on
   type-bucket dirs, handler placement, and the `Executor` suffix (never an ADR-0004 pattern name), so
   `*Proxy`/`*Facade`/`State`/`Strategy` are untouched; a genuinely mandated executor is exempted via the
   script's `EXECUTOR_ALLOWLIST` (empty — the matrix names none).
3. **Build wiring (converged-only).** `make build` runs the guard for a service **only when it is listed
   in the Makefile's `CONVERGED_SERVICES`**. A service still mid-migration is skipped (its build isn't
   blocked); add it to `CONVERGED_SERVICES` the moment its slice-collapse merges — that one edit flips its
   build from skipped to enforcing. `mission-design-service` is converged today; add `identity-access-service`
   and `session-operations-service` as Phase 2 lands each.
4. Update `backend/AGENTS.md` + the `cqrs-mediatr-aspnetcore` skill with the finalized convention.

**Exit criteria:** three services structurally identical; all three in `CONVERGED_SERVICES` so every
`build` enforces the guard (mandated patterns allowlisted); docs updated. **Est:** ~0.5 day.

---

## Separate epic (NOT part of this refactor) — build `scoring-monitoring-service`

`scoring-monitoring-service` is an **empty `.gitkeep` scaffold** (0 `.cs` files). The earlier draft's
claim that "logic lives in Api/Infrastructure" is **false** — this is greenfield, not a refactor.

- Treat as its own feature epic, gated on a **scoring PRD / domain model** (`Scores`, `Metrics`,
  `Alerts`). Build vertical slices per ADR-0011 from the start; pull domain rules into `Domain`; add unit
  + integration tests (the service has none); wire controllers to `ISender`.
- Mandated patterns for this context per the matrix: **Strategy** for score calculation
  (`HU-37A/37B/39B`).
- **Messaging:** scoring **consumes** `AnswerRegistered` over RabbitMQ (`HU-37A`), published by
  session/trivia after transactional success (`HU-34A`). Follow the messaging contract below.
- Phase 3's structural guard applies to this service once code lands — but its construction does **not**
  block the refactor's convergence.

---

## Messaging (RabbitMQ) contract — for session/scoring event flows

RabbitMQ is **not yet in the code** (only a compose container). When the matrix-named flows are built
(`HU-21B/33B/34A/37A/39B`), they must preserve:

- **Publish-after-commit.** Emit the integration event only after the transaction succeeds (transactional
  outbox or post-commit dispatch). No publish inside an uncommitted unit of work.
- **CQRS handlers never talk to RabbitMQ directly.** A handler raises a domain/application event or calls
  an application abstraction (e.g. `IIntegrationEventPublisher`) implemented in `Infrastructure`. In
  session-operations this is the mandated **Facade** responsibility ("session orchestration and event
  publication" — `CONTEXT.md`).
- **Durable event contracts** (versioned payload shapes), **at-least-once** delivery, **idempotent
  consumers** (dedupe by event id), **manual ack**, and a **DLQ + retry** policy.
- **Integration tests** for publish-after-commit and idempotent re-delivery.
- RabbitMQ stays **off the critical path** (per the patterns matrix): the main game flow does not depend
  on it. Canonical flow: `HU-34A` publishes `AnswerRegistered` → consumed by scoring (`HU-37A`) + audit.

---

## Branch / PR strategy

- Phase 0 is one docs-only PR (ADR-0011 + structure.md), merged first.
- One branch per service stream (`refactor/app-layer-<service>`) off `develop`.
- Within a stream: Phase-1/2 slice-collapse is **one atomic move+namespace-fix commit per area** (the
  ripple spans the `Api` layer, so a half-moved feature won't build); de-ceremony is **one commit per
  item**, sequenced on green builds.
- The service streams touch disjoint files and merge independently. Shared-file moments to sequence:
  Phase 3's guard/docs is shared and serial.

## Verification gates (every PR)

1. `make -C backend build SVC=<service>` green.
2. `make -C backend test SVC=<service>` green (unit + integration; integration tests are the
   behavior-preservation net for de-ceremony).
3. `make -C backend gate SVC=<service>` (ADR-0005 coverage) passes.
4. Controllers compile untouched (they use `ISender`) — confirms the move didn't leak into the API layer.
5. **Manual patterns checklist (code review, not automated).** Confirm no mandated pattern was removed:
   each matrix-named `Proxy`/`Facade`/`State`/`Strategy`/`Template Method`/`CoR` for the touched HUs still
   exists and is genuinely realized (a Facade still coordinates ≥2 collaborators, etc.). There is **no**
   automated "patterns gate" — `structure-guard.sh` only enforces *layout* (type-buckets, slice level,
   un-mandated `*Executor` absence); it deliberately does **not** assert a pattern is realized, since that
   needs human judgment. Reviewer signs this off in the PR.

> Never call `dotnet`/`docker` directly (`AGENTS.md`). If a build pre-flight fails on foreign-owned
> `bin/obj`, run `make -C backend clean-artifacts SVC=<service>`.

## Safety / rollback

- Slice-collapse is pure moves → trivially revertible per PR.
- De-ceremony behavior risk is bounded by integration tests; if a slice has thin coverage, add a
  characterization test **before** touching it.
- Never mix a mechanical move and a semantic change in one PR — keep them separable so a regression
  bisects cleanly.
- Before deleting any `*Proxy`/`*Facade`/`*Executor`, confirm it is **not** named in the patterns matrix
  or the phase gate. When in doubt, keep it and refactor it into a genuine structural pattern.

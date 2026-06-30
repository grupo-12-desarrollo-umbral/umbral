# 0011 — Application layer organizes by vertical slice (use-case folders)

## Status

Accepted. Supersedes the **folder-layout** guidance of the `structure.md` baseline
(the `Handlers/` + `DTOs/` type-buckets and the "request and handler files are
intentionally separated" note). Does **not** alter
[ADR-0004](0004-required-domain-patterns.md): the mandated design patterns remain
mandatory.

## Context

- The stack (`structure.md`) is Clean Architecture + CQRS with MediatR. The
  established best practice for that stack is **Vertical Slice** organization:
  everything for one use case lives in one folder.
- The four services' Application layers drifted from the baseline and from each
  other: most use `Handlers/` and `DTOs/` type-buckets, and several wrap the
  MediatR handler in a hand-rolled `Proxy → Service → Executor` forwarding chain
  (a mediator inside the mediator).
- The previous `structure.md` baseline documented `Handlers/` + `DTOs/` buckets and
  stated request/handler files are "intentionally separated." That is the layout
  this ADR changes.
- ADR-0004 mandates a fixed set of GoF patterns (`Proxy`, `Facade`, `State`,
  `Strategy`, `Template Method`, `Chain of Responsibility`, `Composite`). Those are
  **design** decisions, orthogonal to folder layout, and the phase gate fails if a
  named pattern is not realized. They must be preserved.

## Decision

1. **Organize by vertical slice.** One folder per use case under
   `Application/<Area>/Commands/<UseCase>/` or `Queries/<UseCase>/`, containing the
   request, its handler, its validator, and (for queries) the response DTO it owns.
   The `Commands/`/`Queries/` split is **mandatory** and sits between `<Area>` and the
   use-case folder: do **not** flatten an area into a single bag of use-case folders
   (e.g. `Application/<Area>/<UseCase>/`) and do **not** introduce a generic
   `UseCases/` wrapper segment. The path is always `<Area>/{Commands|Queries}/<UseCase>/`,
   and handler/request type names keep the `Command`/`Query` suffix
   (`<UseCase>CommandHandler`, `<UseCase>QueryHandler`).
2. **No `Handlers/`, `DTOs/`, or `Facades/` type-buckets.** Helpers shared by ≥2 slices
   and the area's mandated-pattern implementations live in `Application/<Area>/Common/`,
   grouped by **concern** (e.g. `Common/Authoring/`), never by type; cross-cutting
   concerns (Behaviours, Interfaces, Exceptions, Security, Models) in `Application/Common/`.
   A mandated `Facade` is kept, but lives **co-located in the slice it orchestrates**
   (single consumer) or in `<Area>/Common/` (shared by ≥2 slices) — not in a `Facades/`
   bucket. `EventHandlers/` and `StateTransitions/` are **not** type-buckets in this
   sense: they are the mandated event-dispatch and `State`-machine structural units and
   are preserved.
3. **ADR-0004 stands unchanged.** Mandated patterns (`Proxy` access-guards, `Facade`
   orchestration / event publication, `State` lifecycle, `Strategy`, `Template
   Method`, `Chain of Responsibility`, `Composite`) remain mandatory deliverables,
   realized inside the relevant slice or the area `Common/`. A pattern instance may
   be removed **only when both** hold: (a) it is not named for that use case by
   `docs/required_patterns_matrix.md` or a phase gate, **and** (b) it
   is pure forwarding ceremony that adds no behavior (e.g. an `IService`/`IExecutor`
   indirection that only relays a call). **Where each mandated pattern physically lives across
   all layers** (Domain/Application/Api) — including the Domain-layer patterns this ADR treats as
   orthogonal to layout — is defined in
   [ADR-0012](0012-design-pattern-placement-convention.md).
4. **Authorization split.** Coarse role/policy gates are declarative via
   `[Authorize]` + `AuthorizationBehaviour`. Resource-specific access decisions
   (e.g. "can this actor assign this operator to this session?") remain a
   `Proxy`/domain-policy, consistent with ADR-0004 and the service `CONTEXT.md`
   `Proxy` requirement.
5. **Events.** Domain events stay in `Domain/Events`; application events and their
   handlers stay in `Application/<Area>/Events|EventHandlers` only when a use case
   needs post-completion fan-out. RabbitMQ integration events follow the messaging
   contract in `plans/application-layer-cqrs-refactor.md` (publish-after-commit,
   durable contracts, idempotent consumers) — handlers never talk to the broker
   directly.

## Consequences

- **Positive:** uniform, discoverable structure across all four services; the
  MediatR handler is the single orchestration unit per use case; mandated patterns
  and the ADR-0004 gate are preserved.
- **Cost:** namespace follows folder, so moving a handler/DTO renames its namespace
  and ripples into `using` lines across `src`, the `Api` layer, and the unit tests.
  Moves are done as atomic scripted per-area commits, never half-applied.
- `structure.md` baseline tree and its "intentionally separated" note are updated to
  match this ADR. No change to ADR-0004 or the patterns matrix.
- A structural CI guard (`scripts/structure-guard.sh`, `make structure-guard`) fails
  the build if a `Handlers/`, `DTOs/`, or `Facades/` directory reappears under
  `Application/`, if a generic `UseCases/` wrapper directory appears, if the mandatory
  `Commands/`/`Queries/` level is missing — i.e. a `*CommandHandler.cs`/`*QueryHandler.cs`
  whose grandparent folder is not `Commands`/`Queries` (a flattened area or a missing
  use-case folder) — or if an un-mandated forwarding `*Executor` type is present.
  `Executor` is not one of the ADR-0004 patterns, so it marks the un-mandated bottom
  of the `Proxy → Service → Executor` triplet (§3); a genuinely mandated executor can
  be exempted via the script's `EXECUTOR_ALLOWLIST` (empty today — the patterns matrix
  names none). The guard keys on type-bucket **directory names**, handler placement,
  and the `Executor` suffix — never on whether a pattern is realized — so co-located
  `*Proxy.cs`/`*Facade.cs` files and the plan-preserved `EventHandlers/`/
  `StateTransitions/` folders are not flagged; only the `Facades/` type-bucket folder is.

## Canonical implementation diffs

- `mission-design-service` Phase 1 golden service:
  [application-layer-cqrs-mission-design-phase-1.diff](../refactors/application-layer-cqrs-mission-design-phase-1.diff)

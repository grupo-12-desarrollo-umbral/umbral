# Application-layer over-engineering checklist

A keep/cut checklist for the CQRS vertical-slice refactor
(`plans/application-layer-cqrs-refactor.md`). Phase 1 audited mission-design and
applied these calls; run the same lens on every later phase (identity-access,
session-operations, scoring-monitoring) to confirm the same over-engineering
hasn't crept back in. Grounded in `docs/adr/0004-required-domain-patterns.md`
and `docs/trivia_sprint_required_patterns_matrix.md` (mandated patterns) and the
Phase-1 diff (`application-layer-cqrs-mission-design-phase-1.diff`).

## The one test

> Shared code earns its abstraction only when the shared logic is **more than a
> guard clause**. Mandated patterns (ADR-0004 / matrix) are kept regardless.

Everything below is that test applied to a specific shape.

## CUT — un-mandated abstractions (Phase 1 removed all of these)

- [ ] **Marker interface + base validator whose only shared rule is trivial**
      (e.g. an `I*LifecycleCommand` / `I*ReuseCommand` marker + abstract base
      validator asserting only `Id > 0`). Inline the rule into each concrete
      validator; delete the folder.
      *Phase 1 cut `Trivias/Common/Lifecycle/` and `Trivias/Common/Reuse/`.*
- [ ] **Dead interfaces / speculative scaffolding** — 0 implementations, 0
      consumers, no DI registration. Delete; don't keep "for later".
      *Phase 1 cut `Common/Interfaces/INotifier.cs` + `IWebhookDispatcher.cs`.*
- [ ] **`ICommand` / `IQuery` / `ICommandHandler` markers** — do NOT add them.
      Use `IRequest` / `IRequestHandler` directly. A marker split is justified
      only the day a behaviour must discriminate command-vs-query (classic
      trigger: a write-only `TransactionBehaviour`) — not today.
- [ ] **Handler base-class inheritance** (e.g. `MissionCommandHandlerBase`) —
      not a mandated pattern. Replace with injected mapper/guard or direct repo
      calls. No inheritance between handlers.
- [ ] **Un-mandated forwarding triplets** — `I*Service` / `I*Executor`
      pass-throughs under a Proxy/Facade that relay one call to one collaborator
      and add no behavior. Inline into the handler. (Keep the Proxy/Facade
      itself if the matrix names it for that HU.)
- [ ] **Type-bucket folders** — `Handlers/`, `DTOs/`, `Facades/`. Co-locate
      handler + owned DTO in the use-case slice; a mandated Facade lives in its
      slice (single consumer) or `<Area>/Common/` (shared by ≥2). Enforced by
      `scripts/structure-guard.sh`.
- [ ] **Stale `.gitkeep`** in folders that now hold real files.

## KEEP — do NOT "simplify" these in later phases

- [ ] **Base validator holding real shared validation** reused by ≥2 slices —
      more than a guard clause.
      *Phase 1 kept `Trivias/Common/Authoring/` (75 + 86-line nested
      question/option-tree validator, shared by Create + Update).*
- [ ] **MediatR + the 5 pipeline behaviours** (Validation, Authorization,
      Performance, Logging, UnhandledException) — this is what MediatR buys; a
      replacement is more code.
- [ ] **Real CQRS read/write split** — commands use write repositories, queries
      use `*ReadModelRepository`. This is the part most "CQRS" code fakes.
- [ ] **Mandated Proxy / Facade / State / Strategy / Template Method / CoR**
      named in the matrix for the touched HU. A genuine Facade coordinates ≥2
      collaborators behind one interface; keep it even when its handler is a
      thin delegate.
- [ ] **Single-consumer `*Guard` / helper in `<Area>/Common/` that holds real
      logic** — a cross-aggregate invariant, a formatted domain exception, or is
      paired with a sibling used by ≥2 slices, and lives in `<Area>/Common/`
      deliberately to keep one area's logic out of another's handler. Inline
      only the pure-forwarding, no-logic ones.
      *Phase 1 kept `Missions/Common/ActiveMissionTriviaReferenceGuard.cs`
      (single consumer, but a real invariant paired with the 3-consumer
      `TriviaQuizSelectionGuard`).*

## Before cutting anything

Check `docs/trivia_sprint_required_patterns_matrix.md` and the use case's phase
scope. If the pattern is named there, **keep it** (refactor to a genuine
structure, don't rename). When in doubt, keep and refactor — never delete a
mandated pattern.

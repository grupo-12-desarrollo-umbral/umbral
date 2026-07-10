# 0014 — No cross-layer reflection: an inner layer never names an outer-layer type

## Status

Accepted — 2026-07-09. Implemented and enforced by `scripts/layer-guard.sh`
(`make layer-guard`), an unconditional `build` prerequisite. Does **not** alter
[ADR-0011](0011-application-layer-vertical-slice-organization.md) or
[ADR-0012](0012-design-pattern-placement-convention.md); it closes a hole those
conventions and the compiler cannot see.

## Context

- The stack (`structure.md`) is Clean Architecture: the dependency rule points
  **inward** — Domain ← Application ← Infrastructure, with Api (Presentation) at the
  outer edge. An inner layer must never depend on an outer layer. The compiler and the
  `.csproj` `ProjectReference` graph enforce the honest version of this, and a
  type-reference architecture test (NetArchTest) would too.
- **All of those are blind to a dependency expressed as a string and resolved by
  reflection.** `session-ops`'s `SignalRSessionTimerBroadcaster` lived in
  `Infrastructure/Realtime/` yet drove the Api-layer `SessionsHub`. It reached it not by
  a project reference — there was none — but by scanning `AppDomain.CurrentDomain
  .GetAssemblies()` for the hardcoded type name `"umbral_backend.Api.Hubs.SessionsHub"`
  and building `IHubContext<>` over it with `MakeGenericType`. That launders an
  Infrastructure→Api dependency past every compile-time check: the `.csproj` has no
  reference, so the structure guard and any type-graph guard see nothing, and the build
  is green while the dependency rule is inverted at runtime.
- The violation is visible in exactly one place — the **source text**. Nothing in the
  compiled graph records it, so no compiler-driven or reflection-over-assemblies guard
  can catch it; only a guard that reads source can.

## Decision

1. **An inner layer (Domain/Application/Infrastructure) must never name or reflectively
   resolve an Api-layer type.** No `Type.GetType("...Api...")`, no
   `AppDomain.CurrentDomain.GetAssemblies()` scan for a hub, no
   `typeof(IHubContext<>).MakeGenericType(...)`, no `IServiceProvider` service-location
   to reach a hub. The dependency is either honest (a compiler-visible reference the
   dependency rule forbids) or it does not exist.
2. **A SignalR broadcaster is a Presentation/transport adapter and belongs in the Api
   layer.** The Application layer owns the **port** (`INotifier` / `I*Broadcaster`); only
   Api sees the hub type; registration happens in the Api composition root. HU-34 moved
   `SignalRSessionTimerBroadcaster` and the new `SignalRTeamAnsweredBroadcaster` into
   `Api/Hubs/` — joining three sibling broadcasters already there — where
   `IHubContext<SessionsHub>` is supplied by plain constructor injection. Inner layers
   depend on the Application port, never the outer type.
   `AuthoritativeSessionTimerWorker` stays in `Infrastructure/Realtime/`: it depends only
   on the Application port, not the hub, so it never needed to reach outward.

## Considered Options

- **Impl-in-Infrastructure (rejected).** Keep the broadcaster in Infrastructure and let
  it reach the hub some cleaner way. There is no clean way: the hub is an Api type, so any
  Infrastructure→hub path either relocates the hub (churning the project graph) or
  launders the dependency through reflection again. Moving the *adapter* to the layer that
  owns the type is the only fix that keeps the graph honest.
- **"Just add the project reference" (rejected — worse).** Add an
  `Infrastructure → Api` `ProjectReference` so `IHubContext<SessionsHub>` injects
  honestly. This makes the dependency compiler-visible, but only by **inverting the
  dependency rule outright**: Infrastructure would depend on Presentation, the exact edge
  Clean Architecture forbids. It trades a hidden violation for a declared one — strictly
  worse than moving the adapter to Api, where no rule is broken at all.

## Consequences

- **A source-text CI guard makes the rule enforceable.** `scripts/layer-guard.sh`
  (`make layer-guard`) greps `services/*/src/{Domain,Application,Infrastructure}` for the
  laundering tells (`MakeGenericType`, `.GetType("...")`, `AppDomain`, and a string
  literal naming a `.Api.` namespace) and fails the build on any hit. Api itself is not
  scanned — it owns the hubs and legitimately injects `IHubContext`. Full-line comments
  are stripped before matching, so doc-comment prose mentioning a token is not flagged.
- **The gate is unconditional, not opt-in.** `make layer-guard` is a hard prerequisite of
  `make build` (like `structure-guard`), so the violation cannot re-enter through a green
  build. Evidence: across all service trees, `develop` alone reports 3 violations and
  exit 1 (all in the one offending file); `develop` + HU-34 reports `layer-guard: OK`,
  exit 0, 12 trees checked.
- **The fix is never a cleverer lookup.** A genuine future exception is exempted by adding
  its backend-relative file path to the script's `ALLOWLIST` **with a reason** (empty by
  design — the laundering pattern has no legitimate use in an inner layer), mirroring
  `structure-guard.sh`'s `EXECUTOR_ALLOWLIST`.
- **No `dotnet` dependency.** The guard is pure text, so it runs on any machine and in CI
  without a build, exactly like `structure-guard.sh`.

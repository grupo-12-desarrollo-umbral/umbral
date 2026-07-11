# Reflection layer-guard review (2026-07-10)

## Question

What does `backend/scripts/layer-guard.sh` actually enforce today, does it currently pass, and which reflection patterns should be treated as safe vs suspicious in this repo?

## Sources checked

- `backend/scripts/layer-guard.sh`
- `backend/Makefile`
- `backend/docs/adr/0014-no-cross-layer-reflection.md`
- `backend/services/identity-access-service/src/Application/DependencyInjection.cs`
- `backend/services/identity-access-service/src/Application/Common/Behaviours/AuthorizationBehaviour.cs`
- `backend/services/identity-access-service/src/Infrastructure/Persistence/ApplicationDbContext.cs`
- `backend/services/session-operations-service/src/Application/DependencyInjection.cs`
- `backend/services/session-operations-service/src/Application/Common/Behaviours/AuthorizationBehaviour.cs`
- `backend/services/session-operations-service/src/Infrastructure/Persistence/ApplicationDbContext.cs`
- `backend/services/mission-design-service/src/Application/DependencyInjection.cs`
- `backend/services/mission-design-service/src/Application/Common/Behaviours/AuthorizationBehaviour.cs`
- `backend/services/mission-design-service/src/Infrastructure/Persistence/ApplicationDbContext.cs`

## Script result

Command run from `backend/`:

```text
make layer-guard
```

Observed output:

```text
layer-guard: OK — no cross-layer reflection in Domain Application Infrastructure (12 tree(s) checked).
```

## What the guard actually enforces

`layer-guard.sh` is a targeted source-text guard, not a general ban on all reflection.

It scans only these inner-layer trees:

- `services/*/src/Domain`
- `services/*/src/Application`
- `services/*/src/Infrastructure`

It intentionally does not scan `Api`.

It fails on these pattern families in `*.cs` files:

- `MakeGenericType`
- `.GetType("...")`
- `AppDomain`
- string literals containing `.Api.`

The guard's purpose is the one described in `backend/docs/adr/0014-no-cross-layer-reflection.md`: stop inner layers from laundering an `inner -> Api` dependency through reflection or type-name strings.

## Safe reflection in this repo

These patterns appear acceptable under the current architecture when they stay within the current assembly or honest project-reference graph and do not reach `Api` types indirectly.

- `Assembly.GetExecutingAssembly()` for local assembly scanning used by framework registration
- `AddValidatorsFromAssembly(Assembly.GetExecutingAssembly())`
- `RegisterServicesFromAssembly(Assembly.GetExecutingAssembly())`
- `ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly())`
- `request.GetType().GetCustomAttributes<AuthorizeAttribute>(...)` for request metadata inspection
- attribute inspection that does not build or resolve outer-layer type names
- reflection over types already reachable through valid compile-time references inside Domain/Application/Infrastructure

### Current safe-looking call sites checked manually

- `backend/services/identity-access-service/src/Application/DependencyInjection.cs`
- `backend/services/session-operations-service/src/Application/DependencyInjection.cs`
- `backend/services/mission-design-service/src/Application/DependencyInjection.cs`
- `backend/services/identity-access-service/src/Infrastructure/Persistence/ApplicationDbContext.cs`
- `backend/services/session-operations-service/src/Infrastructure/Persistence/ApplicationDbContext.cs`
- `backend/services/mission-design-service/src/Infrastructure/Persistence/ApplicationDbContext.cs`
- `backend/services/identity-access-service/src/Application/Common/Behaviours/AuthorizationBehaviour.cs`
- `backend/services/session-operations-service/src/Application/Common/Behaviours/AuthorizationBehaviour.cs`
- `backend/services/mission-design-service/src/Application/Common/Behaviours/AuthorizationBehaviour.cs`

These usages rely on `System.Reflection`, but none of them currently appears to be naming or resolving `Api` types from an inner layer.

## Suspicious reflection in this repo

Treat these as likely violations or at least high-scrutiny patterns when they appear in `Domain`, `Application`, or `Infrastructure`.

- `MakeGenericType(...)`
- `Type.GetType(...)`
- `Assembly.GetType(...)`
- `AppDomain.CurrentDomain.GetAssemblies()`
- `Assembly.Load(...)`, `LoadFrom(...)`, `LoadFile(...)`
- `Activator.CreateInstance(...)` when the target type is runtime-resolved
- string construction that can resolve or name `Api` types
- `typeof(SomeApiType)` or extracting `.FullName` / `.AssemblyQualifiedName` for an `Api` type from an inner layer
- any inner-layer code trying to reach SignalR hubs, controllers, transport adapters, or other Presentation-layer types indirectly

### Repo-specific reject examples

- scanning assemblies to find `SessionsHub`
- constructing `IHubContext<THub>` with `MakeGenericType`
- resolving `umbral_backend.Api.*` by string name from `Infrastructure`
- keeping a broadcaster in `Infrastructure` that reflectively reaches `Api/Hubs/*`

## Guard gaps and non-goals

The script is useful, but it is not a complete detector for every possible reflective outer-layer reach.

Obvious gaps:

- it matches only four narrow shapes
- it would miss variable-based `Type.GetType(variable)` or `Assembly.GetType(variable)` cases
- it would miss type-name construction via interpolation, concatenation, config, env vars, or database values
- it would miss `typeof(SomeApiType).FullName` / `.AssemblyQualifiedName`-style indirection
- it scans only `*.cs`

So the correct claim is not "the repo has no dangerous reflection." The correct claim is narrower:

- the current targeted guard passes
- the current manual inspection found no present `inner layer -> Api` reflection laundering in the checked call sites
- the guard still has known blind spots outside its intended pattern set

## Bottom line

- `backend/scripts/layer-guard.sh` currently passes
- it checks only inner layers, not `Api`
- it is a targeted guard for cross-layer reflection laundering, not a blanket reflection ban
- current reflection usages reviewed in inner layers look framework-oriented and acceptable
- future inner-layer reflection should be judged by this rule of thumb:

1. Does it stay inside the current layer's honest dependency graph?
2. Would it still make sense if `Api` did not exist?
3. Is it framework registration/metadata inspection, or is it an indirect reach into Presentation?

If the answer trends toward indirect Presentation reach, treat it as suspicious even if the current script would not fail.

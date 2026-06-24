# 0012 — Design-pattern placement convention (all layers)

## Status

Accepted. **Complements** [ADR-0004](0004-required-domain-patterns.md) (which patterns are
mandatory and why) and [ADR-0011](0011-application-layer-vertical-slice-organization.md)
(the Application-layer vertical-slice layout). This ADR adds the missing third axis: **where
each mandated pattern physically lives** across **all** layers — Domain, Application, and Api —
and the *genuine-vs-ceremony* test that decides whether an instance is a real pattern or
forwarding to collapse. It does **not** add or remove any mandated pattern (ADR-0004 stands)
and does **not** change the Application slice tree (ADR-0011 stands).

## Context

Three documents already speak about the mandated patterns, but none aligns them to the folder
structure across layers:

- `docs/ddd_solution_model.md` §8 maps each pattern to a **responsibility** per bounded context
  (conceptual — "`State` for `LiveSession` lifecycle"), not to a folder.
- `docs/trivia_sprint_required_patterns_matrix.md` maps **HU → pattern** (which is required where),
  not where the files live.
- ADR-0011 + `plans/application-layer-cqrs-refactor.md` define the **folder layout**, but are
  **scoped to the Application layer**, and within it only **`Facade`** received a concrete
  placement treatment (table, verdicts, genuine-vs-ceremony test, worked example). ADR-0011 §3
  deliberately calls Domain patterns "orthogonal to folder layout" and leaves them ungoverned.

Consequences of that gap, verified against the code:

- **Domain-layer patterns are ungoverned.** `Composite` (`mission-design Domain/Entities/`),
  `State` (`session-operations Domain/Services/SessionStates/`), and `Strategy`
  (`session-operations Domain/Services/`) each sit somewhere sensible, but no convention says so —
  the next service has no reference for where to put them.
- **`Proxy` is realized two different ways, in two different layers.** identity-access has
  `Api/Services/AuthenticatedUserLoginProxy` + `UserManagementProxy` — each a real guard
  (`EnsureTrustedIdentity` gateway-trust check) **paired with a forwarding `*Handler`** — *and*
  Application-slice `*AuthorizationProxy.cs` guards (JoinTokens, Users, Teams, Sessions).
  session-operations realizes Proxy only as Application-slice `*AuthorizationProxy.cs`. This is the
  "authorization implemented two different ways" the refactor plan already flagged, still unresolved.
  Because `scripts/structure-guard.sh` scans **only `Application/`**, the Api-layer Proxy→Handler
  forwarding pair is invisible to enforcement.
- **`Template Method` is already aligned and is the reference.** mission-design realizes it across
  two layers cleanly: Domain templates live as private sealed classes inside the aggregate
  (`TriviaQuiz`), and the Application side puts abstract base validators in
  `Trivias/Common/{Authoring,Lifecycle,Reuse}/` with concrete overrides in the use-case slices.
  This is the cross-layer analog of the Facade treatment and the model for the rest.

## Decision

### Canonical home per pattern

Each mandated pattern has **one canonical home** keyed to its layer and concern. "Slice" means the
use-case folder `Application/<Area>/{Commands|Queries}/<UseCase>/`; "`<Area>/Common/`" means shared
by ≥2 slices, grouped by **concern** (ADR-0011 §2).

| Pattern | Layer | Canonical home | Genuine vs. ceremony (the realization test) |
| --- | --- | --- | --- |
| **Composite** | Domain | `Domain/Entities/` — abstract node base + concrete node types | Recursive whole-part tree where composites and leaves share one abstract type and a parent enumerates children polymorphically. *Ceremony:* a flat aggregate-with-a-`List<>` that never recurses and shares no base type — that is an aggregate collection, not `Composite`. |
| **Template Method** | Domain **and** Application | Domain: private sealed template classes **inside the aggregate** (`Domain/Entities/<Aggregate>.cs`). Application: abstract base validator in `<Area>/Common/<Concern>/`, concrete overrides in each slice | An invariant skeleton method with ≥1 abstract/virtual extension point overridden by **≥2** concretes. *Ceremony:* a base class with no fixed skeleton — just shared helpers — is plain inheritance; prefer composition or collapse. |
| **Facade** | Application | Slice it orchestrates (single consumer) or `<Area>/Common/` (shared ≥2). **Never** a `Facades/` bucket | Coordinates **≥2 collaborators** behind one interface, owning a transaction / orchestration / event-publication boundary. *Ceremony:* a 1-to-1 pass-through relaying one call to one collaborator — the un-mandated `IService`/`IExecutor` layer; collapse it. (See ADR-0011 §2 and the plan's Facade subsection.) |
| **State** | Domain (authority) + thin Application trigger | Domain: `Domain/Services/<Aggregate>States/` — state interface + abstract base + **one class per state** + a factory. Application trigger lives in `<Area>/StateTransitions/` (see CoR) | Behavior differs by **state object**, transitions delegated to those objects. *Ceremony:* an enum plus `switch` statements scattered across handlers is not `State` — it is the branching `State` exists to remove. |
| **Chain of Responsibility** | Application | `<Area>/StateTransitions/` (transition guards) and `<Area>/Common/<Pipeline>/Validators/` (submission/answer pipelines): a chain builder + abstract link + ordered concrete links in `Validators/` | Ordered links each decide handle-or-pass-on via `SetNext`/`Next`; links are composable and reorderable without editing each other. *Ceremony:* one validator with sequential `if` blocks is not a chain. |
| **Proxy** | Application (preferred) | Co-located in the guarded slice as `<UseCase>AuthorizationProxy.cs`, or `<Area>/Common/` if shared. **Api-layer Proxy only** for a genuine edge/transport concern with no application use case — and never with a forwarding `*Handler`/`*EntryPoint` pair beneath it | Wraps the real subject and adds an **access decision** (load actor, `IsActive`, `AccessPolicy.Evaluate`, resource-specific "may this actor act on this resource") before delegating. *Ceremony:* a `*Proxy` that only relays to a `*Handler`/`*Service` with no guard is the forwarding chain — collapse it (coarse role/policy gates go to `[Authorize]` + `AuthorizationBehaviour`, ADR-0011 §4). |
| **Strategy** | Domain | `Domain/Services/<Concern>Strategies/` (or `Domain/Services/`) — strategy interface + concrete strategies + a selector/factory | An interface with **≥2 interchangeable** implementations chosen at runtime by a key/policy, with **no** behavior branching left in the handler. *Ceremony:* a single implementation behind an interface with no selection point is premature — but **keep** it if matrix-named (its siblings are coming) and add the selector when the second arrives. |

### Proxy unification (the one instance that needs a move)

A mandated `Proxy` that guards an **application use case** is realized in that use case's
Application slice (`<UseCase>AuthorizationProxy.cs`) or `<Area>/Common/`. The identity-access
`Api/Services/AuthenticatedUserLoginProxy` and `UserManagementProxy` each guard an application use
case (`AuthenticateUser`, user management) and are each paired with a forwarding
`*Handler`/`*EntryPoint`. The aligned end-state: keep the **guard**, move it into the Application
slice (or `AuthorizationBehaviour` if the check is coarse), and **delete the forwarding
`*Handler`/`*EntryPoint` pair** — it is the Api-layer twin of the `IService`/`IExecutor` ceremony
ADR-0011 §3 removes. A `Proxy` stays in `Api/` **only** when it guards a genuine
edge/transport concern that has no application use case behind it, and even then with no forwarding
partner.

### Enforcement

- **Application-layer placement is machine-enforced** by `scripts/structure-guard.sh`
  (`make structure-guard`): no `Handlers/`/`DTOs/`/`Facades/` buckets, mandatory `Commands/Queries`
  level, no un-mandated `*Executor`. This already covers Facade buckets and the Application side of
  Proxy/CoR/Template-Method.
- **Domain- and Api-layer placement is NOT machine-checked** — the guard scans `Application/` only.
  Composite, State, Strategy, the Domain side of Template Method, and any Api-layer Proxy are
  governed by the **manual patterns checklist** in the refactor plan's verification gates (a
  reviewer confirms each touched pattern lives in its canonical home and is genuinely realized).
  A future guard extension may scan `Domain/Services/` and `Api/Services/` for the Proxy→Handler
  forwarding shape; until then it is a code-review responsibility, called out here so it is not
  mistaken for "enforced because the build is green."

## Consequences

- **Positive:** one place answers "where does pattern X go, in any service, in any layer," with a
  per-pattern realization test that distinguishes a real pattern from forwarding to collapse. New
  services (notably greenfield `scoring-monitoring-service`, whose `Strategy` score policies land in
  `Domain/Services/<Concern>Strategies/`) copy a documented convention instead of guessing.
- **One concrete code move falls out:** the identity-access `Api/Services` Proxy→Handler pairs
  unify with the Application-slice `*AuthorizationProxy` convention (guard moves in, forwarding
  pair deletes). Treat it as its own behavior-preserving change, gated by the auth integration tests
  (never mixed with a mechanical move), per the plan's safety rules.
- **Cost / limit:** Domain and Api placement remain manual-review-enforced until/unless the guard is
  extended. This ADR records the convention and the gap explicitly so the gap is visible.
- No change to ADR-0004, ADR-0011, the patterns matrix, or `ddd_solution_model.md`. mission-design's
  `Template Method` (Domain + `Common/<Concern>/`) and `Composite` (`Domain/Entities/`) are the
  reference realizations; session-operations `State` (`Domain/Services/SessionStates/`) and CoR
  (`Application/Sessions/StateTransitions/`) are the reference for those two.

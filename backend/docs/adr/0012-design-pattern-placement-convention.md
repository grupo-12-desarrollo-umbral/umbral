# 0012 — Design-pattern placement convention (all layers)

## Status

Accepted. **Complements** [ADR-0004](0004-required-domain-patterns.md) (which patterns are
mandatory and why) and [ADR-0011](0011-application-layer-vertical-slice-organization.md)
(the Application-layer vertical-slice layout). This ADR adds the missing third axis: **where
each mandated pattern physically lives** across **all** layers — Domain, Application, and Api —
and the *genuine-vs-ceremony* test that decides whether an instance is a real pattern or
forwarding to collapse. It does **not** add or remove any mandated pattern (ADR-0004 stands);
the Facade/Proxy **placement** here is amended in lockstep with
[ADR-0011](0011-application-layer-vertical-slice-organization.md) §1/§2 — a single-consumer
Facade is realized by its handler (no standalone class), response DTOs move to the central
`Application/Dtos/` root — with no change to which patterns are mandated.

## Context

Three documents already speak about the mandated patterns, but none aligns them to the folder
structure across layers:

- `docs/ddd_solution_model.md` §8 maps each pattern to a **responsibility** per bounded context
  (conceptual — "`State` for `LiveSession` lifecycle"), not to a folder.
- `docs/required_patterns_matrix.md` maps **HU → pattern** (which is required where),
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
| **Composite** | Domain | `Domain/Entities/` — abstract node base + concrete node types | Recursive whole-part tree where composites and leaves share one abstract type and a parent enumerates children polymorphically. *Ceremony:* a flat aggregate-with-a-`List<>` that never recurses and shares no base type — that is an aggregate collection, not `Composite`. *Scope note (mission-design):* `MissionNode` = `Stage`/`Substage`/`Clue` only; `Target` is TreasureHunt **play-content** of a `Substage` (parallel to a Trivia substage's `TriviaQuizId`), deliberately **not** a node — it is validated directly, not traversed. Don't re-flag this as a Composite gap (see `plans/pattern-placement-remediation.md` §3). |
| **Template Method** | Domain **and** Application | Domain: private sealed template classes **inside the aggregate** (`Domain/Entities/<Aggregate>.cs`). Application: abstract base validator in `<Area>/Common/<Concern>/`, concrete overrides in each slice | An invariant skeleton method with ≥1 abstract/virtual extension point overridden by **≥2** concretes. *Ceremony:* a base class with no fixed skeleton — just shared helpers — is plain inheritance; prefer composition or collapse. |
| **Facade** | Application | **Single consumer: realized by the MediatR handler itself** — orchestration inlined into `Handle`, no standalone `*Facade.cs`. A standalone class exists **only when shared by ≥2 slices**, and then in `<Area>/Common/`. **Never** a `Facades/` bucket | Coordinates **≥2 collaborators** behind one interface, owning a transaction / orchestration / event-publication boundary. The handler's `IRequestHandler<,>` **is** that one interface, so a handler that orchestrates ≥2 collaborators and owns the transaction/event boundary *realizes* the Facade — inlining it into the handler is realization, not removal (ADR-0011 §3). *Ceremony:* a 1-to-1 pass-through relaying one call to one collaborator — the un-mandated `IService`/`IExecutor` layer; collapse it. (See ADR-0011 §1/§2.) |
| **State** | Domain (authority) + thin Application trigger | Domain: `Domain/Services/<Aggregate>States/` — state interface + abstract base + **one class per state** + a factory. Application trigger lives in `<Area>/StateTransitions/` (see CoR) | Behavior differs by **state object**, transitions delegated to those objects. *Ceremony:* an enum plus `switch` statements scattered across handlers is not `State` — it is the branching `State` exists to remove. |
| **Chain of Responsibility** | Application | `<Area>/StateTransitions/` (transition guards) and `<Area>/Common/<Pipeline>/Validators/` (submission/answer pipelines): a chain builder + abstract link + ordered concrete links in `Validators/` | Ordered links each decide handle-or-pass-on via `SetNext`/`Next`; links are composable and reorderable without editing each other. *Ceremony:* one validator with sequential `if` blocks is not a chain. |
| **Proxy** | Application (preferred) | Co-located in the guarded slice as `<UseCase>AuthorizationProxy.cs`, or `<Area>/Common/Authorization/` — relocate single-consumer proxies there to keep the slice pipeline-pure (ADR-0011 §1), shared ones must live there. Kept as a decorator, **never inlined** into the handler (authorization is cross-cutting). **Api-layer Proxy only** for a genuine edge/transport concern with no application use case — and never with a forwarding `*Handler`/`*EntryPoint` pair beneath it | Wraps the real subject and adds an **access decision** before delegating. Two genuine shapes by what the decision needs: **(a) capability guard** — check needs no resource, so the proxy *is* the subject's interface (`IRequestHandler<,>`), holds the concrete handler as `_inner`, runs `AccessPolicy.EnsureCanAccess`, then delegates (identity-access is the reference); **(b) resource-ownership guard** — check needs the loaded entity (`resource.OwnerId == actor.Id`), so the proxy implements a resolver interface that **loads → authorizes → returns the authorized subject** to its single caller, fusing the load the caller would otherwise repeat (session-operations `SessionAdministrationAuthorizationProxy` is the reference). *Ceremony (both shapes):* a `*Proxy` that relays to a `*Handler`/`*Service` with **no** access decision is the forwarding chain — collapse it; coarse role/policy gates go to `[Authorize]` + `AuthorizationBehaviour` (ADR-0011 §4). |
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

### Two genuine Proxy realizations (capability vs. resource-ownership)

The genuine-Proxy test above ("wrap the subject, decide, delegate") assumes the access
decision needs nothing but the actor. That holds for **capability guards**
(`EnsureCanAccess(actor, OperatorPanel)`) — identity-access realizes these as a
handler-decorator: the `*AuthorizationProxy` is the registered `IRequestHandler<,>`, the
concrete handler is deliberately *not* an `IRequestHandler` (DI registers only the proxy,
so MediatR's scan resolves the guard as the handler), and it delegates to `_inner`.

It does **not** hold for **resource-ownership guards**, where the decision needs the loaded
entity (`liveSession.AssignedOperatorUserId == actor.UserId`). A wrap-and-delegate proxy
there would load the resource for its check and then delegate to a subject that loads the
**same** resource again — a redundant round-trip — or pass the loaded entity inward, which
breaks the transparent same-interface contract that makes it a Proxy at all. The genuine
realization for this case is a **resolver proxy**: it implements a small resolver interface
(`ISessionAdministrationAccessResolver`), loads the resource, applies the ownership decision,
and **returns the authorized subject** to its caller (the Facade/handler), fusing the load
the caller would otherwise repeat. This is a real access proxy, not the forwarding ceremony —
the distinguishing line is unchanged: **an access decision is made before the subject is used.**

The ceremony tell for this shape is structural: a resolver proxy must throw on the
deny path between the load and the return (`ForbiddenAccessException` /
`UnauthorizedAccessException`); a resolver that loads and returns with no deny-throw is
a checked loader that never checks — collapse it into its caller. (session-operations'
proxy throws on both the wrong-role and wrong-owner paths, lines 57–67 — genuine.)

Both shapes live in the same canonical home (the guarded slice, or `<Area>/Common/` if shared
by ≥2 slices). This is a *within-Application* difference in form, distinct from the
Api-vs-Application split in §Context — it is not an unresolved divergence to collapse.

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
- No change to ADR-0004, the patterns matrix, or `ddd_solution_model.md` (patterns stay
  mandated and realized); the Facade/Proxy **placement** is co-amended with ADR-0011 §1/§2.
  mission-design's
  `Template Method` (Domain + `Common/<Concern>/`) and `Composite` (`Domain/Entities/`) are the
  reference realizations; session-operations `State` (`Domain/Services/SessionStates/`) and CoR
  (`Application/Sessions/StateTransitions/`) are the reference for those two.

# ADR-0012 — Pattern realizations by layer (worked examples)

> Companion reference to [`adr/0012-design-pattern-placement-convention.md`](adr/0012-design-pattern-placement-convention.md).
> Shows how each mandated pattern actually looks in the codebase today, in the canonical home
> ADR-0012 assigns it. Snippets are trimmed to the essential shape — see the cited `file:line`
> for the full source.

---

## 1. Composite — Domain (`Domain/Entities/`)

**Reference realization** (ADR-0012 names this *the* model). Abstract node base + concrete node
types sharing it; parent enumerates children polymorphically.

```csharp
// mission-design Domain/Entities/MissionNode.cs:12
public abstract class MissionNode : BaseEntity
{
    public abstract MissionNodeType NodeType { get; }
    protected abstract IEnumerable<MissionNode> ChildNodes { get; }
    public IReadOnlyList<MissionNode> Children => ChildNodes.OrderBy(c => c.SequenceOrder)...;
    public IEnumerable<MissionNode> Descendants() { /* recurses through ChildNodes */ }
}
// Stage.cs:9   → sealed Stage : MissionNode     (ChildNodes => _substages)
// Substage.cs  → sealed Substage : MissionNode  (CanContain => Clue)
// Clue.cs:40   → sealed Clue : MissionNode      (leaf: ChildNodes => [])
```

**Genuine** ✓ — recursive whole-part tree, shared base, `Descendants()` recurses. Not a flat `List<>`.

---

## 2. Template Method — **two layers** (Domain *and* Application)

**Domain side** — private sealed templates *inside* the aggregate:

```csharp
// mission-design Domain/Entities/TriviaQuiz.cs:226
private abstract class TriviaQuizLifecycleTemplate
{
    public void Apply(TriviaQuiz quiz, DateTimeOffset at) {        // fixed skeleton
        EnsureCurrentStateAllowsTransition(quiz.Status);
        EnsureReadiness(quiz); ApplyTransition(quiz, at); RaiseDomainEvent(quiz);
    }
    protected abstract void ApplyTransition(...);                 // extension points
}
// :470 sealed PublishTriviaQuizLifecycleTemplate overrides EnsureReadiness/ApplyTransition
```

**Application side** — abstract base validator in `<Area>/Common/<Concern>/`, overrides in slices:

```csharp
// Application/Trivias/Common/Authoring/TriviaQuizAuthoringCommandValidator.cs:3
public abstract class TriviaQuizAuthoringCommandValidator<TCommand> : AbstractValidator<TCommand> {
    protected TriviaQuizAuthoringCommandValidator() { /* shared rules */ AddOperationSpecificRules(); }
    protected virtual void AddOperationSpecificRules() { }        // extension point
}
// Commands/UpdateTriviaQuiz/UpdateTriviaQuizCommandValidator.cs:5  overrides the hook (adds Id > 0)
// Commands/CreateTriviaQuiz/CreateTriviaQuizCommandValidator.cs:5  inherits the default hook (no extra rules)
```

**Genuine** ✓ — fixed skeleton with extension points. The Domain side has concrete subclasses overriding
the hooks; the Application side has **two concrete subclasses, one overriding the hook** (`Update`) and one
inheriting the default (`Create`). (A single subclass that just inherits would be plain inheritance, not
Template Method — here the overriding subclass is what makes it genuine.)

---

## 3. State — Domain authority + thin Application trigger

**Domain** (`Domain/Services/<Aggregate>States/`): interface + abstract base + one class per state + factory.

```csharp
// session-operations Domain/Services/SessionStates/
internal interface ILiveSessionState { SessionState State { get; } bool CanTransitionTo(...); void Enter(...); }
internal abstract class LiveSessionStateBase : ILiveSessionState { /* virtual defaults */ }
internal sealed class ActiveLiveSessionState : LiveSessionStateBase {
    public override bool CanTransitionTo(SessionState n) => n is Paused or Finished or Cancelled;
    public override void Enter(LiveSession s, ...) { s.EnterActiveSessionState(...); }
}
internal static class LiveSessionStateFactory { internal static ILiveSessionState For(SessionState s) => ... }
```

**Trigger** — polymorphic dispatch, no enum-switch in handlers:

```csharp
// Domain/Entities/LiveSession.cs:235  MoveTo(...) → LiveSessionStateFactory.For(nextState).Enter(this, ...)
```

**Genuine** ✓ — behavior differs by state object. (ADR-0012 reference for State.)

---

## 4. Chain of Responsibility — Application (`<Area>/StateTransitions/`)

```csharp
// session-operations Application/Sessions/StateTransitions/
public abstract class SessionTransitionValidator {           // abstract link
    public SessionTransitionValidator SetNext(SessionTransitionValidator n) { _next = n; return n; }
    public async Task ValidateAsync(ctx, ct) { await CheckAsync(ctx, ct); if (_next != null) await _next.ValidateAsync(ctx, ct); }
    protected abstract Task CheckAsync(...);
}
// Validators/CurrentStateGate.cs       — link 1 (transition allowed?)
// Validators/OperatorAssignmentGate.cs — link 2 (operator required?)
// SessionTransitionChain.cs:8 — builder wires links via SetNext in order
```

**Genuine** ✓ — ordered links, `SetNext`/`Next`, reorderable. (ADR-0012 reference for CoR.)

---

## 5. Facade — Application (slice it orchestrates, or `<Area>/Common/`)

```csharp
// session-operations SessionTeamAssociationFacade — coordinates 3 collaborators:
//   ILiveSessionRepository + ITeamReferenceCatalogClient + ISessionTeamAssociationSyncClient
```

⚠️ **Placement gap ADR-0012 flags:** both `SessionTeamAssociationFacade` and
`TriviaRoundOrchestratorFacade` currently sit in a `Application/Sessions/Facades/` **type-bucket**.
ADR-0012 + the refactor plan say: keep the Facades (genuine — each coordinates ≥2 collaborators), but
**dissolve the bucket**. Both go to `Sessions/Common/` as shared application concerns:
`SessionTeamAssociation` (4 consumers), and `TriviaRoundOrchestrator` — which has **≥2 consumers**, the
`TriviaRoundStartedNotificationHandler` event-handler **and** the Infrastructure
`AuthoritativeSessionTimerWorker` (`Infrastructure/Realtime/AuthoritativeSessionTimerWorker.cs:57`), so it
is not a single-slice co-location. Concrete Phase 2 work.

---

## 6. Proxy — Application (preferred), with an API-edge / transport-edge exception

**Genuine Application Proxy** ✓ — wraps subject, adds an access decision:

```csharp
// identity-access JoinTokens/.../JoinTokenIssuanceAuthorizationProxy.cs:9
public sealed class JoinTokenIssuanceAuthorizationProxy : IIssueJoinTokenService {
    public async Task<...> IssueAsync(cmd, ct) {
        var actor = await _userRepository.GetByExternalIdentityIdAsync(...) ?? throw new NotFoundException();
        _accessPolicy.EnsureCanAccess(actor, ProtectedCapability.OperatorPanel);   // the decision
        return await _inner.IssueAsync(cmd, ct);
    }
}
```

⚠️ **Two ceremony issues ADR-0012 surfaces, both Phase 2:**

- The `_inner` here is an `IIssueJoinTokenExecutor` — that **Executor pass-through is the un-mandated
  forwarding** to collapse into the handler (the Proxy guard stays).
- `identity-access Api/Services/AuthenticatedUserLoginProxy` (real `EnsureTrustedIdentity` guard) is
  paired with a forwarding `AuthenticatedUserLoginHandler → _sender.Send(...)`. ADR-0012 §"Proxy
  unification": **keep the guard, move it into the Application slice, delete the forwarding
  `*Handler`/`*EntryPoint` pair.** Same for `UserManagementProxy`.

---

## 7. Strategy — Domain (`Domain/Services/<Concern>Strategies/`)

```csharp
// session-operations Domain/Services/IQuestionActivationStrategy.cs
public interface IQuestionActivationStrategy { int? Next(LiveSession session); }
// SequentialQuestionActivationStrategy.cs:5 — one concrete impl
```

⚠️ **Only one implementation exists today**, and it sits directly in `Domain/Services/` (not a
`*Strategies/` folder). ADR-0012's test: a single impl behind an interface is *premature ceremony*
**unless matrix-named** — and it is (`HU-33A/33B`, plus `HU-37A/B/39B` scoring). So: **keep it, add the
selector when the second strategy arrives.** Most other domain decisions here are correctly **Policy**
classes (`SessionCreationPolicy`, `AccessPolicy`), not Strategy — that's fine, Policy isn't a mandated
pattern.

---

## Summary — what this means for the refactor

- mission-design's **Composite** and **Template Method** are already in their ADR-0012 canonical homes —
  Phase 1 needs no rework; mission-design is the reference (and `make structure-guard SVC=mission-design-service`
  is green).
- The gaps ADR-0012 makes actionable are **all Phase 2**: the session-operations `Facades/` bucket
  dissolution, the identity-access Api `Proxy→Handler` unification, and the `*Executor` pass-throughs
  beneath the Application proxies.

## Next — Phase 2 work-list (grounded in `make structure-guard`)

The unscoped guard reports the exact backlog: **identity-access (31 violations)** and
**session-operations (18)**. Two parallel branches, serial within each; every step its own commit on a
green build + tests (`make -C backend test SVC=<service>`). Full procedure in
[`plans/application-layer-cqrs-refactor.md`](../plans/application-layer-cqrs-refactor.md) §Phase 2.

**identity-access** — `refactor/app-layer-identity-access`
1. Slice-collapse `Application/<Area>/Handlers/*` → `Commands|Queries/<UseCase>/`, DTOs into their slices
   (areas: Users, Teams, Sessions, JoinTokens, Permissions). Mechanical, behavior-preserving.
2. Collapse the `*Executor` forwarding into handlers — **keep** the `*AuthorizationProxy` guards
   (matrix-named, HU-01/02/03/06/07A/07B/19/20). See §6 above.
3. **Proxy unification** (separate commit, gated by auth integration tests, never mixed with a move):
   `Api/Services/AuthenticatedUserLoginProxy` + `UserManagementProxy` → guard into the Application slice,
   delete the forwarding `*Handler`/`*EntryPoint` pair. See §6 above.

**session-operations** — `refactor/app-layer-session-operations`
1. Slice-collapse `Sessions/Handlers/*` → `Commands|Queries/<UseCase>/`. **Keep** `StateTransitions/`
   (State + CoR, §3/§4) and `EventHandlers/`.
2. Dissolve `Sessions/Facades/` → both facades to `Sessions/Common/` (§5).
3. Collapse the three `*Executor`s (`ISessionAdministrationAccessExecutor`,
   `IDisconnectParticipantExecutor`, `IReconnectAuthenticatedParticipantExecutor`) — **keep**
   `SessionAdministrationAuthorizationProxy`.

**Phase 3 (after both land)** — add both services to the Makefile's `CONVERGED_SERVICES` so `make build`
enforces the guard, diff the three Application trees for identical vocabulary, update `AGENTS.md` + the
`cqrs-mediatr-aspnetcore` skill. Done when `make structure-guard` (unscoped) is green.

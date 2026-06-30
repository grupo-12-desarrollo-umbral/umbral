# Plan: Align partial-pattern tickets to ADR-0012 placement convention

**Status:** Proposed
**Scope:** `identity-access-service`, `session-operations-service`, `mission-design-service`
**Goal:** Resolve the five DONE tickets whose mandated design pattern is present in *behavior* but off-convention in *placement/realization* per [ADR-0012](../docs/adr/0012-design-pattern-placement-convention.md). Each item below is either a concrete placement fix, a fix gated on a canon decision, or a deliberate no-op recorded so it stops resurfacing in reviews.

> Source of the findings: validation of the done backlog against
> [`docs/required_patterns_matrix.md`](../docs/required_patterns_matrix.md) and ADR-0012.
> 11 tickets PASS, 5 PARTIAL, 0 FAIL. This plan covers the 5 PARTIAL.

---

## 1. Summary

| HU | Ticket | Pattern | ADR-0012 issue | Action class |
|----|--------|---------|----------------|--------------|
| HU-10A | DES-15 | Composite | `Target` modeled outside the `MissionNode` tree, contradicting `required_patterns_matrix.md:88` | **Resolved via (B)** — matrix corrected; code is coherent (`Target` = play-content) |
| HU-01 | DES-5 | Proxy | Access guard embedded in `AuthenticateUserCommandHandler`, not a `*AuthorizationProxy`. *(The forwarding Api-layer proxy ADR-0012 §"Proxy unification" named is **already removed**.)* | **Decide → likely no-op** (decision needs the provisioned entity) |
| HU-12 | DES-18 | Template Method | Domain template genuine; no `Trivias/Common/Lifecycle/` app base validator | **Decide → likely no-op** (ceremony caveat) |
| HU-13 | DES-19 | Template Method | Domain template genuine; no `Trivias/Common/Reuse/` app base validator | **Decide → likely no-op** (ceremony caveat) |
| HU-07A/B | DES-11 / DES-12 | Proxy | No local `*AuthorizationProxy`; admission via domain `JoinPolicy` + cross-service guard | **Decide → likely no-op** (coarse gate → `AuthorizationBehaviour`) |

**Correction since first draft:** the only item ADR-0012 named as unconditional debt — HU-01's Api-layer proxy + forwarding `*Handler` — has **already been collapsed** (no `AuthenticatedUserLoginProxy`/`EnsureTrustedIdentity` remains; `Api/Services/` holds only `AuthorizationPolicies`, `CurrentUser`, `ProblemDetailsExceptionHandler`). So **none of the five is an unconditional code fix.** HU-10A turned out to be doc drift, not a code defect — `Target` is coherently modeled as TreasureHunt play-content (parallel to `TriviaQuizId`), `Descendants()` has no callers, and targets are validated directly; resolved by correcting `required_patterns_matrix.md:88` (see §3). The other four are "confirm-and-record" items that are very likely **correct as-is** under ADR-0012's own genuine-vs-ceremony test.

---

## 2. HU-01 — embedded access guard in `AuthenticateUserCommandHandler` (decide; likely no-op)

**Current state.** The forwarding Api-layer proxy is gone. What remains: `AuthenticateUserCommandHandler.Handle` provisions the user **and** runs the access decision inline — `_accessPolicy.Evaluate(user, AuthenticatedPlatformAccess)` then `EnsureAccess(user, …)` (handler lines 47–56, 86–97). There is no `AuthenticateUserAuthorizationProxy`.

**ADR-0012 rule.** Proxy preferred in the Application slice as `<UseCase>AuthorizationProxy.cs`. Shape (a) capability guard applies **only when the check needs no resource** — the proxy *is* the `IRequestHandler`, runs `EnsureCanAccess(actor, …)`, then delegates to `_inner`.

**Why this is likely correct as-is.** Shape (a) does **not** fit: the decision needs the *provisioned* `user` — `EnsureAccess` reads `user.IsActive`, which only exists after `SynchronizeOrCreate` runs against the repo. A wrap-and-delegate proxy can't make that call before delegating without duplicating provisioning (and the result DTO itself carries the access decision). This is the same "decision needs the loaded entity" situation ADR-0012 §2 carves out — and the only clean realization there (shape (b) resolver) is awkward when the handler also *creates* the subject. The guard is genuine and enforced; it's just interleaved with provisioning by necessity.

**Before (today — guard interleaved with provisioning):**
```csharp
// AuthenticateUserCommandHandler.Handle
var user = _identityProvisioningPolicy.SynchronizeOrCreate(existingUser, …, role);
var decision = _accessPolicy.Evaluate(user, ProtectedCapability.AuthenticatedPlatformAccess);
EnsureAccess(user, decision.IsAllowed);   // reads user.IsActive — needs the provisioned entity
```

**After (the "fix" — and why it doesn't pay off):**
```csharp
// AuthenticateUserAuthorizationProxy : IRequestHandler<…>  (shape a)
public Task<…> Handle(cmd, ct) {
    _accessPolicy.EnsureCanAccess(actor, AuthenticatedPlatformAccess); // ❌ has no `user` yet —
    return _inner.Handle(cmd, ct);                                     //    can't read IsActive
}
// → forces re-provisioning the user inside the proxy just to check IsActive: duplicate work,
//   two round-trips, and the proxy now owns provisioning logic. Net negative.
```

**Cost if unfixed.** Low. The forwarding pair that ADR-0012 actually flagged is already gone, so there's nothing rotting and no `structure-guard` blind spot left here. The only residue is a cosmetic inconsistency with the other slices' `*AuthorizationProxy` shape — and ADR-0012 itself sanctions in-place guards when the decision needs the entity.

**Action.** Confirm the access decision genuinely depends on the provisioned `user` (it does, via `IsActive`). If confirmed → **record as intentionally inline**; no proxy. Revisit only if the active-check moves to a pre-provision gateway claim.

---

## 3. HU-10A — `Target` is modeled as play-content, not a tree node (recommend: fix the canon)

**Current state.** `MissionNode` (abstract) + `Stage`/`Substage`/`Clue` is a textbook Composite. `Target : BaseEntity` (not `MissionNode`), held in a separate `_targets` list on `Substage`, excluded from `ChildNodes` (`Substage.cs:126` → `_clues` only). `MissionNodeType` has no `Target` value.

**This is deliberate, and coherent — `Target` mirrors `TriviaQuizId`.** A `Substage` carries play-content by mode, and neither mode's content is a tree node:
- TreasureHunt → `_targets` + `WinnerScore` (`Substage.cs:38-42`)
- Trivia → `TriviaQuizId` (`Substage.cs:44-45`)
- `Clue` is the **only** true leaf node, shared by both modes.
Making `Target` a node but leaving `TriviaQuizId` a plain reference would break that symmetry for no behavioral gain. `CONTEXT.md` and both entities' XML docs already describe it this way; only `required_patterns_matrix.md:88` calls Target a tree leaf.

**Two facts that shrink the "Composite gap" to nothing:**
- **`Descendants()` has zero callers** (grep: only its own definition in `MissionNode.cs`). The recursive traversal Target is "missing from" is never exercised — no tree-wide op silently skips Targets, because there are no tree-wide ops.
- **Targets are already validated** directly: `MissionActivationPolicy.cs:88` requires ≥1 active target per TreasureHunt substage (`substage.Targets.Any(t => t.IsActive)`). Readiness/activation never routes through the Composite.
- **Persistence is per-table, not TPH.** EF `OwnsMany<Target>` → `MissionTargets`, with `NodeType`/`Children` `Ignore`d for every node type (`MissionConfiguration.cs:106-110,147`). The Composite is pure in-memory behavior; the storage model doesn't care whether Target is a node.

**Two mutually exclusive resolutions:**
- **(B) — recommended. Matrix is the outlier** → amend `required_patterns_matrix.md:88` to state Targets are TreasureHunt play-content of `Substage` (parallel to `TriviaQuizId`), with `Clue` as the leaf node. Zero code; aligns the matrix with code + `CONTEXT.md` + the entity docs.
- **(A) — make `Target : MissionNode`** → only if the team genuinely wants QR targets traversable as nodes. Mechanical but not free, and it introduces real wrinkles:
```csharp
public sealed class Target : MissionNode {                 // was : BaseEntity
    public override MissionNodeType NodeType => MissionNodeType.Target;  // new enum value
    protected override IEnumerable<MissionNode> ChildNodes => [];        // leaf, like Clue
    protected override bool CanContain(MissionNodeType _) => false;
    // + Name → Title to satisfy the base  ⇒ column-rename migration + DTO/API change
}
public sealed class Substage : MissionNode {
    protected override IEnumerable<MissionNode> ChildNodes => _clues.Concat(_targets);
    // CanContain must now allow Clue AND Target; AddChildNode must route by type
}
// Wrinkle: Clue and Target become ordered siblings in Children, but keep PER-TABLE
// uniqueness (MissionClues / MissionTargets each have their own (SubstageId, SequenceOrder)
// index) — so a Clue and a Target can share SequenceOrder 1. Sibling ordering is now ambiguous.
// Buys: a Descendants() path nobody calls. Costs: the rename migration, the ordering wrinkle,
// and the lost Trivia/TreasureHunt symmetry.
```

**Cost if unfixed (and undecided).** **Low** — pure doc drift. With `Descendants()` unused and activation validating targets directly, there is no latent correctness gap; the only cost is that each validation pass re-discovers the matrix↔code mismatch (this plan being the second). Resolve the drift by editing the doc (B), not the code.

**Action.** Apply (B): edit `required_patterns_matrix.md:88` (done alongside this plan) and, if desired, add a one-line note to ADR-0012's Composite row. Pick (A) only on an explicit product call that QR targets must be tree-traversable — then it's a separate ticket carrying the `Name→Title` migration and the sequence-ordering decision.

---

## 4. HU-12 / HU-13 — app-layer Template Method (decide; likely no-op)

**Current state.** Domain templates are genuine and complete: `TriviaQuizLifecycleTemplate` (publish/archive) and `TriviaQuizReuseWorkflowTemplate` (duplicate/retire/remove), each with a fixed skeleton + overridable steps + ≥2 concretes. The matching Application validators (`PublishTriviaQuizCommandValidator`, `ArchiveTriviaQuizCommandValidator`, `Duplicate…`, `Retire…`) are single-rule FluentValidation classes with no shared base.

**ADR-0012 rule.** Template Method canonical home is Domain **and** Application (`<Area>/Common/<Concern>/` abstract base + concrete overrides per slice). HU-11/14 satisfy both layers; HU-12/13 satisfy only Domain.

**The caveat that likely makes this a no-op.** ADR-0012's genuine-vs-ceremony test for Template Method: *"a base class with no fixed skeleton — just shared helpers — is plain inheritance; prefer composition or collapse."* Lifecycle/reuse validation is one rule each — there is no real shared skeleton to template across slices. Forcing a `Common/Lifecycle/` abstract validator would manufacture the exact ceremony the ADR warns against.

**Before (today — trivial per-slice validators):**
```csharp
public sealed class PublishTriviaQuizCommandValidator : AbstractValidator<PublishTriviaQuizCommand>
{ public PublishTriviaQuizCommandValidator() => RuleFor(x => x.QuizId).NotEmpty(); }

public sealed class ArchiveTriviaQuizCommandValidator : AbstractValidator<ArchiveTriviaQuizCommand>
{ public ArchiveTriviaQuizCommandValidator() => RuleFor(x => x.QuizId).NotEmpty(); }
// Readiness skeleton already lives in the genuine domain template (TriviaQuizLifecycleTemplate).
```

**After (the "fix" — and why it's ceremony here):**
```csharp
// Trivias/Common/Lifecycle/TriviaQuizLifecycleCommandValidator.cs
public abstract class TriviaQuizLifecycleCommandValidator<T> : AbstractValidator<T> {
    protected TriviaQuizLifecycleCommandValidator() {
        RuleFor(QuizIdSelector()).NotEmpty();   // the ONLY shared rule
        AddLifecycleSpecificRules();             // hook that every subclass leaves empty
    }
    protected virtual void AddLifecycleSpecificRules() { }
}
// → an abstract base whose "skeleton" is one NotEmpty and a hook nobody overrides.
//   That's plain inheritance dressed as Template Method — the ADR says collapse it.
```

**Cost if unfixed.** Negligible. The pattern's *purpose* (a fixed readiness skeleton with mode-specific steps) is fully met by the domain template. Adding the app base buys nothing and creates the ceremony the ADR explicitly removes. Leaving it is the cheaper *and* more correct choice.

**Action.** Confirm there is no genuine shared validation across the lifecycle slices (and separately the reuse slices). If confirmed → **record as deliberately Domain-only** (the readiness skeleton lives in the domain template; the app validators are intentionally trivial). Add app-layer base validators **only if** real shared rules emerge.

---

## 5. HU-07A / HU-07B — local Proxy vs. domain policy (decide; likely no-op)

**Current state.** Admission/reconnection is enforced by domain `JoinPolicy.EnsureCanJoin` / `EnsureCanReconnect` (session state, capacity, role+team assignment), invoked inside `LiveSession.AdmitParticipant`, plus a cross-service `IParticipantMembershipAccessClient` call that hits identity-access's genuine `ParticipantMembershipAccessAuthorizationProxy` (the HU-06 PASS proxy). There is no local `session-operations` `*AuthorizationProxy`.

**ADR-0012 rule.** Proxy is preferred in Application as `<UseCase>AuthorizationProxy.cs`. But: *"coarse role/policy gates go to `[Authorize]` + `AuthorizationBehaviour`."*

**The caveat that likely makes this a no-op.** The actual authorization decision (is this participant allowed into this team) is already made by a genuine proxy — in identity-access. Locally, `JoinPolicy` is a *domain admission rule*, not an access proxy. The matrix even names `JoinPolicy` as the mechanism. Wrapping the handler in a second local proxy would duplicate a decision already owned upstream.

**Before (today — access decided upstream, admission in the domain):**
```csharp
// ReconnectAuthenticatedParticipantCommandHandler.Handle
await _participantMembershipAccessClient.ValidateAsync(…, ct);     // ← access guard: identity-access
liveSession.AdmitParticipant(participantId, requestedTeamId, _joinPolicy); // ← domain admission rules
//   JoinPolicy.EnsureCanReconnect: session state, role+team assignment (NOT an access proxy)
```

**After (the "fix" — and why it duplicates):**
```csharp
// session-operations Sessions/Commands/.../ReconnectAuthorizationProxy.cs : IRequestHandler<…>
public Task Handle(cmd, ct) {
    await _participantMembershipAccessClient.ValidateAsync(…, ct); // ❌ same call the handler
    return _inner.Handle(cmd, ct);                                 //    already makes — moved, not added
}
// → the access decision still lives in identity-access; the local proxy is a pass-through
//   wrapper around a cross-service call. Two hops to relocate one line. No new guarantee.
```

**Cost if unfixed.** Negligible. The access decision is enforced (upstream, by a PASS proxy) and admission is enforced (domain `JoinPolicy`). A local proxy relocates an existing call without adding protection. ADR-0012 routes exactly this — a guard with no local resource decision — to `AuthorizationBehaviour`/upstream, not a `*AuthorizationProxy`.

**Action.** Confirm the cross-service guard fully covers the access decision and `JoinPolicy` is purely domain admission. If confirmed → **record as intentional**: access guarded by the identity-access proxy; local rules are domain policy by design. No local proxy added.

---

## 6. Execution order

No item is an unconditional code change. In priority order:

1. **HU-10A — done via (B).** `required_patterns_matrix.md:88` (and the ADR-0004 responsibility row, line 24) corrected to describe `Target` as TreasureHunt play-content, not a node. Code unchanged. Pick (A) only on an explicit product call that QR targets must be tree-traversable — then a separate ticket carries the `Name→Title` migration + sequence-ordering decision (§3). *(Optional follow-up: a one-line note on ADR-0012's Composite row.)*
2. **HU-01, HU-12/13, HU-07A/B** — confirm-and-record passes; produce a short rationale paragraph each (here or in the HU context docs) so the next validation run doesn't re-flag them. Each §2/§4/§5 above already states the confirmation to make and why the "fix" is a no-op or net-negative. **No code** unless a confirmation fails.

---

## 7. Out of scope

- The 11 PASS tickets (HU-03, HU-06, HU-20, HU-19, HU-15, HU-18, HU-11, HU-14A, HU-14B, and the no-pattern HU-02/04/05/09) — already aligned, no action.
- Any pattern for HUs not yet DONE.
- Domain/Api placement enforcement tooling (`structure-guard.sh` scans `Application/` only — ADR-0012 §Enforcement records this gap; not reopened here).

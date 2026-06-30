# Plan — apply over-engineering findings 6–9

**Created:** 2026-06-30 · **Source:** `backend/docs/findings/violations-overengineering.md` (re-validated against the working tree this session, not the 2026-06-29 snapshot).

Findings 1–4 (mission-design) are already applied (separate work). Finding 5 (identity-access `GetCurrentActorAsync`) is **already fixed** by `b7be5c5` (`ICurrentActor`) — out of scope. This plan covers the remaining four:

| # | Service | Finding | Verdict | Risk |
|---|---|---|---|---|
| 8 | session-operations | dead `SourceTriviaQuizNotPublishedException` | real | trivial |
| 9 | session-operations | duplicated trivia-question load/projection | real | low |
| 6 | identity-access | 10 inline `EnsureActorCanXxx` guards reinvent `AccessPolicy.EnsureCanAccess` | real | low |
| 7 | identity-access | 4 (+1) forwarder handlers + `I*Service` triplets | real, **contested** | medium / decision-gated |

Phases are ordered easiest-first to build a clean test record before the contested one. Findings 8–9 (session-operations) and 6–7 (identity-access) live on **different Phase-2 branches** (per `HANDOFF.md`) — keep each service's work on its own branch; do not mix.

## Governing rules (apply to every phase)

- **ADR-0011 §3 removal test:** a pattern instance may be removed only when **both** (a) the matrix (`docs/trivia_sprint_required_patterns_matrix.md`) does not name it for that HU **and** (b) it is pure forwarding ceremony adding no behavior. Mandated `*AuthorizationProxy` guards are KEPT.
- **ADR-0012 Proxy/Facade realization test:** a Proxy is genuine when it adds an access decision; the un-mandated `IService`/`IExecutor` pass-through layer is the ceremony to collapse.
- **Commit discipline (refactor-plan safety rules):** mechanical moves and behavior-changing edits go in **separate commits**, one area per commit. No `Co-Authored-By: Claude` trailer.
- **Per-phase gate:** `make -C backend build SVC=<svc>` (incl. `structure-guard`) · `make -C backend test SVC=<svc>` · `make -C backend gate SVC=<svc>` (coverage ≥ threshold) all green before moving on. Sandbox: run `make`/`find`/`ls` with the sandbox disabled (nested-userns seccomp).
- **Lazy-check rule:** any non-trivial behavior change leaves one runnable check that fails if the logic breaks.

---

## Phase 1 — session-operations finding 8: delete dead exception

`SourceTriviaQuizNotPublishedException` has **0 throw sites** in `src/`. Only its definition and one ProblemDetails test reference it.

**Changes**
1. Delete `src/Application/Common/Exceptions/SourceTriviaQuizNotPublishedException.cs`.
2. Delete the single test method `TryHandleAsync_WithSourceTriviaQuizNotPublishedException_ReturnsConflictProblemDetails` (`tests/IntegrationTests/Api/ProblemDetailsExceptionHandlerTests.cs`, ~L67–75).

**Why it's safe:** the generic `IErrorMetadata`→ProblemDetails Conflict mapping it exercised stays covered by `TryHandleAsync_WithDomainConflictException_…` (`TeamCapacityReachedException`, same 409 path) — no coverage loss.

**Verify:** `grep -rn SourceTriviaQuizNotPublished src tests` returns nothing → build + test + gate green. One commit.

---

## Phase 2 — session-operations finding 9: extract trivia-question helper

Identical load+projection in two files, both already in `Application/Sessions/Common/`:
- `TriviaRoundOrchestratorFacade.ActivateQuestionAsync` (~L97–103)
- `SessionTimerSnapshotDtoFactory.CreateActiveQuestionSnapshot` (~L40–47)

Both do `MissionRuntimeSnapshot.TriviaQuestionSnapshots.OrderBy(s => s.SequenceOrder).ElementAt(index)` then `question.Options.OrderBy(o => o.SequenceOrder).Select(o => o.OptionText).ToArray()`.

**Changes**
1. Add one static helper in `Sessions/Common/` (new `TriviaQuestionSnapshotSelector.cs`, or a static method on an existing `Sessions/Common/` helper if a natural home exists):
   ```csharp
   internal static (TriviaQuestionSnapshot Question, string[] Options) GetOrderedTriviaQuestion(
       LiveSession session, int questionIndex)
   ```
   returning the ordered question + projected option texts.
2. Replace both blocks with a call to the helper. Keep the surrounding lines (`questionTimer`, broadcast DTO build) in place — only the load+projection moves.

**Why this passes the one test (not over-extraction):** shared logic is *more than a guard clause* (an ordered index lookup + a projection), 2 distinct consumers — earns its abstraction per the checklist.

**Lazy-check:** one `test_*` / `[Fact]` asserting the helper returns options in `SequenceOrder` for an out-of-order snapshot (the bug the projection prevents).

**Verify:** both call sites compile to a single helper call; existing trivia-round + timer-snapshot tests still pass. One commit (pure refactor — no behavior change).

---

## Phase 3 — identity-access finding 6: collapse 10 guards onto `AccessPolicy.EnsureCanAccess`

`AccessPolicy.EnsureCanAccess(user, capability)` already does exactly `Evaluate → (!IsActive → DeactivatedUserAccessDeniedException) → (!IsAllowed → UserRoleNotAuthorizedException)`. Ten private guards reimplement that byte-for-byte, varying only the capability.

**Sites + capability to preserve (1:1):**

| File | Guard | Capability |
|---|---|---|
| `Users/Commands/DeactivateUser/DeactivateUserCommandHandler.cs` | `EnsureActorCanDeactivate` | `AdministratorPanel` |
| `Users/Commands/AssignUserRole/UserRoleAssignmentAuthorizationProxy.cs` | `EnsureActorCanAssignRole` | `AdministratorPanel` |
| `Users/Queries/GetUsers/GetUsersQueryHandler.cs` | `EnsureActorCanListUsers` | `UserAccessCatalog` |
| `Teams/Commands/RegisterTeam/RegisterTeamCommandHandler.cs` | `EnsureActorCanManageTeams` | `OperatorPanel` |
| `Teams/Commands/UpdateTeam/UpdateTeamCommandHandler.cs` | `EnsureActorCanManageTeams` | `OperatorPanel` |
| `Teams/Commands/DeactivateTeam/DeactivateTeamCommandHandler.cs` | `EnsureActorCanManageTeams` | `OperatorPanel` |
| `Teams/Commands/AssignParticipantToTeam/AssignParticipantToTeamCommandHandler.cs` | `EnsureActorCanManageTeams` ⚠️ | `OperatorPanel` |
| `Teams/Queries/GetTeams/GetTeamsQueryHandler.cs` | `EnsureActorCanReadTeams` | `OperatorPanel` |
| `Teams/Queries/GetTeamById/GetTeamByIdQueryHandler.cs` | `EnsureActorCanReadTeams` | `OperatorPanel` |
| `Teams/Queries/GetTeamParticipants/GetTeamParticipantsQueryHandler.cs` | `EnsureActorCanReadTeams` ⚠️ | `OperatorPanel` |

⚠️ = the two that throw `ForbiddenAccessException` on `!IsAllowed` instead of `UserRoleNotAuthorizedException`.

**Out of scope (verified different shape — do NOT touch):** `CheckProtectedCapabilityAccessQueryHandler.EnsureAccess` (this *is* the access-check use case; it returns a decision DTO) and `AuthenticateUserCommandHandler.EnsureAccess` (bootstrap login precondition, deliberately in the handler per `HANDOFF.md`). Both take a precomputed bool, not a capability — not `EnsureCanAccess` duplicates.

**Pre-flight decision (roll the audit's "confirm HTTP status" note in here):**
- Both `ForbiddenAccessException` and `UserRoleNotAuthorizedException` map to `ErrorCategory.Forbidden` → **same HTTP 403**. So the collapse is **status-preserving**; only the response *body* for `AssignParticipantToTeam` + `GetTeamParticipants` changes from error-code `forbidden-access` (generic) to the role-not-authorized message — which makes them **consistent** with every sibling endpoint (arguably the intended behavior all along).
- **Gate:** `grep -rn 'forbidden-access' tests` in the service. If any integration test asserts that error code specifically for those two endpoints, update it in the same commit (it's the consistency fix, not a regression). If none, no test change needed beyond construction-site updates.

**Changes (per file):** replace the private guard body's call sites — `EnsureActorCanXxx(actor)` → `_accessPolicy.EnsureCanAccess(actor, ProtectedCapability.<Cap>)` — and delete the now-unused private method. `_accessPolicy` is already injected in every one of these classes. Drop now-unused `using` for `DeactivatedUserAccessDeniedException`/`UserRoleNotAuthorizedException`/`ForbiddenAccessException` where they were only used by the deleted guard.

**Commit split:** one mechanical commit for the 8 already-`UserRoleNotAuthorizedException` sites; a **separate** commit for the 2 ⚠️ sites (the body-changing ones) so the behavior delta is isolated and gated by the auth integration tests.

**Lazy-check:** existing per-handler authorization tests (deactivated → 403, wrong-role → 403) already cover this; confirm they still pass. No new test unless the ⚠️ grep finds an error-code assertion to migrate.

**Verify:** `grep -rn 'private.*void Ensure' src/Application` returns only the 2 out-of-scope `EnsureAccess` methods → build + structure-guard + test + gate green.

---

## Phase 4 — identity-access finding 7: collapse forwarder triplets (DECISION-GATED)

**This phase is contested and must not start until the gate below is resolved.** It overlaps with very recent, deliberate work on this same branch (`18294e9`/`3a0e700`/`5dee93a`/`09287f6` "collapse Executor into single Proxy subject"), and `HANDOFF.md` (2026-06-29) records the team position that "the three app layers are NOT over-engineered… every `*Service` under a proxy holds real domain logic." Finding 7 disagrees about the **forwarder handler**, not the Service's logic.

### The shape today (5 slices)

For each: a one-line `*CommandHandler`/`*QueryHandler` forwards to an `I*Service`, which DI binds to the `*AuthorizationProxy` (genuine guard) decorating the concrete `*Service` (real logic). See `Application/DependencyInjection.cs:38–56`.

| Slice | Handler (forwarder) | `I*Service` | Proxy (keep) | Service (logic) |
|---|---|---|---|---|
| `JoinTokens/Commands/IssueJoinToken` | `IssueJoinTokenCommandHandler` | `IIssueJoinTokenService` | `JoinTokenIssuanceAuthorizationProxy` | `JoinTokenIssuanceService` |
| `JoinTokens/Queries/ValidateParticipantMembershipAccess` | `…QueryHandler` | `IValidateParticipantMembershipAccessService` | `ParticipantMembershipAccessAuthorizationProxy` | `ParticipantMembershipAccessValidationService` |
| `Sessions/Queries/GetSessionTeamsForParticipant` | `…QueryHandler` | `IGetSessionTeamsForParticipantService` | `ParticipantSessionTeamLobbyAuthorizationProxy` | `ParticipantSessionTeamLobbyService` |
| `Teams/Commands/JoinTeamAsParticipant` | `JoinTeamAsParticipantCommandHandler` | `IJoinTeamAsParticipantService` | `ParticipantTeamSelfJoinAuthorizationProxy` | `ParticipantTeamSelfJoinService` |
| `Users/Commands/AssignUserRole` | `AssignUserRoleCommandHandler` | `IUserRoleAssignmentService` | `UserRoleAssignmentAuthorizationProxy` | `UserRoleAssignmentService` |

The redundancy finding 7 targets: the handler is a pure forwarder **and** the `I*Service` exists only as the Proxy's decoration seam. ADR-0012's canonical Proxy "wraps the real subject" — and a use case's real subject is its **handler**, not a separate Service. So the aligned end-state collapses the seam.

### GATE 0 — confirm the end-state before any edit

Decide, against ADR-0011 §3 + ADR-0012 Proxy row + the matrix (and with whoever owns those ADRs):

- **Option A — Collapse (finding 7 as written, recommended if ADRs allow):** Proxy implements `IRequestHandler<TCmd,TRes>` and decorates the concrete handler, which absorbs the `*Service` logic. Delete `I*Service` + `*Service`. DI registers the handler decorator. End-state per slice: **2 types** (handler with logic + Proxy guard) instead of 3 types + 1 interface — and the Proxy now genuinely wraps the real subject, matching ADR-0012 more closely than today's shape.
- **Option B — Accept current shape (partial retract):** if the team holds that handler→Proxy(`I*Service`)→Service is the blessed ADR-0012 realization (the `HANDOFF.md` 2026-06-29 reading), finding 7 becomes a **documentation note**, not code. Record the rationale in the findings doc and stop.

Do not proceed to the changes below unless Gate 0 picks **A**. Apply the audit's second under-stated note here: treat **all 5** slices identically — `IUserRoleAssignmentService` is the same forwarder shape; the "it's mocked in tests" carve-out is not a reason to keep it (the test moves to mocking the handler/decorator the same way).

### Changes if Option A (one commit per slice, mechanical-then-semantic discipline)

Per slice:
1. Move the `*Service.XAsync(...)` body into the concrete `*CommandHandler`/`*QueryHandler.Handle(...)` (inject what the Service injected: repos, `ICurrentActor`, policies, `TimeProvider`).
2. Change the `*AuthorizationProxy` to implement `IRequestHandler<TCmd,TRes>` (keep its actor-load + `AccessPolicy.EnsureCanAccess`/role guard, then `await _inner.Handle(request, ct)`), wrapping the concrete handler as inner.
3. Delete `I*Service.cs` + `*Service.cs`.
4. DI (`DependencyInjection.cs`): replace the `AddScoped<I*Service>(proxy wrapping service)` pair with `AddScoped<ConcreteHandler>()` + register the Proxy as `IRequestHandler<TCmd,TRes>` decorating it (`ActivatorUtilities.CreateInstance`, same mechanism already used). **Confirm MediatR resolves the decorator** — `Send` does `GetRequiredService<IRequestHandler<,>>`; the manual decorator registration must win over MediatR's assembly-scan registration (verify ordering, or exclude that handler from the scan).
5. Update tests: the Proxy unit test now mocks the inner `IRequestHandler` (or the concrete handler); delete the `I*Service` mock seam.

**Watch items**
- The double-fetch already killed by `ICurrentActor` memoization (2026-06-30) must stay killed — the actor load in the Proxy and any load in the absorbed handler logic share the one scoped `CurrentActor`. No regression expected; the gate's integration tests confirm.
- `structure-guard` forbids `*Executor` only — `I*Service`/`*Service` are not machine-checked, so Phase 4's correctness rests on the **manual** patterns checklist + the auth integration tests. Run the full auth integration suite after each slice.

**Lazy-check:** the existing `*AuthorizationProxy` guard tests (deactivated/wrong-role → throws before delegating) must still pass against the new `IRequestHandler` decorator — that's the proof the guard still runs before the handler.

**Verify per slice:** build + structure-guard + full unit + auth integration + gate green; `grep -rn 'I.*Service' src/Application/<slice>` shows the interface gone.

---

## Sequencing & branches

1. **session-operations branch:** Phase 1 → Phase 2 (independent, trivial/low). Land first.
2. **identity-access branch (this branch):** Phase 3 (low) → Gate 0 → Phase 4 (only if Option A).
3. Phase 4 is the only one that may **not** ship — that's an acceptable outcome; record the Option-B rationale in `backend/docs/findings/violations-overengineering.md` if so.

## Net (if all phases land Option A)

- session-operations: −1 dead exception + 1 dead test; −7 duplicated lines → 1 helper.
- identity-access: −10 private guards (~−110 lines); −5 `I*Service` interfaces + −5 `*Service` classes folded into handlers (~−150 lines, exact TBD), 0 mandated patterns removed.

## Out of scope / explicitly NOT touched

- Finding 5 (already fixed — `ICurrentActor`).
- The mandated `*AuthorizationProxy` guards (kept in every phase — matrix-named for HU-03/07A/07B/19/20).
- The 2 `EnsureAccess` methods in Phase 3's service (different semantics).
- `AuthenticateUser` bootstrap guard and the Api-layer proxy unification (done, `eecac6a`).

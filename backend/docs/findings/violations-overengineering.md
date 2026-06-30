# Application-layer over-engineering violations

A ponytail-audit of the Application layer of three backend services, cross-checked against `backend/docs/refactors/application-layer-overengineering-checklist.md`. Audit-only — no files were modified. Findings are ranked biggest cut first within each service.

- **Date:** 2026-06-29
- **Scope:** `src/Application/` of `session-operations-service`, `identity-access-service`, `mission-design-service`
- **Lens:** the checklist's one test — *"Shared code earns its abstraction only when the shared logic is more than a guard clause. Mandated patterns (ADR-0004 / matrix) are kept regardless."*
- **Out of scope:** correctness bugs, security holes, performance. Only complexity / over-engineering.

## Aligned findings

These map directly to a CUT or KEEP rule in the checklist.

### mission-design-service

| Tag | Finding | Replacement | Checklist rule |
|---|---|---|---|
| `yagni` | Duplicate command triple: `SetTriviaQuizSelection` and `UpdateTriviaQuizSelection` have byte-identical Command records and Handlers (diff confirms only namespace/class names differ; both call `mission.SelectTriviaQuiz(...)`). | Collapse to one `SelectTriviaQuizCommand`; route both controller verbs to it. | YAGNI duplicate (consistent with checklist spirit; not a named line item) |
| `delete` | Dead cluster: `Permissions.cs` (4 const strings, 0 consumers), `LookupDto` (self-referenced only), `PagedResult<T>` (only a unit test references it; catalog queries return `IReadOnlyList`), `Result` + `IIdentityService.CreateUserAsync`/`DeleteUserAsync` stubs (return `Result.Failure(...)`, never called). | Drop all + their tests. | CUT #2 (dead/speculative) |
| `yagni` | `IIdentityService.AuthorizeAsync` + policy branch in `AuthorizationBehaviour`: `AuthorizeAsync` is a return-`false` stub; zero production commands use `[Authorize(Policy=...)]` (only one unit test). | Drop the method, the ~13-line policy block, `AuthorizeAttribute.Policy`, and the test. | CUT #2 |
| `yagni` | `IIdentityService.IsInRoleAsync`: return-`false` stub, zero callers (role branch reads `_user.Roles` directly). | Drop the method + stub. | CUT #2 |

### identity-access-service

| Tag | Finding | Replacement | Checklist rule |
|---|---|---|---|
| `shrink` | `GetCurrentActorAsync` duplicated ~14× — same Id-null / `GetByExternalIdentityIdAsync` / `NotFoundException` 3-liner inlined across ~7 handlers + ~5 proxies + 2 more. | One `ICurrentUser.GetActorAsync(IUserRepository, CancellationToken)` extension. | passes the one test (shared logic > guard clause, ≥2 consumers) |
| `shrink` | ~10 inline `EnsureActorCanXxx` private guards reinvent `AccessPolicy.EnsureCanAccess` (which already does `Evaluate` + `DeactivatedUserAccessDeniedException` + `UserRoleNotAuthorizedException`). | Replace each with `_accessPolicy.EnsureCanAccess(actor, capability)`. **Confirm first:** `AssignParticipantToTeam` + `GetTeamParticipants` throw `ForbiddenAccessException` instead — verify intended HTTP status before merging. | KEEP (real-logic helper in `<Area>/Common/`) |
| `shrink` | 4 pure-forwarder handlers + 4 service interfaces (`IIssueJoinTokenService`, `IValidateParticipantMembershipAccessService`, `IJoinTeamAsParticipantService`, `IGetSessionTeamsForParticipantService`) — each handler body is one `await _service.XAsync()`. **Correction applied:** the original audit proposed keeping the service and killing the handler; the checklist direction is the inverse. | Delete the `I*Service` + service class; move logic into the handler; let the mandated `*AuthorizationProxy` wrap the handler. `IUserRoleAssignmentService` STAYS — mocked in tests. | CUT #5 (un-mandated forwarding triplet; keep the Proxy — matrix-mandated) |

### session-operations-service

| Tag | Finding | Replacement | Checklist rule |
|---|---|---|---|
| `delete` | `SourceTriviaQuizNotPublishedException` — no throw site anywhere in `src/` (only its definition file + a ProblemDetails integration test that exercises a dead mapping). | Remove the exception and that test. | CUT #2 |
| `shrink` | Trivia-question load + option projection copy-pasted in `TriviaRoundOrchestratorFacade.ActivateQuestionAsync` (L97–103) and `SessionTimerSnapshotDtoFactory.CreateActiveQuestionSnapshot` (L40–47). | Extract one static `GetOrderedTriviaQuestion(LiveSession, int)` helper. | passes the one test (shared logic > guard clause, 2 consumers) |

## Retracted findings (considered and rejected against the checklist)

These were proposed in the initial audit but **do not align** with the checklist's KEEP rules. Recorded here so the same temptation doesn't recur in later phases.

### mission-design-service — RETRACT

- **"Seven one-rule validators → `IdValidator<T>` + `IHasId` marker"** — CONTRADICTS CUT #1. Phase 1 explicitly removed this exact shape (`Trivias/Common/Lifecycle/` + `Reuse/`: marker interface + base validator whose only shared rule is `Id > 0`). The checklist says *inline the rule into each concrete validator; delete the folder.* The seven validators (`Archive/Delete/Duplicate/Publish/RetireTriviaQuiz`, `Activate/DeactivateMission`) are already in the correct shape — `RuleFor(c => c.Id).GreaterThan(0)` is inlined per concrete validator. Do not extract.

### session-operations-service — RETRACT

- **"Cut `IAssignOperatorToSessionFacade` + `ITransitionSessionStateFacade` interfaces"** — CONTRADICTS the KEEP rule. Verified both facades coordinate ≥3 collaborators each:
  - `AssignOperatorToSessionFacade`: `ISessionAdministrationAccessResolver` + `IAssignableSessionOperatorAccessClient` + `ILiveSessionRepository` + `TimeProvider`.
  - `TransitionSessionStateFacade`: `ISessionAdministrationAccessResolver` + `SessionTransitionChain` + `SessionStateTransitionPolicy` + `ILiveSessionRepository` + `TimeProvider`.
  
  KEEP rule: *"A genuine Facade coordinates ≥2 collaborators behind one interface; keep it even when its handler is a thin delegate."* Facade is mandated by `session-operations-service/CONTEXT.md`. Interface stays.

### identity-access-service — CORRECTION (not full retract)

- **"4 forwarder handlers → keep service, kill handler"** — partially misaligned. Original finding kept the `I*Service` layer; CUT #5 says *inline into the handler* (kill the `I*Service`). **Corrected in the Aligned table above:** delete the `I*Service` + service class, move logic into the handler, mandated proxy wraps the handler.

## Outside the checklist (ponytail ladder, not checklist lines)

Recorded for completeness but not grounded in the checklist's abstraction-shape rules.

| Tag | Finding | Replacement | Note |
|---|---|---|---|
| `stdlib` | `GatewayRoleParser.TryParse` hand-rolls a string switch + `Assign(out Role)` helper. | `Enum.TryParse<Role>(value, out role) && Enum.IsDefined(role)` in one line. Keep the `Parse` wrapper for its `ValidationException`. | stdlib rung, not a checklist line |
| `delete` | `UserRoleAssignmentService.TryParseRole` + `CreateUnknownRoleValidationException` duplicate `AssignUserRoleCommandValidator.BeKnownRole`. | The validator already rejects unknown roles in the MediatR pipeline before the service runs. | Checklist default is "when in doubt, keep and refactor"; on a privileged role-assignment path, keeping is defensible as defense-in-depth. Leave unless the team confirms the duplication is unwanted. |

## Net tally

**~−380 lines, −3 deps possible** (drop `Result` / `PagedResult` / `LookupDto`; mission-design `IIdentityService` flattens to a single method once stubs go).

Per service:
- `mission-design-service`: ~−190 lines, −3 deps
- `identity-access-service`: ~−190 lines, 0 deps
- `session-operations-service`: ~−41 lines, 0 deps

## What was deliberately NOT flagged

Per the checklist KEEP list and the services' CONTEXT.md required patterns:

- **Mandated patterns** (Facade / State / Chain of Responsibility / Template Method / Proxy / Composite) — kept regardless of thinness. The session-ops facades, the `StateTransitions/Validators/` chain, the `*AuthorizationProxy` classes, and the composite `Mission`/`MissionNode` tree are all intentional.
- **MediatR pipeline behaviours** (Validation, Authorization, Performance, Logging, UnhandledException) — canonical Clean Architecture template; replacing them is more code.
- **Anti-corruption ports** (`I*AccessClient`, `I*Source`, `ISessionTeamAssociationSyncClient`, cross-context integration interfaces) — designed ports, not over-engineering.
- **Standard template** (`AuthorizeAttribute`, `ICurrentUser`, `IDatabaseHealthCheck`, `IClock`).
- **Real-logic guards/helpers in `<Area>/Common/`** with ≥2 consumers or paired siblings enforcing ADR-0003 (trivia reference integrity) — e.g. `ActiveMissionTriviaReferenceGuard`, `MissionStructureEditor`, `MissionTriviaPublicationChecker`, `TriviaQuizSelectionGuard`, `TriviaAuthoringInputMapper`, `MissionDtoMapper`, `TriviaQuizDtoMapper`, the `Trivias/Common/Authoring/` validators.
- **Authoring interfaces** `ITriviaQuizAuthoringCommand` / `ITriviaQuestionAuthoringCommand` — two implementors each, drive the shared validators (canonical Template Method per CONTEXT.md).

## Next steps

This is a findings doc only. To apply, file each finding as a separate commit-sized ticket under `backend/docs/refactors/` (or the issue tracker) and execute against the checklist's "Before cutting anything" preamble: confirm the pattern isn't named in `docs/trivia_sprint_required_patterns_matrix.md` for the touched HU. When in doubt, keep and refactor — never delete a mandated pattern.

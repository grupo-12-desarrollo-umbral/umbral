# Handoff — Unblock unassigned Open-Team-Selection participants across gameplay (2026-07-15)

**Status:** investigated + planned; implementation not yet started.
**Scope:** session-operations-service (backend) + mobile. identity-access-service: no change.

## Problem

A newly registered participant with no `RegisteredTeamMembership` (e.g. `participant@umbral.local` on
SMOKE1) can *see* a session's teams as joinable and *self-join*, but is then blocked everywhere in mobile
with "Participant is not authorized for the registered team." This is not a seed-data quirk — it is a
contradiction between two designs:

- **Open Team Selection** (session-ops `CONTEXT.md:25-26`, PRD `backend/docs/prd/DES-69-*.md:10-11`): an
  eligible-but-unassigned participant may self-select *any* attached team; this writes only a session-ops
  `SessionParticipant`/`TeamMember` and **never** a `RegisteredTeamMembership`. Session-ops owns the final
  admission decision.
- **The runtime guard** (`RuntimeParticipationGuard`) instead re-checks identity-access
  `POST /api/permissions/participant-membership-access`, which hard-denies anyone without a
  `RegisteredTeamMembership` row (`ParticipantMembershipAccessAuthorizationProxy.cs:66-72`, returned as
  HTTP 200 `isAllowed=false` — not a 403). It runs on **every gameplay path** — reconnect,
  answer/evidence submit, team board, timer — so an unassigned participant is blocked everywhere and gets a
  Participation Block + connection eviction.

**Where the error surfaces:** mobile `team-lobby.tsx:122` calls `validate()` right after a successful
self-join, using the *reference* team id; the same guard is then hit again on each runtime interaction via
session-ops `RuntimeParticipationGuard.EnsureAllowedAsync`.

## Decision

The runtime guard should validate the **eligibility** access-fact, not per-team `RegisteredTeamMembership`.
An unassigned participant has no membership row *by design*, so any per-team-membership requirement can only
be satisfied by pre-assigning everyone — the opposite of the goal. Relaxing the identity-access guard is
architecturally wrong: identity no longer owns a session's attached-team set (`SessionTeamAssociation` was
removed in the Users↔SessionOps realignment), and the boundary docs put admission in session-ops.

`IParticipantEligibleTeamsClient.GetAsync()` → `GET /api/permissions/participant-eligible-teams` already
returns `ParticipantEligibleTeamsDto(IsEligible, ReasonCode, Teams[])`, where
`GetParticipantEligibleTeamsQueryHandler.cs:32-40` sets `IsEligible=false` **only** for deactivated /
non-Participant actors — exactly the Participation Block trigger. `SelectTeamCommandHandler.cs:44-48` already
gates self-join on this same fact, so the guard just needs to mirror it.

## Key verified facts

- **Team-id scheme:** downstream (reconnect, board, timer, answer-submit) keys on the **reference** team id.
  `ParticipantSessionMembershipChecker.cs:41` matches on `ReferenceTeamId`; mobile `team-space.tsx` treats
  `params.teamId` as the reference id throughout. The self-join *response* carries the *runtime* id — so
  mobile must keep using the lobby card's `referenceTeamId` downstream, **not** the join result's id.
- **Local team-binding audit of the 4 guarded paths** (critical): reconnect
  (`JoinPolicy.EnsureCanReconnect` → `ParticipantAssignedToDifferentTeamException`), team board and timer
  (`IParticipantSessionMembershipChecker.Check`) already bind the caller to the submitted team **locally**.
  **Evidence/trivia intake (`RuntimeParticipationLink`) does NOT** — today only the identity guard stops a
  participant submitting an answer attributed to a foreign team. **A local ownership check must be added
  there** or the guard change opens a hole.

## Changes

### A) session-operations backend

1. **`.../Sessions/Common/RuntimeParticipationGuard.cs`** — swap `IParticipantMembershipAccessClient` for
   `IParticipantEligibleTeamsClient`. Body:
   ```
   var whitelist = await _eligibleTeamsClient.GetAsync(cancellationToken);
   if (whitelist.IsEligible) return;
   await ApplyParticipationBlockAsync(liveSessionId, cancellationToken);
   throw new ForbiddenAccessException();
   ```
   Keep `ApplyParticipationBlockAsync` (block + evict) unchanged. Update the class comment
   (Users membership-access fact → eligibility access-fact).

2. **Simplify the guard signature** to `EnsureAllowedAsync(Guid liveSessionId, CancellationToken)` —
   `teamId`/`token` are no longer meaningful. Update `IRuntimeParticipationGuard.cs` and the 4 call sites:
   `ReconnectAuthenticatedParticipantCommandHandler.cs:36`, `GetParticipantTeamBoardQueryHandler.cs:33`,
   `GetParticipantSessionTimerSnapshotQueryHandler.cs:33`,
   `EvidenceIntakeValidation/Validators/RuntimeParticipationLink.cs:21`.

3. **SECURITY-CRITICAL — `.../EvidenceIntakeValidation/Validators/RuntimeParticipationLink.cs`**: inject
   `IParticipantSessionMembershipChecker` (already DI-registered) and, after `EnsureAllowedAsync`, assert
   `_membershipChecker.Check(context.Session, context.TeamId).IsAllowed` else `ForbiddenAccessException`.
   Mirrors the board/timer handlers. Binds trivia + target-scan submissions to a live, non-blocked member of
   the submitted (reference) team.

4. **Remove now-dead membership-access client** (confirm with a final grep first):
   `IParticipantMembershipAccessClient.cs`, `Infrastructure/Identity/ParticipantMembershipAccessClient.cs`,
   `Dtos/Sessions/ParticipantMembershipAccessDecisionDto.cs`, and its `AddHttpClient` registration at
   `Infrastructure/DependencyInjection.cs:32-36`.
   **Do NOT delete `ParticipantMembershipAccessClientOptions.cs`** — `DependencyInjection.cs:24-30` reuses
   its `SectionName`/`BaseAddress` as the shared identity-access base address for several other clients
   (`IAssignableSessionOperatorAccessClient`, `ITeamReferenceCatalogClient`,
   `IAuthenticatedActorProfileAccessClient`, `IParticipantEligibleTeamsClient`).

### B) identity-access-service — NO CHANGE

`GetParticipantEligibleTeams` already returns the right semantics. The `participant-membership-access`
endpoint becomes unused by both callers but is harmless — leave it (optional later deprecation).

### C) mobile

5. **`mobile/src/app/(app)/team-lobby.tsx`** — drop the post-join `validate()` step. After `join()` returns
   `kind: 'joined'`, build reconnect context from local state
   (`buildReconnectContext({ liveSessionId, teamId: referenceTeamId, displayName })`), `saveReconnectContext`,
   then `router.replace('/(app)/team-space', { liveSessionId, teamId: referenceTeamId })` (drop the `reason`
   param — team-space never reads it). Remove `useMembershipAccess`, `accessStatus`, `getJoinOutcomeCopy`,
   and the membership branch of `banner`; `isJoining` becomes `joinStatus === 'joining'`. Keep the
   `team.referenceTeamId ?? team.teamId` call-site fallback.

6. **Remove dead mobile membership code:** `mobile/src/lib/membership/use-membership-access.ts`,
   `mobile/src/lib/api/membership.ts`, `mobile/src/lib/membership/membership-policy.ts` (also clears the
   stale "403" comments there — the denial was actually HTTP 200 `isAllowed=false`).

### D) Tests

- **Unit (session-ops):**
  - `RuntimeParticipationGuardTests.cs` — swap mock to `IParticipantEligibleTeamsClient` (`IsEligible`
    true/false); keep block/evict/idempotent assertions; new signature.
  - `EvidenceIntakeValidationChainTests.cs` — construct `RuntimeParticipationLink(guard, membershipChecker)`;
    **add** eligible-but-foreign-team → `ForbiddenAccessException`, and eligible+member → passes.
  - Update `ReconnectAuthenticatedParticipantCommandHandlerTests`, `GetParticipantTeamBoardQueryHandlerTests`,
    `GetParticipantSessionTimerSnapshotQueryHandlerTests`, `SubmitTriviaAnswerCommandHandlerTests` for the new
    guard mock/signature; add an eligible-but-unassigned (session-ops `TeamMember`, no reference membership)
    passes-and-plays case.
- **Integration (session-ops):** in `SessionOperationsApiWebApplicationFactory.cs` replace the
  `FakeParticipantMembershipAccessClient` override with a `FakeParticipantEligibleTeamsClient` (settable
  `IsEligible`); flip endpoint tests that drove `IsAllowed=false` to `IsEligible=false`; add a foreign-team
  answer-submit → 403 case (exercises change #3). Remove the orphaned
  `Identity/ParticipantMembershipAccessClientTests.cs`.
- **Mobile:** delete `membership-policy.test.ts`; add/adjust a team-lobby test — successful join saves
  reconnect context with `teamId === referenceTeamId`, navigates to team-space, and makes **no**
  membership-validate call.
- **Coverage gate:** `backend/scripts/cover-gate.sh` enforces at least 95% aggregate branch coverage; the new
  eligibility-deny and checker-deny branches each need a covering test (included above).

## Verification (end-to-end)

1. `backend/scripts/seed-all.sh`; confirm `participant@umbral.local` has no `RegisteredTeamMembership` and
   SMOKE1 is `Scheduled` with attached teams.
2. Bring up session-ops + identity-access + mobile. Sign in as `participant@umbral.local`, open SMOKE1
   lobby → all attached teams show `joinable`.
3. Tap a team → immediate navigation to team-space, **no** `participant-membership-access` request in the
   network log; board + timer load (previously 403).
4. Advance session to Active; submit a trivia answer → accepted, attributed to the joined team.
5. Kill/relaunch app → reconnect restores into the same team (reference id round-trips via reconnect context).
6. **Negative:** craft an answer-submit with a different team's reference id → 403 (change #3).
7. **Negative:** deactivate the participant in identity-access → any gameplay path applies the Participation
   Block + eviction (eligibility deny still works).

## Open decisions folded in

- Guard signature simplified to `(liveSessionId, ct)` (cleaner; slightly larger diff).
- Identity `participant-membership-access` endpoint left in place; only the session-ops client is deleted.

## Related docs

- `backend/services/session-operations-service/CONTEXT.md` — Open Team Selection, Participation Block,
  Admission Ownership.
- `backend/docs/prd/DES-69-participant-membership-validation.md` — conditional self-select supersession.
- `backend/docs/participant-lobby-join-handoff-2026-07-08.md` — lobby/self-join move to session-ops;
  "a session pick never writes a `RegisteredTeamMembership`".
- `backend/docs/users-realignment-decisions-2026-07-06.md` — the realignment decisions.

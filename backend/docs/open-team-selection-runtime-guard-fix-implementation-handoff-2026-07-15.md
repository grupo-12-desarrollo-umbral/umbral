# Handoff — Open Team Selection runtime guard implementation (2026-07-15)

## Purpose

This is the implementation-session continuation note for
`backend/docs/open-team-selection-runtime-guard-fix-handoff-2026-07-15.md`.
Read that handoff for the problem analysis, architecture decision, required changes, and manual
end-to-end scenarios; this document records only what changed during implementation and what remains.

## Current status

Implementation is complete in `session-operations-service` and `mobile`. Identity Access was not
changed. Backend compilation and all session-operations tests passed under the historical gate. The
current branch-coverage gate must be rerun. The focused mobile regression and targeted lint passed.

**The core manual end-to-end scenarios have now been run against the local docker stack and pass** — see
"Manual end-to-end verification" below. The unassigned participant plays, and the authorized-set gate
returns 403 on both routes into a team. What remains unrun is the wider negative/gameplay list from the
original handoff (foreign-reference-team answer 403, target scans, reconnect mid-session).

A later review of this implementation found and closed one gap the original handoff's local team-binding
audit missed: reconnect's **first-join** branch bypassed the eligible-teams authorized set. See
"Post-implementation review fix" below. That work is included in the current numbers.

That same manual session surfaced a **pre-existing bug unrelated to this HU** — rejoining a team you
previously left returned 500 — plus two dev-loop traps that silently invalidate manual testing. Both are
recorded below under "Adjacent findings"; neither is part of this change.

No commit was created. The worktree already contained substantial unrelated backend, frontend, and
mobile work before this implementation. Preserve it and use a path-aware diff before staging.

## Implemented

- `RuntimeParticipationGuard` now consumes `IParticipantEligibleTeamsClient.GetAsync()` and allows on
  `IsEligible=true`. Eligibility denial still applies the existing Participation Block, persists it,
  evicts the participant, and throws `ForbiddenAccessException`.
- `IRuntimeParticipationGuard.EnsureAllowedAsync` is now `(liveSessionId, cancellationToken)`, with all
  reconnect, board, timer, and evidence-intake callers/tests updated. (It later gained a
  `Task<ParticipantEligibleTeamsDto>` return so reconnect can reuse the whitelist — see the
  post-implementation review fix below.)
- `RuntimeParticipationLink` now also uses `IParticipantSessionMembershipChecker` after the eligibility
  guard. Foreign-team trivia and target-scan submissions are therefore rejected locally.
- The session-operations membership-access HTTP adapter, port, decision DTO, DI registration, adapter
  integration tests, and integration fake were removed. The shared
  `ParticipantMembershipAccessClientOptions` type remains intentionally because the other Identity
  clients still use its configuration section/base address.
- The integration factory now overrides `IParticipantEligibleTeamsClient` with
  `FakeParticipantEligibleTeamsClient`, exposing `IsEligible`, `ReasonCode`, and `Teams`.
- Existing evidence/trivia integration fixtures now distinguish runtime team IDs from reference team
  IDs: participant requests carry the reference ID while persisted entities/responses continue to be
  asserted with runtime IDs where applicable.
- `mobile/src/app/(app)/team-lobby.tsx` no longer performs post-join membership validation. Successful
  self-join immediately persists reconnect context and navigates using the lobby card's reference team
  ID, without the unused `reason` route parameter. The existing
  `referenceTeamId ?? teamId` fallback remains.
- Dead mobile membership API/hook/policy code and its policy test were removed. A new lobby screen test
  verifies runtime-ID join plus reference-ID persistence/navigation.

Use `git diff` for the exact code changes rather than reconstructing them from this summary.

## Post-implementation review fix — reconnect authorized-set bypass

### The gap

The original handoff's "local team-binding audit" cleared reconnect on the grounds that
`JoinPolicy.EnsureCanReconnect` throws `ParticipantAssignedToDifferentTeamException`. That is true only for
the **existing-participant** branch. `LiveSession.AdmitParticipant` has a second branch: when the caller is
not yet a participant, it is a **first join**, and it ran only `JoinPolicy.EnsureCanJoin` — session state,
join status, capacity. It never consulted the authorized team set.

Because the new guard admits on `IsEligible` alone and discarded `whitelist.Teams`, an eligible participant
whitelisted for Team A could `POST /api/sessions/{id}/participants/reconnect` (participant-authorized,
arbitrary `request.TeamId`) with Team B during `Scheduled`/`Preparing` and obtain a Team B membership —
bypassing the `TeamNotInAuthorizedSetException` that `SelectTeam` raises. The old membership-access guard
had blocked this, since the caller held no `RegisteredTeamMembership` for Team B.

Scope: this never affected the unassigned participant this fix targets (an empty whitelist means "every
attached team" anyway, so reconnect-joining was already equivalent to self-join). It was an authorized-set
bypass for an *assigned* participant, pre-start only. Gameplay attribution and the Participation Block were
never affected.

### The fix

Both routes into a team now share one authority, mirroring `SelectTeamCommandHandler`:

- `LiveSession.AdmitParticipant` gates its first-join branch through
  `OpenTeamSelectionPolicy.EnsureCanSelfAssign`, **after** `EnsureCanJoin`. That order is deliberate: state
  and capacity keep reporting late-join and team-full ahead of any whitelist verdict, so existing status
  codes are unchanged.
- `IRuntimeParticipationGuard.EnsureAllowedAsync` now returns the `ParticipantEligibleTeamsDto` it already
  fetched, so reconnect reuses the access fact instead of making a second identity-access round-trip. The
  other three call sites ignore the return value.
- `ReconnectAuthenticatedParticipantCommandHandler` injects `OpenTeamSelectionPolicy` and passes the
  whitelist through.
- Corrected a stale comment in `SubmitTriviaAnswerCommandHandler` claiming `RuntimeParticipationLink`
  "never" authorizes by caller identity — it does now, via the membership checker.

The two new `AdmitParticipant` parameters are **optional**, which kept ~50 existing test call sites
untouched. This is not fail-open by accident: an empty/omitted set means "unassigned → every attached team
is selectable", exactly as `ParticipantEligibleTeamsDto` and `OpenTeamSelectionPolicy` already document, and
the single production caller always supplies the real set.

### Tests added (11)

- Domain (`LiveSessionTests`): first join outside the authorized set throws and persists nothing; first join
  into a whitelisted team creates the membership; first join with an empty set (unassigned) creates the
  membership; first join with the policy supplied but the set omitted is likewise treated as unassigned
  (added to cover the `?? new HashSet<Guid>()` fallback — see the coverage gate below); a returning
  participant still reconnects to their assigned team even when the whitelist no longer lists it (a changed
  whitelist must not strand someone mid-session).
- Application (`ReconnectAuthenticatedParticipantCommandHandlerTests`): the same three first-join cases at
  handler level, asserting no repository write on denial.
- Integration (`ReconnectParticipantEndpointTests`): first join into a non-whitelisted team returns **403**
  where the team is attached, open and has room, so the authorized set is the only thing standing in the
  way; whitelisted-team first join returns 200; unassigned first join returns 200.

**Non-vacuity:** a true mutation check (disabling the gate to watch the tests fail) was **not** run — the
permission classifier correctly blocked weakening an auth check in production code. Assurance is structural
instead: the throwing test and the admitting test are identical except for the authorized set, so both
passing proves the whitelist is the discriminator. Run the mutation check explicitly if you want stronger
proof.

This gate has since been **exercised against the real stack** (see "Manual end-to-end verification"): the
same participant is refused DV-SN2 with 403 and admitted to DV-SOON with 200, differing only in the
whitelist, with no rows written on the denial. That is independent of the test suite and corroborates the
structural argument above, though it still is not a mutation check.

## Manual end-to-end verification — RUN 2026-07-15, PASSING

Run against the local `docker compose` dev stack after `backend/scripts/seed-all.sh`, driving the real
gateway on `:8000` with Keycloak tokens. **Read the dev-loop traps in "Adjacent findings" before trusting
any manual run** — the first attempt of this session tested a 1h52m-old process and would have reported
whatever the pre-edit code did.

Fixture facts worth knowing (they make SMOKE1 the natural bed for this HU):

- `seed-all.sh` assigns only `participant01`–`participant08` to teams (the loop filters on
  `participant0[1-8]`). Bare **`participant@umbral.local` is whitelisted for nothing** — it is the
  unassigned participant this HU targets, with no setup required.
- SMOKE1 is `Scheduled` (open for selection) with two attached teams, `DV-SOON` and `DV-SN2`, and
  **neither has any registered member**, so every participant is "unassigned" against it.
- Self-join is `POST /api/sessions/by-code/{code}/teams/{runtimeTeamId}/join`.

| Scenario | Route | Result |
| --- | --- | --- |
| Unassigned participant first-joins an attached team | self-join | **200** |
| Participant whitelisted for DV-SOON joins DV-SN2 | self-join | **403** `team-not-in-authorized-set` |
| Same participant joins DV-SOON | self-join | **200** |
| Whitelisted for DV-SOON, first-join into DV-SN2 while `Scheduled` | **reconnect** | **403**, and **no participant/membership row created** |
| Whitelisted for DV-SOON, first-join into DV-SOON | **reconnect** | **200**, `isReconnect: false` |

The last two are the scenario the "Remaining work" list asked to add, and they are the ones that matter:
they exercise the post-implementation review fix itself, because the new `EnsureCanSelfAssign` gate lives
on `AdmitParticipant`'s first-join branch. `isReconnect: false` in the 200 response is the evidence that
the first-join branch — not the existing-participant branch — is what ran. The self-join 403 above does
**not** test the review fix; `SelectTeamCommandHandler` already had that gate.

One correction to the original wording: that scenario says to post "a different attached team's
*reference* id". The reconnect endpoint takes the **runtime** team id (`AdmitParticipant` resolves it via
`GetTeam(teamId)`); only the whitelist it is checked against holds reference ids.

`GET /api/permissions/participant-eligible-teams` returning 200 in the service logs confirms the new
client is the one on the path.

Still unrun from the original handoff's list: foreign-reference-team answer 403, target-scan submissions,
and reconnect for an already-assigned participant mid-session.

## Verification completed

Backend commands required disabling SourceLink generation because the host otherwise failed while
writing `*.sourcelink.json`:

```text
env GenerateSourceLinkFile=false EnableSourceControlManagerQueries=false \
  make -C backend build SVC=session-operations-service
```

Result: build passed, including structure and layer guards plus API, application-unit, domain-unit, and
integration test-project compilation.

```text
env GenerateSourceLinkFile=false EnableSourceControlManagerQueries=false \
  make -C backend test SVC=session-operations-service
```

Result: 1,181 tests passed, 0 failed:

- Application unit: 394
- Domain unit: 395
- Integration: 392

(A later run of the same target with the separate rejoin fix in the tree reports **1,184** — 394/395/395 —
still 0 failed. The extra 3 are `SelectTeamEndpointTests`; see "Adjacent findings".)

(1,170 at the end of the original implementation session — 391/390/389 — plus the 11 tests added by the
post-implementation review fix. No regressions; the pre-existing
`Reconnect_FirstJoinIntoActiveSession_ReturnsForbiddenLateJoin` and
`Reconnect_FirstJoinWhenTeamFull_ReturnsConflict` still pass, confirming the gate ordering preserved their
status codes.)

### Coverage gate — RUN, GREEN

The ADR-0005 gate has now been run (driver-authorized; it was skipped in the original implementation
session). Use the Makefile target, not `cover-gate.sh` directly — it discovers the project set and puts the
integration project last:

```text
env GenerateSourceLinkFile=false EnableSourceControlManagerQueries=false \
  make -C backend gate SVC=session-operations-service
```

Result at the time: **historical gate green; current branch-coverage gate requires a rerun.**

- Line coverage: 98%
- Historical branch coverage was below the current threshold.

Report: `services/session-operations-service/coverage/gate/index.html`.

A later rerun, *including* the separate rejoin fix and its 3 tests, was green under the historical
threshold but is below the current branch requirement. Rerun the current tree. Note the console
summary prints an unweighted per-module
`Average` (92.89% branch) that is **not** what the gate enforces — `cover-gate.sh` passes
`/p:Threshold /p:ThresholdType=branch` to Coverlet, which gates the merged branch total. Read
`coverage/gate/merged.cobertura.xml` (`branch-rate`) for the real figure rather than the Average row.

Two things worth carrying forward:

- **Branch coverage requires additional tests.** The historical run is below the current gate. This is pre-existing repo debt, not
  something this work introduced — the change added branches *with* tests and nudged the number up. But the
  next person to add defensive code without tests will trip it and it will look like their fault.
- The first gate run showed the new `AdmitParticipant` authorized-set line at **75% (3/4)** condition
  coverage. The uncovered path was policy-supplied-with-set-omitted, reachable only through the
  `?? new HashSet<Guid>()` fallback and produced by no production caller. A domain test now pins that
  documented default, taking the line to **100% (4/4)**. The numbers above are from the re-run *after* that
  test — `cover-gate.sh` requires the demonstrated number to come from the same run as the gated one, so do
  not quote a number from a run that predates a test change.

Mobile verification:

```text
npm test -- --runInBand src/__tests__/team-lobby-screen.test.tsx
npm exec eslint -- 'src/app/(app)/team-lobby.tsx' \
  src/__tests__/team-lobby-screen.test.tsx src/lib/api/teams.ts
```

Both passed.

The full mobile suite ran 309 tests: 308 passed. The sole failure was the pre-existing
`src/__tests__/ranking-hook.test.ts` score-drop animation/timer failure (`Animated.parallel` undefined),
along with asynchronous warnings from existing team-space tests. The new lobby test passed within the
full run.

`npm exec tsc -- --noEmit` is not a usable clean gate in the current checkout: it reports the existing
missing `react-test-renderer` type declarations and downstream implicit-`any` errors across many older
test files, including files unrelated to this change.

`git diff --check -- backend/services/session-operations-service mobile/src` passed, as did the final
grep for removed membership-access types/modules. Hits for
`ParticipantMembershipAccessClientOptions` are expected and must remain.

## Adjacent findings (NOT part of this change)

### Rejoin bug — fixed separately, do not attribute it to this HU

Manual testing hit a 500 that looks like a runtime-guard failure and is not one. Rejoining a team you
previously left violated the unique index on `live_session_team_members`:

- A membership row is **per stint**: `Team.ReleaseParticipant` marks the row `Removed` (keeping `left_at`)
  and `Team.AssignParticipant` adds a fresh one, because it only looks for an **active** member.
- The index was `UNIQUE (team_id, session_participant_id)` with no filter, permitting one row per pair
  *ever*, so the second stint raised `23505` and the endpoint returned 500.
- Repro: join A → 200, switch A→B → 200, switch **back** to A → 500.

Fixed by filtering the index to `WHERE membership_status = 'Active'` (migration
`FilterTeamMemberUniqueIndexToActive`), which matches the invariant the code already enforces — every
member lookup in the codebase (`AssignParticipant`, `ReleaseParticipant`, `ActiveMemberCount`,
`FindAssignedTeam`, `ParticipantSessionMembershipChecker`, `SelectTeamCommandHandler`) filters on
`IsActive`. No call sites changed.

It is **pre-existing and independent**: `Team.cs` is unmodified on this branch and HEAD already contains
the `IsActive` filter. It escaped review because there was **no integration test for the self-join
endpoint at all**, and only the Testcontainers suite can catch it — the in-memory domain tests that cover
switching have no unique index to violate. `tests/IntegrationTests/Api/SelectTeamEndpointTests.cs` now
covers it (3 tests), mutation-checked by stripping the filter and confirming the two rejoin tests fail
with the original 500.

Keep this off the HU-24b branch — see "Remaining work" item 6.

### Dev-loop traps that invalidate manual testing

Both cost real time this session; neither is visible unless you look for it.

1. **`dotnet watch` can silently stop reloading.** `docker-compose.override.yml` bind-mounts `src/` and
   runs `dotnet watch run`, so edits are expected to hot-reload. session-operations had instead crashed
   **10 times** with `Failed to bind to address http://[::]:8080: address already in use` →
   `Exited with error code 134` → `Waiting for a file to change...`. An orphaned process kept the port, so
   every rebuild died and the container kept serving a **1h52m-old process**: the edits compiled (DLLs
   restamped) but were never loaded. Nothing surfaces this — the service answers requests normally.
   Check before trusting a manual result, and `docker compose restart session-operations-service` to fix:

   ```text
   docker exec backend-session-operations-service-1 ps -eo etime,cmd | grep bin/Debug
   ```

   If that uptime predates your edit, you are testing old code.

2. **Host and container fight over `obj/`.** The container restores with `NUGET_PACKAGES=/tmp/nuget`, so
   `project.assets.json` records `packageFolders: ['/tmp/nuget']` and host `dotnet ef` / `dotnet build`
   fails with `NETSDK1064: Package ... was not found`. A host `dotnet restore` fixes it, but the running
   container rewrites it back within seconds. **Stop the service before host-side migration/test work**,
   then start it again (it re-restores on boot, ~30s):

   ```text
   docker compose stop session-operations-service
   # ... dotnet restore / make ef / make test / make gate ...
   docker compose start session-operations-service
   ```

## Remaining work / recommended next session

1. The path-scoped diff has now been reviewed against
   `backend/docs/open-team-selection-runtime-guard-fix-handoff-2026-07-15.md`; that review produced the
   authorized-set fix above. Any further review still needs to avoid absorbing unrelated dirty-worktree
   changes.
2. Run the current coverage gate again. The historical result no longer satisfies the active branch
   threshold; see "Coverage gate" above before adding untested defensive code.
3. ~~Run the manual end-to-end and negative scenarios~~ — **the core ones are done and PASS**, including
   the added reconnect authorized-set scenario. See "Manual end-to-end verification" above. What is left
   is the wider negative/gameplay list: foreign-reference-team answer 403, target-scan submissions, and
   reconnect for an already-assigned participant mid-session.
4. Decide whether the unrelated mobile `ranking-hook.test.ts` failure and repository-wide TypeScript
   baseline should be repaired separately. They should not be conflated with this runtime-guard fix.
5. Consider whether first-join-via-reconnect should remain supported at all, or whether reconnect should
   only ever *re*-connect an existing participant and leave membership creation to `SelectTeam`. That is a
   product decision deliberately left out of scope here; the authorized-set gate makes the current
   behaviour safe either way.
6. Stage only the intended runtime-guard/mobile changes and commit when explicitly requested. **The
   worktree now holds two unrelated changes** on top of the pre-existing dirty work — keep them apart.
   The rejoin fix ("Adjacent findings") is confined to these files and belongs on its own branch off
   `develop`, not on HU-24b:

   ```text
   src/Infrastructure/Persistence/Configurations/LiveSessionConfiguration.cs   (index filter only)
   src/Infrastructure/Migrations/20260716030328_FilterTeamMemberUniqueIndexToActive.cs
   src/Infrastructure/Migrations/20260716030328_FilterTeamMemberUniqueIndexToActive.Designer.cs
   src/Infrastructure/Migrations/ApplicationDbContextModelSnapshot.cs
   tests/IntegrationTests/Api/SelectTeamEndpointTests.cs
   ```

   Note `ApplicationDbContextModelSnapshot.cs` is shared migration state: if HU-24b is committed first
   and the rejoin fix lands later, its migration must be regenerated on top rather than cherry-picked.

## Suggested skills

- `review` — compare the intended implementation against the original handoff while separating the
  unrelated dirty-worktree changes.
- `diagnose` — only if the next session is explicitly tasked with the unrelated mobile ranking/timer or
  TypeScript baseline failures.
- `commit-work` — when asked to stage and commit the completed implementation safely.

## Important repository instructions

Before continuing, read the root `AGENTS.md`, `backend/AGENTS.md`,
`backend/.agents/backend-agent.md`, and `mobile/AGENTS.md`. Mobile work requires consulting the exact Expo
SDK 56 documentation. Do not modify Identity Access for this fix, and do not delete
`ParticipantMembershipAccessClientOptions`.

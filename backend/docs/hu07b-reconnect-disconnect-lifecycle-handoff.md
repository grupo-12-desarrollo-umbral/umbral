# Handoff — HU-07B Reconnect: fix missing disconnect lifecycle + harden error contract

**Branch:** `feature/hu-07b-participant-reconnection` (base: `develop`)
**Tip commit at handoff:** `241319c feat(hu-07b): participant reconnection`
**Working tree:** dirty — an in-progress capacity refactor is uncommitted (see "Working-tree state"). The fix below is **not started**.

## Why this session exists

User reported in the Expo app (`mobile/`): entering a team (whether they had **already joined**
or **never joined**) shows **"Something went wrong restoring your team space."** In the never-joined
case, tapping the team shows the error, tapping "Try again" goes back to the home screen.
Diagnosis traced it to a structural backend defect, not a copy/mapping bug.

## Root-cause diagnosis (confirmed)

The resume/reconnect path **can never succeed in the real app** because nothing ever marks a
participant as *disconnected*.

1. First `team-space` entry → hub `ReconnectAsync` → `LiveSession.AdmitParticipant`
   (`backend/services/session-operations-service/src/Domain/Entities/LiveSession.cs:127`). No
   participant exists → creates one, marks **active**, returns `isReconnect: false`. ✅
2. Connection drops (background/kill/leave/re-enter) but **nothing marks the participant
   disconnected**. `SessionsHub`
   (`backend/services/session-operations-service/src/Api/Hubs/SessionsHub.cs`) has **no
   `OnDisconnectedAsync`**. `LiveSession.DisconnectParticipant` (`LiveSession.cs:164`) has **zero
   production callers** — only tests call it.
3. Re-entry → `AdmitParticipant` finds the still-**active** participant → `JoinPolicy.EnsureCanReconnect`
   (`backend/services/session-operations-service/src/Domain/Services/JoinPolicy.cs:30`) hits
   `if (!participant.IsDisconnected) throw new ParticipantAlreadyConnectedException(...)`. So
   re-entry **always** throws.

Happy-path hub test only passes because it manually seeds the disconnect:
`tests/IntegrationTests/Api/ReconnectParticipantHubTests.cs:152`
(`session.DisconnectParticipant(...)`). The app has no equivalent.

### Why the *generic* message specifically
`mobile/src/lib/realtime/reconnect-policy.ts` classifies rejections by string-matching the server's
exception text (e.g. `'is already connected'` → `lost-access`). That only works when SignalR sends
the real exception message — i.e. `EnableDetailedErrors`, tied to `IsDevelopment()` at
`backend/services/session-operations-service/src/Api/DependencyInjection.cs:19`. The way the user
runs the backend, detailed errors are **off**, so the client gets the masked
`"An unexpected error occurred invoking 'ReconnectAsync' on the server."`, which matches no pattern
→ `kind: 'error'` → the generic screen copy at `mobile/src/app/(app)/team-space.tsx:56`.

Two layered defects:
- **Root cause:** missing disconnect lifecycle → reconnect structurally impossible.
- **Fragility:** client rejection classification depends on an environment-dependent error string.

## Approved fix (user decisions)

1. **Root fix → Add `OnDisconnectedAsync`.** Hub stores `participantId` (and `liveSessionId`) in
   `Context.Items` during `ReconnectAsync`, and on connection drop marks the participant
   disconnected. Needs a new application command + handler + repository update (there is currently
   **no** application-layer disconnect command — only the domain method `LiveSession.DisconnectParticipant`).
   This makes resume-after-kill work (test-matrix scenario 1 in
   `mobile/docs/hu-07b-mobile-test-workflow.md`).
2. **Error contract → Add a hub filter.** Add an `IHubFilter` that converts domain exceptions into
   stable `HubException` messages/codes regardless of environment, so `reconnect-policy.ts` keys off
   a reliable contract instead of human-readable, env-dependent strings. Registered via
   `AddSignalR(...).AddHubOptions` / filter registration near `DependencyInjection.cs:19`.

## Implementation notes / watch-outs

- `SessionsHub` is `[Authorize(Policy = Participant)]` and resolves identity via
  `CurrentUserContext` from `Context.User`. `OnDisconnectedAsync` runs **without** an active invoke
  context — capture what you need (participant id) into `Context.Items` during `ReconnectAsync`
  rather than re-deriving it on disconnect.
- The aggregate disconnect method is `LiveSession.DisconnectParticipant(participantId, occurredAt)`
  → `participant.Disconnect(occurredAt)`. Use the injected `TimeProvider` for `occurredAt` (the
  service already does: `ReconnectAuthenticatedParticipantService.cs`).
- Mirror the existing command shape under
  `src/Application/Sessions/Commands/ReconnectAuthenticatedParticipant/` when adding the new
  disconnect command (executor interface + service + validator + MediatR command), and persist via
  `ILiveSessionRepository.UpdateAsync`.
- If the hub filter introduces **stable error codes**, update the message-matching in
  `mobile/src/lib/realtime/reconnect-policy.ts` (`interpretHubError`, the `isLostAccess` /
  `isNetworkError` / `isUnauthorized` helpers) to read the new contract, and update
  `mobile/src/__tests__/reconnect-policy.test.ts`.
- After the lifecycle fix, the `ParticipantAlreadyConnectedException` → `lost-access` mapping is
  still the right behavior for a genuine **concurrent second connection** (e.g. two devices); don't
  delete it.

## Working-tree state (uncommitted, unrelated to this fix)

An in-progress **team-capacity refactor** is dirty across backend + mobile: `Team` now owns
`Capacity` (`Team.cs`), `JoinPolicy.EnsureCanJoin` no longer takes `teamCapacity`
(`JoinPolicy.cs`), `LiveSession.RegisterTeam`/`AdmitParticipant` signatures changed, and the mobile
reconnect contract dropped `teamCapacity` (`sessions-hub-types.ts`, `use-reconnect.ts`,
`team-space.tsx`, `team-lobby.tsx`, plus migrations and tests). See `git diff HEAD` for the full
set. Decide whether to commit this refactor before starting the disconnect-lifecycle work to keep
the diffs separable.

## Verify after implementing

- Backend tests: `make -C backend ...` (sandbox excludes `make`/`dotnet`/`docker`; run on host —
  see auto-memory `sandbox-toolchain.md`).
- On-device matrix: `mobile/docs/hu-07b-mobile-test-workflow.md` — especially **scenario 1** (resume
  after kill → "Live session resumed", `isReconnect: true`) and **scenario 8** (transient WS drop →
  "Reconnecting…" banner, not kicked out). Hub smoke: `npm run smoke:reconnect:hub` from `mobile/`.

## Suggested skills

- `tdd` — drive the new disconnect command + hub filter test-first.
- `diagnose` — if the disconnect doesn't fire as expected at runtime.
- `verify` / `run` — exercise scenarios 1 and 8 on device against the live stack.
- `code-review` — review the branch diff before PR to `develop`.

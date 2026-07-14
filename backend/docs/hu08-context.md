# HU-08 Context — Sincronización multi-dispositivo del equipo

> Paste this section into any agent session that needs context for HU-08.
> Last updated: 2026-07-13 | Branch: `feature/hu-08-multi-device-team-sync`

## State

- DES-13 (HU-08): **`Todo`**, **ungated** — reconstructed from local docs (`workflow_refactor.md` re-validated 2026-07-09 row 19; `sprint_1_final_tickets.md`; `parallel-work-lanes.md` 2026-07-12). Labels expected `Feature`, `svc:session-operations-service`, `ready-for-agent` — **no Linear MCP was available when this file was generated; verify all three in Linear before driving.**
- **Resolved mode: feature flow → refined to VERIFICATION.** DES-13 is not `canon-realign`/`needs-rebuild`; it is a plain-flow ticket. Resolution step 6 found the multi-device contract is **already satisfied by shipped predecessor code** (see Per-phase derivation), so the four phases *verify + lock with tests*, they do not build new types. No supersession handling applied — DES-13 is the **survivor** of the DES-13/DES-59 duplicate (`ab-ticket-merge-validation-handoff-2026-07-09.md`: DES-13 `blocks` DES-59; the re-validated order runs DES-13 → DES-59), **not** in any superseded column.
- **Service resolution (non-obvious):** `sprint_1_final_tickets.md` and `required_patterns_matrix.md` file HU-08 under `identity`, but the buildable slice lives in **`session-operations-service`** (Lane A). `DES-67` (identity PRD) **explicitly disclaims** it: §non-goals lists "diseñar sincronización multi-dispositivo completa dentro de Identity" as out of scope, §223 calls HU-08 "una capacidad de sincronización del **runtime**", §89 keeps join/reconnect/sync with SessionOperations. Same re-scope pattern as HU-07B (identity → session-operations). Confirmed by human at generation time.
- Predecessors (build-on): **HU-07B** (`session-operations` reconnect/presence/`JoinPolicy`/`ConnectionTracker`) **Done**; **HU-23 / DES-31** (live team board broadcast) **Done** (PR #178). Untouched-by-this-HU landed surface: #91 participation-block, HU-34 (`TeamAnswered` operator group), HU-21/22/24 live-session broadcasters — see below.
- PRD: **DES-70** (`svc:session-operations-service`, session-ops runtime authority). ⚠️ DES-70 enumerates HU-15–HU-36 and **does not name HU-08**; the runtime-sync authority for HU-08 is DES-70 **+ canon** `bd_umbral_entity_spec.md §SessionParticipant`, with DES-67 explicitly delegating the sync here. Scope-gap noted in the prompt rationale (constraint 3).
- Branch: `feature/hu-08-multi-device-team-sync`, base = **`develop`** (all predecessors merged; none In Progress).

## Required design patterns

**None mandated.** `required_patterns_matrix.md` tags HU-08 as "— (no mandated pattern) … real-time enabler (SignalR)". SignalR is the transport, **not** a design-pattern gate.

- HU-08 is **not** in the mandated-`Proxy` set (HU-01/02/03/06/07A/07B/19/20/36A) and **not** in the applies-where set (HU-04/05/36B). Reconnect/hub access is already guarded by the **existing** inherited guard — `SessionsHub` carries `[Authorize(Policy = ParticipantOrOperator)]`, the application flow runs through `RuntimeParticipationGuard` + `AuthorizationBehaviour`. This is the standard gateway/authorization guard the endpoints inherit anyway (ADR-0001/0002); **no new pattern gate** for this HU.

## What predecessors have already landed

All of this is on `develop`. This is the surface HU-08 **verifies** — it is the reason there is no new build.

**Build-on: HU-07B — reconnect, presence, multi-connection tracking**
- `SessionParticipant` presence aggregate-child: `ParticipantStatus` {`Joined`,`Active`,`Disconnected`,`Removed`,`Blocked`}, `LastSeenAt` heartbeat, `MarkActive` / `RefreshPresence` / `Disconnect` / `Block` / `Remove`.
- `LiveSession.AdmitParticipant(...)` — first-join branch (`Join` + `MarkActive`, `IsReconnect=false`) vs **returning-identity branch** (`RefreshPresence`, idempotent, `IsReconnect=true`).
- `JoinPolicy.EnsureCanReconnect(...)` — team-mismatch → `ParticipantAssignedToDifferentTeamException`; `Removed` → `ParticipantRemovedFromSessionException`; `Finished`/`Cancelled` → `LateJoinNotAllowedException`.
- Application: `ReconnectAuthenticatedParticipantCommand(+Handler+Validator)`, `DisconnectParticipantCommand(+Handler)`, `RuntimeParticipationGuard`, `IRuntimeParticipationGuard`.
- Api: `SessionsHub` (`ReconnectAsync`, `OnDisconnectedAsync`), **`ConnectionTracker`** — counts N connections per `sessionParticipantId`; `OnDisconnectedAsync` sends `DisconnectParticipantCommand` **only when the last connection drops** (`hasRemainingConnections == false`).
- Persistence: migration `20260603145000_AddRuntimeRecoveryState` persists `ParticipantStatus` + `LastSeenAt`; `LiveSessionConfiguration`, `LiveSessionRepository`.

**Build-on: HU-23 / DES-31 — live team board broadcast**
- `SignalRTeamBoardBroadcaster : ITeamBoardBroadcaster` → pushes `ParticipantTeamBoardDto` (`TeamBoardUpdated`) to the **`team:{teamId}`** group only.
- `BroadcastTeamBoardNotificationHandler` (event handler) → invokes the broadcaster on the team-board domain event.
- `ParticipantTeamBoardDtoFactory`, `ParticipantTeamBoardSnapshot`, `GetParticipantTeamBoardQuery` — team-scoped score/progress snapshot (hydration + broadcast payload).

**Landed, untouched by this HU** (one-line notes — do not verify as HU-08 scope):
- #91 participation-block — `ParticipantBlockNotifier` → `participant:{id}` group eviction.
- HU-34 — `SignalRTeamAnsweredBroadcaster` operator-only group `live-session-operators:{id}`.
- HU-21/22/24 — `SessionStateBroadcaster`, `SignalRSessionTimerBroadcaster`, `SignalRSessionQuestionBroadcaster` → `live-session:{id}` group (reach every participant device joined to the session).

**Coverage:** `session-operations-service` is under the ADR-0005 `cover-gate.sh` bar (line **and** branch). X.4 must keep the merged suite at or above the enforced threshold; the new multi-device test only adds coverage.

## What this HU adds

HU-08 adds **no new domain/application/infrastructure types and no new endpoints.** It closes the verification + regression-lock gap: the multi-device contract is emergent from three shipped slices and is only tested *per-mechanism* today, never *end-to-end*.

| Concern | New work |
|---|---|
| Multi-device sync (AC1/AC2) | **Verify** every authorized device of the same participant joins `team:{teamId}` + `live-session:{id}` and receives team-board + session broadcasts; **lock** with an end-to-end test. |
| Reconnect state restore (AC3) | **Verify** `AdmitParticipant` returning-identity branch is idempotent (`RefreshPresence`, `IsReconnect=true`) and the reconnect result carries current `SessionState` + timer for hydration; **add** a hydration assertion. |
| Team isolation (AC4) | **Verify** `SignalRTeamBoardBroadcaster` targets only `team:{teamId}`; **lock** a cross-team negative test (a second team's device never receives the first team's board). |
| Last-device presence | **Verify** `ConnectionTracker` keeps the participant present until the last connection drops (only then `DisconnectParticipantCommand`); **lock** with a test. |
| Regression safety | The one genuine deliverable: a single `MultiDeviceTeamSyncHubTests` that asserts AC1–AC4 together, so a future hub/broadcaster/tracker refactor cannot silently break multi-device. |

## Touched surfaces

- `backend/services/session-operations-service` — **tests only** across Domain / Application / Persistence / Api (product code verified, not modified, unless a phase gate exposes a real defect).
- No API contract change; no new endpoint. Existing contract boundary (SignalR hub `/hubs/sessions`, `GET /api/sessions/{id}/participants/team-board`, `POST /api/sessions/{id}/participants/reconnect`) is what the mobile/web client already consumes.
- Frontend: no new backend contract to consume — the multi-device behavior is client-transparent (reconnect + subscribe). Any frontend/mobile work is confirming resubscribe-on-reconnect against the unchanged contract.

## Committed phases

| Commit | Phase | Description |
|---|---|---|
| — | — | none yet |

## Known quirks / gotchas

- **This is a verification HU.** The default outcome of every phase is "behavior confirmed + test added", **not** new production types. If a subagent proposes a new command/entity/endpoint/migration, that is a red flag — stop and re-read the derivation. The multi-device sync is *emergent*, per canon (`bd_umbral_entity_spec.md:498`: `lastSeenAt` is the "operational heartbeat for multi-device visibility"; no per-device entity exists in canon).
- `ParticipantAlreadyConnectedException` (`SessionParticipant.MarkActive`) is a **first-join-only** guard. The reconnect / 2nd-device path routes through `RefreshPresence`, which is idempotent and never throws it. Do **not** "fix" multi-device by removing this exception — that would weaken the first-join invariant; the 2nd device already works.
- The `svc:` label conflict (identity vs session-operations) is resolved to **session-operations** (see State). Do not regenerate against the identity PRD.
- DES-70 does not name HU-08; do not treat the PRD's silence as "out of scope". The runtime-sync authority is DES-70 + canon `§SessionParticipant`; DES-67 delegated it here explicitly.
- **Open question (do not build by default):** the reconnect result (`ReconnectParticipantResultDto`) carries `SessionState` + `Timer` but **not** the current team-board snapshot — a reconnecting device pulls the board via `GET .../participants/team-board`. If product wants the board pushed inside the reconnect result too, that is a small additive enhancement — flag it, do not invent it inside a verification slice.
- If X.3's `ef migrations add` produces a non-empty migration, that is a **defect** — multi-device adds no column. Stop and reconcile rather than shipping a schema change.

## Per-phase derivation — authoritative for implementation

> Primary source for the phase subagent (per `backend-agent.md` "Read first").
> Derived from `bd_umbral_entity_spec.md §SessionParticipant` (lines 482–505),
> `ddd_solution_model.md` (HU-08 is listed under Identity HU-01–08 but carries **no
> dedicated design** — an emergent runtime capability), PRD `DES-70` (session-ops runtime
> authority; does not enumerate HU-08), and the shipped HU-07B / HU-23 code cited under
> "What predecessors have already landed".
> **Every phase is verify-and-lock: confirm the cited behavior, add the missing test, and
> only edit product code if a gate exposes a real defect.**

### Phase X.1 — Domain  *(verify + lock)*
**Derive** (`bd_umbral_entity_spec.md §SessionParticipant` 482–505 — `participantStatus`, `lastSeenAt` "operational heartbeat for multi-device visibility", "one `SessionParticipant` may be represented by one or more `TeamMember`"; i.e. one participant / many device connections, **no per-device entity**):
- `SessionParticipant` presence transitions — `RefreshPresence(seenAt)` idempotent → `Active` (never throws `ParticipantAlreadyConnectedException`); `MarkActive` throws only on the first-join double-activate path; `Disconnect`, `Block`, `Remove`.
- `LiveSession.AdmitParticipant(externalIdentityId, displayName, teamId, occurredAt, joinPolicy)` — returning-identity branch calls `RefreshPresence` and returns `IsReconnect=true` (the multi-device / reconnect path).
- `JoinPolicy.EnsureCanReconnect` — team-mismatch → `ParticipantAssignedToDifferentTeamException` (AC4); `Removed` → `ParticipantRemovedFromSessionException`; `Finished`/`Cancelled` → `LateJoinNotAllowedException` (AC3).

**Target files** (verify | create test — file to mirror):
- verify `Domain/Entities/SessionParticipant.cs`, `Domain/Entities/LiveSession.cs` (`AdmitParticipant`), `Domain/Services/JoinPolicy.cs`, `Domain/Enums/ParticipantStatus.cs`
- create `tests/UnitTests/Domain/Entities/LiveSessionMultiDeviceReconnectTests.cs` — mirror `tests/UnitTests/Domain/Entities/LiveSessionTeamBoardTests.cs`

**Pattern this phase owns:** none.
**Gate:** unit tests proving (a) a second `AdmitParticipant` for the same `externalIdentityId` is idempotent → `Active`, `IsReconnect=true`, **no** `ParticipantAlreadyConnectedException`; (b) reconnect requesting a different team → `ParticipantAssignedToDifferentTeamException`; (c) reconnect while `Removed`/`Finished`/`Cancelled` → rejected. **No new domain type.**

### Phase X.2 — Application  *(verify + lock)*
**Derive** (`ddd_solution_model.md §SessionOperations` application flow; shipped HU-07B/HU-23 slices):
- `ReconnectAuthenticatedParticipantCommandHandler` — guards via `RuntimeParticipationGuard.EnsureAllowedAsync`, calls idempotent `AdmitParticipant`, returns `ReconnectParticipantResultDto` carrying `SessionState`, `IsReconnect`, `JoinedAt`, `LastSeenAt`, **`Timer`** snapshot (AC3 hydration).
- `DisconnectParticipantCommandHandler` — sets `Disconnected`; invoked by the hub only on last-device drop (the N-connection decision is `ConnectionTracker`'s, X.4).
- `BroadcastTeamBoardNotificationHandler` → `ITeamBoardBroadcaster.BroadcastTeamBoardUpdatedAsync(ParticipantTeamBoardDto)` on the team-board domain event (AC1/AC2 — a change propagates to the team).

**Target files** (verify | extend test — file to mirror):
- verify `Application/Sessions/Commands/ReconnectAuthenticatedParticipant/*`, `Application/Sessions/Commands/DisconnectParticipant/*`, `Application/Sessions/EventHandlers/BroadcastTeamBoardNotificationHandler.cs`, `Application/Sessions/Common/RuntimeParticipationGuard.cs`
- extend `tests/Application.UnitTests/Sessions/Commands/ReconnectAuthenticatedParticipant/ReconnectAuthenticatedParticipantCommandHandlerTests.cs` — assert the result exposes current `SessionState` + timer snapshot for a reconnecting device (AC3); confirm `BroadcastTeamBoardNotificationHandlerTests` covers the team-broadcaster call (AC1/AC2)

**Pattern this phase owns:** none (access via the **existing** `RuntimeParticipationGuard` + `AuthorizationBehaviour`; not a new Proxy).
**Gate:** existing handler/event-handler tests green **plus** the hydration assertion above. **No new command/handler/validator.**

### Phase X.3 — Infrastructure  *(verify — schema no-op)*
**Derive:** presence recovery state (`ParticipantStatus`, `LastSeenAt`) and team membership are already persisted by migration `20260603145000_AddRuntimeRecoveryState` under `LiveSessionConfiguration`; multi-device adds **no column**.
**Target files** (verify | extend test):
- verify `Infrastructure/Persistence/Configurations/LiveSessionConfiguration.cs`, `Infrastructure/Persistence/Repositories/LiveSessionRepository.cs`; **grep** `SessionParticipant` / `LastSeenAt` in `ApplicationDbContextModelSnapshot.cs` (do not full-read)
- extend `tests/IntegrationTests/Persistence/…` — round-trip: a participant reloads with team assignment + presence (`ParticipantStatus`, `LastSeenAt`) after a disconnect→reconnect cycle (AC3 across persistence). Mirror `ParticipantTeamBoardRepositoryIntegrationTests.cs`.

**Pattern this phase owns:** none.
**Gate:** `ef migrations add` is a **no-op** (an empty/non-generated migration — a non-empty one is a defect, stop); persistence integration test proves presence + team assignment survive a disconnect→reconnect. **No new migration.**

### Phase X.4 — Api  *(verify + THE deliverable)*
**Derive:**
- `SessionsHub.ReconnectAsync` joins each connection to `live-session:{id}` (state/timer/question), `team:{teamId}` (`TeamBoardUpdated`, AC1), `participant:{id}` (eviction).
- `ConnectionTracker` counts N connections per `sessionParticipantId`; `OnDisconnectedAsync` sends `DisconnectParticipantCommand` **only** when `hasRemainingConnections == false` (last-device presence).
- `SignalRTeamBoardBroadcaster.BroadcastTeamBoardUpdatedAsync` targets only `team:{board.TeamId}` (AC4 isolation).

**Target files** (verify | create test — file to mirror):
- verify `Api/Hubs/SessionsHub.cs`, `Api/Services/ConnectionTracker.cs`, `Api/Hubs/SignalRTeamBoardBroadcaster.cs`
- create `tests/IntegrationTests/Api/MultiDeviceTeamSyncHubTests.cs` — mirror `tests/IntegrationTests/Api/SignalRTeamBoardDeliveryTests.cs` + `tests/IntegrationTests/Api/ConnectionTrackerTests.cs`

**Pattern this phase owns:** none.
**Gate (the real deliverable):** one integration test asserting **AC1–AC4 together** — (AC1/AC2) two concurrent connections for the same participant both receive `TeamBoardUpdated` after a team change; (last-device) the participant is not disconnected until the last connection drops; (AC4) a device on a **different** team never receives the first team's board; (AC3) a fresh/reconnecting connection is admitted idempotently and receives current `SessionState`/timer. Service stays at/above the ADR-0005 coverage gate (line **and** branch). **No new endpoint.**

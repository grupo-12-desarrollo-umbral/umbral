# HU-08 — Driver brief
_Driver reads only this file. The full prompt + context are for the subagent
and human review; the driver never loads them._

**Nature of this HU:** **verification** — the multi-device team-sync contract is already shipped by HU-07B
(reconnect / presence / `ConnectionTracker` N-connection tracking) and HU-23 (team-board broadcast to the
`team:{teamId}` group); canon (`bd_umbral_entity_spec.md:498`, `lastSeenAt` = "operational heartbeat for
multi-device visibility") models it as emergent, with no per-device entity. **Every phase verifies shipped
behavior and adds the missing test — X.1–X.3 are tests-only over unchanged product code; X.4 adds the one
genuine deliverable (an end-to-end multi-device test). Do not authorize a subagent to add a new domain type,
command, endpoint, or migration; product code is edited only if a gate exposes a real defect.**

## Slice
| HU | DES | PRD | Service | Branch | Base |
|----|-----|-----|---------|--------|------|
| HU-08 — Sincronización multi-dispositivo del equipo | DES-13 | DES-70 | session-operations-service | feature/hu-08-multi-device-team-sync | develop |

## Required pattern(s) → owning phase
- **None mandated** (patterns matrix: real-time enabler, SignalR is transport not a pattern). Reconnect/hub access is already guarded by the existing `SessionsHub` `[Authorize(Policy=ParticipantOrOperator)]` + `RuntimeParticipationGuard` + `AuthorizationBehaviour` — inherited guard, **no new gate**.

## Per phase — gate + owned pattern
| Phase | Gate | Pattern |
|-------|------|---------|
| X.1 Domain | New `LiveSessionMultiDeviceReconnectTests`: (a) 2nd `AdmitParticipant` for same identity is idempotent → Active, `IsReconnect=true`, **no** `ParticipantAlreadyConnectedException`; (b) reconnect to a different team → `ParticipantAssignedToDifferentTeamException` (AC4); (c) reconnect while Removed/Finished/Cancelled rejected (AC3). No new domain type; `ParticipantAlreadyConnectedException` left intact | — |
| X.2 Application | `ReconnectAuthenticatedParticipantCommandHandlerTests` asserts result carries current `SessionState` + timer for hydration (AC3); `BroadcastTeamBoardNotificationHandlerTests` covers the team-broadcaster call (AC1/AC2); access stays via existing guard. No new command/handler/validator | — |
| X.3 Infrastructure | `ef migrations add` is a **no-op** (non-empty migration = defect, stop); persistence integration test round-trips presence (`ParticipantStatus`, `LastSeenAt`) + team assignment after disconnect→reconnect (AC3). No new migration | — |
| X.4 Api | New `MultiDeviceTeamSyncHubTests` asserting AC1–AC4 together (two devices both receive `TeamBoardUpdated`; participant present until last connection drops; other-team device never receives the board; reconnect admits idempotently + current state/timer) + ADR-0005 coverage (line AND branch). No new endpoint | — |

Commit subjects — copy each phase's exact subject from the prompt's Steps 5–8 verbatim (the driver presents what the human approved; do not paraphrase or normalize punctuation):
- X.1 `test(session-ops): phase X.1 — verify domain multi-device reconnect (HU-08)`
- X.2 `test(session-ops): phase X.2 — verify reconnect hydration + team broadcast (HU-08)`
- X.3 `test(session-ops): phase X.3 — verify presence persistence round-trip (HU-08)`
- X.4 `test(session-ops): phase X.4 — end-to-end multi-device team sync (HU-08)`

Trailer (every phase): `Ref: HU-08` / `Ref: DES-13` / `Ref: DES-70`

## Acceptance criteria
- AC1 — the system syncs progress, score, and game state across the devices of the same team
- AC2 — a relevant change by one member is reflected on the team's other authorized devices
- AC3 — reconnection restores the team's current state
- AC4 — sync does not mix state between different teams

## Endpoints + smoke (driver verifies at Stop 2)
_No endpoints are added — this is a verification HU. Smoke the existing contract multi-device relies on:_
- SignalR hub `/hubs/sessions` — connect **two** clients as the same participant/team; on a team-board change both receive `TeamBoardUpdated` (AC1/AC2); a client on another team does **not** (AC4). Request/response: hub method `ReconnectAsync(liveSessionId, {TeamId, DisplayName, Token})` → `ReconnectParticipantResultDto`; server-push method `TeamBoardUpdated(ParticipantTeamBoardDto)`.
- `POST /api/sessions/{liveSessionId}/participants/reconnect` — as an authorized participant → expect 200; result carries `SessionState` + `Timer` (reconnect hydration, AC3).
- `GET /api/sessions/{liveSessionId}/participants/team-board` — expect 200 with the team's score/progress snapshot (`ParticipantTeamBoardDto`, the frontend contract for hydration, AC1).

## Frontend slice
Human-driven — see Steps 9 (generate plan) + 9b (implement it) of
`prompt_example_feature_hu08.md`. Verification only: confirm resubscribe-on-reconnect + multi-device rendering + team isolation against the **unchanged** hub / team-board / reconnect contract; no new API calls or types.

# Prompt Example - HU-08 Sincronización multi-dispositivo del equipo (Verification Slice)

Concrete prompt sequence for driving DES-13 / HU-08 through a **verification** slice on `feature/hu-08-multi-device-team-sync` in `session-operations-service`. Follows the pattern in [workflow_for_prompts.md](./workflow_for_prompts.md).

**Key difference for HU-08:** this is **not a feature build.** The multi-device team-sync contract is **already satisfied** by shipped predecessor code — HU-07B (reconnect / presence / `ConnectionTracker` N-connection tracking) and HU-23 (team-board broadcast to the `team:{teamId}` group). Canon (`bd_umbral_entity_spec.md:498`) models multi-device visibility as an *emergent* runtime capability keyed on `SessionParticipant.lastSeenAt`, with **no per-device entity**. So every phase **verifies** the cited behavior and **locks it with a test**; a phase must not add a new domain type, command, endpoint, or migration. The one genuine deliverable is a single end-to-end test (`MultiDeviceTeamSyncHubTests`) that asserts AC1–AC4 together so a future hub/broadcaster/tracker refactor cannot silently break multi-device.

When working from the monorepo root, make the target workload explicit in each prompt. For backend steps, point to `@backend/.agents/backend-agent.md`. For frontend steps, point to `@frontend/AGENTS.md`. Do not ask for backend and frontend implementation in the same phase prompt.

---

## Stop 1 acceptance guard

Reject this generated prompt before implementation if it does not explicitly scope all of the following:

- **AC1** every authorized device of the same participant receives the team's progress/score/game-state (joins `team:{teamId}` + `live-session:{id}`)
- **AC2** a relevant change by one member propagates to the team's other authorized devices (team-board domain event → broadcaster)
- **AC3** a reconnecting device restores the current team state (idempotent `AdmitParticipant` via `RefreshPresence`, `IsReconnect=true`; result carries `SessionState` + timer)
- **AC4** sync never mixes state across teams (`team:{teamId}` group isolation; cross-team negative test)
- the slice is **verify-and-lock**: no new domain type / command / endpoint / migration; product code is edited only if a gate exposes a real defect
- multi-device is emergent from HU-07B + HU-23 (it is not rebuilt here), and `ParticipantAlreadyConnectedException` is a first-join-only guard that must **not** be removed

---

## Required design patterns

**None mandated.** `required_patterns_matrix.md` tags HU-08 as "— (no mandated pattern) … real-time enabler (SignalR)". SignalR is transport, not a pattern gate.

- HU-08 is **not** in the mandated-`Proxy` set nor the applies-where set. Reconnect/hub access is already guarded by the **existing** inherited guard — `SessionsHub` `[Authorize(Policy = ParticipantOrOperator)]` + `RuntimeParticipationGuard` + `AuthorizationBehaviour` (ADR-0001/0002). **No new pattern gate.**

> Resolution note: verified against `backend/docs/required_patterns_matrix.md` (HU-08 row) and `backend/docs/adr/0004-required-domain-patterns.md`. No pattern lands in any phase gate; the transport (SignalR/WebSockets) is inherited from the shipped `SessionsHub`.

---

## Pre-resolved orient (as of 2026-07-13)

> Step 1 has already been run. Paste this section into any agent session that needs context before picking up a phase — no need to re-run the orient prompt.

### What predecessors have already landed

`session-operations-service` is a mature service in the ADR-0011 vertical-slice layout. The multi-device contract is **already built** across three shipped slices — HU-08 verifies them:

- **HU-07B (reconnect/presence):** `SessionParticipant` (`ParticipantStatus` {Joined,Active,Disconnected,Removed,Blocked}, `LastSeenAt`), `LiveSession.AdmitParticipant` (returning-identity branch → idempotent `RefreshPresence`, `IsReconnect=true`), `JoinPolicy.EnsureCanReconnect` (team-mismatch / removed / terminal-state guards), `ReconnectAuthenticatedParticipantCommand`, `DisconnectParticipantCommand`, `RuntimeParticipationGuard`, `SessionsHub`, **`ConnectionTracker`** (N connections per participant; disconnect only on last drop), migration `AddRuntimeRecoveryState`.
- **HU-23 (team board):** `SignalRTeamBoardBroadcaster : ITeamBoardBroadcaster` → `team:{teamId}` group only; `BroadcastTeamBoardNotificationHandler`; `ParticipantTeamBoardDtoFactory` / `GetParticipantTeamBoardQuery`.
- **Untouched by this HU:** #91 participation-block (`participant:{id}` eviction), HU-34 operator-only `TeamAnswered` group, HU-21/22/24 `live-session:{id}` state/timer/question broadcasters.

Branch base is `develop` — all predecessors merged, none In Progress. Mirror targets for the new tests: `LiveSessionTeamBoardTests`, `ReconnectAuthenticatedParticipantCommandHandlerTests`, `ParticipantTeamBoardRepositoryIntegrationTests`, `SignalRTeamBoardDeliveryTests`, `ConnectionTrackerTests`.

**Coverage:** the merged suite reaches at least 95% aggregate branch coverage under ADR-0005; the new multi-device test only adds coverage.

### What HU-08 adds (per DES-70 + canon `§SessionParticipant`)

| Concern | New work |
|---|---|
| Multi-device sync (AC1/AC2) | Verify each device joins `team:{teamId}` + `live-session:{id}` and receives broadcasts; lock end-to-end. |
| Reconnect restore (AC3) | Verify idempotent admit + reconnect result hydration (state + timer); add a hydration assertion. |
| Team isolation (AC4) | Verify `team:{teamId}`-only broadcast; lock a cross-team negative test. |
| Last-device presence | Verify `ConnectionTracker` keeps the participant present until the last connection drops; lock with a test. |
| Regression safety | The deliverable: one `MultiDeviceTeamSyncHubTests` asserting AC1–AC4 together. |

No new domain/application/infrastructure type, no new endpoint, no migration.

### Branch state

`feature/hu-08-multi-device-team-sync` branches from `develop`. No same-service predecessor is In Progress, so there is no feature-branch dependency to inherit.

### Linear state (as of 2026-07-13)

- DES-13 (HU-08): expected **`Todo`**, ungated, labels `Feature`, `svc:session-operations-service`, `ready-for-agent` — **reconstructed from local docs; no Linear MCP was available at generation.** DES-13 is the survivor that `blocks` DES-59 (the multi-device enabler) — **not** superseded.
- DES-70 (PRD): `svc:session-operations-service`, `ready-for-agent`. ⚠️ Enumerates HU-15–36, **does not name HU-08** — runtime-sync authority is DES-70 + canon `bd_umbral_entity_spec.md §SessionParticipant`; DES-67 explicitly delegated the sync here (see Rationale).
- Predecessors Done: HU-07B (reconnect), HU-23/DES-31 (team board). Same-service In Progress: none.

> Linear live state may have changed. Use the Linear MCP to verify DES-13 status, the `svc:session-operations-service` label, and that DES-13 is not superseded, but do not re-fetch PRD scope — read the local file at `@backend/docs/prd/DES-70-primera-implementacion-de-session-operations-service-hu-15-a-hu-36.md`.

---

## 1. Orient - read service state and predecessors

> **Skip this step if you have read the pre-resolved orient section above.** Run it only if the service source, README, or Linear state may have changed since 2026-07-13.

```text
Read the following and summarise what is decided and already shipped:
- @backend/services/session-operations-service/README.md
- @backend/services/session-operations-service/CONTEXT.md - bounded-context language
- @backend/docs/prd/DES-70-primera-implementacion-de-session-operations-service-hu-15-a-hu-36.md - session-ops PRD (note: does NOT name HU-08)
- @backend/docs/hu08-context.md - the pre-resolved HU-08 context (verification HU)

Then confirm the shipped multi-device surface exists:
- @backend/services/session-operations-service/src/Api/Hubs/SessionsHub.cs
- @backend/services/session-operations-service/src/Api/Services/ConnectionTracker.cs
- @backend/services/session-operations-service/src/Api/Hubs/SignalRTeamBoardBroadcaster.cs
- @backend/services/session-operations-service/src/Domain/Entities/SessionParticipant.cs
- @backend/services/session-operations-service/src/Domain/Entities/LiveSession.cs (AdmitParticipant)

Then use the Linear MCP to fetch only the current live state of DES-13 (HU-08) - status and labels,
and confirm DES-13 is not in any superseded column.

Output:
- confirmation the multi-device contract is already built (hub groups, ConnectionTracker, team-board broadcaster)
- the canonical HU-08 scope: verify AC1-AC4 and lock with tests; no new types/endpoints/migration
- current Linear status and labels for DES-13

Do not start planning or implementing yet.
```

---

## 2. Label DES-13 as ready-for-agent

```text
Use the Linear MCP to confirm DES-13 still carries the label ready-for-agent and svc:session-operations-service.
If ready-for-agent is missing, add it. If the svc label is identity-access-service, STOP and report the
conflict — the buildable slice is session-operations-service (see hu08-context State block).
Output the updated DES-13 ticket state and labels.
```

---

## 3. Confirm slice readiness

```text
Use the Linear MCP to confirm DES-13 carries both svc:session-operations-service and ready-for-agent
and output its current status and acceptance criteria.

The PRD scope is in the local file at
@backend/docs/prd/DES-70-primera-implementacion-de-session-operations-service-hu-15-a-hu-36.md.
Do not re-fetch PRD scope from Linear. Note DES-70 does not name HU-08 by number; the runtime-sync
authority is DES-70 + canon bd_umbral_entity_spec.md §SessionParticipant (see hu08-context).

Before planning, explicitly confirm the Stop 1 acceptance guard:
- AC1 every authorized device of the same participant receives team progress/score/game-state
- AC2 a change by one member propagates to the team's other authorized devices
- AC3 a reconnecting device restores current team state (idempotent admit; result carries state + timer)
- AC4 sync never mixes state across teams
- the slice is verify-and-lock: no new domain type / command / endpoint / migration
- multi-device is emergent from HU-07B + HU-23; ParticipantAlreadyConnectedException stays (first-join guard)

Output the confirmed HU id, title, acceptance criteria, labels, and the guard confirmation before planning.
```

In the remaining examples below, `HU-08` and `DES-13` are the resolved values for this slice. `DES-70` is the shared PRD reference for `session-operations-service`; its content lives in the local file above.

---

## 4. Start the slice

```text
Prepare the multi-device team-sync verification slice on branch feature/hu-08-multi-device-team-sync.
Use the HU id and DES id resolved from Linear in the previous step.
This slice affects backend session-operations-service (tests only, unless a gate exposes a defect).

The pre-resolved orient at the top of this document lists what is already shipped and what HU-08 verifies.
Do not re-read the PRD for scoping unless you need a precise implementation detail.

This is a VERIFICATION HU: each phase confirms shipped behavior and adds the missing test. Do not
authorize new domain types, commands, endpoints, or migrations.

Move DES-13 to In Progress and output the exact scope, branch name, base branch, and touched surfaces.
```

---

## 5. Backend phase X.1 - Domain layer (verify + lock)

```text
Use @backend/.agents/backend-agent.md.
Implement backend phase X.1 for HU-08 in session-operations-service, per the
**X.1 derivation block in @backend/docs/hu08-context.md** (your spec — do not
re-read the canon or re-inspect the tree; open a cited canon section only to
fill a gap the block leaves open).

This phase VERIFIES shipped domain behavior and adds the missing unit test. Do not add a new domain type.

Gate:
- Domain build passes
- new unit test LiveSessionMultiDeviceReconnectTests proves: (a) a second AdmitParticipant for the same
  externalIdentityId is idempotent -> Active, IsReconnect=true, NO ParticipantAlreadyConnectedException;
  (b) reconnect requesting a different team throws ParticipantAssignedToDifferentTeamException (AC4);
  (c) reconnect while Removed/Finished/Cancelled is rejected (AC3)
- ParticipantAlreadyConnectedException (first-join guard) is left intact
- no new domain type introduced

Do not touch other backend layers or frontend.
```

Commit:

```text
test(session-ops): phase X.1 — verify domain multi-device reconnect (HU-08)

Ref: HU-08
Ref: DES-13
Ref: DES-70
```

---

## 6. Backend phase X.2 - Application layer (verify + lock)

```text
Use @backend/.agents/backend-agent.md.
Implement backend phase X.2 for HU-08 in session-operations-service, per the
**X.2 derivation block in @backend/docs/hu08-context.md** (your spec — do not
re-read the canon or re-inspect the tree; open a cited canon section only to
fill a gap the block leaves open).

This phase VERIFIES the reconnect handler + team-board event handler and adds the missing assertion.
Do not add a new command/handler/validator.

Gate:
- clean build passes
- ReconnectAuthenticatedParticipantCommandHandlerTests asserts the result exposes current SessionState +
  timer snapshot for a reconnecting device (AC3 hydration)
- BroadcastTeamBoardNotificationHandlerTests covers the team-broadcaster call on the team-board event (AC1/AC2)
- access remains through the existing RuntimeParticipationGuard + AuthorizationBehaviour — no new Proxy, no ad-hoc role if
- no new command/handler/validator introduced

Do not touch Infrastructure, Api, or frontend.
```

Commit:

```text
test(session-ops): phase X.2 — verify reconnect hydration + team broadcast (HU-08)

Ref: HU-08
Ref: DES-13
Ref: DES-70
```

---

## 7. Backend phase X.3 - Infrastructure layer (verify — schema no-op)

```text
Use @backend/.agents/backend-agent.md.
Implement backend phase X.3 for HU-08 in session-operations-service, per the
**X.3 derivation block in @backend/docs/hu08-context.md** (your spec — do not
re-read the canon or re-inspect the tree; grep the model snapshot rather than
full-reading it).

This phase VERIFIES presence persistence. Multi-device adds NO column.

Gate:
- build passes
- `ef migrations add` is a no-op — it must produce an empty/non-generated migration. A non-empty migration
  is a DEFECT: stop and reconcile, do not ship a schema change.
- a persistence integration test proves a participant reloads with team assignment + presence
  (ParticipantStatus, LastSeenAt) after a disconnect->reconnect cycle (AC3 across persistence)
- no new migration committed

Do not touch Api or frontend.
```

Commit:

```text
test(session-ops): phase X.3 — verify presence persistence round-trip (HU-08)

Ref: HU-08
Ref: DES-13
Ref: DES-70
```

---

## 8. Backend phase X.4 - API layer (verify + THE deliverable)

```text
Use @backend/.agents/backend-agent.md.
Implement backend phase X.4 for HU-08 in session-operations-service, per the
**X.4 derivation block in @backend/docs/hu08-context.md** (your spec — do not
re-read the canon or re-inspect the tree; open a cited canon section only to
fill a gap the block leaves open).

This phase adds the genuine HU-08 deliverable: one end-to-end multi-device test. No new endpoint.

Gate:
- new integration test MultiDeviceTeamSyncHubTests asserts AC1-AC4 together:
  (AC1/AC2) two concurrent connections for the same participant both receive TeamBoardUpdated after a team change;
  (last-device) the participant is not disconnected until the last connection drops;
  (AC4) a device on a different team never receives the first team's board;
  (AC3) a fresh/reconnecting connection is admitted idempotently and receives current SessionState/timer
- hub group routing (live-session:{id}, team:{teamId}, participant:{id}) and ConnectionTracker N-connection
  counting are exercised, not modified
- service reaches the ADR-0005 gate of at least 95% aggregate branch coverage
- no new endpoint introduced

Do not touch frontend.
```

Commit:

```text
test(session-ops): phase X.4 — end-to-end multi-device team sync (HU-08)

Ref: HU-08
Ref: DES-13
Ref: DES-70
```

---

## 8.5. Docker rebuild and smoke

```text
From the monorepo root, rebuild and restart the backend stack after the API phase:

docker compose build session-operations-service api-gateway
docker compose up -d session-operations-service api-gateway

Run smoke checks through the gateway (no new endpoint — confirm the existing contract multi-device relies on):
- POST /api/sessions/{liveSessionId}/participants/reconnect as an authorized participant — expect success,
  result carries SessionState + timer (reconnect hydration, AC3)
- GET  /api/sessions/{liveSessionId}/participants/team-board — expect the team's score/progress snapshot (AC1)
- SignalR hub at /hubs/sessions: connect two clients as the same participant/team and confirm both receive
  TeamBoardUpdated on a team-board change (AC1/AC2); a client on another team does not (AC4)

Output:
- container status
- smoke command results
- confirmation that the multi-device behavior holds over the unchanged contract (nothing new for the frontend to consume)
```

---

## 9. Frontend slice

```text
Generate a multi phase plan in a markdown file — following the frontend plan concreteness rule (below), modelled on the exemplar closest to this slice's shape (@frontend/plans/hu-03-frontend-role-permission-assignment.md for a small 1–few-endpoint surface; @frontend/plans/hu-10a-frontend-mission-hierarchy-authoring.md for a large/multi-endpoint or partially-blocked surface) — save it in @frontend/plans/ for the following:
Use @frontend/AGENTS.md.
Implement the frontend slice for HU-08 multi-device team sync.

Scope (this is a verification slice — there is NO new backend contract):
- confirm the participant/team client resubscribes to the SignalR hub (/hubs/sessions) on reconnect and re-hydrates
  team state from the existing GET /api/sessions/{id}/participants/team-board + reconnect result
- confirm two devices of the same participant/team both render the same live team board/score/game state (AC1/AC2)
- confirm a device only ever renders its own team's state — never another team's (AC4)
- confirm a reconnecting device restores the current team state (AC3)
- do NOT add new API calls or types beyond the existing team-board / reconnect / hub contract

Gate:
- frontend typecheck/build passes
- the flow exercises multi-device rendering + reconnect against the real, unchanged API/hub contract
- no state is mixed across teams in the UI

Frontend plan concreteness rule (embed verbatim in the plan):
1. Proportion concreteness to certainty. Write code-complete detail — exact DTO/request types, real component skeletons, exact client-fn + server-action bodies, a data-testid contract — only for the fully-knowable near-term increments (typically the foundation + first authoring increment). Keep later, large, or blocked increments at contract + gate altitude: a contract table, scope, and gate, with no invented bodies. Never write code for an increment blocked on an open question.
2. Verify every code anchor against the real source before writing it. Open the files the plan names — exported vs. private helpers, exact signatures, the const/env it reads, the line a refactor targets — and write only what the source actually supports. A confident-but-wrong anchor is worse than an altitude note. If a detail is not verifiable, state the assumption under Open Questions rather than inventing it.
3. Required sections (a plan missing one is a defect): Context · Verified Backend Contract (endpoint/shape table) · Architecture Decisions · Environment (env vars / config consts reused) · data-testid contract · phased Scope + Gate per increment · Acceptance-criteria → test mapping · Open Questions / Dependencies · Out of Scope.
4. Final forms only, sequential by default. Write only the final version of each anchor — no "wrong → revised" trails — and keep increments sequential unless the slice genuinely parallelizes.

Do not modify backend code in this step.
```

---

## 9b. Implement the frontend plan

> Run only after the Step 9 plan is written and reviewed.

```text
Use @frontend/AGENTS.md and the Step 9 plan at @frontend/plans/<hu-08 plan file>.
Implement phase by phase in the plan's order, per the plan's own Scope / Gate / Commit Sequence.
The plan is the source of truth and supersedes the Step 9 seed scope.

Stop at any increment the plan marks blocked on an Open Question (name it).
Do not re-generate the plan. Do not modify backend code.
```

Commit (frontend):

```text
feat(frontend): multi-device team sync verification — HU-08

Ref: HU-08
Ref: DES-13
Ref: DES-70
```

---

## 10. Close-out

```text
Use the Linear MCP to re-check DES-13 acceptance criteria and labels.
Verify the final implementation against the Stop 1 acceptance guard:
- AC1 every authorized device of the same participant receives team progress/score/game-state
- AC2 a change by one member propagates to the team's other authorized devices
- AC3 a reconnecting device restores current team state (idempotent admit; result carries state + timer)
- AC4 sync never mixes state across teams
- the slice stayed verify-and-lock: no new domain type / command / endpoint / migration

Run final backend and frontend verification required by the repo instructions.
Summarise:
- commits created (tests-only backend + frontend verification)
- confirmation that no production types/endpoints/migrations were added
- tests and gates run (incl. MultiDeviceTeamSyncHubTests and the ADR-0005 coverage gate)
- any newly surfaced ambiguity (the service/PRD resolution and the verification nature are already decided —
  flag only new ones, e.g. if product wants the team-board pushed inside the reconnect result)

Create the PR:

gh pr create \
  --base develop \
  --head feature/hu-08-multi-device-team-sync \
  --title "test(session-ops): verify multi-device team sync (HU-08)" \
  --body "Implements DES-13/HU-08 as a verification slice in session-operations-service. Multi-device team sync is emergent from HU-07B (reconnect/presence/ConnectionTracker N-connections) and HU-23 (team-board broadcast to the team group); this HU verifies AC1-AC4 and locks the contract with a new end-to-end MultiDeviceTeamSyncHubTests plus domain/application/persistence assertions. No new domain type, endpoint, or migration."
```

---

## Rationale

**Why this HU is a verification slice, not a feature build.** Multi-device team sync is not a new capability to construct — it is an emergent property of three shipped slices. `ConnectionTracker` already counts N connections per participant and defers `DisconnectParticipant` until the last device drops; `SessionsHub.ReconnectAsync` already joins every device to `team:{teamId}` and `live-session:{id}`; `SignalRTeamBoardBroadcaster` already isolates the payload to the team group; `LiveSession.AdmitParticipant` already routes a returning identity through idempotent `RefreshPresence`. Canon confirms the design intent — `bd_umbral_entity_spec.md:498` defines `lastSeenAt` as the "operational heartbeat for multi-device visibility" and models one `SessionParticipant` with one-or-more `TeamMember` representations, with **no per-device entity**. `ddd_solution_model.md` gives HU-08 no dedicated design. So the risk HU-08 addresses is *regression*, not *absence*: the mechanisms are each tested individually but never asserted together, so a future hub/broadcaster/tracker refactor could silently break the multi-device contract. The deliverable is that end-to-end lock.

**Two resolutions carried into scope (not left open).** (1) **Service = `session-operations-service`.** `sprint_1_final_tickets.md` and `required_patterns_matrix.md` file HU-08 under `identity`, but `DES-67` (the identity PRD) explicitly lists "diseñar sincronización multi-dispositivo completa dentro de Identity" as a non-goal, calls HU-08 "una capacidad de sincronización del runtime", and keeps join/reconnect/sync with SessionOperations — the same identity→session-ops re-scope HU-07B underwent. The buildable runtime lives here. (2) **PRD = `DES-70`.** DES-70 owns the session-ops runtime but does **not** enumerate HU-08 by number; the runtime-sync authority is therefore DES-70 **+ canon** `§SessionParticipant`, with DES-67 delegating the sync here. This PRD scope gap is recorded per constraint 3.

**One ambiguity deliberately left open** (do not build inside this slice): the reconnect result (`ReconnectParticipantResultDto`) carries `SessionState` + `Timer` but not the current team-board snapshot — a reconnecting device pulls the board via `GET /api/sessions/{id}/participants/team-board`. If product later wants the board pushed inside the reconnect result, that is a small additive enhancement for its own ticket, not verification scope.

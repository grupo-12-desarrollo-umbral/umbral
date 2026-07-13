# Prompt Example — HU-26 Liberación manual de pistas (Feature Slice)

Concrete prompt sequence for driving `DES-36` / `HU-26` through a backend feature slice on `feature/hu-26-manual-clue-release`, then an operator + participant frontend slice. Follows the pattern in [workflow_for_prompts.md](./workflow_for_prompts.md).

**Key difference for HU-26:** this is the first **operator write action over the runtime clue model** — an operator releases a treasure-hunt `Target`'s optional **hidden** clue (`HiddenUntilOperatorRelease`) to **one team or all teams** during an **Active** session, appending a per-team `ClueReleaseRecord` (the local *historial*) and pushing the newly-visible clue to the affected team's board. Unlike the read HUs (HU-23/HU-24A), HU-26 mutates the aggregate, so it carries **two** mandated patterns: a **`Facade`** (`ClueReleaseFacade` — orchestration, not inline handler logic) and a **`Proxy`** (the reused `ISessionAdministrationAccessResolver` ownership guard). Release changes **visibility only** — it does **not** advance the substage or resolve a target. Publishing `ClueReleased` to RabbitMQ for async audit is the sibling enabler **DES-92** (consumed by DES-56/HU-40A), **not** this slice.

When working from the monorepo root, make the target workload explicit in each prompt. For backend steps, point to `@backend/.agents/backend-agent.md`. For the frontend step, point to `@frontend/AGENTS.md`. Do not ask for backend and frontend implementation in the same phase prompt; coordinate them as separate scoped steps tied together by the verified API contract.

---

## Stop 1 acceptance guard

Reject this generated prompt before implementation if it does not explicitly scope all of the following:

- the operator releases a **treasure-hunt `Target`'s optional hidden clue** to **one team or all teams**; the clue is scoped to a **`Target`** inside the **active substage**, not "la etapa"
- release is gated by the **mandated `Proxy`** — `ISessionAdministrationAccessResolver.GetAuthorizedSessionAsync` (Administrator all / Operator `AssignedOperatorUserId == actor.UserId` else `ForbiddenAccessException`); no ad-hoc role/owner `if` in handler, facade, controller, or hub
- orchestration goes through the **mandated `Facade`** (`ClueReleaseFacade` / `IClueReleaseFacade`), not inline in the command handler or controller
- the release appends an **append-only per-team `ClueReleaseRecord`** (teamId, targetId, clueId?, releaseMode=Manual, releasedByUserId, releasedAt) — the local *historial de la sesión*
- **no leak:** a clue released to one team surfaces on **that team's** board only
- **no duplicate:** the same clue may not be released twice to the same team for the same `Target` (uniqueness on `(teamId, targetId)`)
- **does not advance:** release changes visibility only; it does **not** advance the substage or resolve the target
- session validity gate is `State == Active`
- live updates push the newly-visible clue to the reused **`team:{teamId}`** SignalR group; a release to one team does not reach another team's connections
- **out of scope:** RabbitMQ/MassTransit publication of `ClueReleased` (DES-92), a `SessionEvent` history table (DES-56/HU-40A owns the consolidated history), conditional/auto release (HU-27), operator-added runtime clues (HU-28), evidence/QR/target resolution (HU-29/30/31), and re-guarding existing endpoints

---

## Required design patterns

- `Facade` **(mandated — `required_patterns_matrix.md` HU-26 row line 119; ADR-0013)**
  - Owning phase: **X.2 Application**.
  - Obligation: a single `ClueReleaseFacade` / `IClueReleaseFacade` (`ReleaseCluesAsync`) orchestration entry point over the ownership resolver (Proxy) → aggregate load → per-team release fan-out → persistence. Mirror `SessionTeamAssociationFacade`; do not inline the coordination in the handler or controller.
- `Proxy` **(mandated — `required_patterns_matrix.md` HU-26 row line 119; ADR-0009 + ADR-0012 §resource-ownership guard)**
  - Owning phases: **X.2 Application** (resolver in the facade/handler) **and X.4 Api** (endpoint authorization policy).
  - Obligation: the release passes through `ISessionAdministrationAccessResolver` / `SessionAdministrationAuthorizationProxy`. Administrator all; Operator only where `LiveSession.AssignedOperatorUserId == actor.UserId`, else `ForbiddenAccessException`. Coarse role gate stays `[Authorize(Roles="Operator")]` (command) / `[Authorize(Policy = AuthorizationPolicies.Operator)]` (endpoint). **No ad-hoc role/owner `if` checks** anywhere.
- Transport: **SignalR / WebSockets** (`required_patterns_matrix.md:57,119`) — verified in X.4. The newly-visible clue is pushed to the reused `team:{teamId}` group via `ITeamBoardBroadcaster`.

---

## Pre-resolved orient (as of 2026-07-13)

> Step 1 has already been run. Paste this section into any agent session that needs context before picking up a phase; no need to re-run the orient prompt unless source or Linear state changed.

### What predecessors have already landed

`DES-36` is live, **not superseded** (it is not in the realignment map's superseded column — DES-23/28/30/44/47), and now carries both required labels. It has `canon-realign` **without** `needs-rebuild` → **feature flow with a canon-reword comment** (clue scoped to `Target`, not "la etapa"; release does not advance the substage). Base is `develop` — all build-on predecessors are Done.

- **DES-22/DES-24 (HU-15/HU-16, PRIMARY)** — `LiveSession` + immutable `MissionRuntimeSnapshot`. Treasure-hunt clue guidance is on `TargetSnapshot.ClueText` / `.ClueVisibilityPolicy`; `LiveSession.CollectVisibleClues/CollectTargetVisibleClues/CollectSubstageVisibleClues` already **withhold** `HiddenUntilOperatorRelease` clues with explicit `// HU-26/HU-28` TODOs marking where per-team release plugs in. Runtime `Team` already has `ReleasedClueCount` / `CurrentClueNodeId` columns but **no release mechanics**.
- **DES-76 (HU-21A, PRIMARY)** — `LiveSession.State` lifecycle + `SessionStateChangedEvent`; state recording is via domain events + outbox (`DispatchDomainEventsInterceptor` → `OutboxDomainEventDispatcher`). **No persisted `SessionEvent` table exists** — the `ClueReleaseRecord` is HU-26's local trace.
- **DES-31 / HU-23 (PRIMARY)** — `LiveSession.ProjectParticipantTeamBoard(...)` + `BroadcastTeamBoardNotificationHandler` + `ITeamBoardBroadcaster` (`SignalRTeamBoardBroadcaster`, method `"TeamBoardUpdated"`, group `team:{teamId:D}`). HU-26 adds a `ClueReleasedEvent` handler that re-projects + pushes the affected team's board.
- **DES-26 / HU-19 (PRIMARY)** — operator assignment + `AssignedOperatorUserId` (the ownership seam the Proxy checks); `AssignOperatorToSession` is the operator-scoped **write** slice to mirror.
- **DES-32 / HU-24A** — `SessionAdministrationAuthorizationProxy` + `ISessionAdministrationAccessResolver.GetAuthorizedSessionAsync` (the exact Proxy). **DES-25 / HU-18** — runtime `Team` association. **DES-77 / HU-22** — authoritative timer (not consumed by release).
- Existing facades to mirror: `SessionTeamAssociationFacade`, `TriviaRoundOrchestratorFacade` (`Application/Sessions/Common/`).

Superseded originals excluded: DES-23, DES-28, DES-30, DES-44, DES-47. **HU-29 (evidence submission) is In Progress** but is a different aggregate concern that HU-26 does not build on, so the branch base stays `develop`.

### What HU-26 adds on top

| Concern | New work |
|---|---|
| Operator clue release | Domain `LiveSession.ReleaseClue` / `ReleaseClueToAllTeams` over an Active session |
| Per-team trace | New append-only `ClueReleaseRecord` child entity (the local *historial*) |
| Orchestration | `ClueReleaseFacade` (Facade) over resolver (Proxy) → aggregate → repository |
| Backend contract | `POST /api/sessions/{liveSessionId}/clues/release` (operator-guarded) |
| Live updates | `ClueReleasedEvent` → re-project + push the affected team board over `team:{teamId}` |
| Frontend | Operator release control + participant board clue reveal |

### Branch state and prerequisite

`feature/hu-26-manual-clue-release` should be branched from `develop`. No **build-on** same-service predecessor is In Progress, so there is no feature-branch dependency to inherit first.

### Linear state (as of 2026-07-13)

- DES-36 (HU-26): **Todo**, labels: `svc:session-operations-service`, `Feature`, `canon-realign`, `ready-for-agent`
- DES-70 (PRD): **Backlog**, labels: `svc:session-operations-service`, `canon-realign`, `ready-for-agent`
- Same-service Done build-on predecessors: DES-22, DES-24, DES-76, DES-31, DES-26, DES-32, DES-25, DES-77
- Same-service In Progress issues: HU-29 (evidence submission) — untouched by this HU

> Linear live state may have changed. Use the Linear MCP to verify DES-36 status and labels if needed, but do not re-fetch PRD scope — read the local PRD file at `@backend/docs/prd/DES-70-primera-implementacion-de-session-operations-service-hu-15-a-hu-36.md` and the resolved context at `@backend/docs/hu26-context.md`.

---

## 1. Orient — read service state and the resolved HU-26 context

> **Skip this step if you have read the pre-resolved orient section above.** Run it only if the service source, README, or Linear state may have changed since 2026-07-13.

```text
Read the following files and summarize what has already landed and what HU-26 must add:
- @backend/services/session-operations-service/README.md
- @backend/services/session-operations-service/CONTEXT.md
- @backend/docs/prd/DES-70-primera-implementacion-de-session-operations-service-hu-15-a-hu-36.md
- @backend/docs/hu26-context.md

Then inspect only the current session-operations seams HU-26 anchors on:
- @backend/services/session-operations-service/src/Domain/Entities/LiveSession.cs (CollectVisibleClues / CollectTargetVisibleClues + the `// HU-26/HU-28` TODOs; MoveTo state guard)
- @backend/services/session-operations-service/src/Domain/Entities/Team.cs (runtime team: ReleasedClueCount / CurrentClueNodeId)
- @backend/services/session-operations-service/src/Application/Sessions/Commands/AssignOperatorToSession/AssignOperatorToSessionCommandHandler.cs
- @backend/services/session-operations-service/src/Application/Sessions/Common/SessionTeamAssociationFacade.cs
- @backend/services/session-operations-service/src/Application/Sessions/Common/Authorization/SessionAdministrationAuthorizationProxy.cs
- @backend/services/session-operations-service/src/Application/Sessions/EventHandlers/BroadcastTeamBoardNotificationHandler.cs
- @backend/services/session-operations-service/src/Api/Controllers/SessionsController.cs
- @backend/services/session-operations-service/src/Api/Hubs/SignalRTeamBoardBroadcaster.cs

Then use the Linear MCP to fetch only the current live state of:
- DES-36 (HU-26 - Liberación manual de pistas)
- DES-70 (PRD - SessionOperations)

Output:
- the live DES-36 status and labels
- confirmation that HU-26 is not superseded and is feature flow (canon-realign without needs-rebuild → comment-only reword)
- the canonical scope: operator release of a treasure-hunt Target's hidden clue to one/all teams, Proxy-guarded, orchestrated by a Facade, append-only per-team ClueReleaseRecord, no leak, no duplicate, does not advance the substage, Active-state gated, affected-team live push
- the explicit exclusions: RabbitMQ publication (DES-92), SessionEvent history table (DES-56/HU-40A), conditional/auto release (HU-27), operator runtime clues (HU-28), evidence/QR/target resolution (HU-29/30/31), re-guarding existing endpoints

Do not start planning or implementing yet.
```

---

## 2. Label DES-36 as ready-for-agent

```text
Use the Linear MCP to confirm DES-36 still carries the label ready-for-agent.
If it is missing, add it (pass the full explicit label set to avoid dropping svc:session-operations-service / canon-realign / Feature).
Output the updated DES-36 ticket state and labels.
```

---

## 3. Confirm slice readiness

```text
Use the Linear MCP to confirm DES-36 carries both svc:session-operations-service
and ready-for-agent labels and output its current status and acceptance criteria.

The PRD scope is already in the local file at
@backend/docs/prd/DES-70-primera-implementacion-de-session-operations-service-hu-15-a-hu-36.md.
Do not re-fetch PRD scope from Linear. Apply the canon reword from DES-36's `⚠️ Deuda de canon`
comment: the clue is scoped to a Target inside the active substage (not "la etapa"), and release
does not advance the substage.

Before planning, explicitly confirm the Stop 1 acceptance guard:
- operator releases a treasure-hunt Target's hidden clue to one team or all teams, scoped to a Target
- access via the mandated Proxy (ISessionAdministrationAccessResolver.GetAuthorizedSessionAsync); no ad-hoc role/owner if checks
- orchestration through the mandated Facade (ClueReleaseFacade), not inline
- append-only per-team ClueReleaseRecord (the local historial); no SessionEvent table
- no leak (per-team visibility); no duplicate ((teamId, targetId) uniqueness); does not advance the substage
- session validity gate is State == Active
- live push to the reused team:{teamId} group
- out of scope: RabbitMQ publication (DES-92), HU-27/HU-28, evidence/QR/target resolution, re-guarding existing endpoints

Output the confirmed HU id, title, acceptance criteria, labels, and the guard confirmation before planning the slice.
```

In the remaining examples below, `HU-26` and `DES-36` are the resolved values for this slice. `DES-70` is the shared PRD reference for `session-operations-service`.

---

## 4. Start the slice

```text
Prepare the HU-26 slice on branch feature/hu-26-manual-clue-release.
Use the HU id and DES id resolved from Linear in the previous step.
This slice affects backend `session-operations-service` and the operator + participant frontend surfaces.

The pre-resolved orient at the top of this document lists what existing code has already
landed and what HU-26 adds. Do not re-read the PRD for scoping unless you need to resolve an ambiguity.

Treat HU-26 as one operator write action + its live reveal: add a domain clue-release method + per-team ClueReleaseRecord (X.1), a ClueReleaseFacade + Proxy-guarded ReleaseClue command (X.2), EF persistence + migration for the release records (X.3), and the operator endpoint + affected-team SignalR push (X.4). Release changes visibility only — it must not advance the substage or resolve a target. Do not build RabbitMQ publication (DES-92), a SessionEvent history table, conditional/auto release (HU-27), operator runtime clues (HU-28), evidence/QR/target resolution (HU-29/30/31), or re-guard existing endpoints.

Move DES-36 to In Progress if the team process requires it, and output the exact scope,
branch name, base branch, and touched surfaces.
```

---

## 5. Backend phase X.1 — Domain layer

```text
Use @backend/.agents/backend-agent.md.
Implement backend phase X.1 for HU-26 in session-operations-service, per the
**X.1 derivation block in @backend/docs/hu26-context.md** (your spec — do not
re-read the canon or re-inspect the tree; open a cited canon section only to
fill a gap the block leaves open).

Gate:
- Domain build passes; unit tests cover ClueReleaseRecord, ReleaseMode, ClueReleasedEvent, and LiveSession.ReleaseClue/ReleaseClueToAllTeams
- ReleaseClue requires State == Active, resolves a HiddenUntilOperatorRelease clue on a Target in the active treasure-hunt substage, and enforces (teamId, targetId) uniqueness (duplicate rejected)
- all-teams release writes one record + one ClueReleasedEvent per team
- release does NOT advance the substage or resolve the target
- the released clue surfaces only for the released team (no leak); CollectTargetVisibleClues honors the per-team release record

Do not touch other backend layers or frontend.
```

Commit:

```text
feat(session-operations): phase X.1 - domain layer (HU-26)

Ref: HU-26
Ref: DES-36
Ref: DES-70
```

---

## 6. Backend phase X.2 — Application layer

```text
Use @backend/.agents/backend-agent.md.
Implement backend phase X.2 for HU-26 in session-operations-service, per the
**X.2 derivation block in @backend/docs/hu26-context.md** (your spec — do not
re-read the canon or re-inspect the tree; open a cited canon section only to
fill a gap the block leaves open).

Gate:
- Application build passes; handler + facade tests cover the operator release command (one team + all teams)
- orchestration goes through ClueReleaseFacade / IClueReleaseFacade (registered in DependencyInjection) — not inline in the handler
- access goes through ISessionAdministrationAccessResolver.GetAuthorizedSessionAsync (the Proxy); ForbiddenAccessException for a non-owning operator; no ad-hoc role/owner if
- [Authorize(Roles="Operator")] on the command; duplicate release rejected; release does not advance the substage
Gate (pattern): orchestration behind the single ClueReleaseFacade entry point (Facade); access enforced through the resource-ownership resolver Proxy — no inline role/owner checks (Proxy).

Do not touch Infrastructure, Api, or frontend.
```

Commit:

```text
feat(session-operations): phase X.2 - application layer (HU-26)

Ref: HU-26
Ref: DES-36
Ref: DES-70
```

---

## 7. Backend phase X.3 — Infrastructure layer

```text
Use @backend/.agents/backend-agent.md.
Implement backend phase X.3 for HU-26 in session-operations-service, per the
**X.3 derivation block in @backend/docs/hu26-context.md** (your spec — do not
re-read the canon or re-inspect the tree; open a cited canon section only to
fill a gap the block leaves open).

Gate:
- Infrastructure build passes
- ClueReleaseRecord persists as an EF OwnsMany collection on LiveSession (mirror the runtime Teams OwnsMany)
- `ef migrations add` succeeds and represents the new owned collection; the runtime Teams.ReleasedClueCount increment round-trips
- repository/integration test proves round-trip of a LiveSession + clue-release records (team/target/releasedAt persisted, uniqueness respected)
- grep the model snapshot for the new table; do not full-read it

Do not touch Api or frontend.
```

Commit:

```text
feat(session-operations): phase X.3 - infrastructure layer (HU-26)

Ref: HU-26
Ref: DES-36
Ref: DES-70
```

---

## 8. Backend phase X.4 — Api layer

```text
Use @backend/.agents/backend-agent.md.
Implement backend phase X.4 for HU-26 in session-operations-service, per the
**X.4 derivation block in @backend/docs/hu26-context.md** (your spec — do not
re-read the canon or re-inspect the tree; open a cited canon section only to
fill a gap the block leaves open).

Gate:
- endpoint integration tests cover POST /api/sessions/{liveSessionId}/clues/release: 200 for the assigned operator releasing to one team and to all teams; 403 (RFC 7807) for a non-owning operator; duplicate release -> 409/ProblemDetails
- new domain exceptions mapped in ProblemDetailsExceptionHandler
- SignalR test proves the released clue reaches only the affected team's board (team:{teamId}) and not other teams' connections; release does not advance the substage
- access remains [Authorize(Policy=Operator)] + the ownership resolver; no ad-hoc authorization in controller/hub
- `backend/docs/adr/0005-coverlet-msbuild-for-aggregate-coverage.md` coverage gate passes
Gate (pattern): endpoint authorized via the Operator policy AND the ownership resolver Proxy; no ad-hoc role/owner checks (Proxy). SignalR transport gate verified here.

Do not touch frontend.
```

Commit:

```text
feat(session-operations): phase X.4 - api layer (HU-26)

Ref: HU-26
Ref: DES-36
Ref: DES-70
```

---

## 8.5. Docker rebuild and smoke

```text
docker compose build session-operations-service api-gateway
docker compose up -d session-operations-service api-gateway

Smoke through the gateway:
- authenticate as the assigned operator of an Active session with a treasure-hunt substage that has a HiddenUntilOperatorRelease target clue
- POST /api/sessions/{liveSessionId}/clues/release with { targetId, teamId } -> 200; verify the response reports the released team(s) and target
- POST the same { targetId, teamId } again -> duplicate rejected (409/ProblemDetails)
- POST with { targetId } and no teamId -> 200 releasing to all teams
- authenticate as a different (non-owning) operator, POST the same -> 403/ProblemDetails (RFC 7807)
- connect to the SignalR hub as a participant of the released team and verify the newly-visible clue arrives on team:{teamId} without reload; connect as a participant of a different team and confirm it does NOT arrive there; confirm the substage did not advance
```

---

## 9. Operator + participant frontend slice

```text
Generate a multi phase plan in a markdown file — following the **frontend plan concreteness rule** (below), modelled on the exemplar closest to this slice's shape (@frontend/plans/hu-03-frontend-role-permission-assignment.md for a small 1-few-endpoint surface; @frontend/plans/hu-10a-frontend-mission-hierarchy-authoring.md for a large/multi-endpoint or partially-blocked surface) — save it in @frontend/plans/ for the following:
Use @frontend/AGENTS.md.

Build the HU-26 clue-release frontend against the verified backend contract:
- HTTP action: POST /api/sessions/{liveSessionId}/clues/release (operator-authenticated) with { targetId, teamId? } (omit teamId to release to all teams), returning the released team ids + target
- operator surface (web): a control on the operator session view to release a target's hidden clue to one selected team or all teams; disable/hide it unless the session is Active; surface duplicate (already released) and forbidden (non-owning operator) as non-crashing states
- participant surface: the participant board reveals the newly-visible clue when it is released to that team, via the existing team:{teamId} SignalR board push (no new invoke/group); a team must not see another team's released clue
- reuse the existing operator/participant auth/session context and the existing team-board SignalR client/hook; do NOT render clue-as-progress, and do not advance the substage from the UI

Frontend plan concreteness rule:
1. Proportion concreteness to certainty. Write code-complete detail — exact DTO/request types, real
   component skeletons, exact client-fn + server-action bodies, a data-testid contract — only for the
   fully-knowable near-term increments (the release action client-fn + the operator release control +
   the participant board clue reveal off the existing board push). Keep later, large, or blocked
   increments at contract + gate altitude: a contract table, scope, and gate, with no invented bodies.
   Never write code for an increment blocked on an open question.
2. Verify every code anchor against the real source before writing it. Open the files the plan names —
   the operator session views, the existing team-board SignalR client/hook, the identity/header helpers,
   the host const/env it reads — and write only what the source actually supports. A confident-but-wrong
   anchor is worse than an altitude note. If a detail is not verifiable, state the assumption under Open
   Questions rather than inventing it.
3. Required sections (a plan missing one is a defect): Context · Verified Backend Contract
   (endpoint/shape table) · Architecture Decisions · Environment (env vars / config consts reused) ·
   data-testid contract · phased Scope + Gate per increment · Acceptance-criteria -> test mapping ·
   Open Questions / Dependencies · Out of Scope.
4. Final forms only, sequential by default. Write only the final version of each anchor — no
   "wrong -> revised" trails — and keep increments sequential unless the slice genuinely parallelizes.
```

Commit:

```text
feat(frontend): manual clue release — HU-26

Ref: HU-26
Ref: DES-36
Ref: DES-70
```

---

## 9b. Implement the frontend plan

```text
Use @frontend/AGENTS.md.
Use the HU-26 frontend plan saved at @frontend/plans/<the plan file written in Step 9>.

Implement phase by phase in the plan's order, per the plan's own Scope / Gate / Commit Sequence.
The plan is the source of truth and supersedes the Step 9 seed scope.

Stop at any increment the plan marks blocked on an Open Question (name it). Do not re-generate the plan. Do not modify backend code. Do not restate per-phase scope or commit subjects from this prompt; the plan owns them.
```

---

## 10. Close-out

Acceptance criteria to verify before PR (from DES-36, canon-reworded):

- an operator can release a clue to a team during a valid (Active) session
- releasing a clue to one team does not release it to other teams
- the same clue cannot be released twice to the same team for the same Target
- the release is recorded in the session history (the local append-only ClueReleaseRecord)
- releasing a clue does not advance the substage or resolve a target
- no RabbitMQ publication (DES-92), SessionEvent history table (DES-56/HU-40A), conditional/auto release (HU-27), operator runtime clues (HU-28), evidence/QR/target resolution, or re-guarding of existing endpoints was added

```bash
gh pr create \
  --base develop \
  --head feature/hu-26-manual-clue-release \
  --title "feat(session-operations): manual clue release — HU-26" \
  --body "Adds HU-26 manual clue release (DES-36): an operator releases a treasure-hunt Target's optional hidden clue to one team or all teams during an Active session, gated to the assigned operator through the existing ISessionAdministrationAccessResolver ownership Proxy (Administrator all / Operator owns-else-403) and orchestrated by a new ClueReleaseFacade (Facade). Each release appends an append-only per-team ClueReleaseRecord (the local session history), never releases twice to the same team for the same Target, and never advances the substage or resolves a target. Exposes POST /api/sessions/{id}/clues/release and pushes the newly-visible clue to the affected team's live board over the reused team:{teamId} SignalR group (no leak to other teams), plus an operator release control and participant board reveal. Async ClueReleased publication (DES-92 -> DES-56/HU-40A), conditional/auto release (HU-27), operator runtime clues (HU-28), and evidence/QR/target resolution are out of scope." \
  --draft
```

## Rationale

HU-26's mandated patterns are `Facade` **and** `Proxy` because manual clue release is the context's first operator **write** over the runtime clue model: the matrix lists HU-26 in both the `Facade` column (line 40/119 — "orchestrates a runtime visibility change + history record `ClueReleasePolicy`") and the `Proxy` column (line 43/119 — "guards access to restricted clues; ADR-0004 names restricted clues explicitly"). The design risks are: (1) collapsing the orchestration (authorize → load → per-team fan-out → persist → event) into the command handler instead of the `ClueReleaseFacade` the PRD requires (ADR-0013); (2) re-inventing an ownership check inline instead of routing through `ISessionAdministrationAccessResolver` (the ceremony ADR-0012 forbids); and (3) treating clue release as progression — the canon reword is explicit that a clue is scoped to a `Target` inside the active substage and that **release does not advance the substage**. The slice therefore realizes the Facade in X.2, the Proxy in X.2 (resolver) + X.4 (endpoint policy), SignalR as the hard transport gate over the reused `team:{teamId}` group in X.4, and the per-team `ClueReleaseRecord` as the local history — deferring the async event publication to DES-92/DES-56.

**Ambiguity noted (canon-silent, recorded so the driver does not treat these as assumptions):**
- **Session-state gate.** Canon does not name an explicit state for release. A `Target` "inside the active substage" exists only after `Preparing → Active` starts the first substage, so the derivation gates on `State == Active`. `Paused` is canon-silent (submissions are blocked while paused; clue release is not mentioned) — the build defaults to Active-only; allowing `Paused` is a deliberate build decision, not an assumption.
- **Clue identity in the snapshot.** For treasure-hunt substages the clue guidance is embedded on `TargetSnapshot.ClueText` / `.ClueVisibilityPolicy`, so the release is keyed by `TargetId` (canon `ClueReleaseRecord.targetId`); store `clueId` when the snapshot carries a distinct clue node id, else null. Substage-level / trivia clue release is canon-silent (`ClueReleaseRecord.targetId` assumes a target) and is kept out of this HU's primary path.
- **`historial de la sesión`.** Canon models a `SessionEvent` append-only log, but the code never built one (HU-21A used domain events + outbox). The AC is satisfied locally by the append-only `ClueReleaseRecord`; the consolidated queryable event history is DES-56/HU-40A, fed by DES-92 — not this HU.

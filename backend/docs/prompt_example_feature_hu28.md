# Prompt Example — HU-28 Pistas operativas ad-hoc durante sesión en vivo (Feature Slice)

Concrete prompt sequence for driving `DES-38` / `HU-28` through a backend feature slice on `feature/hu-28-operative-clues`, then an operator + participant frontend slice. Follows the pattern in [workflow_for_prompts.md](./workflow_for_prompts.md).

**Key difference for HU-28:** the operator **authors a new ad-hoc operative clue** during a **live (Active or Paused)** session and assigns it to **one team or several** — a clue that did **not** exist in the mission. It is stored as **`LiveSession` runtime state** (a new `OperativeClue` child entity), **never** injected into the immutable `MissionRuntimeSnapshot` nor the source `Mission`, so "sin modificar la misión original" holds by construction. This is the sibling of HU-26 but the opposite verb: **HU-26 releases a *planned* clue; HU-28 authors a *new* one.** Per `required_patterns_matrix.md:121`, HU-28 has **no mandated pattern** (no `Facade`, no `Proxy` gate, no SignalR): the operator action **inherits** the existing session-ownership guard (`ISessionAdministrationAccessResolver`) and the standard Operator endpoint policy — reuse them, do not add a new pattern gate. Authoring changes **visibility only** — it does **not** advance the substage or resolve a target. Authoring `Target`s / mission structure (HU-10), mutating the snapshot, a consolidated `SessionEvent` history (DES-56/HU-40A), and conditional/auto release (HU-27) are out of scope.

When working from the monorepo root, make the target workload explicit in each prompt. For backend steps, point to `@backend/.agents/backend-agent.md`. For the frontend step, point to `@frontend/AGENTS.md`. Do not ask for backend and frontend implementation in the same phase prompt; coordinate them as separate scoped steps tied together by the verified API contract.

---

## Stop 1 acceptance guard

Reject this generated prompt before implementation if it does not explicitly scope all of the following:

- the operator **authors a new operative clue** (authored `clueText` not present in the mission) and assigns it to **one team or several**
- the clue is stored as **`LiveSession` runtime state** (`OperativeClue` child), **never** in the immutable `MissionRuntimeSnapshot` nor the source `Mission` — the mission is not modified
- access **inherits** the existing ownership guard — `ISessionAdministrationAccessResolver.GetAuthorizedSessionAsync` (Administrator all / Operator `AssignedOperatorUserId == actor.UserId` else `ForbiddenAccessException`) — **no new pattern gate, no ad-hoc role/owner `if`**
- **no mandated Facade** — the single-aggregate write is a plain command handler (mirror `AssignOperatorToSession`), not a facade
- the authoring appends an **append-only `OperativeClue`** per assigned team (teamId, clueText, createdByUserId, createdAt) — the local *historial de la sesión*; **not** a `SessionEvent` history table
- **no leak:** an operative clue assigned to one team surfaces on **that team's** board only
- **does not advance:** authoring changes visibility only; it does **not** advance the substage or resolve a target
- session validity gate is `State == Active || State == Paused`
- **SignalR is not mandated** (matrix line 59): the clue surfaces via the existing board projection; a live push is optional and non-gated
- **out of scope:** authoring `Target`s / mission structure (HU-10), snapshot/`Mission` mutation, a consolidated `SessionEvent` history (DES-56/HU-40A), RabbitMQ/MassTransit publication (DES-92), conditional/auto release (HU-27), manual release of planned snapshot clues (HU-26, shipped), evidence/QR (HU-29/30/31), and re-guarding existing endpoints

---

## Required design patterns

- **No mandated pattern** (`required_patterns_matrix.md` HU-28 row line 121; no-pattern row line 45; no-SignalR row line 59).
  - Do **not** add a `Facade`, a new `Proxy` gate, or a SignalR gate. Forcing any would be the exact "invent a pattern to fill the gap" defect the generator guards against.
- **Applies-where `Proxy` (note, not a gate).** The operator action inherits the **existing** `ISessionAdministrationAccessResolver` / `SessionAdministrationAuthorizationProxy` guard (ADR-0009) and the standard `[Authorize(Policy = AuthorizationPolicies.Operator)]` endpoint policy (ADR-0001/0002) it would get anyway. Reuse them; verify ownership as behaviour (non-owner → 403), **not** as a mandated-pattern gate line.

---

## Pre-resolved orient (as of 2026-07-13)

> Step 1 has already been run. Paste this section into any agent session that needs context before picking up a phase; no need to re-run the orient prompt unless source or Linear state changed.

### What predecessors have already landed

`DES-38` is live, **not superseded** (it is a *dependent* of the superseded rows DES-28→DES-76 / DES-30→DES-77, not itself in the superseded column), and carries both required labels. It has `canon-realign` **without** `needs-rebuild` → **feature flow with a canon-reword comment**. The reword (DES-38 `⚠️ Nota de canon`, 2026-07-13) reinterprets "agregar una pista" as **authoring an ad-hoc operative clue as `LiveSession` runtime state** — never in the immutable snapshot. Base is `develop` — all build-on predecessors are Done.

- **DES-36 (HU-26, PRIMARY mirror — Done/merged)** — the runtime clue-write pattern HU-28 copies: `ClueReleaseRecord` (append-only `LiveSession` child), `LiveSession.ReleaseClue`/`ReleaseClueToAllTeams` (per-team fan-out, one record + event per team), `_clueReleaseRecords` + `GetClueReleaseRecords()`, and the `CollectVisibleClues(teamId)` per-team projection (`LiveSession.cs:408,418,435,810,832,929`). HU-28 mirrors this shape for `OperativeClue` — but **authors** instead of releasing, and **omits the Facade** (not mandated).
- **DES-22/DES-24 (HU-15/HU-16, PRIMARY)** — `LiveSession` + **immutable** `MissionRuntimeSnapshot`; `CollectVisibleClues(teamId)` (`LiveSession.cs:810`) is the projection HU-28 extends per team. Runtime `Team` child carries `ReleasedClueCount`/`CurrentClueNodeId`.
- **DES-76 (HU-21A, PRIMARY)** — `LiveSession.State` lifecycle; HU-28 gates authoring on `State ∈ {Active, Paused}`. `SessionEvent` **now exists** but is specialized to state-changes (`SessionEvent.ForStateChange`) — HU-28 does not extend it.
- **DES-26 (HU-19, PRIMARY)** — operator assignment + `AssignedOperatorUserId` (the ownership seam the inherited guard checks); `AssignOperatorToSession` is the operator-scoped **write** slice to mirror (no facade).
- **DES-32 / HU-24A + DES-31 / HU-23** — `SessionAdministrationAuthorizationProxy` + `ISessionAdministrationAccessResolver.GetAuthorizedSessionAsync` (the inherited guard) and the participant team-board projection HU-28's operative clues ride on.

Superseded originals excluded: DES-23, DES-28, DES-30, DES-44, DES-47. **HU-30 (DES-95 evidence validation) and HU-31 (DES-42 QR target scan) are In Progress** but are the evidence/QR aggregate, not built on here, so the branch base stays `develop`.

### What HU-28 adds on top

| Concern | New work |
|---|---|
| Operator authors ad-hoc clue | Domain `LiveSession.AddOperativeClue` over an Active/Paused session |
| Per-team runtime clue | New append-only `OperativeClue` child entity (runtime state; the local *historial*) — one per assigned team |
| Command | Plain `AddOperativeClue` command/handler (inherited ownership guard) — **no facade** |
| Backend contract | `POST /api/sessions/{liveSessionId}/operative-clues` (operator-guarded) |
| Read reveal | Operative clues surface per team via the existing `CollectVisibleClues` board projection (live push optional, non-gated) |
| Frontend | Operator authoring control + participant board operative-clue reveal |

### Branch state and prerequisite

`feature/hu-28-operative-clues` should be branched from `develop`. No **build-on** same-service predecessor is In Progress, so there is no feature-branch dependency to inherit first.

### Linear state (as of 2026-07-13)

- DES-38 (HU-28): **In Progress**, labels: `svc:session-operations-service`, `Feature`, `canon-realign`, `ready-for-agent`
- DES-70 (PRD): **Backlog**, labels: `svc:session-operations-service`, `canon-realign`, `ready-for-agent`
- Same-service Done build-on predecessors: DES-36, DES-22, DES-24, DES-76, DES-31, DES-26, DES-32, DES-25, DES-77
- Same-service In Progress issues: HU-30 (DES-95), HU-31 (DES-42) — evidence/QR aggregate, untouched by this HU

> Linear live state may have changed. Use the Linear MCP to verify DES-38 status and labels if needed, but do not re-fetch PRD scope — read the local PRD file at `@backend/docs/prd/DES-70-primera-implementacion-de-session-operations-service-hu-15-a-hu-36.md` and the resolved context at `@backend/docs/hu28-context.md`.

---

## 1. Orient — read service state and the resolved HU-28 context

> **Skip this step if you have read the pre-resolved orient section above.** Run it only if the service source, README, or Linear state may have changed since 2026-07-13.

```text
Read the following files and summarize what has already landed and what HU-28 must add:
- @backend/services/session-operations-service/README.md
- @backend/services/session-operations-service/CONTEXT.md
- @backend/docs/prd/DES-70-primera-implementacion-de-session-operations-service-hu-15-a-hu-36.md
- @backend/docs/hu28-context.md

Then inspect only the current session-operations seams HU-28 anchors on:
- @backend/services/session-operations-service/src/Domain/Entities/LiveSession.cs (CollectVisibleClues + the per-team clue projection; ReleaseClue/ReleaseClueToAllTeams as the fan-out to mirror; MoveTo state guard)
- @backend/services/session-operations-service/src/Domain/Entities/ClueReleaseRecord.cs (the append-only per-team child entity to mirror for OperativeClue)
- @backend/services/session-operations-service/src/Domain/Entities/Team.cs (runtime team)
- @backend/services/session-operations-service/src/Application/Sessions/Commands/AssignOperatorToSession/AssignOperatorToSessionCommandHandler.cs (the operator-scoped single-aggregate write to mirror — no facade)
- @backend/services/session-operations-service/src/Application/Sessions/Common/Authorization/SessionAdministrationAuthorizationProxy.cs (the inherited ownership guard)
- @backend/services/session-operations-service/src/Api/Controllers/SessionsController.cs

Then use the Linear MCP to fetch only the current live state of:
- DES-38 (HU-28 - Agregar pistas operativas durante sesión en vivo)
- DES-70 (PRD - SessionOperations)

Output:
- the live DES-38 status and labels
- confirmation that HU-28 is not superseded and is feature flow (canon-realign without needs-rebuild → comment-only reword)
- the canonical scope: operator authors an ad-hoc OperativeClue (runtime state on LiveSession, never in the snapshot/Mission), assigns it to one/several teams, Active/Paused-gated, no leak, does not advance the substage, historial = the OperativeClue record; access inherits the existing ownership guard; NO mandated Facade/Proxy/SignalR
- the explicit exclusions: Target/mission-structure authoring (HU-10), snapshot/Mission mutation, SessionEvent history (DES-56/HU-40A), RabbitMQ (DES-92), conditional/auto release (HU-27), manual release of planned clues (HU-26), evidence/QR (HU-29/30/31), re-guarding existing endpoints

Do not start planning or implementing yet.
```

---

## 2. Label DES-38 as ready-for-agent

```text
Use the Linear MCP to confirm DES-38 still carries the label ready-for-agent.
If it is missing, add it (pass the full explicit label set to avoid dropping svc:session-operations-service / canon-realign / Feature).
Output the updated DES-38 ticket state and labels.
```

---

## 3. Confirm slice readiness

```text
Use the Linear MCP to confirm DES-38 carries both svc:session-operations-service
and ready-for-agent labels and output its current status and acceptance criteria.

The PRD scope is already in the local file at
@backend/docs/prd/DES-70-primera-implementacion-de-session-operations-service-hu-15-a-hu-36.md.
Do not re-fetch PRD scope from Linear. Apply the canon reword from DES-38's `⚠️ Nota de canon`
comment: "agregar una pista operativa" = author an ad-hoc OperativeClue as LiveSession runtime
state (never in the immutable snapshot/Mission); distinct from HU-26 (which releases planned clues).

Before planning, explicitly confirm the Stop 1 acceptance guard:
- operator authors a new operative clue (authored clueText) and assigns it to one team or several
- the clue is LiveSession runtime state (OperativeClue child), never in the snapshot/Mission
- access inherits the existing ownership guard (ISessionAdministrationAccessResolver.GetAuthorizedSessionAsync); no new pattern gate, no ad-hoc role/owner if
- NO mandated Facade — plain command handler (mirror AssignOperatorToSession)
- append-only OperativeClue per assigned team (the local historial); NOT a SessionEvent table
- no leak (per-team visibility); does not advance the substage
- session validity gate is State == Active || State == Paused
- SignalR not mandated; the clue surfaces via the existing board projection (optional non-gated push)
- out of scope: Target/mission-structure authoring (HU-10), snapshot/Mission mutation, SessionEvent history (DES-56/HU-40A), RabbitMQ (DES-92), HU-27, HU-26, evidence/QR, re-guarding existing endpoints

Output the confirmed HU id, title, acceptance criteria, labels, and the guard confirmation before planning the slice.
```

In the remaining examples below, `HU-28` and `DES-38` are the resolved values for this slice. `DES-70` is the shared PRD reference for `session-operations-service`.

---

## 4. Start the slice

```text
Prepare the HU-28 slice on branch feature/hu-28-operative-clues.
Use the HU id and DES id resolved from Linear in the previous step.
This slice affects backend `session-operations-service` and the operator + participant frontend surfaces.

The pre-resolved orient at the top of this document lists what existing code has already
landed and what HU-28 adds. Do not re-read the PRD for scoping unless you need to resolve an ambiguity.

Treat HU-28 as one operator write action: add a domain AddOperativeClue method + per-team OperativeClue child entity (X.1), a plain Proxy-inheriting AddOperativeClue command/handler — no facade (X.2), EF persistence + migration for the operative clues (X.3), and the operator endpoint (X.4). Authoring changes visibility only — it must not advance the substage or resolve a target, and it must never write to the MissionRuntimeSnapshot or the source Mission. Do not author Targets/mission structure (HU-10), extend SessionEvent history (DES-56/HU-40A), build RabbitMQ publication (DES-92), conditional/auto release (HU-27), manual release of planned clues (HU-26), evidence/QR (HU-29/30/31), or re-guard existing endpoints.

Move DES-38 to In Progress if the team process requires it, and output the exact scope,
branch name, base branch, and touched surfaces.
```

---

## 5. Backend phase X.1 — Domain layer

```text
Use @backend/.agents/backend-agent.md.
Implement backend phase X.1 for HU-28 in session-operations-service, per the
**X.1 derivation block in @backend/docs/hu28-context.md** (your spec — do not
re-read the canon or re-inspect the tree; open a cited canon section only to
fill a gap the block leaves open).

Gate:
- Domain build passes; unit tests cover OperativeClue, OperativeClueAddedEvent, and LiveSession.AddOperativeClue
- AddOperativeClue requires State == Active || State == Paused, a non-empty clueText, and non-empty/known teamIds (each a runtime Team) — invalid inputs rejected with the new exceptions
- assigning to several teams writes one OperativeClue + one OperativeClueAddedEvent per team (fan-out)
- authoring does NOT advance the substage or resolve a target, and never writes to the MissionRuntimeSnapshot / Mission
- an operative clue surfaces via CollectVisibleClues only for its assigned team (no leak)

Do not touch other backend layers or frontend.
```

Commit:

```text
feat(session-operations): phase X.1 - domain layer (HU-28)

Ref: HU-28
Ref: DES-38
Ref: DES-70
```

---

## 6. Backend phase X.2 — Application layer

```text
Use @backend/.agents/backend-agent.md.
Implement backend phase X.2 for HU-28 in session-operations-service, per the
**X.2 derivation block in @backend/docs/hu28-context.md** (your spec — do not
re-read the canon or re-inspect the tree; open a cited canon section only to
fill a gap the block leaves open).

Gate:
- Application build passes; handler tests cover the operator authoring command (one team + several teams)
- the handler is a plain command handler (mirror AssignOperatorToSessionCommandHandler) — NO facade
- access goes through the existing ISessionAdministrationAccessResolver.GetAuthorizedSessionAsync (inherited guard); ForbiddenAccessException for a non-owning operator; no ad-hoc role/owner if
- [Authorize(Roles="Operator")] on the command; authoring rejected when the session is not Active/Paused; authoring does not advance the substage

Do not touch Infrastructure, Api, or frontend.
```

Commit:

```text
feat(session-operations): phase X.2 - application layer (HU-28)

Ref: HU-28
Ref: DES-38
Ref: DES-70
```

---

## 7. Backend phase X.3 — Infrastructure layer

```text
Use @backend/.agents/backend-agent.md.
Implement backend phase X.3 for HU-28 in session-operations-service, per the
**X.3 derivation block in @backend/docs/hu28-context.md** (your spec — do not
re-read the canon or re-inspect the tree; open a cited canon section only to
fill a gap the block leaves open).

Gate:
- Infrastructure build passes
- OperativeClue persists as an EF OwnsMany collection on LiveSession (mirror the ClueReleaseRecord OwnsMany)
- `ef migrations add AddOperativeClues` succeeds and represents the new owned collection
- repository/integration test proves round-trip of a LiveSession + operative-clue records (team/clueText/createdBy/createdAt persisted)
- grep the model snapshot for the new table; do not full-read it

Do not touch Api or frontend.
```

Commit:

```text
feat(session-operations): phase X.3 - infrastructure layer (HU-28)

Ref: HU-28
Ref: DES-38
Ref: DES-70
```

---

## 8. Backend phase X.4 — Api layer

```text
Use @backend/.agents/backend-agent.md.
Implement backend phase X.4 for HU-28 in session-operations-service, per the
**X.4 derivation block in @backend/docs/hu28-context.md** (your spec — do not
re-read the canon or re-inspect the tree; open a cited canon section only to
fill a gap the block leaves open).

Gate:
- endpoint integration tests cover POST /api/sessions/{liveSessionId}/operative-clues: 200 for the assigned operator authoring to one team and to several teams; 403 (RFC 7807) for a non-owning operator; empty clueText / empty teamIds -> 400 or 422; authoring on a non-Active/Paused session -> 409
- new domain exceptions mapped in ProblemDetailsExceptionHandler
- the operative clue appears on an assigned team's board and NOT on a non-assigned team's board (no leak); authoring does not advance the substage
- access remains [Authorize(Policy=Operator)] + the existing ownership resolver; no ad-hoc authorization in the controller
- SignalR is NOT mandated: do not add a SignalR gate. A live push (OperativeClueAddedEvent -> existing team board broadcaster) is optional; build it only if the live reveal is wanted and keep it out of the gate
- `backend/docs/adr/0005-coverlet-msbuild-for-aggregate-coverage.md` coverage gate passes

Do not touch frontend.
```

Commit:

```text
feat(session-operations): phase X.4 - api layer (HU-28)

Ref: HU-28
Ref: DES-38
Ref: DES-70
```

---

## 8.5. Docker rebuild and smoke

```text
docker compose build session-operations-service api-gateway
docker compose up -d session-operations-service api-gateway

Smoke through the gateway:
- authenticate as the assigned operator of an Active session with at least two teams
- POST /api/sessions/{liveSessionId}/operative-clues with { clueText, teamIds:[teamA] } -> 200; verify the response reports the created operative clue id(s) and the assigned team(s)
- POST with { clueText, teamIds:[teamA, teamB] } -> 200 (assigns to several teams)
- move the session to Paused and POST again -> 200 (authoring is allowed while Paused)
- POST with an empty clueText or empty teamIds -> 400/422 (ProblemDetails)
- authenticate as a different (non-owning) operator, POST the same -> 403/ProblemDetails (RFC 7807)
- read the participant board as a member of teamA and confirm the operative clue is visible; read it as a member of a non-assigned team and confirm it is NOT visible; confirm the substage did not advance
```

---

## 9. Operator + participant frontend slice

```text
Generate a multi phase plan in a markdown file — following the **frontend plan concreteness rule** (below), modelled on the exemplar closest to this slice's shape (@frontend/plans/hu-03-frontend-role-permission-assignment.md for a small 1-few-endpoint surface; @frontend/plans/hu-10a-frontend-mission-hierarchy-authoring.md for a large/multi-endpoint or partially-blocked surface) — save it in @frontend/plans/ for the following:
Use @frontend/AGENTS.md.

Build the HU-28 operative-clue frontend against the verified backend contract:
- HTTP action: POST /api/sessions/{liveSessionId}/operative-clues (operator-authenticated) with { clueText, teamIds:[...] } (one or several team ids), returning the created operative clue id(s) + assigned team ids + clueText
- operator surface (web): a control on the operator session view to author an operative clue (free text) and assign it to one selected team or several/all; disable/hide it unless the session is Active or Paused; surface forbidden (non-owning operator) and validation (empty text / no teams) as non-crashing states
- participant surface: the participant board shows operative clues assigned to that team, read from the existing board projection; a team must not see another team's operative clue. A live push is not guaranteed by the backend (SignalR is not mandated for HU-28) — refresh on the existing board update if present, else on the board's normal fetch; do not assume a new SignalR invoke/group
- reuse the existing operator/participant auth/session context and the existing team-board client/hook; do NOT render the clue as progress, and do not advance the substage from the UI

Frontend plan concreteness rule:
1. Proportion concreteness to certainty. Write code-complete detail — exact DTO/request types, real
   component skeletons, exact client-fn + server-action bodies, a data-testid contract — only for the
   fully-knowable near-term increments (the authoring action client-fn + the operator authoring control +
   the participant board operative-clue reveal off the existing board projection). Keep later, large, or
   blocked increments at contract + gate altitude: a contract table, scope, and gate, with no invented
   bodies. Never write code for an increment blocked on an open question.
2. Verify every code anchor against the real source before writing it. Open the files the plan names —
   the operator session views, the existing team-board client/hook, the identity/header helpers, the host
   const/env it reads — and write only what the source actually supports. A confident-but-wrong anchor is
   worse than an altitude note. If a detail is not verifiable, state the assumption under Open Questions
   rather than inventing it.
3. Required sections (a plan missing one is a defect): Context · Verified Backend Contract
   (endpoint/shape table) · Architecture Decisions · Environment (env vars / config consts reused) ·
   data-testid contract · phased Scope + Gate per increment · Acceptance-criteria -> test mapping ·
   Open Questions / Dependencies · Out of Scope.
4. Final forms only, sequential by default. Write only the final version of each anchor — no
   "wrong -> revised" trails — and keep increments sequential unless the slice genuinely parallelizes.
```

Commit:

```text
feat(frontend): operative clues during live session — HU-28

Ref: HU-28
Ref: DES-38
Ref: DES-70
```

---

## 9b. Implement the frontend plan

```text
Use @frontend/AGENTS.md.
Use the HU-28 frontend plan saved at @frontend/plans/<the plan file written in Step 9>.

Implement phase by phase in the plan's order, per the plan's own Scope / Gate / Commit Sequence.
The plan is the source of truth and supersedes the Step 9 seed scope.

Stop at any increment the plan marks blocked on an Open Question (name it). Do not re-generate the plan. Do not modify backend code. Do not restate per-phase scope or commit subjects from this prompt; the plan owns them.
```

---

## 10. Close-out

Acceptance criteria to verify before PR (from DES-38, canon-reworded):

- during an Active or Paused session, the assigned operator (or an Administrator) can author an operative clue that did not exist in the mission
- the operative clue is stored as LiveSession runtime state; the source Mission and MissionRuntimeSnapshot are unchanged
- the operative clue can be assigned to one or several teams and is visible only to the assigned teams (no leak)
- the authoring is recorded in the session history (the local append-only OperativeClue record)
- authoring or assigning an operative clue does not advance the substage or resolve any target
- no Target/mission-structure authoring (HU-10), snapshot/Mission mutation, SessionEvent history (DES-56/HU-40A), RabbitMQ publication (DES-92), conditional/auto release (HU-27), release of planned clues (HU-26), evidence/QR, or re-guarding of existing endpoints was added

```bash
gh pr create \
  --base develop \
  --head feature/hu-28-operative-clues \
  --title "feat(session-operations): operative clues during live session — HU-28" \
  --body "Adds HU-28 ad-hoc operative clues (DES-38): during an Active or Paused session, an operator authors a new operative clue (free text that did not exist in the mission) and assigns it to one team or several, gated to the assigned operator through the existing ISessionAdministrationAccessResolver ownership guard (Administrator all / Operator owns-else-403, ADR-0009) — no new pattern. The clue is stored as LiveSession runtime state (a new append-only per-team OperativeClue child entity, the local session history), never injected into the immutable MissionRuntimeSnapshot or the source Mission, so the mission is unmodified. Each operative clue is visible only to its assigned team(s) (no leak) and never advances the substage or resolves a target. Exposes POST /api/sessions/{id}/operative-clues and surfaces the clue on the assigned teams' boards via the existing projection, plus an operator authoring control and participant board reveal. Per the patterns matrix HU-28 has no mandated Facade/Proxy/SignalR. Target/mission-structure authoring (HU-10), snapshot mutation, consolidated SessionEvent history (DES-56/HU-40A), async publication (DES-92), conditional/auto release (HU-27), and manual release of planned clues (HU-26) are out of scope." \
  --draft
```

## Rationale

HU-28 has **no mandated pattern**: `required_patterns_matrix.md` lists it in the no-pattern row (line 45), the no-SignalR row (line 59), and its HU→pattern row is `HU-28 | — | —` (line 121, "Operator adds operational clues to a live session without modifying the source mission … re-scoped to runtime clue addition"). The design risks are therefore the mirror-image of HU-26's: (1) **over-scoping** by copying HU-26's mandated `Facade`/`Proxy`/SignalR gates onto a HU the matrix explicitly leaves pattern-free — the single-aggregate write is a plain command handler that *reuses* the existing ownership guard, and a facade or new proxy gate here is the ceremony ADR-0012 forbids; (2) **breaking snapshot immutability** — the whole scope decision rests on storing the operative clue as `LiveSession` runtime state (a new `OperativeClue` child), never in the `MissionRuntimeSnapshot` or the source `Mission`, so any snapshot-mutation path is wrong; and (3) **conflating HU-28 with HU-26** — HU-26 *releases* a planned snapshot clue, HU-28 *authors* a new ad-hoc one; they share structure (per-team fan-out, `CollectVisibleClues` projection, the append-only record as *historial*) but not verb or scope.

**Ambiguity noted (canon-silent, recorded so the driver does not treat these as assumptions):**
- **The `OperativeClue` concept is canon-silent.** No `OperativeClue` / "pista operativa" appears in `bd_umbral_entity_spec.md` / `ddd_solution_model.md` / `grilling-session-mission-restructure.md`. Its authority is the reworded DES-38 AC + `⚠️ Nota de canon` + matrix line 121; its shape is mirrored from HU-26's merged `ClueReleaseRecord`. This is the realignment map's "model as an explicit exception" option (line 113), resolved by layering rather than by exception code.
- **Session-state gate.** HU-26 gated release on `State == Active` (Paused was a build decision it declined). HU-28's reworded AC explicitly allows **Paused**, so the gate is `State == Active || State == Paused`.
- **`historial de la sesión`.** `SessionEvent` now exists but is specialized to state-changes (`SessionEvent.ForStateChange`). The AC is satisfied locally by the append-only `OperativeClue` record (actor/time/team); the consolidated queryable event history is DES-56/HU-40A — extending `SessionEvent` here is a deliberate build decision, not required.
- **Live reveal.** SignalR is not mandated (matrix line 59). The clue surfaces via the existing `CollectVisibleClues` projection; an `OperativeClueAddedEvent` → existing team-board broadcaster push is optional and explicitly kept out of the phase gate.

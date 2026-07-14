# HU-28 — Driver brief
_Driver reads only this file. The full prompt + context are for the subagent
and human review; the driver never loads them._

**Nature of this HU:** feature build (`canon-realign` **without** `needs-rebuild` →
comment-only canon reword, not a rebuild). HU-28 is one operator **write** that **authors a
new ad-hoc operative clue** during a live session: X.1 adds a `LiveSession.AddOperativeClue`
domain method + an append-only per-team `OperativeClue` child entity; X.2 adds a plain
`AddOperativeClue` command/handler that **reuses** the existing ownership guard (**no
facade**); X.3 persists the operative clues + a migration; X.4 adds the operator endpoint.
The clue is **`LiveSession` runtime state — never** written to the immutable
`MissionRuntimeSnapshot` or the source `Mission` (so the mission is unmodified). Authoring
changes **visibility only** — it must not advance the substage or resolve a target. **Do not
authorize** a subagent to: add a `Facade`/`Proxy`/SignalR gate (HU-28 has **no mandated
pattern**), author `Target`s / mission structure (HU-10), mutate the snapshot, extend
`SessionEvent` history (DES-56/HU-40A), build RabbitMQ publication (DES-92), conditional/auto
release (HU-27), manual release of planned clues (HU-26, shipped), evidence/QR (HU-29/30/31),
or re-guard existing endpoints.

## Slice
| HU | DES | PRD | Service | Branch | Base |
|----|-----|-----|---------|--------|------|
| HU-28 — Pistas operativas ad-hoc durante sesión en vivo | DES-38 | DES-70 | session-operations-service | feature/hu-28-operative-clues | develop |

## Required pattern(s) → owning phase
- **None mandated** (`required_patterns_matrix.md` HU-28 row line 121; no-pattern row line 45; no-SignalR row line 59). Do **not** force a Facade/Proxy/SignalR gate.
- Applies-where `Proxy` (note, **not** a gate): the operator action inherits the existing `ISessionAdministrationAccessResolver` ownership guard (Administrator all / Operator owns-else-`ForbiddenAccessException`, ADR-0009) + the standard `[Authorize(Policy=Operator)]` endpoint policy. Reuse them; verify ownership as behaviour (non-owner → 403), not as a pattern gate.

## Per phase — gate + owned pattern
| Phase | Gate | Pattern |
|-------|------|---------|
| X.1 Domain | Domain build; unit tests — `LiveSession.AddOperativeClue` requires `State == Active \|\| State == Paused`, non-empty `clueText`, non-empty/known `teamIds`; assigning to several teams writes one `OperativeClue` + one `OperativeClueAddedEvent` per team (fan-out); authoring does **not** advance the substage/resolve a target and never writes to the snapshot/`Mission`; the clue surfaces via `CollectVisibleClues` only for its assigned team (no leak) | — |
| X.2 Application | App build; handler tests (one team + several teams); **plain handler, no facade** (mirror `AssignOperatorToSession`); access via the existing ownership resolver (`ForbiddenAccessException` for non-owner, no ad-hoc `if`); `[Authorize(Roles="Operator")]`; rejected when session not Active/Paused; does not advance the substage | — |
| X.3 Infrastructure | Infra build; `OperativeClue` persists as EF `OwnsMany` on `LiveSession` (mirror the `ClueReleaseRecord` `OwnsMany`); `ef migrations add AddOperativeClues` succeeds + represents it; repo/integration test round-trips the operative-clue records (team/clueText/createdBy/createdAt) | — |
| X.4 Api | `POST /api/sessions/{id}/operative-clues` → 200 (one team + several teams) for assigned operator, 403 RFC 7807 for non-owner, 400/422 on empty clueText/teamIds, 409 when not Active/Paused; new exceptions mapped; clue appears on assigned team's board and **not** a non-assigned team's (no leak), does not advance the substage; **no SignalR gate** (optional non-gated push); coverage (ADR-0005) | — |

Commit subjects — copy each phase's exact subject from the prompt's Steps 5–8 verbatim:
- X.1 `feat(session-operations): phase X.1 - domain layer (HU-28)`
- X.2 `feat(session-operations): phase X.2 - application layer (HU-28)`
- X.3 `feat(session-operations): phase X.3 - infrastructure layer (HU-28)`
- X.4 `feat(session-operations): phase X.4 - api layer (HU-28)`

Trailer (every phase): `Ref: HU-28` / `Ref: DES-38` / `Ref: DES-70`

## Acceptance criteria
- during an Active or Paused session, the assigned operator (or an Administrator) can author an operative clue that did not exist in the mission
- the operative clue is stored as `LiveSession` runtime state; the source `Mission` and `MissionRuntimeSnapshot` are unchanged
- the operative clue can be assigned to one or several teams and is visible only to the assigned teams (no leak)
- the authoring is recorded in the session history (the local append-only `OperativeClue` record)
- authoring or assigning an operative clue does not advance the substage or resolve any target
- no `Target`/mission-structure authoring (HU-10), snapshot/`Mission` mutation, `SessionEvent` history (DES-56/HU-40A), RabbitMQ publication (DES-92), conditional/auto release (HU-27), release of planned clues (HU-26), evidence/QR, or re-guarding of existing endpoints was added

## Endpoints + smoke (driver verifies at Stop 2)
- `POST /api/sessions/{liveSessionId}/operative-clues` — operator-auth write; as the **assigned** operator on an **Active** session with `{ clueText, teamIds:[teamA] }` expect **200** reporting the created operative clue id(s) + assigned team(s); request/response shape is the frontend contract
- same endpoint with `{ clueText, teamIds:[teamA, teamB] }` — expect **200** (assigns to several teams)
- same endpoint on a **Paused** session — expect **200** (authoring allowed while Paused)
- same endpoint with empty `clueText` or empty `teamIds` — expect **400/422** (ProblemDetails)
- same endpoint as a **non-owning** operator — expect **403** RFC 7807
- participant board — as a member of `teamA` the operative clue is visible; as a member of a non-assigned team it is **not** (no leak); the substage does **not** advance

## Frontend slice
Human-driven — see Steps 9 (generate plan) + 9b (implement it) of
`prompt_example_feature_hu28.md`. Operator **web** authoring control + participant board
operative-clue reveal (`@frontend/AGENTS.md`); pick the plan exemplar by shape (small
1–few-endpoint surface → `hu-03`). Primary outcome: operator authors an operative clue and
assigns it to one/several teams (POST) and the participant board reveals it for the assigned
teams via the existing projection — SignalR live push not mandated, no clue-as-progress, no
substage advance from the UI.

# HU-26 — Driver brief
_Driver reads only this file. The full prompt + context are for the subagent
and human review; the driver never loads them._

**Nature of this HU:** feature build (`canon-realign` **without** `needs-rebuild` →
comment-only canon reword, not a rebuild). HU-26 is the context's first operator **write**
over the runtime clue model: X.1 adds a `LiveSession.ReleaseClue` domain method + an
append-only per-team `ClueReleaseRecord`; X.2 adds a `ClueReleaseFacade` (Facade) + a
Proxy-guarded `ReleaseClue` command; X.3 persists the release records + a migration; X.4
adds the operator endpoint + affected-team SignalR push. Release changes **visibility
only** — it must not advance the substage or resolve a target. Do not authorize a
subagent to build RabbitMQ publication (DES-92), a `SessionEvent` history table
(DES-56/HU-40A owns it), conditional/auto release (HU-27), operator runtime clues
(HU-28), evidence/QR/target resolution (HU-29/30/31), or to re-guard existing endpoints.

## Slice
| HU | DES | PRD | Service | Branch | Base |
|----|-----|-----|---------|--------|------|
| HU-26 — Liberación manual de pistas | DES-36 | DES-70 | session-operations-service | feature/hu-26-manual-clue-release | develop |

## Required pattern(s) → owning phase
- `Facade` (phase X.2) — orchestrates a runtime visibility change + local trace across the aggregate, ownership guard, and persistence — obligation: single `ClueReleaseFacade` / `IClueReleaseFacade` (`ReleaseCluesAsync`) entry point over resolver (Proxy) → load → per-team fan-out → persist; mirror `SessionTeamAssociationFacade`, no inline handler/controller coordination.
- `Proxy` (phase X.2 + X.4) — restricted clues gated to the assigned operator — obligation: access via `ISessionAdministrationAccessResolver.GetAuthorizedSessionAsync` / `SessionAdministrationAuthorizationProxy` (Administrator all / Operator `AssignedOperatorUserId == actor.UserId` else `ForbiddenAccessException`, ADR-0009); no ad-hoc role/owner `if` in handler, facade, controller, or hub.
- Transport: SignalR — the newly-visible clue pushes to the reused `team:{teamId}` group via `ITeamBoardBroadcaster`; a release to one team must not reach another team's connections.

## Per phase — gate + owned pattern
| Phase | Gate | Pattern |
|-------|------|---------|
| X.1 Domain | Domain build; unit tests — `LiveSession.ReleaseClue`/`ReleaseClueToAllTeams` require `State == Active`, resolve a `HiddenUntilOperatorRelease` clue on a `Target` in the active treasure-hunt substage, enforce `(teamId, targetId)` uniqueness (duplicate rejected); all-teams fan-out = one `ClueReleaseRecord` + `ClueReleasedEvent` per team; release does **not** advance the substage/resolve the target; released clue surfaces only for the released team (no leak) | — |
| X.2 Application | App build; handler + facade tests (one team + all teams); orchestration behind `ClueReleaseFacade` (registered), not inline; access via the ownership resolver Proxy (`ForbiddenAccessException` for non-owner, no ad-hoc `if`); `[Authorize(Roles="Operator")]`; duplicate rejected; release does not advance the substage | `Facade` + `Proxy` |
| X.3 Infrastructure | Infra build; `ClueReleaseRecord` persists as EF `OwnsMany` on `LiveSession` (mirror runtime `Teams`); `ef migrations add` succeeds + represents it; `Teams.ReleasedClueCount` increment round-trips; repo/integration test round-trips the release records | — |
| X.4 Api | `POST /api/sessions/{id}/clues/release` → 200 (one team + all teams) for assigned operator, 403 RFC 7807 for non-owner, 409/ProblemDetails on duplicate; new exceptions mapped; SignalR push reaches only the affected `team:{teamId}` board (no leak), release does not advance the substage; coverage gate (ADR-0005) | `Proxy` |

Commit subjects — copy each phase's exact subject from the prompt's Steps 5–8 verbatim:
- X.1 `feat(session-operations): phase X.1 - domain layer (HU-26)`
- X.2 `feat(session-operations): phase X.2 - application layer (HU-26)`
- X.3 `feat(session-operations): phase X.3 - infrastructure layer (HU-26)`
- X.4 `feat(session-operations): phase X.4 - api layer (HU-26)`

Trailer (every phase): `Ref: HU-26` / `Ref: DES-36` / `Ref: DES-70`

## Acceptance criteria
- an operator can release a clue to a team during a valid (Active) session
- releasing a clue to one team does not release it to other teams
- the same clue cannot be released twice to the same team for the same `Target` (canon reword: per Target, not "la etapa")
- the release is recorded in the session history (the local append-only `ClueReleaseRecord`)
- releasing a clue does not advance the substage or resolve a target
- no RabbitMQ publication (DES-92), `SessionEvent` history table (DES-56/HU-40A), conditional/auto release (HU-27), operator runtime clues (HU-28), evidence/QR/target resolution, or re-guarding of existing endpoints was added

## Endpoints + smoke (driver verifies at Stop 2)
- `POST /api/sessions/{liveSessionId}/clues/release` — operator-auth write; as the **assigned** operator with `{ targetId, teamId }` expect **200** reporting the released team(s) + target; request/response shape is the frontend contract
- same endpoint, same `{ targetId, teamId }` **again** — expect duplicate rejected (**409**/ProblemDetails)
- same endpoint with `{ targetId }` and **no** teamId — expect **200** releasing to all teams
- same endpoint as a **non-owning** operator — expect **403** RFC 7807
- SignalR — as a participant of the released team, the newly-visible clue arrives on `team:{teamId}` without reload; as a participant of a **different** team it does **not** arrive; the substage does **not** advance

## Frontend slice
Human-driven — see Steps 9 (generate plan) + 9b (implement it) of
`prompt_example_feature_hu26.md`. Operator **web** release control + participant board
clue reveal (`@frontend/AGENTS.md`); pick the plan exemplar by shape (small
1–few-endpoint surface → `hu-03`). Primary outcome: operator releases a target's hidden
clue to one/all teams (POST) and the participant board reveals it live over the existing
`team:{teamId}` push — no clue-as-progress, no substage advance from the UI.

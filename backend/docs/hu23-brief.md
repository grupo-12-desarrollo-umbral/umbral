# HU-23 — Driver brief
_Driver reads only this file. The full prompt + context are for the subagent
and human review; the driver never loads them._

> **Nature of this HU:** canon-realigned feature build — DES-31 is not superseded and not `needs-rebuild`, but its AC wording is stale. Build the live board as **target-based active-substage progress + optional visible clues**, with HU-22 timer reuse and team-scoped SignalR updates. Do not authorize score ledger/ranking, QR target validation, clue release authoring, evidence intake, or `ScoringMonitoring` work.

## Slice
| HU | DES | PRD | Service | Branch | Base |
|----|-----|-----|---------|--------|------|
| HU-23 — Tablero de equipo en vivo | DES-31 | DES-70 | session-operations-service | feature/hu-23-live-team-board | develop |

## Required pattern(s) → owning phase
- none mandated — `HU-23` is a real-time read projection. Transport SignalR is required and verified in X.4. Participant/team scoping uses the existing `[Authorize(Participant)]` + `IRuntimeParticipationGuard`/team-group admission; no new Proxy gate.

## Per phase — gate + owned pattern
| Phase | Gate | Pattern |
|-------|------|---------|
| X.1 Domain | Domain build; unit tests prove the board resolves only the requested team, score is current-or-zero, treasure-hunt progress counts active targets not clues, visible clues are guidance only, and trivia board uses active timer context without inventing target progress | — |
| X.2 Application | App build; handler tests prove `IRuntimeParticipationGuard.EnsureAllowedAsync` runs before returning data, DTO maps score/timer/progress/clues, NotFound/rejection branches work, and no ad-hoc authorization appears | — |
| X.3 Infrastructure | Infra build; repository/integration test hydrates teams + active-substage targets/questions + any persisted progress/clue state; no `winner_score` regression; migration absent unless minimal board state is justified | — |
| X.4 Api | Endpoint tests cover authorized 200 + unauthorized/other-team rejection; SignalR test proves update reaches `team:{teamId}` and not another team; `backend/docs/adr/0005-coverlet-msbuild-for-aggregate-coverage.md` coverage | — (SignalR transport) |

Commit subjects — copy each phase's exact subject from the prompt's Steps 5-8 verbatim:
- X.1 `feat(session-operations): phase X.1 - domain layer (HU-23)`
- X.2 `feat(session-operations): phase X.2 - application layer (HU-23)`
- X.3 `feat(session-operations): phase X.3 - infrastructure layer (HU-23)`
- X.4 `feat(session-operations): phase X.4 - api layer (HU-23)`

Trailer (every phase): `Ref: HU-23` / `Ref: DES-31` / `Ref: DES-70`

## Acceptance criteria
- participant sees accumulated/current score for their own team
- participant sees optional visible clues without treating clues as progress
- participant sees active-substage target progress where treasure-hunt target data exists
- participant sees the authoritative timer from HU-22
- board updates without manual reload via SignalR/WebSockets
- unauthorized participant/team access is rejected
- out of scope: score ledger/ranking, QR target validation, clue release authoring, evidence intake, penalties, and `ScoringMonitoring`

## Endpoints + smoke (driver verifies at Stop 2)
- `GET /api/sessions/{liveSessionId}/participants/team-board?teamId={teamId}&token={token}` — Participant -> expect **200** for the participant's team; response shape includes team identity, score/current-or-zero, `SessionTimerSnapshotDto`, active-substage target progress, visible clues
- Same endpoint with another team id/token mismatch -> expect **403 ProblemDetails** or existing forbidden mapping
- SignalR `/hubs/sessions` after participant reconnect -> board update reaches `team:{teamId}` without reload and does not reach another team group

## Frontend slice
Human-driven — see Steps 9 (generate plan) + 9b (implement it) of
`prompt_example_feature_hu23.md`.

## Ambiguities / assumptions to keep visible
- **Score source:** DES-31 can start before `ScoringMonitoring`; show current/session-owned score or zero, not a ledger/ranking.
- **Target progress source:** if target-resolution persistence is not present yet, show 0 resolved over active targets and add only a minimal projection seam if necessary; do not implement QR validation/evidence intake.
- **Visible clues source:** read already-visible/released clue guidance only; clue release itself belongs to HU-26/HU-28.

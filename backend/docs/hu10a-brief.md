# HU-10A — Driver brief
_Driver reads only this file. The full prompt + context are for the subagent
and human review; the driver never loads them._

> **Nature of this HU:** HU-09's rebuild (DES-14, Done) already implemented the
> entire backend. Backend phases X.1–X.4 are **verification only** (the planned
> X.2 exception-mapping refinement rested on a stale premise — already 409
> Conflict, no code change), not new implementation — never let a subagent
> rebuild existing domain types or endpoints. The main new work is the frontend
> (Step 9, human-driven).

## Slice
| HU | DES | PRD | Service | Branch | Base |
|----|-----|-----|---------|--------|------|
| HU-10A — Estructura jerarquica de misiones | DES-15 | DES-62 | mission-design-service | feature/hu-10a-mission-hierarchy-structure | develop |

## Required pattern(s) → owning phase
- `Composite` (phase X.1 Domain; carried through X.2 Application and X.4 API
  contract) — Why: `Mission` owns the hierarchical authoring structure;
  `MissionNode` models the `Stage` → `Substage` → optional `Clue` tree while
  treasure-hunt `Target`s stay attached to their owning `Substage`; the domain,
  not handlers, enforces which children each node admits — obligation: a real
  Composite over ordered `MissionNode`s with containment rules, play-mode
  enforcement, and traversal/readiness centralized in domain entities + policy
  (not flattened records or handler conditionals). **Already implemented by
  HU-09 — verify it is structurally present; do not rebuild.** (Mandated by
  ADR-0004 + CONTEXT.md §Required Patterns, not the trivia matrix, which omits
  mission HUs.)

## Per phase — gate + owned pattern
| Phase | Gate | Pattern |
|-------|------|---------|
| X.1 Domain | Build + existing domain tests pass (`MissionNodeTests`, `MissionStructureTests`, `SubstageTests`, `MissionActivationPolicyTests`, `MissionActivationPolicyRuntimePlanTests`); Composite present as `Mission → MissionNode(Stage/Substage/Clue)`, not flattened; one `SubstagePlayMode` per substage; Target-based treasure hunt, Clue optional max one per target; rejections throw `InvalidMissionNodeChildException` naming parent+child; readiness validates runtime plan; no `SessionMode`; **no new domain types** | `Composite` (verify present) |
| X.2 Application | Build + existing handler/validator tests (valid + rejection branches); Composite preserved in handlers (no flattened traversal); activation/readiness via `MissionActivationPolicy`; **`MissionAlreadyDeactivatedException` already mapped to 409 Conflict by HU-09 (shared arm with `MissionAlreadyActiveException`) — verify only, no code change**; no `SessionMode`; **no new commands/handlers** | `Composite` preserved in handlers |
| X.3 Infrastructure | Build passes; existing EF migration/snapshot represents Mission wrapper + MissionNode Composite + Target + optional Clue + `SubstagePlayMode` + TriviaQuizSelection; repository integration tests prove deep round-trip; **no new migration**; no `SessionMode` in schema | — |
| X.4 Api | Existing endpoint tests pass (CRUD/detail/readiness + full structure flow: nodes, play-mode, targets, clue-association, trivia-quiz-selection, activate); `MissionAlreadyDeactivatedException` returns **409 Conflict** at endpoint level; `MissionResponse` exposes the full Composite tree; no `SessionMode` / `TriviaQuiz`-as-`SessionSource` in any payload; **no new endpoints** + ADR-0005 coverage (service ≥ repo gate) | `Composite` in detail contract (`MissionResponse → MissionStageResponse → MissionSubstageResponse → MissionTargetResponse / MissionClueResponse`) |

Commit subjects (present per phase — match the prompt exactly):
- X.1 `feat(mission-design): phase X.1 - domain layer verification (HU-10A)`
- X.2 `feat(mission-design): phase X.2 - application layer verification (HU-10A)`
- X.3 `feat(mission-design): phase X.3 - infrastructure layer verification (HU-10A)`
- X.4 `feat(mission-design): phase X.4 - api layer verification (HU-10A)`

Trailer (every phase): `Ref: HU-10A` / `Ref: DES-15` / `Ref: DES-62`

## Acceptance criteria
- `MissionNode` Composite (`Stage`, `Substage`, `Clue`) — verified in backend tests
- containment rules enforced by domain entities, not handlers — verified
- each `Substage` has exactly one `SubstagePlayMode` (`TreasureHunt` | `Trivia`) — verified
- treasure-hunt progress is `Target`-based, not clue-based — verified
- `Clue` optional guidance, max one per target, same substage — verified
- every rejection informs parent + child node types — verified
- readiness/activation validates the runtime plan — verified
- `MissionAlreadyDeactivatedException` mapped to 409 Conflict (HU-09) — verified, no code change
- frontend hierarchy authoring UI consumes the existing backend contract — implemented
- no `SessionMode`; no `TriviaQuiz` as `SessionSource`

## Endpoints + smoke (driver verifies at Stop 2)
**No new endpoints** — HU-09 exposed the full `/api/missions` set. After the X.4
docker rebuild (`docker compose build mission-design-service api-gateway &&
docker compose up -d mission-design-service api-gateway`), smoke through the gateway:
- `GET /api/missions` — mission catalog — expect 200
- `GET /api/missions/{id}` — detail for a mission with hierarchy — expect 200, full Composite tree (stages/substages/targets/clues)
- `POST /api/missions/{id}/nodes` — add a stage / substage / clue — expect 200/201
- `GET /api/missions/{id}/readiness` + `POST /api/missions/{id}/activate` — readiness/activation validation — expect 200 / readiness failures
- deactivate an already-deactivated mission — **expect 409 Conflict** (mapped by HU-09; the "400 / not 500" premise was stale)
- Frontend contract: `MissionResponse` with the full Composite tree (stages → substages → targets/clues, play modes, readiness).

## Frontend slice
Human-driven — see Step 9 of `prompt_example_feature_hu10a.md`.

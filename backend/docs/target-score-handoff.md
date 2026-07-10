# Target Score Handoff — Per-Target Scoring for Treasure Hunt

Date: 2026-07-09

## Context

The canonical docs described an old **winner-takes-all** scoring model for treasure-hunt substages (`WinnerScoreValue` at the `Substage` level, only the first team to resolve all targets gets points, others get zero).

The correct model is **per-target scoring**:
- Each `Target` has its own `ScoreValue` (integer 1-100)
- Teams accumulate points from each target they resolve
- The first team to resolve all targets becomes `TreasureHuntSubstageWinner` — this triggers **advancement**: all teams move to the next substage
- All teams keep their earned points regardless of who won the substage
- A team cannot resolve the same target twice

## What was updated (✅ docs fixed 2026-07-09)

| File | Change |
|---|---|
| `backend/docs/grilling-session-mission-restructure.md` | Lines 40-43: winner-takes-all → per-target scoring. Line 126: readiness `WinnerScoreValue` → per-target `ScoreValue` |
| `backend/services/session-operations-service/CONTEXT.md` | Lines 125-127: `TreasureHuntSubstageScore` → `TreasureHuntTargetScore` (per resolved target) |
| `backend/docs/bd_umbral_entity_spec.md` | Lines 78, 358, 598, 896: added `scoreValue` to Target fields, deprecated `winnerScoreValue`, updated constraints |

## What still needs refactor (code)

The **generator-agent** reads canon docs, not the handoff. Since docs now reflect per-target scoring, generated tickets will be correct. However, the **code still has the old model**:

| Location | Current | Needs |
|---|---|---|
| `mission-design-service` — `Substage.WinnerScore` | Substage-level winner score | Remove; per-target `ScoreValue` on `Target` entity |
| `session-operations-service` — `SubstageSnapshot.WinnerScore` | Snapshotted substage-level | Remove; `TargetSnapshot.ScoreValue` |
| `mission-design-service` — `Target` entity | No `ScoreValue` field | Add `ScoreValue` |
| `session-operations-service` — `TargetSnapshot` VO | No `ScoreValue` field | Add `ScoreValue` |
| Migrations in both services | `WinnerScore` column on substage table | Migrate to per-target `scoreValue` column |
| Handlers referencing `WinnerScore` | `AddTarget`, `UpdateTarget`, `CreateSession`, etc. | Point at target-level score |

## What does NOT need change

**No Linear ticket references the old model.** Checked DES-39/40/41/42/51/53/54/85 — none mention `WinnerScoreValue`, winner-takes-all, or "non-winning teams receive zero". Their descriptions describe standard cumulative scoring.

## Recommendation

Before any treasure-hunt ticket (e.g. DES-42 / HU-31) touches this code, either:

1. **Create a refactor ticket** in Linear to migrate `WinnerScore` → per-target `ScoreValue`, or
2. **Do the refactor now** — scoped change across two services (mission-design + session-ops).

If you generate a ticket like DES-42 (HU-31) now, the generator spec will be correct (per-target), but the driver will hit the old `WinnerScore` code during implementation.

## Suggested skills for next session

- `ef-core-postgresql` — Target/TargetSnapshot `ScoreValue` migration
- `cqrs-mediatr-aspnetcore` — vertical slice update (commands, handlers, validators)
- `handoff` — to continue across sessions

# Handoff — difficulty-derived target scoring (2026-07-10)

Date: 2026-07-10 · Branch: `feat/mission-editor-target-score`

## What this session did (in one line)

Inverted target scoring: a treasure-hunt **target's score is no longer authored** — it is
**derived from the owning mission's difficulty** as `50 × difficultyFactor`
(Beginner→1=50, Intermediate→2=100, Advanced→3=150), fully read-only at every layer.

This **supersedes the per-target authored-score model** (DES-86 / ADR-0015) *and* the immediately
prior session's "score is a dropdown of 10…100 step 10" work — see
[`../../frontend/docs/mission-editor-polish-and-target-score-2026-07-10.md`](../../frontend/docs/mission-editor-polish-and-target-score-2026-07-10.md)
for that now-obsolete dropdown pass (kept as history; **do not implement from it**).

## Where the work lives

Bulk is **already committed**: `92a11a0 feat(mission-editor): difficulty-derived target
scoring + authoring UI polish`. Read the diff for specifics rather than re-deriving them here:
`git show 92a11a0`. That commit was authored by an external process mid-session (a hook/agent),
not by a deliberate user "commit now" — see **Git state** below.

### Design (the load-bearing decisions)

- **Derivation lives in the `Mission` aggregate** (it is the only node that knows `Difficulty`).
  `Mission.AddTarget`/`UpdateTarget` dropped their `score` parameter and compute
  `ScoreValue.BaseTargetScore * Difficulty.ScoreFactor`.
- **Lower-level builders keep their `int score` params** — `Substage.AddTarget` and
  `Target.Create` are unchanged, so `SubstageTests`/`TargetTests` were untouched. Only the
  aggregate-level `Mission.AddTarget/UpdateTarget` and the commands lost `score`.
- **`Difficulty.ScoreFactor`** = 1-based index into `AllowedValues` (case-insensitive), so it
  stays correct if tiers are ever reordered/extended.
- **`ScoreValue`**: `MaximumPoints` 100→**150**, added `BaseTargetScore = 50`. The positive +
  multiple-of-10 invariants (and `ScoreValueMustBeMultipleOfTenException`) were **kept** — every
  derived value {50,100,150} satisfies them, so keeping them was lower-churn than deleting the
  exception + its `ErrorCodeContractTests` registration.
- **Re-pricing on difficulty change**: `Mission.UpdateDetails` now calls `RepriceTargets()` →
  `Target.Reprice(score)` for every target, so scores never drift after a difficulty edit. This
  is the riskiest new behavior; it has a dedicated domain test.
- **Frontend**: add/edit target forms lost the score field. Add form shows a read-only derived
  hint (`100 (set by Intermediate difficulty)`); the row shows `Score: 100 (Intermediate)`.
  `difficulty` is threaded `MissionTree → SubstageEditor → AddTargetControl/TargetRow`.
  `AddTargetRequest`/`UpdateTargetRequest` (`frontend/app/lib/definitions.ts`) dropped `score`.

### Load-bearing edge case

`Difficulty.ScoreFactor` **throws `InvalidDifficultyValueException` for a legacy/invalid
difficulty** (e.g. the raw-SQL `"Easy"` in `MissionEndpointsTests`). This is by design — a target
cannot be added/updated on a mission whose difficulty is off-canon. The one legacy-difficulty test
only switches play mode (discards targets), so it never hits the derivation. If a real legacy
`"Easy"` mission ever needs a target, that add will fail until its difficulty is corrected.

## Verification (all green this session)

- Backend: `UnitTests` **338**, `Api.UnitTests` **95**, `IntegrationTests` **55** (Docker was
  available). Run per-project (no `.sln`): `dotnet test tests/UnitTests` etc.
- Frontend: `npx tsc --noEmit` clean (one PRE-EXISTING unrelated `keycloak-tokens.test.ts`
  `NODE_ENV` error, not ours); `npx vitest run` **36/36**.
- **NOT driven in the live app.** `frontend/tests/e2e/mission-hierarchy.spec.ts` was updated
  (score input removed; asserts `Score: 100 (Intermediate)`; the reseed test now exercises the
  `name` field) but Playwright was not run — needs the full stack (Next dev + identity-access
  `:5002` + gateway `:8000` + a minted HS256 session cookie).

## Git state — READ THIS

- Branch silently moved `docs/serialized-order-des87` → **`feat/mission-editor-target-score`**
  and `92a11a0` appeared mid-session (external hook/agent). The commit is cleanly scoped to
  mission-design + frontend; it also carried the prior session's dashboard-polish working tree.
- **Two test files are still UNCOMMITTED** (extra coverage written after `92a11a0`):
  - `tests/UnitTests/Domain/ValueObjects/DifficultyTests.cs` — `ScoreFactor` theory.
  - `tests/UnitTests/Domain/Entities/MissionStructureTests.cs` — derive + re-price tests.
  Decide: `git commit --amend` them into `92a11a0`, or a follow-up commit. (Per repo memory:
  squash the branch to one commit before a PR; no `Co-Authored-By: Claude` trailer;
  `gh pr edit --title/--body` is broken here — use `gh api` REST PATCH.)
- Untracked and **unrelated** to this work (leave alone): `mobile/**`,
  `backend/docs/findings/reflection-layer-guard-review-2026-07-10.md`.

## Next steps

1. Resolve the two uncommitted test files (amend vs follow-up).
2. Drive the flow live: add a target on missions of each difficulty → 50/100/150; edit a
   mission's difficulty and confirm existing targets re-price.
3. Consider whether ADR-0015 (per-target scoring ownership) needs a superseding note, since
   authored per-target scores are gone.

## Suggested skills

- **`/verify`** or **`run`** — drive the add-target + difficulty-change flows in the real app.
- **`/code-review`** (or `code-review ultra`) on `92a11a0` before opening a PR.
- **`aspnet-backend-testing`** — if extending `ScoreValue` / `Difficulty.ScoreFactor` / reprice tests.

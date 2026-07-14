# HU-28 — Operator clue-release picker: seed fix + clue-surface findings — 2026-07-14

**Branch**: `feature/hu-28-operative-clues` · **Base commit**: `b55357b` · **Nothing committed**

## Purpose

The operator dashboard's "Release clue" dropdown was always empty. It was a **seed-data
problem, not a code defect** — the backend behaved correctly throughout. This session fixed
the seed, verified the whole path end-to-end against a live stack, and mapped how clues
actually reach participants. Supersedes the opencode note
`/tmp/opencode/hu-28-operator-dashboard-clue-release-empty.md`, whose root cause was right
but whose recommended fix was wrong.

## The fix (done, verified)

The HU-171 manual seed authored **both** treasure-hunt clues as `VisibleWhenSubstageStarts`.
`LiveSession.ProjectReleasableTargets()` only lists `HiddenUntilOperatorRelease` targets, so
`GET /api/sessions/{id}/clues/releasable` honestly returned `{"targets":[]}`.

Changed clue 2 to `HiddenUntilOperatorRelease` while keeping clue 1 visible, in both copies of
the SQL (the spec and its byte-identical doc mirror) — see `git diff`:

- `frontend/tests/e2e/session-substage-progress-manual-seed.spec.ts`
- `frontend/docs/hu-171-manual-test.md`

**Do not flip both clues to hidden.** `hu-171-manual-test.md` and `hu-28-manual-test.md` both
depend on a visible clue being on the participant board. The mix satisfies both and covers
both policies in one fixture.

Verified live: seed → Start → trivia times out (~30s/question) → substage advances → picker
returns Target 2 → `POST /clues/release` succeeds → participant board projects it.
`LiveSessionClueReleaseTests.ProjectParticipantTeamBoard_PinsAlwaysVisibleCluesAboveReleasedClues`
already pinned this scenario at the domain level.

## How clues actually reach participants (verify before assuming)

Three distinct mechanisms, easy to conflate — this caused a wrong turn in this session:

| Mechanism | Play mode | Source | Operator-releasable? |
|---|---|---|---|
| Target clues (`CollectTargetVisibleClues`) | TreasureHunt only | Per-target `TargetSnapshots` | Yes — this is what the picker lists |
| Substage-initial clues (`CollectSubstageVisibleClues`, #145) | Target-less (trivia) | Substage-scoped `ClueSnapshots` | **No** |
| Operative clues (`AddOperativeClue`) | **Any** — no play-mode gate | Free text authored live | N/A — pushed directly |

Consequences worth internalising:

- **The release dropdown is treasure-hunt-only, permanently.** `ProjectReleasableTargets()`
  returns `[]` before reading any clue when the active substage isn't a TreasureHunt, and it
  projects from `TargetSnapshots` — trivia substages have no targets. No seed data changes this.
- **A trivia substage *can* have clues.** `VisibleWhenSubstageStarts` clues on a trivia substage
  do surface via `CollectSubstageVisibleClues`. (An earlier claim in this session that trivia
  clues "never appear" was wrong and is corrected here.)
- **Operative clues have no play-mode gate.** `AddOperativeClue` checks only: session live,
  non-empty text, ≥1 team. This is HU-28's actual feature and works on trivia today.

### Suspected gap — worth a ticket, unverified

A `HiddenUntilOperatorRelease` clue on a **trivia** substage appears to be permanently dead
data: `CollectSubstageVisibleClues` explicitly won't project it, and the picker can't list it
(TreasureHunt gate), so no UI path can ever release it. Its comment says such clues "stay
withheld until an operator release exists", but release resolves per-target and a trivia clue
has no target. Not reproduced — the seeds don't create this shape. Confirm before filing.

### Confirmed docstring inaccuracy

`ProjectReleasableTargets()`'s comment claims "every listed target is one a subsequent release
would accept". Disproved live: after releasing Target 2 to the only team, the picker **still
lists it**, and re-releasing returns `409 clue-already-released-to-team`. Pre-existing HU-26
behaviour, not caused by this work — the picker is target-scoped while the team is chosen
separately, so listing it is correct for multi-team sessions; the single-team fixture just makes
the dead end reachable. `OperatorClueReleasePanel.tsx` handles the 409 with a readable message.
Cheapest correct action is fixing the comment; per-team filtering would be a behaviour change.

## `seed-all.sh` — new "Pana Exito TH" fixture (convenience, NOT a fix)

Added ~96 lines to `backend/scripts/seed-all.sh` (Part 1 authors the mission, Part 3 creates and
Actives the session). A **treasure-hunt-first** mission — substage 1 *is* the treasure hunt — so
the picker is populated the moment the session goes Active, with no trivia round to sit through.
Clue 1 visible, clue 2 = `pana exito` / `HiddenUntilOperatorRelease`.

This fixes no defect. It exists only to make the dropdown observable instantly. **It is
self-contained and safe to revert** if judged noise.

- Idempotent — verified over 3 consecutive runs (1 mission, 1 clue, 2 targets, no orphans). It
  needs its own delete-by-name because it selects no trivia quiz, so Part 1's catalog wipe
  (which deletes missions *with* a trivia substage) never cascades to it.
- Authored in psql with `ActivationState='Ready'` stamped directly, mirroring the HU-171 seed.
- **Assigned to `seed-all.sh`'s own `operator` user, not Playwright's `op-1`.** A dashboard
  logged in as op-1 will not see it. Log in as `operator` / `operator123`, or reassign. Left as
  `operator` deliberately: op-1 is a Playwright global-setup identity this script doesn't own.
- **Not verified by a full `seed-all.sh` run** — the two new blocks were verified in isolation
  (SQL, idempotency, and the API session flow replicated by hand). `seed-all.sh` is reported to
  restart Postgres and kill the `dotnet watch` processes, so a whole-script run was avoided.
  Running it end-to-end is the outstanding validation.

## Working tree — contains concurrent work that is NOT part of this effort

`git diff` at handoff time spans 10 files. **Only these three are this session's:**

- `backend/scripts/seed-all.sh`
- `frontend/tests/e2e/session-substage-progress-manual-seed.spec.ts`
- `frontend/docs/hu-171-manual-test.md`

The rest arrived from another session mid-conversation and were never reviewed here — four
`session-operations-service` files (an operator-panel released-clue-count feature touching
`LiveSession.cs`, `OperatorSessionPanelDto.cs`, `OperatorSessionPanelDtoFactory.cs`,
`OperatorTeamProgress.cs`) and three `mobile/` files (`operative-clue-surface.tsx`, its test,
`team-board-types.ts`). **A `git commit -a` would sweep them in.** Commit by explicit path.

Because `LiveSession.cs` shifted under us, **cite domain methods by name, not line number** —
earlier line references in this work are already stale.

## Remaining work

1. `frontend/docs/hu-28-manual-test.md:109` says the operative clue appears "alongside the
   seeded target clues" (plural). Now only clue 1 shows until clue 2 is released. Reword.
2. Fix the `ProjectReleasableTargets()` docstring overclaim (above).
3. Commit the three files by explicit path.
4. Run `seed-all.sh` end-to-end to validate the new fixture in a real run.
5. Decide on the trivia-hidden-clue gap — reproduce, then file or dismiss.

## Reproducing the verification

Stack must be up (`backend/` compose; all 10 services). App services have **no healthchecks**, so
`Up` is not readiness — check `GET /api/sessions` returns 401 (not 502) before trusting it.

```bash
# 1. Seed (needs the Published quiz "HU-171 Progreso de substages" from seed-all.sh)
cd frontend && npx playwright test tests/e2e/session-substage-progress-manual-seed.spec.ts

# 2. Start as operator, then WAIT ~60s — two 30s questions must time out before the
#    substage advances. The picker is empty until then, and that is correct.
#    Advance is timer-driven only (AuthoritativeSessionTimerWorker); there is no
#    "all teams answered -> advance" path and no operator advance button.

# 3. GET /api/sessions/{id}/clues/releasable  -> expect exactly Target 2
```

Gotchas that cost time here:

- A live session snapshots its mission **immutably** at creation. Re-running the seed mints a new
  session; an older session keeps the old clue visibility forever. Always use the new code.
- Checking the picker immediately after Start always looks broken. It cannot populate until the
  treasure-hunt substage is active.
- `operator@umbral.local` has **two** identity rows in the dev DB (ids 7 and 34) — "which
  operator am I" is genuinely ambiguous. Pre-existing, unrelated, but it will confuse you.
- Sandboxed Bash can fail with `apply-seccomp ... Permission denied` and still report **exit 0** —
  a backgrounded poll can look like it succeeded while never having run. Check the output file.

## Suggested skills

- **`/code-review`** — before committing, over the three files (`git diff` currently includes
  other sessions' work; scope the review explicitly).
- **`aspnet-backend-testing`** — if the trivia-hidden-clue gap is confirmed and needs a
  regression test in `session-operations-service`.
- **`/verify`** — for the `seed-all.sh` end-to-end run (item 4).

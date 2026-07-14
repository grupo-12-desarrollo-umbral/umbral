# HU-28 trivia clue release — backend implementation handoff — 2026-07-14

## Purpose

Backend Phases 1–4 of `backend/docs/hu28-trivia-clue-release-plan-2026-07-14.md`
are implemented and verified in the working tree. Nothing was committed. This handoff records only
session-specific implementation state; use the plan for the full rationale, decisions, and test plan.

## Implemented

- Domain release subjects are now exactly-one-of:
  - target-backed treasure-hunt clue (`TargetId` set, `ClueId` null), or
  - target-less trivia clue snapshot (`ClueId` set, `TargetId` null).
- Added `ClueReleaseSubject`, `ClueReleaseSubjectInvalidException`, and `ReleasableClue`; removed
  `ReleasableTarget`.
- `LiveSession` now:
  - projects releasable clues for active treasure-hunt and trivia substages;
  - resolves and releases either subject shape to one team or all teams;
  - prevents duplicate releases using the applicable subject key;
  - projects released trivia clues only to entitled teams;
  - pins initial trivia clues above released clues and sorts released clues newest-first;
  - counts initial and released trivia clues as disjoint sets in the operator panel, avoiding the
    documented double-count trap.
- `VisibleClue` and the participant board DTO now carry nullable `ClueSnapshotId` for substage clues.
- Application contracts and the CQRS query slice were renamed from `ReleasableTargets` to
  `ReleasableClues` in the ADR-0011 vertical-slice layout.
- `ReleaseClueCommand` and its validator accept nullable `TargetId`/`ClueId` and require exactly one.
  The domain value object enforces the same invariant if the validation pipeline is bypassed.
- `ClueReleaseFacade` constructs the subject, releases it, persists once, and echoes both nullable
  subject keys in `ReleaseClueResultDto`.
- API controller request/response types and documentation were updated.
- EF configuration now has nullable `target_id`, two filtered unique indexes, and a database XOR
  check constraint.
- Generated migration:
  `backend/services/session-operations-service/src/Infrastructure/Migrations/20260714152710_ReleaseClueByClueId.cs`.

## Backend contract after this implementation

`GET /api/sessions/{liveSessionId}/clues/releasable` returns:

```json
{
  "liveSessionId": "...",
  "activeSubstageId": "...",
  "clues": [
    {
      "targetId": null,
      "clueId": "...",
      "targetName": null,
      "sequenceOrder": 2,
      "clueText": "..."
    }
  ]
}
```

Treasure-hunt entries instead set `targetId` and `targetName`, leaving `clueId` null.

`POST /api/sessions/{liveSessionId}/clues/release` accepts:

```json
{
  "targetId": null,
  "clueId": "...",
  "teamId": "..."
}
```

Exactly one subject id is required. A null `teamId` releases to all teams. Both/neither subject ids
return `400`; duplicate team/subject release returns `409`. The result echoes nullable `targetId`,
nullable `clueId`, and `releasedTeamIds`.

Participant board `visibleClues` now includes nullable `clueSnapshotId` in addition to the existing
nullable target and operative clue ids.

## Verification completed

All commands used the backend Makefile. Because this workspace intermittently races on generated
SourceLink artifacts, successful verification set these environment properties without changing repo
configuration:

```bash
GenerateSourceLinkFile=false EnableSourceControlManagerQueries=false \
  make -C backend build SVC=session-operations-service

GenerateSourceLinkFile=false EnableSourceControlManagerQueries=false \
  make -C backend test SVC=session-operations-service

GenerateSourceLinkFile=false EnableSourceControlManagerQueries=false \
  make -C backend gate SVC=session-operations-service
```

Results:

- structure guard: passed;
- layer guard: passed;
- build: passed for Api and all test projects;
- application tests: 324 passed;
- domain tests: 368 passed;
- integration/API/SignalR tests: 369 passed;
- total: 1,061 passed, 0 failed;
- aggregate coverage: 97.95% line, 93.76% branch;
- coverage gate: green (both thresholds at least 93%);
- scoped `git diff --check`: clean.

Coverage report:
`backend/services/session-operations-service/coverage/gate/index.html`.

## Tests added or expanded

- `ClueReleaseSubject` factories, equality/invariant paths, both/neither/empty rejection.
- Trivia releasable-clue projection and visible-policy exclusion.
- Trivia release to one team and all teams, board isolation, persisted subject keys, events, duplicate
  rejection, and operator-panel double-count regression.
- FluentValidation for both valid subject shapes and every malformed shape.
- Facade construction of both target and trivia subjects.
- Real PostgreSQL persistence round-trip for a clue-keyed release.
- Releasable-clues API trivia response.
- Release API by `clueId`, plus malformed both/neither `400` cases.
- SignalR delivery of a trivia board containing `ClueSnapshotId`.
- Treasure-hunt release/order regression tests remain green.

## Working-tree boundaries

The tree was already dirty before this implementation. In particular, another session owns
uncommitted operator-panel, frontend, mobile, seed, and manual-test changes. The backend work had to
touch `LiveSession.cs` and some operator-panel tests/DTOs around those existing changes; those edits
were preserved and the combined tree passes all backend gates.

Do not discard or wholesale-replace dirty files. Commit by explicit paths only; never use
`git commit -a`. Recheck `git status` immediately before staging because frontend/mobile files
continued changing concurrently during this session.

## Remaining work

Backend Phases 1–4 are complete. Phase 5 and manual fixture work from the plan remain outside this
session:

1. Update the frontend release picker contract from `targets` to `clues` and send whichever subject id
   the selected row carries.
2. Render `Pista {sequenceOrder}` when `targetName` is null.
3. Ensure frontend/mobile team-board types consume nullable `clueSnapshotId` without overwriting the
   concurrent edits already present in those files.
4. Complete/reconcile the hidden trivia clue manual seed and documentation changes described by the
   plan. Relevant frontend seed/docs files are already dirty from another session; inspect their diff
   before editing.
5. Run the appropriate frontend/mobile tests, then review the full cross-workload API contract diff.
6. If asked to commit, invoke the repository conventional-commit skill first and stage only intended
   paths.

## Suggested skills

- `frontend/AGENTS.md` plus `vercel-react-best-practices` for the remaining React/Next.js Phase 5.
- `backend/.agents/skills/aspnet-backend-testing` if backend regression coverage changes.
- `backend/.agents/skills/ef-core-postgresql` if the migration or persistence invariant changes.
- `backend/.agents/skills/cqrs-mediatr-aspnetcore` if the application contract/slice is revised.
- `commit-work` / backend conventional-commits guidance if the next session is asked to stage or commit.

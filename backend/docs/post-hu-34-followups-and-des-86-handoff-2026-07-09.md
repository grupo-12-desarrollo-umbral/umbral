# Handoff — post-HU-34 follow-ups + DES-86 (per-target scoring)

Written: 2026-07-09. Recovered from a lost `/tmp` scratch file and reconciled: 2026-07-10.
Workspace: `/home/samu/Desktop/umbral`
Branch: **`chore/post-hu-34-followups`** (branched from `develop` @ `5cdda8b`)
Plan of record: **`plans/post-hu-34-followups-and-des-86.md`** (untracked; full phase breakdown)

This file is a **historical record**. The original was written mid-flight on 2026-07-09, when a
session was interrupted with four subagents in progress and nothing compiled. Sections 1–7 below are
that document, preserved. **Read the reconciliation first** — most of what §2 and §4 describe as
open has since landed.

---

## Reconciliation — state as of 2026-07-10

| Item from the original | Then (2026-07-09) | Now |
|---|---|---|
| §2a error-code contract tests | Written, never compiled | Committed in `4eabcde` |
| §2a missing golden-map line | Known pending failure | Added (`ErrorCodeContractTests.cs:20`) |
| §2b participant attribution | Agent killed at ~90% | Committed (`4eabcde`, tests in `e978ad3`) |
| §2c ADR-0015 | Untracked | Committed in `4eabcde` |
| §2d DES-86 F1 | **Zero files produced** | Committed in `4eabcde` |
| DES-86 F2 | Not started | Committed in `e978ad3` |
| DES-86 F3 | Not started | Committed in `092e448` — see below |
| §5 `Microsoft.OpenApi` gap in test projects | Unfinished defect | Closed; all five test `.csproj`s carry the 2.7.5 pin |
| §5 info-disclosure (`detail = exception.Message`) | Deliberately unfixed, filed nowhere | Fixed in `34d69f6` |

The rollout ratified in §3 is therefore complete through F2, with F3 sitting in the working tree.

### DES-86 F3 — implemented, not yet committed

Every item of the F3 spec is done: `Substage.WinnerScore` and `SetWinnerScore` are gone, along with
the EF mapping, `MissionDto`, `MissionRuntimePlanDto`, the `MissionsController` hits, and
`MissionActivationPolicy`. `Target.Score` is non-nullable in the domain, the configuration, and the
database. The runtime-plan response no longer carries `WinnerScore`. The migration is
`20260710045617_DropSubstageWinnerScoreMakeTargetScoreRequired`.

Validating this turned up two breaks that greps could not see, both fixed in `092e448`. F3 changed
`AddTargetCommand.Score` from an optional `int? Score = null` to a required positional `int Score`,
and left a call site in `MissionRuntimePlanCommandValidatorTests.cs` unupdated — the branch did not
compile. Behind that compile error hid a second failure: `MissionEndpointsTests.cs` still asserted a
readiness failure containing `"must define a winner score"`, a rule F3 correctly deleted from
`MissionActivationPolicy` (per-target scores are required at target creation, so readiness has
nothing left to check).

The F3 exit criterion as originally written — `grep -rn WinnerScore --include='*.cs' backend`
returns zero — reports 29 hits, but none are violations. Twenty-six are in historical EF migration
and `.Designer.cs` snapshot files, which necessarily still name the column they created or dropped.
The other three are in session-operations'
`tests/IntegrationTests/Integrations/MissionDesign/MissionRuntimeSourceIntegrationTests.cs`, which
deliberately feeds `WinnerScore` into a mission-design payload to assert the consumer tolerates a
field it no longer needs. The criterion needs `--exclude-dir=Migrations` to mean what it was meant
to mean.

The migration's `Up()` re-backfills any `NULL` target `Score` from the substage `WinnerScore`, then
runs `SET NOT NULL` with **no `defaultValue`**, so a target belonging to a substage that never had a
`WinnerScore` makes the migration **fail loudly** rather than get a fabricated score. That is
intentional: fail-fast over silent mutation. The shared `PostgreSqlFixture` only ever migrates a
fresh database, so it never reaches either branch; `PerTargetScoreMigrationTests` covers all three
paths (backfill, preserve-existing, fail-loudly) against seeded rows on its own container.

The historical exit criterion was met under the gate in effect at the time. Both
services must be rerun against the current ADR-0005 branch-coverage threshold.

One loose end remains. The comment at `MissionRuntimeSourceIntegrationTests.cs:36` says `WinnerScore`
"is still emitted by mission-design during the F1/F2" — stale, since F3 is the commit that stops
emitting it. The test still earns its keep (it now proves the consumer ignores a legacy field); the
comment should say so.

### Related docs

`backend/docs/target-score-handoff.md` (2026-07-09, committed in `2370a93`) predates the F1/F2/F3
plan. Its "What still needs refactor (code)" table is now fully done. Supersede or delete it rather
than trusting it.

---

# Original handoff, 2026-07-09 (preserved)

> ⚠️ **Nothing below the first section has been compiled or tested.** Four subagents ran; two
> completed, two were killed mid-edit. The working tree contains unverified edits. **Build before
> you trust anything.**

## 1. Done and committed

**`d8aec99` — `chore(backend): pin Microsoft.OpenApi off CVE-2026-49451 and restart app containers`**

- `Microsoft.OpenApi` pinned to `2.7.5` via `CentralPackageTransitivePinningEnabled` in all four
  services' `src/Directory.Packages.props`. **Verified:** `Microsoft.OpenApi/2.7.5` resolves in
  all three Api projects; `dotnet list package --vulnerable --include-transitive` reports clean;
  NU1903 no longer fails the build.
- `restart: unless-stopped` on the four app services in `backend/docker-compose.yml`.
  `docker compose config` validates.

**Also done:** `backend-mission-design-service-1` was Exited(255) and is now `Up` (plain
`docker start`; no code was involved — see §5).

## 2. Uncommitted, INCOMPLETE, and UNVERIFIED — the actual work queue

### 2a. Error-code contract — agent COMPLETED, never compiled

Three new files, no production code touched:
- `backend/services/{session-operations,mission-design,identity-access}-service/tests/.../Domain/Exceptions/ErrorCodeContractTests.cs`

Each reflects over the Domain assembly for concrete `DomainException` subclasses, reads `ErrorCode`
via `RuntimeHelpers.GetUninitializedObject`, and asserts the map against a hand-pinned golden
dictionary (57 / 46 / 18 entries) plus a no-duplicate-codes assertion.

**Status:** `identity-access` — 2 tests **PASS** (verified by me).
`mission-design` and `session-operations` — **NOT RUN** (other agents were editing those services).

**⚠️ Known failure waiting for you:** the session-operations golden map does **not** contain
`AnswerSubmitterIsNotSessionParticipantException` (added by §2b). The test will fail until you add
one line to `ExpectedErrorCodes`:
```csharp
["AnswerSubmitterIsNotSessionParticipantException"] = "answer-submitter-is-not-session-participant",
```
There is a comment in the dict saying exactly this. It is intended, not a bug.

### 2b. Participant attribution — agent KILLED at ~90%, never compiled

Production code looks complete and correct:
- **NEW** `src/Domain/Exceptions/AnswerSubmitterIsNotSessionParticipantException.cs`
  (`ErrorCategory.Forbidden`, derived code `answer-submitter-is-not-session-participant`)
- `SubmitTriviaAnswerCommandHandler.cs` — `ResolveParticipantId` now returns non-nullable `Guid`
  and **throws** instead of silently returning `null`.
- `LiveSession.cs` (`RegisterTriviaAnswer`, `AcceptTriviaAnswer`) and `TriviaAnswerSubmission.cs`
  (ctor + `Accept`) — `Guid? submittedByParticipantId` → `Guid`.
- Base `EvidenceSubmission.SubmittedByParticipantId` deliberately **left `Guid?`**, DB column
  deliberately **left nullable**, **no migration** — the invariant is trivia-path-only. Correct; keep it.

Tests touched (7 files): `SubmitTriviaAnswerCommandHandlerTests`, `TriviaAnswerValidationChainTests`,
`LiveSessionTestFactory`, `SubmitTriviaAnswerEndpointTests`, `EvidenceSubmissionTests`,
`LiveSessionTests`, `TriviaAnswerSubmissionTests`.

**The agent's last words before it was killed:**
> "Now point the two happy-path endpoint tests at the admitted participant's identity."

So **`tests/IntegrationTests/Api/SubmitTriviaAnswerEndpointTests.cs` is likely unfinished** — its two
happy-path tests probably still authenticate as an identity that is not an admitted participant, and
will now get a 403 instead of a 200. **That is where to start.**

### 2c. ADR-0015 — agent COMPLETED

`backend/docs/adr/0015-per-target-scoring-ownership.md` (untracked). Records the two ratified
decisions (see §3). Number `0015` confirmed free. Read it; do not re-derive.

### 2d. DES-86 F1 — agent KILLED, produced **ZERO** files

No `mission-design-service/src` changes exist. `git status` on that service shows only the
untracked `ErrorCodeContractTests.cs` from §2a. **F1 has not been started.** Re-run it from the
plan (`plans/post-hu-34-followups-and-des-86.md`, "WAVE 2 → F1").

## 3. Decisions ratified this session (do not re-litigate)

The user chose both, explicitly, from the options in `workflow_refactor.md:364-377`:

1. **Ownership:** MissionDesign *authors* the points (`Target` carries its own `ScoreValue`, 1-100)
   → SessionOperations *relays* them in `TargetResolved` → ScoringMonitoring *accumulates* them into
   `ScoreEntry`. No mutable total outside `ScoreEntry`.
2. **Rollout:** **expand → migrate → contract**, three commits, *not* one atomic two-service change.
   - F1 mission-design: add `Target.ScoreValue`, emit it **additively**, keep `WinnerScore` in the payload.
   - F2 session-operations: read the per-target score, drop `WinnerScore` from `SubstageSnapshot`.
   - F3 mission-design: delete `WinnerScore` entirely.

This is possible because the cross-service seam is an **HTTP call with hand-duplicated DTOs**
(producer `MissionsController.cs:565`, consumer `MissionRuntimeSource.cs:89`), **not** a shared
contracts assembly. `workflow_refactor.md` claims the two services must land together; that is wrong.

**Ordering constraint:** the error-code contract (§2a) **must** land before F2/F3, because F2 deletes
`TriviaSubstageSnapshotCannotDeclareWinnerScoreException` and re-homes
`TreasureHuntSubstageSnapshotWinnerScoreRequiredException` — and today an exception's *class name*
**is** its public wire error code.

### F3 — Contract (mission-design-service), as specified

- Drop `Substage.WinnerScore` (`Substage.cs:42`, `SetWinnerScore` `:86`), `MissionConfiguration.cs:112-115`,
  `MissionDto`, `MissionRuntimePlanDto`, the 8 hits in `MissionsController.cs`, and
  `MissionActivationPolicy.cs`.
- EF migration dropping the column. Make `Target.Score` non-nullable in the same migration.
- Remove `WinnerScore` from the `runtime-plan` response.
- **Exit:** `grep -rn WinnerScore --include='*.cs' backend` returns zero. `gate-all` green in both services.

`DES-42` (HU-31, QR `Target` resolution) unblocks the moment F3 merges.

## 4. Immediate next actions, in order

1. **Add the missing golden-map line** (§2a) or session-operations' `ErrorCodeContractTests` fails.
2. **Finish `SubmitTriviaAnswerEndpointTests.cs`** (§2b) — the two happy-path tests need an admitted
   participant's identity.
3. **Build + test session-operations and mission-design.** Nothing in §2a/§2b has ever been compiled.
   `make -C backend test SVC=session-operations-service` (note: `SVC` defaults to
   `identity-access-service`; `make gate-all` for the ADR-0005 branch-coverage gate).
4. **Close the `Microsoft.OpenApi` gap in test projects** — see §5, this is a real, unfinished defect.
5. Commit §2a + §2b + §2c as separate commits.
6. **Then** start DES-86 F1 from scratch.

## 5. Findings and gotchas discovered this session

- **`develop` was six commits stale locally.** PR #134 (per-target scoring canon) was merged on
  GitHub but never pulled, so `workflow_refactor.md` locally contained **zero** mentions of DES-86.
  Fast-forwarded to `5cdda8b` before branching. **Always `git fetch` before trusting local canon.**

- **The `Microsoft.OpenApi` pin does NOT reach test projects — UNFINISHED.**
  Test `.csproj`s live outside `src/`, so `src/Directory.Packages.props` never governs them. Building
  `identity-access-service` tests emits `MSB3277` and states plainly that
  *"Microsoft.OpenApi 2.0.0 was chosen because it was primary"*. Production Api output is correct
  (2.7.5); the vulnerable 2.0.0 assembly still lands in test bins.
  Five test projects reference `Api.csproj` and need an explicit
  `<PackageReference Include="Microsoft.OpenApi" Version="2.7.5" />` (they use inline versions, no CPM):
  - `identity-access-service/tests/UnitTests/Application.UnitTests.csproj`
  - `identity-access-service/tests/IntegrationTests/Infrastructure.IntegrationTests.csproj`
  - `mission-design-service/tests/IntegrationTests/Infrastructure.IntegrationTests.csproj`
  - `mission-design-service/tests/Api.UnitTests/Api.UnitTests.csproj`
  - `session-operations-service/tests/IntegrationTests/Infrastructure.IntegrationTests.csproj`

- **Three of the five HANDOFF.md follow-up claims were factually wrong.** Corrected, with evidence,
  in `plans/post-hu-34-followups-and-des-86.md` §"Verification summary". Summary:
  - `submitted_by_participant_id` was **already populated** on the happy path. The real defect was a
    *silent null fallback*, which is what §2b fixes.
  - `detail` does **not** leak an exception type name (PR #123 fixed that). The type name leaks
    kebab-cased into the RFC 7807 **`type`** field via `DomainException.ErrorCode`. Different field,
    different fix — that is what §2a addresses.
  - `mission-design-service` Exited(255) was **not a code defect**: `dotnet watch` exited, and no
    compose service declared a restart policy, so it stayed dead while the stack was restarted.

- **`Substage.WinnerScore` blast radius is 90 occurrences / 43 files**, not the 94/42 the docs claim.
  `ScoreValue` **already exists** as a mission-design value object and is already used by
  `Substage.WinnerScore`; `Target` simply has no score field. **No new value object is needed.**
  `TargetResolved` does not exist anywhere yet. `AnswerRegisteredEvent` is the precedent to mirror.

- **Information-disclosure follow-up, filed nowhere.** `detail = exception.Message`, and ~25 domain
  exceptions interpolate GUIDs/ints into their message (`TeamNotFoundException(teamId)`,
  `ParticipantAlreadyConnectedException(participantId)`, `IdentityProviderRoleSyncException(externalIdentityId)`,
  …). Full enumerated list is in this session's error-code agent output; re-derive with a grep over
  `src/Domain/Exceptions/` in each service. Not fixed, deliberately.

### Environment gotchas (cost real time this session)

- **The Bash sandbox is broken on this machine**: `apply-seccomp: write /proc/self/setgroups`. Every
  `grep`/`find`/`ls`/`docker`/`dotnet` call needs `dangerouslyDisableSandbox: true`.
- **Subagents therefore cannot run Bash at all.** They may not self-authorize the bypass — the safety
  classifier denies it, and two agents died on this in the first wave. One later reported
  *"Grep/Glob/Bash are all unavailable in my session"* and stalled without producing output.
  **Consequence: give subagents edit-only work (Read/Edit/Write/Grep/Glob) and run all builds, tests,
  and `dotnet ef migrations add` from the main session.** Or get the user to fix the sandbox
  (`/sandbox`) before delegating.
- The Bash tool runs **zsh**: quote globs (`--include='*.cs'`), and unquoted variables do not word-split.
- **Never `git add .`** — stage explicit paths. `HANDOFF.md` is gitignored; scratch files are not.

## 6. Repo conventions that bit / will bite

- Services live at `backend/services/<name>/`, **not** `backend/<name>/`.
- Test project filenames do not match their folders: `tests/UnitTests/Application.UnitTests.csproj`,
  `tests/Api.UnitTests/Api.UnitTests.csproj`. `dotnet test tests/UnitTests/UnitTests.csproj` fails.
- Commit messages in this repo carry **no** `Co-Authored-By` / `Claude-Session` trailers. Use `Ref: DES-NN`.
- ADRs: two colliding trees (`backend/adr/` 0001-0005 and `backend/docs/adr/` 0001-0014, unrelated
  topics). `backend/AGENTS.md:12-21` mandates **cite by path**, never by bare number, in that range.
  New ADRs go in `backend/docs/adr/`. Unification was assessed and **deliberately deprioritized**
  (see the plan doc, Track C) — nothing is broken, it is hygiene.
- `mobile/plans/post-hu-34a-mobile-trivia-breakdown.md` still has the `34a` filename with a
  `# Post-HU-34` title. Two live referrers to rewrite; `docs/tree-snapshot-2026-07-08.md:535` is a
  dated snapshot and must be **left alone**. Also deprioritized.

## 7. Suggested skills

- **`aspnet-backend-testing`** — for §4 steps 1-3: the golden-file error-code tests and the unfinished
  endpoint tests, keeping the ADR-0005 branch-coverage gate green.
- **`cqrs-mediatr-aspnetcore`** — to finish §2b; the attribution guard sits in a command handler and a
  Chain-of-Responsibility validator (canonical home per ADR-0012:
  `<Area>/Common/<Pipeline>/Validators/`).
- **`ef-core-postgresql`** — DES-86 F1 needs a nullable `Target.Score` column and an
  `dotnet ef migrations add` run from the main session (subagents cannot run it).
- **`grill-with-docs`** — before touching DES-86 F2/F3; ADR-0010 is still *in revisión* and
  `backend/docs/adr/0015-*` is brand new.
- **`generator-agent` / `driver-agent`** (`backend/.agents/`) — the standard per-ticket loop for
  DES-86 F1/F2/F3. Each phase is single-service, so the loop's one-service assumption now holds.

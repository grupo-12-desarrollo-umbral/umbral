# Follow-up plan — post-HU-34 loose ends + DES-86

Created: 2026-07-09. Source: `HANDOFF.md` (gitignored) items 1–5 + `backend/docs/workflow_refactor.md` row 11b.
Every claim below was re-verified against code on `develop` at `2370a93`. **Three of the five
HANDOFF items were stated wrong**; the corrections are recorded in each track and they change the
ordering.

## Verification summary — read before planning anything off the HANDOFF

| # | HANDOFF claim | Verdict | What is actually true |
|---|---|---|---|
| 1 | `submitted_by_participant_id` persists as NULL | **Wrong as stated** | It is resolved and persisted on the happy path (`SubmitTriviaAnswerCommandHandler.cs:78-88`). The real defect is that resolution *silently degrades to NULL* — there is no invariant. |
| 2 | `detail` leaks an internal exception type name | **Wrong field** | `detail` carries the curated `exception.Message`. The type name leaks — kebab-cased — into the RFC 7807 **`type`** field via `DomainException.ErrorCode => DeriveErrorCode(GetType().Name)`. Same in all three services. |
| 3 | `mission-design-service` is Exited(255) | **Not a code defect** | The container built, listened on `:8080`, hot-reloaded, and exited with no error. No restart policy exists anywhere in compose. Restarted clean; it is `Up` now. |
| 4 | Two colliding ADR trees | **True**, and already documented | `backend/AGENTS.md:12-21` names the collision and mandates cite-by-path. `0001`–`0005` collide on unrelated topics. |
| 5 | Mobile plan filename drift | **True** | Filename `post-hu-34a-…`, H1 `# Post-HU-34 …`. Three referrers; only two should be rewritten. |
| 6 | DES-86: `WinnerScore` 94× / 42 files | **Close; actual 90× / 43 files** | Cross-service seam is an **HTTP** call with hand-duplicated DTOs, not a shared contract project. |

---

## Dependency graph

```
WAVE 1  (five tracks, fully parallel — no shared files)
  A  mobile plan rename            docs only
  B  local-dev hardening           compose + csproj
  C  ADR tree unification          docs + one script comment
  D  participant attribution       session-operations only
  E  error-code contract           all three services  ◄── enables F
  G  ScoreValue ownership ADR      decision only       ◄── gates F

WAVE 2  (serial within itself; needs E + G, and C for the ADR number)
  F  DES-86 per-target scoring     expand → migrate → contract

WAVE 3
  DES-42 (HU-31) unblocked
```

**The one non-obvious edge: E must precede F.** F deletes
`TriviaSubstageSnapshotCannotDeclareWinnerScoreException` and re-homes
`TreasureHuntSubstageSnapshotWinnerScoreRequiredException`. Today an exception's *class name* is its
public wire error code, so F would silently change the API contract. E severs that coupling first,
making F's renames safe.

---

## WAVE 1 — five parallel tracks

### Track A — mobile plan filename (docs, ~15 min)

The file was realigned in content by `2370a93` but never renamed.

- `git mv mobile/plans/post-hu-34a-mobile-trivia-breakdown.md mobile/plans/post-hu-34-mobile-trivia-breakdown.md`
- Rewrite the two **live** referrers:
  - `plans/trivia-build-order-and-priority.md:5` — a real relative markdown link; both display text and target.
  - `plans/trivia-7-requirements-coverage.md:4` — inline code path.
- **Leave** `docs/tree-snapshot-2026-07-08.md:535` alone. It is a dated point-in-time tree snapshot;
  the file *was* named `34a` on that date. Editing it falsifies the record. This matches the
  HANDOFF "historical records are deliberately not rewritten" rule.
- No script/config references the path (checked `*.json/js/ts/sh/yml/yaml/toml`).

**Exit:** `grep -rn post-hu-34a` returns exactly one hit, in the tree snapshot.

### Track B — local-dev hardening (infra, ~30 min)

Root cause of the Exited(255): `dotnet watch` terminated and **no compose service declares a restart
policy**. Only `postgres` has a healthcheck. The stack was restarted 2h ago; mission-design was not,
because nothing restarts it.

- Add `restart: unless-stopped` to the four app services in `backend/docker-compose.yml`.
- Add a healthcheck to each app service (`/health` or a `curl -f localhost:8080`), so
  `depends_on: condition: service_healthy` becomes meaningful beyond postgres.
- **Separately filed finding:** every build logs
  `NU1903: Package 'Microsoft.OpenApi' 2.0.0 has a known high severity vulnerability
  (GHSA-v5pm-xwqc-g5wc)` and MSBuild reports `[Failure]` on it. It is a **transitive** dependency —
  no `.csproj` references it directly. Pin it explicitly (or via `Directory.Packages.props`) to a
  patched version. This is a real security item that no ticket covers.

**Exit:** `docker compose down && up -d` leaves all four services `Up (healthy)`; build log is free of NU1903.

### Track C — ADR tree unification (docs, ~1–2 h)

Two sequences, `backend/adr/` (5 files) and `backend/docs/adr/` (14 files), colliding on `0001`–`0005`
with **completely unrelated topics**:

| # | `backend/adr/` | `backend/docs/adr/` | Same topic? |
|---|---|---|---|
| 0001 | team-reference-data-in-identity | gateway-central-jwt-validation | no |
| 0002 | trivia-question-score-and-timer-ranges | websocket-token-extraction-at-gateway | no |
| 0003 | archive-time-enforcement-for-quizzes | api-gateway-keycloak-design-summary | no |
| 0004 | exception-to-problemdetails-mapping | required-domain-patterns | no |
| 0005 | substage-advancement-pointer | coverlet-msbuild-for-aggregate-coverage | no |

The damage is live: a bare `ADR-0005` appears **132×** and a bare `ADR-0004` **61×** across the repo,
each ambiguous between two documents. `HANDOFF.md` itself cites both meanings of `ADR-0005` in one
file. `backend/AGENTS.md:12-21` has already conceded the collision and mandates *cite by path*, with
"a new ADR under `backend/adr/` starts at `0014`, so the collision stops growing" — that caps the
bleeding but never heals it.

**`backend/docs/adr/` is canonical by evidence:** 14 files vs 5, cited by `backend/README.md`,
`backend/AGENTS.md`, `structure.md`, `required_patterns_matrix.md`, `ddd_solution_model.md`,
`bd_umbral_entity_spec.md`, and — the only machine dependency — `backend/scripts/layer-guard.sh:4`.

Migrate `backend/adr/0001…0005` → `backend/docs/adr/0015…0019`, preserving order. Then rewrite
inbound references. Roughly 12 markdown files cite `backend/adr/` paths (`hu33a/33b` contexts,
`prompt_example_feature_hu33a/33b`, `hu34-context.md`, `hu16-context.md`, `workflow_refactor.md:191`,
`ab-ticket-merge-findings-handoff`, `backend/plans/exception-mapper-alignment.md:157`).

> **Judgment call to make explicitly:** several of those are *historical* generator artifacts
> (`prompt_example_feature_hu33a.md`, `hu33a-brief.md`). Under the HANDOFF's own rule these are
> records, not canon. **Recommendation: rewrite the paths anyway** — unlike a retired HU *name*, a
> file *path* that no longer resolves is a broken pointer, not a historical fact. Leave the prose,
> fix the link. Decide this before starting; it is the whole difference between a 4-file and a
> 12-file diff.

Finish by deleting the `## ADR citations` workaround section from `backend/AGENTS.md` and replacing
it with a one-line "all ADRs live in `backend/docs/adr/`, numbered without gaps."

**Exit:** `backend/adr/` no longer exists; every `ADR-00NN` resolves to exactly one file; `layer-guard.sh` still passes.

### Track D — participant attribution (session-operations, ~2 h)

**Corrected premise.** `SubmitTriviaAnswerCommandHandler.ResolveParticipantId` (`:78-88`) already
resolves the participant from `session.Participants` by `ExternalIdentityId`, the repository does
`.Include(session => session.Participants)` (`LiveSessionRepository.cs:23`), and the value is
persisted through `EvidenceSubmission`'s ctor to column `submitted_by_participant_id`
(`LiveSessionConfiguration.cs:582`). An integration test round-trips a non-null value.

The genuine defect is the **silent fallback**:

```csharp
if (!Guid.TryParse(_currentUser.Id, out var externalIdentityId))
    return null;                                   // unparseable / absent claim → NULL, no error
return session.Participants
    .SingleOrDefault(p => p.ExternalIdentityId == externalIdentityId)?
    .SessionParticipantId;                         // authenticated non-participant → NULL, no error
```

`RuntimeParticipationLink` — the first link in the chain — validates by `(liveSessionId, teamId, token)`,
**not by caller identity**. So an authenticated `Participant`-role caller holding a valid team token
who is not in `session.Participants` writes an accepted, unattributable answer.

- Decide the invariant: *an accepted trivia answer is always attributable to a session participant.*
- Add a chain link (or extend `RuntimeParticipationLink`) that resolves the participant and **throws**
  a classified `DomainException` when it cannot. Canonical home per ADR-0012:
  `Sessions/Common/TriviaAnswerValidation/Validators/`.
- Handler then takes a non-nullable `Guid`.
- **Keep the column nullable.** `SubmittedByParticipantId` lives on the *base* `EvidenceSubmission`,
  shared with future non-trivia evidence forms. The invariant belongs in the trivia path, not the schema.
  No migration needed.
- Update `TriviaAnswerSubmissionTests.cs:59` (the explicit-null case) to assert the new rejection.

**Exit:** no code path persists a NULL `submitted_by_participant_id` for a trivia answer; ADR-0005 coverage gate green.

### Track E — error-code contract (all three services, ~3 h) — *enables Track F*

**Corrected premise.** `detail` does **not** carry a type name. Per
`ProblemDetailsExceptionHandler.cs:31-35`, the classified arm sets `Detail = exception.Message`
(curated domain prose) and the unclassified arm hard-codes `"An unexpected error occurred."` — PR #123
already closed the message-echo hole. `DomainExceptionHubFilter` likewise emits curated codes.

The actual leak is one level over, in the `type` field:

```csharp
// DomainException.cs:31 — identical in all three services
public virtual string ErrorCode => DeriveErrorCode(GetType().Name);   // TeamNotFoundException → "team-not-found"
```

Two distinct problems, one root:
1. **The public API error code is the internal class name.** Renaming a domain exception is a silent
   breaking change to every client.
2. `detail = exception.Message` — domain messages interpolate identifiers (e.g. `TeamNotFoundException(teamId)`).
   Lower severity than a type name, but it is the remaining information-disclosure surface. Audit it.

Work:
- Make `ErrorCode` an **explicit** `abstract`/required member on `DomainException`, or keep the
  derivation but add a test that pins every concrete exception's current code as a golden file. The
  golden-file route is cheaper and catches exactly the class of regression F would otherwise cause.
- Do this in all three services (`session-operations`, `mission-design`, `identity-access`); the
  handlers are byte-identical apart from two `ServiceUnavailable` arms in identity-access.
- ~11 handler/hub assertions and ~11 curated-detail integration assertions exist already; extend, don't rewrite.

**Exit:** every `DomainException` subclass has a code pinned by test, independent of its class name.

### Track G — `ScoreValue` ownership ADR (decision, ~1 h) — *gates Track F*

`workflow_refactor.md:364-377` says two contract decisions must be settled *inside* DES-86 and that
"nobody has ratified" the proposed reading. Ratify it before writing code, not during.

1. **`TargetResolved` carries the resolved target's `ScoreValue`.** Precedent already in the tree:
   `AnswerRegisteredEvent` (`session-operations-service/src/Domain/Events/AnswerRegisteredEvent.cs:6`)
   carries `IsCorrect` + `ScoreValue` for exactly this reason.
2. **Ownership:** `MissionDesign` authors the points → `SessionOperations` relays them in the event →
   `ScoringMonitoring` accumulates them in the ledger. No mutable total outside `ScoreEntry`.
   This must be reconciled with `DES-85`, which declares `ScoreValue` a value object of
   `ScoringMonitoring` and puts score computation in session-operations out of scope.

Write it as `backend/docs/adr/0020-per-target-scoring-ownership.md` (number depends on Track C).
Use the `grill-with-docs` skill — ADR-0010 is still *in revisión* and this touches it.

**Exit:** ADR merged and `Accepted`. If decision 2 is rejected, DES-86's acceptance criteria change — which is precisely why this gates F.

---

## WAVE 2 — Track F: DES-86 per-target scoring

**Blast radius (verified):** 90 occurrences across 43 `.cs` files — mission-design 28 hits/14 files
(+26 hits in 9 test files), session-operations 16 hits/13 files (+7 hits in 2 test files).

**The good news, and it reshapes the ticket.** `workflow_refactor.md:378-381` warns "two services,
one ticket… the migration must land in both or the runtime snapshot projection breaks," and asks you
to decide the worktree/PR shape. You don't have to. The seam is an **HTTP call with hand-duplicated
DTOs on both sides** — producer `MissionsController.cs:565`, consumer
`MissionRuntimeSource.cs:89` (`GET /api/missions/{missionId}/runtime-plan`), no shared contracts
assembly. That admits a standard **expand → migrate → contract** rollout: three sequential
single-service PRs, no lockstep deploy, each independently revertible.

Also: `ScoreValue` **already exists** as a value object in mission-design
(`src/Domain/ValueObjects/ScoreValue.cs`, `Create(int points)` enforcing 1–100) and `Substage.WinnerScore`
already uses it. `Target` (`src/Domain/Entities/Target.cs:12`) has no score field. No new value object
is needed — the VO just moves onto `Target`.

### F1 — Expand (mission-design-service)
- Add `ScoreValue Score` to `Target`; thread through `AddTargetCommand`/`UpdateTargetCommand`
  (+ handlers, + validators), `MissionDtoMapper`, `MissionRuntimePlanDto`.
- EF migration: add the `Score` column to targets, **nullable**, backfilled from the parent substage's
  `WinnerScore`.
- Emit `Score` **additively** in the `runtime-plan` response. **Keep `WinnerScore` in the payload.**
- **Exit:** session-operations, unchanged, still deserializes and passes its whole suite. Deployable alone.

### F2 — Migrate (session-operations-service)
- `MissionRuntimeSource.cs:89` consumer DTO reads the new per-target `Score`.
- `SubstageSnapshot` (`src/Domain/ValueObjects/SubstageSnapshot.cs`): drop `WinnerScore` (field `:46`,
  assignment `:35`, and `GetEqualityComponents()` `:64`).
  > ⚠️ Removing it from `GetEqualityComponents()` **changes value-object equality.** Expect
  > snapshot-comparison fallout across `SubstageSnapshotTests.cs` (4 hits) and
  > `MissionRuntimeSourceIntegrationTests.cs` (3 hits).
- Delete `TriviaSubstageSnapshotCannotDeclareWinnerScoreException` — its only production throw
  (`SubstageSnapshot.cs:28`) guards a field that no longer exists.
- Re-home `TreasureHuntSubstageSnapshotWinnerScoreRequiredException` **from the substage to the target**.
  It throws once, at `MissionRuntimeSnapshot.cs:132` inside `EnsureSubstageInvariants`. The contiguous
  guard at `:135` already walks `targetSnapshots` filtered by `SubstageSnapshotId` — fold the per-target
  score check in there. Do **not** invert it.
  *(This is where Track E pays for itself: both exception changes move a public error code.)*
- Define `TargetResolved` carrying the target's `ScoreValue`, per Track G. It does not exist yet, so it
  can be defined correctly from the start. Mirror `AnswerRegisteredEvent` +
  `AnswerRegisteredIntegrationEvent` + the RabbitMQ publisher bridge.
- **Exit:** session-operations reads only per-target scores. Ignores `WinnerScore` if present. Deployable alone.

### F3 — Contract (mission-design-service)
- Drop `Substage.WinnerScore` (`Substage.cs:42`, `SetWinnerScore` `:86`), `MissionConfiguration.cs:112-115`,
  `MissionDto`, `MissionRuntimePlanDto`, the 8 hits in `MissionsController.cs`, and
  `MissionActivationPolicy.cs`.
- EF migration dropping the column. Make `Target.Score` non-nullable in the same migration.
- Remove `WinnerScore` from the `runtime-plan` response.
- **Exit:** `grep -rn WinnerScore --include='*.cs' backend` returns zero. `gate-all` green in both services.

> Run each of F1/F2/F3 through the standard per-ticket driver loop (X.1→X.4). The
> "one service per worktree" assumption in `workflow_refactor.md` holds for each individually —
> that assumption was only violated by treating DES-86 as one atomic change.

---

## WAVE 3

`DES-42` (HU-31, QR `Target` resolution) unblocks the moment F3 merges. `workflow_refactor.md` row 12
already records the ordering; nothing further to decide.

---

## Suggested tracks → agents

- A, C — plain edits, no agent needed.
- B — no agent; verify with `docker compose up -d`.
- D — `cqrs-mediatr-aspnetcore` + `aspnet-backend-testing`.
- E — `aspnet-backend-testing` (golden-file codes).
- G — `grill-with-docs` (ADR-0010 is *in revisión*).
- F1/F2/F3 — the standard `generator-agent` → `driver-agent` loop, once per sub-phase.

## Tickets to file

None of these exist in Linear today:

1. Participant-attribution invariant (Track D) — session-operations.
2. Error-code/class-name decoupling (Track E) — cross-service.
3. `Microsoft.OpenApi` 2.0.0 known-vulnerability pin (Track B) — cross-service, **security**.
4. Compose restart policy + app healthchecks (Track B) — infra.
5. ADR tree unification (Track C) — docs.

Tracks A and G are small enough to ride along with the work they serve.

# Contract Divergences: Ranking, Seeds, and Database

Date: 2026-07-15

Scope checked:

- Mobile ranking DTOs in `mobile/src/lib/realtime/ranking-types.ts`
- Backend ranking DTO/API/SignalR payloads in `scoring-monitoring-service`
- EF configuration and migrations for ranking and score entries
- Live `scoring_monitoring` database schema and `__EFMigrationsHistory`
- Manual seed SQL in:
  - `frontend/tests/e2e/hu-25b-ranking-manual-seed.spec.ts`
  - `fix-issue-153/frontend/tests/e2e/session-target-map-manual-seed.spec.ts`
  - `frontend/tests/e2e/session-clue-release-manual-seed.spec.ts`

## Findings That Need Fixes

### 1. Missing migration file for applied scoring-monitoring migration — RESOLVED (no fix needed)

The live `scoring_monitoring.__EFMigrationsHistory` table contains:

- `20260712220712_InitScoringMonitoring`
- `20260714153000_AddScoringLedgerAndRanking`
- `20260715032633_AddTeamDisplayNameToRankingRow`

The current service migration folder only contains:

- `20260714153000_AddScoringLedgerAndRanking`
- `20260715032633_AddTeamDisplayNameToRankingRow`

Investigation (2026-07-15): `20260712220712_InitScoringMonitoring` was an **empty
placeholder migration** (no-op `Up()`/`Down()`, created zero schema) belonging to an
orphaned default `ApplicationDbContext`. It was intentionally deleted in commit `5dd957f`
("clean up dead scaffold and migrations") along with that context, and the active
migrations were moved to the real `ScoringMonitoringDbContext`.

Impact: none for schema parity. A fresh database built from the current repository
reproduces the identical schema to live. The only residue is one harmless row in live's
shared `__EFMigrationsHistory` pointing at a no-op migration.

Resolution: no code change. The doc's original suggestion to restore the migration file
would be wrong (it would reintroduce removed scaffold). Optionally, the stray history row
can be pruned in the live DB with
`DELETE FROM "scoring_monitoring"."__EFMigrationsHistory" WHERE "MigrationId"='20260712220712_InitScoringMonitoring';`,
but this is cosmetic and not required.

### 2. Manual seed files duplicate admin users — RESOLVED (fixed)

The manual seed files insert `admin-1@umbral.local` after deleting by `ExternalIdentityId`:

```sql
DELETE FROM users WHERE "ExternalIdentityId"='...';
INSERT INTO users ("ExternalIdentityId","DisplayName","Email","Role","IsActive","Created","LastModified")
VALUES (..., 'admin-1@umbral.local', 'Administrator', true, NOW(), NOW());
```

The live `identity_access.users` table has a unique index on `ExternalIdentityId`, but not on `Email`.

Observed live data:

- `admin-1@umbral.local`: 32 active rows
- `op-1@umbral.local`: 1 active row

Impact: repeated seed runs accumulate duplicate active admin rows. Any lookup by email can become ambiguous or return stale data depending on query ordering.

Affected files:

- `frontend/tests/e2e/hu-25b-ranking-manual-seed.spec.ts`
- `fix-issue-153/frontend/tests/e2e/session-target-map-manual-seed.spec.ts`
- `frontend/tests/e2e/session-clue-release-manual-seed.spec.ts`

Fix (applied 2026-07-15): the delete now also matches by email, so stale rows with a
drifted `ExternalIdentityId` are cleared before re-inserting:
`DELETE FROM users WHERE "ExternalIdentityId"='${adminSub}' OR "Email"='admin-1@umbral.local';`
This makes each seed run idempotent regardless of `sub` drift. Note: this prevents future
accumulation; existing duplicate rows already present in a live/dev DB should be pruned
manually if needed.

### 3. `calculationVersion` numeric-width risk across backend and mobile

The ranking contract currently maps `calculationVersion` as:

- Backend DTO: `long CalculationVersion`
- Database: `rankings.calculation_version bigint not null`
- Mobile DTO: `calculationVersion: number`

Impact: JavaScript `number` loses integer precision above `Number.MAX_SAFE_INTEGER`. This is a contract risk if `calculationVersion` can ever grow past that bound.

Fix options:

- Constrain the backend/domain value to `int` if the domain bound is intentionally small.
- Serialize it as a string and type it as `string` in mobile if the `bigint` range is required.

## Checked And Not Divergent

### Ranking row contract

The `RankingRowDto` path is aligned across backend DTO, API/SignalR payload, EF config, live DB, and seed SQL:

| Contract Field | Backend Type | Mobile Type | Database Column | Database Type / Nullability |
| --- | --- | --- | --- | --- |
| `teamId` | `Guid` | `string` | `ranking_rows.team_id` | `uuid not null` |
| `teamDisplayName` | `string` | `string` | `ranking_rows.team_display_name` | `character varying(200) not null` |
| `position` | `int` | `number` | `ranking_rows.position` | `integer not null` |
| `totalScore` | `int` | `number` | `ranking_rows.total_score` | `integer not null` |
| `resolutionTime` | `TimeSpan?` | `string | null` | `ranking_rows.resolution_time` | `interval null` |

REST and SignalR both emit the same `RankingSnapshotDto`.

### Ranking seed SQL

`frontend/tests/e2e/hu-25b-ranking-manual-seed.spec.ts` matches the live `scoring_monitoring` schema for:

- `rankings`
- `ranking_rows`
- `score_entries`

Enum-backed string values also match backend enum names:

- `entry_type = 'Grant'`
- `source_entity_type = 'TriviaAnswerSubmission'`
- `source_entity_type = 'TargetResolution'`

### Target map seed SQL

`fix-issue-153/frontend/tests/e2e/session-target-map-manual-seed.spec.ts` matches the live `mission_design` schema for:

- `Missions`
- `MissionStages`
- `MissionSubstages`
- `MissionClues`
- `MissionTargets`

The coordinate columns are present and compatible:

- `MissionTargets.Latitude double precision not null default 0.0`
- `MissionTargets.Longitude double precision not null default 0.0`

The seed provides explicit numeric coordinate values.

### Clue release seed SQL

`frontend/tests/e2e/session-clue-release-manual-seed.spec.ts` matches the live `mission_design` schema for the same mission authoring tables.

It omits `Latitude` and `Longitude`, which is valid because both columns are non-null with `0.0` defaults.

String values match backend enum names:

- `ActivationState = 'Ready'`
- `PlayMode = 'TreasureHunt'`
- `Visibility = 'HiddenUntilOperatorRelease'`
- API state transition body `targetState = 'Preparing'`

The live `identity_access.registered_teams` table contains both expected teams:

- `a0000000-0000-0000-0000-000000000001`: `Gilded Owls`, active
- `a0000000-0000-0000-0000-000000000002`: `Maple Runners`, active

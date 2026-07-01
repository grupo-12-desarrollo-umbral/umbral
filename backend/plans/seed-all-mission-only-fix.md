# Plan — Fix `seed-all.sh` for the mission-only session contract

**File:** `backend/scripts/seed-all.sh`
**Why:** HU-15 (DES-22) reshaped session creation to be mission-only and dropped the
two-source / `SessionMode` model; HU-17 (DES-24) locked that invariant. The dev seed
script was never updated and now references the removed `session_mode` column and the
removed `sourceTriviaQuizId` request field, so `./scripts/seed-all.sh` aborts partway.
This restores the seed to the current contract. **No production code changes** — script only.

> Found while running the HU-17 Stop-2 smoke. Out of HU-17's slice scope (session-operations
> service + tests), tracked here as a standalone follow-up.

---

## Verified findings (against the live dev stack, 2026-06-30)

Two independent break points, both schema/contract-confirmed. `set -euo pipefail` means the
**first** one aborts the whole run before the second is ever reached.

| # | Location | Stale token | Proof | Severity |
|---|----------|-------------|-------|----------|
| 1 | Part 1, `session_operations.live_sessions` INSERT — `seed-all.sh:312-322` (col list line 313, value `'TreasureHunt'` line 320) | `session_mode` column | `INSERT … (session_mode …)` → `ERROR: column "session_mode" of relation "live_sessions" does not exist` (verified in a rolled-back txn) | **hard abort** (runs first) |
| 2 | Part 3, API-created session — helper `app_create_trivia_session` `seed-all.sh:573-585` (body line 580), quiz resolution `:732-741`, call+trailer `:763-785` | `sourceTriviaQuizId` request field + quiz-as-source model | `POST /api/sessions {"sourceTriviaQuizId":…}` → `400 {"errors":{"MissionId":["'Mission Id' must be greater than '0'."]}}` (field silently ignored, mission-only contract) | **abort / empty id** |

Underlying cause of #2: the script never seeds a **Mission** at all (Part 1 seeds only
`TriviaQuizzes`/`TriviaQuestions`/`TriviaOptions`). Post-HU-15 there is no quiz-as-source
path, so the API-created session has nothing to point at. Fixing the field name alone is
not enough — a runtime-ready mission must exist first.

### Confirmed NOT broken (leave alone)
- `identity_access` inserts — `seed-all.sh:281` (`live_sessions(id, session_code, created_at, updated_at)`) and `:291` (`session_team_associations`). Verified those tables/columns still exist in the `identity_access` DB.
- Cleanup block `:75-88` (deletes from `identity_access`) and `:90-94` — fine.
- The direct-psql session's `source_entity_type` is already `'Mission'` (`:320`); only `session_mode` is dead.
- Downstream API calls on the seeded session — `app_assign_operator_to_session` / `app_associate_team_to_session` / `app_transition_session` (`:769-772`) — contracts unchanged; they only need a valid `liveSessionId`.

---

## Verified contract anchors (mission-design API, from `/openapi/v1.json` + live curl)

- `POST /api/missions` → `CreateMissionRequest { name, description, difficulty, maximumTimeMinutes }`
- `POST /api/missions/{missionId}/nodes` → `AddMissionNodeRequest { nodeType, title, sequenceOrder, stageId?, substageId?, playMode? }` (Stage then Trivia substage)
- `PUT /api/missions/{missionId}/stages/{stageId}/substages/{substageId}/trivia-quiz-selection` → `{ triviaQuizId }`
- `GET /api/missions/{id}/readiness` → `{ isReady, failures[] }`
- `POST /api/missions/{id}/activate` (no body) → mission `activationState: "Ready"`, `isActive: true`
- `POST /api/sessions` → `{ missionId:int, title, maximumTimeMinutes, scheduledAt }` → `201 { liveSessionId, sessionCode, title, sessionState, scheduledAt }`; `409 mission-not-eligible-for-session` when inactive / not-runtime-ready.

A single stage + single **Trivia** substage with a published quiz selected is sufficient for
readiness (no TreasureHunt substage ⇒ no target/winner-score requirement). The implementer
confirms via `GET …/readiness` returning `isReady:true` before `/activate`.

---

## Phase 1 — Drop the dead `session_mode` column (the hard abort)

`seed-all.sh:312-322`, the `session_operations.live_sessions` INSERT:

- Remove `session_mode,` from the column list (line 313).
- Remove the `'TreasureHunt',` value (line 320).

Leave everything else (`source_entity_type 'Mission'`, `source_entity_id gen_random_uuid()`,
the `ON CONFLICT … DO UPDATE`) unchanged — a random snapshot id is fine for a directly-seeded
fake session (no cross-service FK).

**Gate:** `./scripts/seed-all.sh` gets past Part 1 without the `column "session_mode" does not exist` error.

---

## Phase 2 — Make the API-created session mission-only

Goal: keep the one session that exercises the real `POST /api/sessions → assign-operator →
associate-team → transition → Active` happy path (this is exactly the drift that broke us, so
it earns its keep), but drive it from a Mission.

1. **Add `seed_ready_mission()`** (place near the other `app_*` helpers, ~`:573`). Given an
   admin token + a published quiz id, via the mission-design API:
   - `POST /api/missions` → capture `id`
   - `POST /api/missions/{id}/nodes` `{nodeType:"Stage", title:"Stage 1", sequenceOrder:1}` → capture `stageId`
   - `POST /api/missions/{id}/nodes` `{nodeType:"Substage", title:"Trivia", sequenceOrder:1, stageId, playMode:"Trivia"}` → capture `substageId`
   - `PUT /api/missions/{id}/stages/{stageId}/substages/{substageId}/trivia-quiz-selection` `{triviaQuizId:<published>}`
   - `POST /api/missions/{id}/activate`; assert `GET …/readiness` `isReady:true` first (fail loudly if not)
   - echo the mission `id`
   Idempotency: name it deterministically (e.g. `"Seeded Live Trivia Mission"`) and reuse if it already exists, mirroring the script's other idempotent seeds.

2. **Rename `app_create_trivia_session` → `app_create_session`** (`:573-585`) and change the
   body (line 580) from `{"sourceTriviaQuizId":$id,…}` to `{"missionId":$mission_id,…}`. Keep
   the `liveSessionId` extraction.

3. **Replace the quiz-resolution block** (`:732-741`, `SOURCE_TRIVIA_QUIZ_ID`): keep resolving
   a published quiz id (still needed as the substage's quiz), then call `seed_ready_mission`
   with it to get a `READY_MISSION_ID`.

4. **Update the call site** (`:763`) to `app_create_session "$APP_ADMIN_TOKEN" "$READY_MISSION_ID" "$SEEDED_LIVE_TRIVIA_TITLE"`. Lines `:769-772` (operator/team/transition) are unchanged. Tidy the `(Trivia)` label at `:785` → `(Mission)`.

**Gate:** `./scripts/seed-all.sh` exits `0`; a `live_sessions` row with
`title_snapshot='Seeded Live Trivia'` exists with `source_entity_type='Mission'` and reaches
`state='Active'`; no `session_mode`/`sourceTriviaQuizId` token remains in the script
(`grep -nE 'session_mode|sourceTriviaQuizId|app_create_trivia_session' seed-all.sh` → empty).

---

## Lazy alternative (if we'd rather not author a mission in the seed)

Delete the API-created session entirely: drop the helper (`:573-585`), the quiz-resolution
(`:732-741`), and the trailer (`:763-785`). The direct-psql `SESSIONS` (Part 1, already
Mission-sourced after Phase 1) still cover the various session states incl. `Active`. **Cost:**
the seed no longer exercises the live `POST /api/sessions` contract — i.e. it would no longer
catch the exact drift that caused this ticket. **Recommendation:** do Phase 2; the API path is
the seed's only contract-drift canary. Pick this only if seed runtime/maintenance is a concern.

---

## Out of scope
- The separate pre-existing bug surfaced during the smoke: a malformed-type `missionId`
  (e.g. a GUID string for the numeric field) returns `500` NullReference instead of `400`.
  That is a HU-15 production input-validation gap, not a seed issue — its own ticket.
- Any change to session-operations / mission-design production code or contracts.

## Verification (one runnable check, per repo convention)
```bash
cd backend && ./scripts/seed-all.sh && \
PGPASSWORD=postgres psql -h localhost -U postgres -d session_operations -At -c \
  "SELECT source_entity_type, state FROM live_sessions WHERE title_snapshot='Seeded Live Trivia';"
# expect: Mission|Active   (and the script exited 0)
```

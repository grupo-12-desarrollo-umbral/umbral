// Authors a runtime-ready TreasureHunt mission in mission_design. Shared by the HU-23 manual seed and the
// HU-24B QR evidence spec, which both need the one thing global-setup does not provide: a mission whose
// FIRST substage is TreasureHunt (global-setup seeds Trivia only), so that substage becomes the active one
// the moment the operator Starts. POST /api/sessions builds the immutable runtime snapshot from it
// automatically — callers never write snapshot SQL.
import { execSync } from 'child_process'

const DB = 'backend-postgres-1'

// The seeded codes, in sequence order. Stable so a spec can scan a known-good one and assert the
// `target:{guid}` origin, or invent a miss to exercise the `qr:{rawValue}` fallback.
export const TREASURE_HUNT_QR_CODES = ['TH-E2E-QR-1', 'TH-E2E-QR-2', 'TH-E2E-QR-3'] as const

// The lone target of the opt-in second substage. Resolves to a real snapshot but never to the active
// substage, which is the only way to reach the TargetOutsideActiveSubstage rejection.
export const TREASURE_HUNT_OUTSIDE_QR_CODE = 'TH-E2E-QR-OUTSIDE'

// Pipes SQL on stdin (no -c), so the PascalCase-quoted DDL below needs no shell escaping. ON_ERROR_STOP
// makes a broken statement fail the seed loudly instead of silently handing the caller a half-built mission.
function runSql(db: string, sqlText: string): void {
  execSync(`docker exec -i ${DB} psql -v ON_ERROR_STOP=1 -U postgres -d ${db}`, {
    input: sqlText,
    stdio: ['pipe', 'pipe', 'pipe'],
    timeout: 20000,
  })
}

// Idempotently authors the mission: one stage, one TreasureHunt substage (first, so it becomes active on
// Start), three active targets each linked to a clue. Rebuilds the hierarchy on every run (DELETE cascades
// stages -> substages -> targets/clues) so a persistent DB stays clean and the mission id survives.
// Difficulty 'Beginner' matches global-setup's SQL seeds; per-target Score is supplied explicitly (50) because
// it is NOT NULL and, unlike API authoring, is not derived from difficulty.
//
// Pass a missionName unique to your spec: the DELETE above rebuilds by name, so two specs sharing a name
// would race under Playwright's fullyParallel.
//
// withSecondSubstage is opt-in and off by default: the HU-23 manual seed reads the board as a one-substage
// mission, and a second substage would change what a human sees there.
export function authorTreasureHuntMission(
  missionName: string,
  options: { withSecondSubstage?: boolean } = {},
): void {
  const withSecondSubstage = options.withSecondSubstage === true
  runSql('mission_design', `
DO $$
DECLARE
  v_mission_id  INT;
  v_stage_id    INT;
  v_substage_id INT;
  v_clue_id     INT;
  v_seq         INT;
BEGIN
  SELECT "Id" INTO v_mission_id FROM "Missions" WHERE "Name" = '${missionName}' LIMIT 1;

  IF v_mission_id IS NULL THEN
    INSERT INTO "Missions" ("Name", "Description", "Difficulty", "MaximumTimeMinutes", "IsActive", "ActivationState", "Created", "LastModified")
    VALUES ('${missionName}', 'Seeded runtime-ready treasure-hunt mission for e2e — do not delete', 'Beginner', 60, true, 'Ready', NOW(), NOW())
    RETURNING "Id" INTO v_mission_id;
  ELSE
    UPDATE "Missions" SET "IsActive" = true, "ActivationState" = 'Ready', "LastModified" = NOW() WHERE "Id" = v_mission_id;
    DELETE FROM "MissionStages" WHERE "MissionId" = v_mission_id; -- FKs cascade to substages/targets/clues
  END IF;

  INSERT INTO "MissionStages" ("MissionId", "Title", "SequenceOrder")
  VALUES (v_mission_id, 'Stage 1', 1)
  RETURNING "Id" INTO v_stage_id;

  INSERT INTO "MissionSubstages" ("StageId", "Title", "SequenceOrder", "PlayMode")
  VALUES (v_stage_id, 'Treasure Hunt Substage', 1, 'TreasureHunt')
  RETURNING "Id" INTO v_substage_id;

  -- One clue per target; the target links to its clue via ClueId so the runtime plan (and thus the board's
  -- visibleClues) carries the clue text. VisibleWhenSubstageStarts => shown as soon as the substage is active.
  FOR v_seq IN 1..3 LOOP
    INSERT INTO "MissionClues" ("SubstageId", "Title", "SequenceOrder", "Text", "Visibility")
    VALUES (v_substage_id, 'Clue ' || v_seq, v_seq, 'Find landmark #' || v_seq || ' and scan its code.', 'VisibleWhenSubstageStarts')
    RETURNING "Id" INTO v_clue_id;

    INSERT INTO "MissionTargets" ("SubstageId", "Name", "QrCode", "SequenceOrder", "IsActive", "Score", "ClueId")
    VALUES (v_substage_id, 'Target ' || v_seq, 'TH-E2E-QR-' || v_seq, v_seq, true, 50, v_clue_id);
  END LOOP;

  -- Never activated by Start (only the first substage is), so its target is permanently out-of-substage.
  IF ${withSecondSubstage} THEN
    INSERT INTO "MissionSubstages" ("StageId", "Title", "SequenceOrder", "PlayMode")
    VALUES (v_stage_id, 'Treasure Hunt Substage 2', 2, 'TreasureHunt')
    RETURNING "Id" INTO v_substage_id;

    INSERT INTO "MissionClues" ("SubstageId", "Title", "SequenceOrder", "Text", "Visibility")
    VALUES (v_substage_id, 'Clue 4', 1, 'Find landmark #4 and scan its code.', 'VisibleWhenSubstageStarts')
    RETURNING "Id" INTO v_clue_id;

    -- IsActive stays true: the mismatch alone must carry the rejection, so an inactive target would
    -- confound which half of the guard fired.
    INSERT INTO "MissionTargets" ("SubstageId", "Name", "QrCode", "SequenceOrder", "IsActive", "Score", "ClueId")
    VALUES (v_substage_id, 'Target 4', '${TREASURE_HUNT_OUTSIDE_QR_CODE}', 1, true, 50, v_clue_id);
  END IF;
END $$;
`)
}

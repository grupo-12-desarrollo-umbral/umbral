#!/usr/bin/env bash
set -euo pipefail

# seed-dev-data.sh
#
# Seeds aligned dev data across identity_access and session_operations.
# Sessions in each lifecycle state so you can validate the mobile UI:
#
#   SMOKE2  Active     → "The session has moved on — late join isn't allowed."
#   SMOKE3  Scheduled  → first join succeeds
#   SMOKE4  Preparing  → first join succeeds
#   SMOKE5  Paused     → "The session has moved on — late join isn't allowed."
#   SMOKE6  Finished   → "The session has moved on — late join isn't allowed."
#   SMOKE7  Cancelled  → "The session has moved on — late join isn't allowed."
#
# Usage:
#   ./scripts/seed-dev-data.sh
#   PGHOST=192.168.1.50 ./scripts/seed-dev-data.sh
#
# Idempotent: safe to run multiple times.

PGHOST="${PGHOST:-localhost}"
PGPORT="${PGPORT:-5432}"
PGUSER="${PGUSER:-postgres}"
PGPASSWORD="${PGPASSWORD:-postgres}"
export PGPASSWORD

echo "Cleaning dirty dev data …"
psql -h "$PGHOST" -p "$PGPORT" -U "$PGUSER" -d identity_access -c "
  DELETE FROM live_sessions WHERE session_code = 'RSF231';
"
psql -h "$PGHOST" -p "$PGPORT" -U "$PGUSER" -d session_operations -c "
  DELETE FROM live_sessions WHERE session_code = 'RSF231';
"

# ---------------------------------------------------------------------------
# Each session gets a deterministic base ID and team ID.
# Teams share the same GUID in both databases.
# ---------------------------------------------------------------------------
declare -A SESSIONS=(
  [SMOKE2]=b1000000-0000-0000-0000-000000000001:Active:b1000000-0000-0000-0000-000000000010:DV-SMK:Smoke
  [SMOKE3]=b1000000-0000-0000-0000-000000000002:Scheduled:b1000000-0000-0000-0000-000000000011:DV-ECH:Echo
  [SMOKE4]=b1000000-0000-0000-0000-000000000003:Preparing:b1000000-0000-0000-0000-000000000012:DV-FOX:Foxtrot
  [SMOKE5]=b1000000-0000-0000-0000-000000000004:Paused:b1000000-0000-0000-0000-000000000013:DV-GLF:Golf
  [SMOKE6]=b1000000-0000-0000-0000-000000000005:Finished:b1000000-0000-0000-0000-000000000014:DV-HTL:Hotel
  [SMOKE7]=b1000000-0000-0000-0000-000000000006:Cancelled:b1000000-0000-0000-0000-000000000015:DV-IND:India
)

# ---------------------------------------------------------------------------
echo "Seeding identity_access …"
for CODE in "${!SESSIONS[@]}"; do
  IFS=: read -r SID STATE TID TCODE TDISPLAY <<< "${SESSIONS[$CODE]}"

  psql -h "$PGHOST" -p "$PGPORT" -U "$PGUSER" -d identity_access -c "
    INSERT INTO live_sessions (id, session_code, created_at, updated_at)
    VALUES ('$SID', '$CODE', now(), now())
    ON CONFLICT (id) DO NOTHING;
  "
  psql -h "$PGHOST" -p "$PGPORT" -U "$PGUSER" -d identity_access -c "
    INSERT INTO teams (id, display_name, team_code, is_active, created_at, updated_at)
    VALUES ('$TID', '$TDISPLAY Team', '$TCODE', true, now(), now())
    ON CONFLICT (id) DO NOTHING;
  "
  psql -h "$PGHOST" -p "$PGPORT" -U "$PGUSER" -d identity_access -c "
    INSERT INTO session_team_associations (id, live_session_id, team_id)
    VALUES (gen_random_uuid(), '$SID', '$TID')
    ON CONFLICT (live_session_id, team_id) DO NOTHING;
  "
done

# ---------------------------------------------------------------------------
echo "Seeding session_operations …"
for CODE in "${!SESSIONS[@]}"; do
  IFS=: read -r SID STATE TID TCODE TDISPLAY <<< "${SESSIONS[$CODE]}"

  psql -h "$PGHOST" -p "$PGPORT" -U "$PGUSER" -d session_operations -c "
    INSERT INTO live_sessions (
      id, session_mode, source_entity_type, source_entity_id,
      session_code, title_snapshot, state,
      scheduled_at, last_state_changed_at, maximum_time_minutes,
      created_at, updated_at
    ) VALUES (
      '$SID', 'TreasureHunt', 'Mission', gen_random_uuid(),
      '$CODE', '$TDISPLAY Team', '$STATE',
      now(), now(), 60,
      now(), now()
    )
    ON CONFLICT (id) DO NOTHING;
  "
  psql -h "$PGHOST" -p "$PGPORT" -U "$PGUSER" -d session_operations -c "
    INSERT INTO live_session_teams (
      id, live_session_id, team_code, display_name,
      team_capacity, current_score, released_clue_count, join_status
    ) VALUES (
      '$TID', '$SID', '$TCODE', '$TDISPLAY Team',
      10, null, 0, 'Open'
    )
    ON CONFLICT (id) DO NOTHING;
  "
done

# ---------------------------------------------------------------------------
echo "Done."
echo ""
for CODE in "${!SESSIONS[@]}"; do
  IFS=: read -r SID STATE TID TCODE TDISPLAY <<< "${SESSIONS[$CODE]}"
  echo "  $CODE  → $STATE"
done

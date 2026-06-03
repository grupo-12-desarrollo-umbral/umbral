# HU-07B: Cross-Service Data Alignment Fix

## Summary

When signing in as `participant@umbral.local` on the mobile app and entering a session code, teams appeared in the lobby but tapping one failed. Two distinct bugs were found and fixed.

### Root Causes

1. **Cross-service live-session ID mismatch** — The `RSF231` session code pointed to `liveSessionId = a000...0ff` in Identity, but SessionOperations had no live session with that ID (it only had `a111...111` for `SES-SMOKE-01`). The lobby showed teams from Identity, but the reconnect/admission step in SessionOperations couldn't find a matching session. This was the original "dirty dev data" problem — the two databases were never seeded together.

2. **Missing DB column** — EF Core entity `Team` mapped `Capacity` → `team_capacity`, but the migration file was edited after being applied. The column never existed in the database. Loading a `LiveSession` aggregate (which includes teams) threw `column l5.team_capacity does not exist`, which the hub filter translated to `{"code":"ERROR"}` → mobile showed "Something went wrong restoring your team space."

3. **JoinPolicy blocks Active sessions for first join** — `EnsureCanJoin` only allows `Scheduled` or `Preparing` states. An `Active` session with no pre-existing participant in SessionOperations throws `LateJoinNotAllowedException` → mobile showed "The session has moved on — late join isn't allowed."

### Fixes Applied

1. **Created migration `AddTeamCapacityColumn`** that adds `team_capacity` to `live_session_teams` with default `0`. Applied by rebuilding the container.

2. **Created `backend/scripts/seed-dev-data.sh`** — an idempotent seed script that:
   - Cleans the dirty `RSF231` session from both databases
   - Seeds 6 sessions in different lifecycle states, each with their own team
   - Uses the same GUIDs for live_session and team across both databases

3. **The `seed-teams.sh` / E2E test data was left untouched** — those are independent sources of teams. The seed script only adds new aligned data alongside existing data.

## Seed Sessions

| Code | State | Team | Expected Mobile Behaviour |
|------|-------|------|--------------------------|
| SMOKE3 | Scheduled | DV-ECH / Echo Team | Joins → "Joined live session" |
| SMOKE4 | Preparing | DV-FOX / Foxtrot Team | Joins → "Joined live session" |
| SMOKE2 | Active | DV-SMK / Smoke Team | Late join denied |
| SMOKE5 | Paused | DV-GLF / Golf Team | Late join denied |
| SMOKE6 | Finished | DV-HTL / Hotel Team | Late join denied |
| SMOKE7 | Cancelled | DV-IND / India Team | Late join denied |

All 6 sessions share the same GUID prefix (`b100...`) across `identity_access` and `session_operations`.

## How to Use

```bash
# After docker compose up -d has created the tables
./backend/scripts/seed-dev-data.sh

# Enter SMOKE3 thru SMOKE7 in the mobile app to validate each state
```

## Mobile Error Message Mapping

The mobile app maps backend `HubException` codes to user-facing copy in `mobile/src/lib/realtime/reconnect-policy.ts`:

| Backend Exception | Hub Code | Mobile Outcome | Copy |
|---|---|---|---|
| `LateJoinNotAllowedException` | `LATE_JOIN_NOT_ALLOWED` | `forbidden-late-join` | "The session has moved on — late join isn't allowed." |
| `ForbiddenAccessException` | `FORBIDDEN` | `lost-access` | "You no longer have access to this team." |
| `NotFoundException` | `NOT_FOUND` | `lost-access` | "You no longer have access to this team." |
| `ValidationException` | `VALIDATION_FAILED` | `error` | "Something went wrong restoring your team space." |
| Any unhandled exception | `ERROR` | `error` | "Something went wrong restoring your team space." |

## Cross-Service Data Flow

```
Mobile lobby → GET /api/sessions/{code}/teams
                → Gateway routes to identity-access-service
                → Reads from identity_access.live_sessions
                  JOIN session_team_associations JOIN teams

Mobile tap → POST /api/teams/{teamId}/participants/self  { liveSessionId }
                → Identity creates TeamMembership

Mobile → POST /api/permissions/participant-membership-access
                → Validates membership exists

Mobile → SignalR SessionsHub.ReconnectAsync(liveSessionId, { teamId, displayName })
                → SessionOperations calls Identity membership validation (HTTP)
                → SessionOperations loads LiveSession from session_operations DB
                → LiveSession.AdmitParticipant()
                  → First join:  EnsureCanJoin — rejects if not Scheduled/Preparing
                  → Reconnect:   EnsureCanReconnect — rejects if Finished/Cancelled
```

## Key Files

| File | Purpose |
|---|---|
| `backend/scripts/seed-dev-data.sh` | Idempotent seed script for aligned dev data |
| `backend/services/session-operations-service/src/Infrastructure/Migrations/20260603222547_AddTeamCapacityColumn.cs` | Migration adding `team_capacity` column |
| `backend/services/session-operations-service/src/Domain/Services/JoinPolicy.cs` | Domain policy controlling join/reconnect rules |
| `backend/services/session-operations-service/src/Api/Hubs/DomainExceptionHubFilter.cs` | Maps exceptions to machine-readable codes |
| `backend/services/session-operations-service/src/Application/Sessions/Commands/ReconnectAuthenticatedParticipant/ReconnectAuthenticatedParticipantService.cs` | Orchestrates reconnect flow |
| `mobile/src/lib/realtime/reconnect-policy.ts` | Maps hub codes to UI outcomes |
| `mobile/src/app/(app)/team-space.tsx` | Team space screen with error copy |
| `mobile/src/app/(app)/team-lobby.tsx` | Lobby screen with team selection + join flow |

## Misaligned Data Sources (for reference)

The dirty `RSF231` session had 12 associated teams from multiple sources:

- `frontend/tests/setup/global-setup.ts` — seeds Gilded Owls, Maple Runners, Brass Lanterns, Iron Magnolias
- `frontend/tests/e2e/teams.spec.ts` — creates Alpha Squad, Operator Squad
- `backend/scripts/seed-teams.sh` — creates Delta, Echo, Bismarck, Los Panas
- Manual API calls against the shared Docker DB

These are not bugs — they're leftover dev data. The seed script only removes `RSF231` to eliminate the misaligned session reference.

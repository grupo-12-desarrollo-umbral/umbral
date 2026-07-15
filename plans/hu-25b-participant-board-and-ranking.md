# Plan: HU-25B — Participant Board & Ranking Query

**Ref:** HU-25B · **Linear:** DES-35 (`svc:cross-service`, `Feature`, `mobile`) · **Status:** Todo
**Date:** 2026-07-14
**Scope:** Let an authenticated **participant** consult the enabled **session ranking** from their
team context, read-only and scoped to their authorization context. Full vertical (backend + mobile).
**Builds on:**
- **DES-99** (HU-37 + HU-39 — score ledger + real-time ranking, **Done**) — ships the ranking read
  query, the `RankingController` GET endpoint, the `ParticipantOrOperator` policy, and `ScoringHub`
  live broadcast.
- **HU-23 / DES-31** ("Tablero de equipo en vivo", **Done**) — ships the live team board: REST snapshot
  (`GetParticipantTeamBoardQuery`) + SignalR push (`TeamBoardUpdated`) + mobile `TreasureHuntBoard`
  fed by `useTeamBoard`.

> **This is a wiring + hardening slice, not a new backend build.** Do **not** run the
> generator→driver pipeline (DES-35 carries no `ready-for-agent` / `svc:<one-service>` label, and three
> of the four driver phases would be no-ops). The backend read surface already exists; the substance is
> the mobile ranking view plus an authorization-scoping decision on the backend.

---

## Context — what already exists today

| AC | Surface that already serves it | Location |
|---|---|---|
| Consult participant **session context** | REST `GET /api/sessions/{liveSessionId}/participants/team-board` + live `TeamBoardUpdated` | `session-operations-service` `SessionsController.cs:243`; mobile `use-team-board.ts`, `treasure-hunt-board.tsx` |
| Consult **ranking** | REST `GET /api/sessions/{liveSessionId}/ranking` (returns `RankingSnapshotDto`) | `scoring-monitoring-service` `RankingController.cs`; query `GetRankingSnapshotQuery(LiveSessionId)` |
| Role gate | `ParticipantOrOperator` policy (`RequireRole("Participant","Operator")`) | `scoring…/Api/DependencyInjection.cs:31` |
| Read-only | Both endpoints are `HttpGet`; no command path | — |

The mobile participant view renders **placeholder** standings today, explicitly tagged for this HU:
- `treasure-hunt-board.tsx:9` — *"real standings/ranking is HU-39"*
- `treasure-hunt-board.tsx:28` — *"Other-team cards are static placeholders until HU-39"*
- `team-board-types.ts:96` — *"Current/session-owned score or 0 — no ledger/ranking (DES-31)"*

So the ranking wire-up is the primary mobile deliverable; the existing board endpoint remains the
source of participant session/team context.

## The real gap

1. **Backend authorization scoping** — the ranking endpoint is gated by **role only**. A participant can
   currently read **any** session's ranking. The existing board path is stricter, but its participation
   guard delegates to `identity-access-service` for **registered-team membership** validation only; it
   does **not** prove live-session membership. AC #3 ("respeta el rol y el equipo del usuario
   autenticado") demands the read be scoped to the participant's own session/team. The approved fix is
   to source that authorization fact from `session-operations-service`, which owns `LiveSession`,
   `SessionParticipant`, `Team`, and `TeamMember`.
2. **Mobile ranking view** — replace the placeholder standings with the real `RankingSnapshotDto`.
3. **Gateway reachability** — DES-99's gateway routing fix (ranking under `/api/sessions/**` had routed
   to session-ops, not scoring) is reported resolved; the plan **verifies** it rather than assuming it.

---

## Tracer-bullet slices

Each slice is independently shippable and leaves the app runnable.

### Slice 0 — Backend: ranking session-membership gate + gateway reachability
**Tree:** `scoring-monitoring-service` (read side), `session-operations-service` (session access fact), `api-gateway`
- Confirm through the **running gateway** (real Keycloak participant token) that both reads return 200:
  - `GET /api/sessions/{id}/ranking`
  - `GET /api/sessions/{id}/participants/team-board`
- **Session access fact — add a narrow validation read in session-operations** (per Decision 1): expose a
  participant-scoped access endpoint for a concrete `(liveSessionId, teamId)` that answers whether the
  authenticated participant is an active member of that team in that live session. The check must be
  based on session-owned runtime data (`SessionParticipant` + `TeamMember`), not Users membership.
- **Ranking — add the session-membership gate** (per Decision 1): thread `teamId` into
  `GetRankingSnapshotQuery` and validate it in the handler through the new session-ops access endpoint
  before returning the snapshot. A participant in the session gets the full `rows`; a participant not
  in it is rejected. Reject-path unit test + happy-path test.
- **Gate:** `make -C backend gate SVC=scoring-monitoring-service` green (ranking change carries the
  coverage). Session-access endpoint coverage rides `SVC=session-operations-service`. Commit with curl
  evidence for both reads through the gateway.

### Slice 1 — Mobile: ranking API client + hook
**Tree:** `mobile/`
- Add a typed ranking client call to `GET /api/sessions/{liveSessionId}/ranking?teamId={teamId}` returning
  `RankingSnapshotDto` shape: `{ liveSessionId, generatedAt, calculationVersion, rows: [{ teamId,
  teamDisplayName, position, totalScore, resolutionTime }] }` (mirror `RankingSnapshotDto.cs` /
  `RankingRowDto`).
- Add a `useRanking(liveSessionId, teamId)` hook alongside `use-team-board.ts` — fetch on mount, expose
  `{ snapshot, loading, error, refetch }`. Handle the empty snapshot (`RankingSnapshotDto.Empty` →
  `rows: []`, `generatedAt: MinValue`) as a first-class "no standings yet" state.
- **Gate:** unit test for the hook (fetch success, empty snapshot, error).

### Slice 2 — Mobile: real ranking view replacing placeholders
**Tree:** `mobile/`
- **Reference UI:** `mobile/src/app/(app)/ranking-prototype.tsx` — this prototype defines the
  production ranking layout. Extract its two components into reusable building blocks:
  - `PodiumLeaderboard` — top-3 podium (2nd left, 1st center tallest, 3rd right) + scrollable
    list for positions 4+. Empty state: trophy emoji + "Standings will appear once the round begins."
  - `ListRow` — card row with position circle, team name, "(You)" suffix for own team, score,
    resolution time. Own team gets ember accent highlight.
- **Data mapping:** `RankingRowDto` from the API maps 1:1 to the prototype's `RankingRow` type:
  `teamId → teamId`, `teamDisplayName → teamName`, `position → position`, `totalScore → totalScore`,
  `resolutionTime → resolutionTime` (format `TimeSpan?` to `"HH:mm:ss"` string).
- **Own team:** match `teamId` from the board context (`useTeamBoard`) to identify the participant's
  team for highlighting.
- **Integration:** in `treasure-hunt-board.tsx`, replace the static other-team placeholder cards (`:28`)
  with the extracted `PodiumLeaderboard` fed by `useRanking` rows.
- Remove/replace the placeholder comments at `:9`, `:28`, and `team-board-types.ts:96` — the ledger
  score is now real.
- **Gate:** component test asserting real rows render, own-team highlight, and empty-state fallback.

### Slice 3 (optional) — Mobile: live ranking updates via ScoringHub
**Tree:** `mobile/`
- DES-99 broadcasts `RankingRefreshed` over `ScoringHub` (`/hubs/scoring`). Subscribe so standings
  update live instead of only on fetch. Mirror the `use-team-board.ts` SignalR pattern.
- Deferred by default (AC only requires *consult*, i.e. on-demand read). Keep at plan altitude; promote
  only if live standings are wanted for parity with the live board.

---

## Acceptance criteria mapping

| AC (DES-35) | Delivered by |
|---|---|
| Participant can consult their permitted **session ranking** | Slice 1 + 2 (mobile), Slice 0 (verify reachable + scoped) |
| Information respects the authenticated user's **role and team context** | Slice 0 (scoping audit/harden) |
| Queries are **read-only** | Inherent — both endpoints are GET; no command path added |

---

## Resolved decisions

1. **Ranking scoping → session-membership-gated, full standings.** The ranking is a
   **public-within-session** leaderboard: a participant sees **all** teams' standings for a session they
   **belong to**, and is **rejected** for sessions they don't. Realize it by adding a narrow
   participant-scoped access read in `session-operations-service` and calling it from
   `scoring-monitoring-service` before returning the snapshot. The ranking read takes `teamId` so
   session-ops can verify that the authenticated participant is actively assigned to that team in that
   live session. No row-level filtering — the full `rows` list is returned. This satisfies AC#3.
2. **Participant board endpoint → supporting context, not the primary HU acceptance gate.**
   `GetParticipantTeamBoardQueryHandler` remains the existing source of participant session/team context
   for the mobile screen, but this HU's substantive delivery is the participant-visible ranking read and
   its UI wiring. No board feature or authorization change is required to satisfy this HU.
3. **Plan layout → single cross-tree plan** (this file). The scoping decisions couple the two services
   and mobile, so they stay in one place rather than split across `backend/plans` + `mobile/plans`.

## Out of scope
- Operator/admin ranking views (HU-25A was **cancelled/absorbed** — DES-34).
- Any write/mutation to score or ranking (owned by DES-99 / DES-53 penalties).
- New ledger or ranking computation — consumed as-is from DES-99.
